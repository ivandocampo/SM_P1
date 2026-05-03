// =============================================================
// Clase principal del agente guardia (orco) con arquitectura BDI.
// Inicializa todos los componentes del agente: creencias, deseos,
// intenciones, behaviors, sensores y comunicación FIPA-ACL.
// El ciclo Update orquesta percepción, deliberación BDI y ejecución
// del behavior activo. La clase está dividida en varios ficheros
// parciales: GuardBehaviorController, GuardPerception,
// GuardReactiveCommunication, GuardSearchCoordinator,
// GuardExitCoverage y GuardDebugPanel
// =============================================================

using System.Collections.Generic;
using UnityEngine;

public partial class GuardAgent : MonoBehaviour
{
    // Configuración asignable desde el Inspector

    [Header("Identidad")]
    public string agentId = "Guard_01";

    [Header("Referencias")]
    public Transform objetivoFrodo;         // Transform de Frodo, para calcular distancia de captura
    public float distanciaCaptura = 1.2f;

    [Header("Patrulla")]
    public Transform[] puntosPatrulla;      // Waypoints del recorrido de patrulla

    [Header("Bloqueo de Salida")]
    public Transform puntoSalida;
    public Transform[] puntosBloqueoSalida; // Puntos estratégicos para cubrir la salida

    [Header("Pedestal del Anillo")]
    public Transform pedestalAnillo;
    public Transform[] puntosBusquedaMapa;  // Zonas de búsqueda registradas en las creencias

    [Header("Temporizadores")]
    public float tiempoEntreComprobaciones = 30f;   // Intervalo entre comprobaciones periódicas del pedestal
    public float cooldownContractNet = 10f;

    [Header("Comunicación")]
    public float intervaloHeartbeatEstado = 2.5f;           // Frecuencia del broadcast de estado al equipo
    public float intervaloActualizacionAvistamiento = 0.6f;

    [Header("Debug Visual")]
    public bool mostrarDebugEnPantalla = true;
    public bool mostrarMarcadorDebug = true;
    public Vector2 posicionPanelDebug = new Vector2(12f, 12f);
    public float anchoPanelDebug = 520f;

    // Componentes Unity
    private ComunicacionAgente comunicacion;
    private SensorVision sensorVision;
    private SensorOido sensorOido;
    private ActuadorMovimiento actuador;

    // Capa BDI
    private BeliefBase creencias;           // Lo que el guardia sabe del mundo
    private DesireGenerator generadorDeseos; // Genera la lista de deseos según la fase táctica
    private IntentionSelector selectorIntenciones; // Elige la intención activa aplicando histéresis

    // Delegados de comunicación
    private ProtocolHandler protocolHandler;       // Construye y procesa mensajes FIPA-ACL
    private ContractNetManager contractNetManager; // Gestiona el protocolo Contract-Net

    // Lambda guardada para poder desuscribirse correctamente en OnDestroy
    private System.Action<ACLMessage> onCFP;

    private float temporizadorHeartbeat;
    private float ultimoInformeAvistamiento = -100f;
    private int ultimaVentanaChequeoPedestal = -1;
    private float ultimaLimpiezaStale = 0f;

    private float tiempoUltimaDeliberacion = 0f;
    private bool deliberacionPendiente = false;
    private const float INTERVALO_DELIBERACION = 0.5f;

    // Inicialización
    void Start()
    {
        // Obtener componentes del mismo GameObject
        comunicacion = GetComponent<ComunicacionAgente>();
        sensorVision = GetComponent<SensorVision>();
        sensorOido = GetComponent<SensorOido>();
        actuador = GetComponent<ActuadorMovimiento>();

        comunicacion.Inicializar(agentId, GameConstants.AgentTypes.Guard);
        if (!guardiasDebug.Contains(this))
            guardiasDebug.Add(this);

        // Inicializar los tres componentes BDI
        creencias = new BeliefBase(agentId);
        generadorDeseos = new DesireGenerator(creencias);
        selectorIntenciones = new IntentionSelector();

        // Inicializar los delegados de comunicación
        protocolHandler = new ProtocolHandler(creencias, comunicacion, selectorIntenciones, agentId);
        contractNetManager = new ContractNetManager(creencias, comunicacion, agentId, cooldownContractNet);

        // Registrar un behavior por cada tipo de intención posible
        behaviors[BehaviorType.Patrol]         = new PatrolBehavior(puntosPatrulla);
        behaviors[BehaviorType.Pursuit]        = new PursuitBehavior();
        behaviors[BehaviorType.Search]         = new SearchBehavior(12f, 4, 10f);
        behaviors[BehaviorType.SearchAssigned] = new SearchAssignedBehavior();
        behaviors[BehaviorType.Intercept]      = new InterceptBehavior();
        behaviors[BehaviorType.BlockExit]      = new BlockExitBehavior(puntoSalida, puntosBloqueoSalida);
        behaviors[BehaviorType.CheckPedestal]  = new CheckPedestalBehavior(pedestalAnillo);

        // Escalonar el heartbeat para evitar que todos los guardias emitan a la vez
        temporizadorHeartbeat = Random.Range(0f, intervaloHeartbeatEstado);
        if (tiempoEntreComprobaciones > 0f)
            ultimaVentanaChequeoPedestal = Mathf.FloorToInt(Time.time / tiempoEntreComprobaciones);

        // Inyectar posiciones clave en las creencias para que los behaviors puedan usarlas
        if (pedestalAnillo != null)
        {
            creencias.PosicionPedestal = pedestalAnillo.position;
            creencias.TienePosicionPedestal = true;
        }

        if (puntoSalida != null)
        {
            creencias.PosicionSalida = puntoSalida.position;
            creencias.TienePosicionSalida = true;
        }

        RegistrarZonasBusqueda();

        // Suscribirse a eventos de sensores; los manejadores solo actualizan creencias
        if (sensorVision != null)
        {
            sensorVision.OnObjetivoDetectado  += OnLadronVisto;
            sensorVision.OnObjetivoVisible    += OnLadronSigueVisible;
            sensorVision.OnObjetivoPerdido    += OnLadronPerdido;
            sensorVision.OnAnilloDesaparecido += OnAnilloDesaparecido;
        }
        if (sensorOido != null)
        {
            sensorOido.OnSonidoDetectado += OnSonidoDetectado;
        }

        // Suscribirse a todos los eventos de performativas FIPA-ACL del buzón
        onCFP = msg => protocolHandler.ManejarCFP(msg, actuador);

        comunicacion.OnInformRecibido        += protocolHandler.ManejarInform;
        comunicacion.OnInformResultRecibido  += protocolHandler.ManejarInformResult;
        comunicacion.OnRequestRecibido       += protocolHandler.ManejarRequest;
        comunicacion.OnQueryIfRecibido       += protocolHandler.ManejarQueryIf;
        comunicacion.OnQueryRefRecibido      += protocolHandler.ManejarQueryRef;
        comunicacion.OnCFPRecibido           += onCFP;
        comunicacion.OnPropuestaAceptada     += protocolHandler.ManejarPropuestaAceptada;
        comunicacion.OnPropuestaRechazada    += protocolHandler.ManejarPropuestaRechazada;
        comunicacion.OnDoneRecibido          += protocolHandler.ManejarDone;
        comunicacion.OnAgreeRecibido         += protocolHandler.ManejarAgree;
        comunicacion.OnRefuseRecibido        += protocolHandler.ManejarRefuse;
        comunicacion.OnCancelRecibido        += protocolHandler.ManejarCancel;
        comunicacion.OnNotUnderstoodRecibido += protocolHandler.ManejarNotUnderstood;

        Debug.Log($"[{agentId}] Guardia inicializado con arquitectura BDI + FIPA-ACL");
    }

    // Desuscribirse de todos los eventos al destruir el objeto para evitar referencias nulas
    void OnDestroy()
    {
        if (sensorVision != null)
        {
            sensorVision.OnObjetivoDetectado  -= OnLadronVisto;
            sensorVision.OnObjetivoVisible    -= OnLadronSigueVisible;
            sensorVision.OnObjetivoPerdido    -= OnLadronPerdido;
            sensorVision.OnAnilloDesaparecido -= OnAnilloDesaparecido;
        }
        if (sensorOido != null)
        {
            sensorOido.OnSonidoDetectado -= OnSonidoDetectado;
        }

        if (comunicacion != null)
        {
            comunicacion.OnInformRecibido        -= protocolHandler.ManejarInform;
            comunicacion.OnInformResultRecibido  -= protocolHandler.ManejarInformResult;
            comunicacion.OnRequestRecibido       -= protocolHandler.ManejarRequest;
            comunicacion.OnQueryIfRecibido       -= protocolHandler.ManejarQueryIf;
            comunicacion.OnQueryRefRecibido      -= protocolHandler.ManejarQueryRef;
            comunicacion.OnCFPRecibido           -= onCFP;
            comunicacion.OnPropuestaAceptada     -= protocolHandler.ManejarPropuestaAceptada;
            comunicacion.OnPropuestaRechazada    -= protocolHandler.ManejarPropuestaRechazada;
            comunicacion.OnDoneRecibido          -= protocolHandler.ManejarDone;
            comunicacion.OnAgreeRecibido         -= protocolHandler.ManejarAgree;
            comunicacion.OnRefuseRecibido        -= protocolHandler.ManejarRefuse;
            comunicacion.OnCancelRecibido        -= protocolHandler.ManejarCancel;
            comunicacion.OnNotUnderstoodRecibido -= protocolHandler.ManejarNotUnderstood;
        }

        contractNetManager?.Limpiar();
        guardiasDebug.Remove(this);
    }

    // Ciclo principal

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        // Actualizar posición y estado propios en las creencias cada frame
        creencias.MiPosicion = transform.position;
        creencias.EstadoActual = behaviorActivo_tipo;

        if (ComprobarCaptura()) return;

        // Ejecución del behavior y Contract-Net: cada frame para movimiento suave
        EjecutarBehaviorActivo();
        contractNetManager.Gestionar();

        // Percepción y comunicación: cada frame para reaccionar sin latencia
        comunicacion.ProcesarMensajes();
        GestionarCambioTareaAsignada();
        GestionarComunicacionReactiva();
        GestionarCaducidadHipotesisLadron();
        GestionarCoberturaSalidaRobada();
        GestionarAutoAsignacionSalidaRobada();

        // Mantenimiento periódico: heartbeat y limpieza de guardias inactivos
        ActualizarTemporizadorComprobacion();
        ActualizarHeartbeatEstado();
        if (Time.time - ultimaLimpiezaStale > 10f)
        {
            creencias.LimpiarGuardiasStale();
            ultimaLimpiezaStale = Time.time;
        }

        // Deliberación BDI: solo cuando las creencias cambian o cada 0.5 s como fallback
        if (deliberacionPendiente || creencias.NecesitaDeliberar ||
            Time.time - tiempoUltimaDeliberacion >= INTERVALO_DELIBERACION)
        {
            tiempoUltimaDeliberacion = Time.time;
            deliberacionPendiente = false;
            creencias.NecesitaDeliberar = false;

            List<Desire> deseos = generadorDeseos.GenerarDeseos();
            selectorIntenciones.Seleccionar(deseos, creencias);

            // Cambiar el behavior activo si el selector de intenciones lo indica
            if (selectorIntenciones.CambioDeIntencion)
                ActivarBehavior(selectorIntenciones.NombreIntencion);
        }
    }

    void OnGUI()
    {
        DibujarPanelDebug();
    }

    // Comprobar si Frodo está lo suficientemente cerca para ser capturado
    private bool ComprobarCaptura()
    {
        if (objetivoFrodo == null) return false;

        if (Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaCaptura)
        {
            GameManager.Instance.FrodoCapturado();
            return true;
        }
        return false;
    }

    // Descartar la hipótesis de posición del ladrón si lleva demasiado tiempo sin confirmar
    private void GestionarCaducidadHipotesisLadron()
    {
        if (creencias.AnilloRobado) return;
        if (creencias.TiempoUltimaDeteccion < -50f) return;
        if (creencias.FaseActual() != TacticalPhase.NormalPatrol) return;

        busquedaCoordinadaPendiente = false;
        creencias.DescartarHipotesisLadronCaducada();
        selectorIntenciones.ForzarReset();
        deliberacionPendiente = true;
        Debug.Log($"[{agentId}] Hipotesis del ladron caducada; vuelta a patrulla");
    }

    // Activar la comprobación periódica del pedestal usando ventanas de tiempo fijas
    private void ActualizarTemporizadorComprobacion()
    {
        if (creencias.AnilloRobado) return;
        if (tiempoEntreComprobaciones <= 0f) return;

        int ventanaActual = Mathf.FloorToInt(Time.time / tiempoEntreComprobaciones);
        if (ventanaActual == ultimaVentanaChequeoPedestal) return;

        ultimaVentanaChequeoPedestal = ventanaActual;
        creencias.DebeComprobarPedestal = true;
    }

    // Emitir el estado actual del guardia al equipo cada intervaloHeartbeatEstado segundos
    private void ActualizarHeartbeatEstado()
    {
        temporizadorHeartbeat -= Time.deltaTime;
        if (temporizadorHeartbeat > 0f) return;

        temporizadorHeartbeat = intervaloHeartbeatEstado;
        BroadcastEstado();
    }

    // Leer los puntos de búsqueda del mapa y registrarlos en las creencias por nombre de zona
    private void RegistrarZonasBusqueda()
    {
        if (puntosBusquedaMapa == null) return;

        foreach (Transform zona in puntosBusquedaMapa)
        {
            if (zona == null || string.IsNullOrEmpty(zona.name) || zona.childCount == 0)
                continue;

            // Extraer las posiciones de los hijos del Transform de zona
            Vector3[] puntos = new Vector3[zona.childCount];
            for (int i = 0; i < zona.childCount; i++)
                puntos[i] = zona.GetChild(i).position;

            creencias.RegistrarZonaBusqueda(zona.name, puntos);
        }
    }

    public ComunicacionAgente GetComunicacion() => comunicacion;

    // Devolver un resumen del estado del guardia para el panel de debug
    public string GetEstadoDebug()
    {
        return $"[{agentId}] Behavior: {behaviorActivo_tipo}, " +
               $"Ladrón visible: {creencias.LadronVisible}, " +
               $"Anillo robado: {creencias.AnilloRobado}, " +
               $"Info reciente: {creencias.TieneInfoReciente()}, " +
               $"Tarea asignada: {creencias.TieneTareaAsignada}";
    }
}
