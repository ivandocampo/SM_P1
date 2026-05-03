using System.Collections.Generic;
using UnityEngine;

public partial class GuardAgent : MonoBehaviour
{
    // CONFIGURACIÓN (Inspector)

    [Header("Identidad")]
    public string agentId = "Guard_01";

    [Header("Referencias")]
    public Transform objetivoFrodo;
    public float distanciaCaptura = 1.2f;

    [Header("Patrulla")]
    public Transform[] puntosPatrulla;

    [Header("Bloqueo de Salida")]
    public Transform puntoSalida;
    public Transform[] puntosBloqueoSalida;

    [Header("Pedestal del Anillo")]
    public Transform pedestalAnillo;
    public Transform[] puntosBusquedaMapa;

    [Header("Temporizadores")]
    public float tiempoEntreComprobaciones = 30f;
    public float cooldownContractNet = 10f;

    [Header("Comunicación")]
    public float intervaloHeartbeatEstado = 2.5f;
    public float intervaloActualizacionAvistamiento = 0.6f;

    [Header("Debug Visual")]
    public bool mostrarDebugEnPantalla = true;
    public bool mostrarMarcadorDebug = true;
    public Vector2 posicionPanelDebug = new Vector2(12f, 12f);
    public float anchoPanelDebug = 520f;

    

    // COMPONENTES

    private ComunicacionAgente comunicacion;
    private SensorVision sensorVision;
    private SensorOido sensorOido;
    private ActuadorMovimiento actuador;

    // BDI

    private BeliefBase creencias;
    private DesireGenerator generadorDeseos;
    private IntentionSelector selectorIntenciones;

    // DELEGADOS

    private ProtocolHandler protocolHandler;
    private ContractNetManager contractNetManager;

    // Lambda guardada para poder desuscribirse correctamente en OnDestroy
    private System.Action<ACLMessage> onCFP;

    // ESTADO INTERNO

    private float temporizadorHeartbeat;
    private float ultimoInformeAvistamiento = -100f;
    private int ultimaVentanaChequeoPedestal = -1;
    private float ultimaLimpiezaStale = 0f;

    // Deliberación desacoplada del frame rate: se ejecuta cada INTERVALO_DELIBERACION
    // segundos o cuando un evento relevante lo fuerza. La ejecución del behavior
    // activo sí corre cada frame para garantizar movimiento suave.
    private float tiempoUltimaDeliberacion = 0f;
    private bool deliberacionPendiente = false;
    private const float INTERVALO_DELIBERACION = 0.5f;
    // INICIALIZACIÓN

    void Start()
    {
        comunicacion = GetComponent<ComunicacionAgente>();
        sensorVision = GetComponent<SensorVision>();
        sensorOido = GetComponent<SensorOido>();
        actuador = GetComponent<ActuadorMovimiento>();

        comunicacion.Inicializar(agentId, GameConstants.AgentTypes.Guard);
        if (!guardiasDebug.Contains(this))
            guardiasDebug.Add(this);

        // BDI
        creencias = new BeliefBase(agentId);
        generadorDeseos = new DesireGenerator(creencias);
        selectorIntenciones = new IntentionSelector();

        // Delegados
        protocolHandler = new ProtocolHandler(creencias, comunicacion, selectorIntenciones, agentId);
        contractNetManager = new ContractNetManager(creencias, comunicacion, agentId, cooldownContractNet);

        // Behaviors
        behaviors[BehaviorType.Patrol]         = new PatrolBehavior(puntosPatrulla);
        behaviors[BehaviorType.Pursuit]        = new PursuitBehavior();
        behaviors[BehaviorType.Search]         = new SearchBehavior(12f, 4, 10f);
        behaviors[BehaviorType.SearchAssigned] = new SearchAssignedBehavior();
        behaviors[BehaviorType.Intercept]      = new InterceptBehavior();
        behaviors[BehaviorType.BlockExit]      = new BlockExitBehavior(puntoSalida, puntosBloqueoSalida);
        behaviors[BehaviorType.CheckPedestal]  = new CheckPedestalBehavior(pedestalAnillo);

        temporizadorHeartbeat = Random.Range(0f, intervaloHeartbeatEstado);
        if (tiempoEntreComprobaciones > 0f)
            ultimaVentanaChequeoPedestal = Mathf.FloorToInt(Time.time / tiempoEntreComprobaciones);

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

        // Sensores
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

        // Comunicación
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

    // CICLO PRINCIPAL

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

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

        // Mantenimiento periódico
        ActualizarTemporizadorComprobacion();
        ActualizarHeartbeatEstado();
        if (Time.time - ultimaLimpiezaStale > 10f)
        {
            creencias.LimpiarGuardiasStale();
            ultimaLimpiezaStale = Time.time;
        }

        // Deliberación BDI: solo cuando algo relevante ha cambiado o cada 0.5s
        // como fallback. Desacoplada del frame rate para no generar deseos 60 veces
        // por segundo cuando las creencias no han cambiado.
        if (deliberacionPendiente || creencias.NecesitaDeliberar ||
            Time.time - tiempoUltimaDeliberacion >= INTERVALO_DELIBERACION)
        {
            tiempoUltimaDeliberacion = Time.time;
            deliberacionPendiente = false;
            creencias.NecesitaDeliberar = false;

            List<Desire> deseos = generadorDeseos.GenerarDeseos();
            selectorIntenciones.Seleccionar(deseos, creencias);

            if (selectorIntenciones.CambioDeIntencion)
                ActivarBehavior(selectorIntenciones.NombreIntencion);
        }
    }

    void OnGUI()
    {
        DibujarPanelDebug();
    }

    

    // CAPA REACTIVA

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

    // TEMPORIZADOR PEDESTAL

    private void ActualizarTemporizadorComprobacion()
    {
        if (creencias.AnilloRobado) return;
        if (tiempoEntreComprobaciones <= 0f) return;

        int ventanaActual = Mathf.FloorToInt(Time.time / tiempoEntreComprobaciones);
        if (ventanaActual == ultimaVentanaChequeoPedestal) return;

        ultimaVentanaChequeoPedestal = ventanaActual;
        creencias.DebeComprobarPedestal = true;
    }

    private void ActualizarHeartbeatEstado()
    {
        temporizadorHeartbeat -= Time.deltaTime;
        if (temporizadorHeartbeat > 0f) return;

        temporizadorHeartbeat = intervaloHeartbeatEstado;
        BroadcastEstado();
    }

    private void RegistrarZonasBusqueda()
    {
        if (puntosBusquedaMapa == null) return;

        foreach (Transform zona in puntosBusquedaMapa)
        {
            if (zona == null || string.IsNullOrEmpty(zona.name) || zona.childCount == 0)
                continue;

            Vector3[] puntos = new Vector3[zona.childCount];
            for (int i = 0; i < zona.childCount; i++)
                puntos[i] = zona.GetChild(i).position;

            creencias.RegistrarZonaBusqueda(zona.name, puntos);
        }
    }

    // ACCESO PÚBLICO

    public ComunicacionAgente GetComunicacion() => comunicacion;

    public string GetEstadoDebug()
    {
        return $"[{agentId}] Behavior: {behaviorActivo_tipo}, " +
               $"Ladrón visible: {creencias.LadronVisible}, " +
               $"Anillo robado: {creencias.AnilloRobado}, " +
               $"Info reciente: {creencias.TieneInfoReciente()}, " +
               $"Tarea asignada: {creencias.TieneTareaAsignada}";
    }
}
