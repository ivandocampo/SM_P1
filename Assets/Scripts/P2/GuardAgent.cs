using System.Collections.Generic;
using UnityEngine;

public class GuardAgent : MonoBehaviour
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

    

    // COMPONENTES

    private ComunicacionAgente comunicacion;
    private SensorVision sensorVision;
    private SensorOido sensorOido;
    private ActuadorMovimiento actuador;

    // BDI

    private BeliefBase creencias;
    private DesireGenerator generadorDeseos;
    private IntentionSelector selectorIntenciones;

    // BEHAVIORS

    private Dictionary<BehaviorType, IBehavior> behaviors = new Dictionary<BehaviorType, IBehavior>();
    private IBehavior behaviorActivo = null;
    private BehaviorType behaviorActivo_tipo = BehaviorType.None;


    // DELEGADOS

    private ProtocolHandler protocolHandler;
    private ContractNetManager contractNetManager;

    // Lambda guardada para poder desuscribirse correctamente en OnDestroy
    private System.Action<ACLMessage> onCFP;

    // ESTADO INTERNO

    private float temporizadorComprobacion;
    private float temporizadorHeartbeat;
    private float ultimoInformeAvistamiento = -100f;
    private int ultimaVentanaChequeoPedestal = -1;
    private float ultimaLimpiezaStale = 0f;

    // INICIALIZACIÓN

    void Start()
    {
        comunicacion = GetComponent<ComunicacionAgente>();
        sensorVision = GetComponent<SensorVision>();
        sensorOido = GetComponent<SensorOido>();
        actuador = GetComponent<ActuadorMovimiento>();

        comunicacion.Inicializar(agentId, "guard");

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
        behaviors[BehaviorType.BlockExit]      = new BlockExitBehavior(puntoSalida, puntosBloqueoSalida);
        behaviors[BehaviorType.CheckPedestal]  = new CheckPedestalBehavior(pedestalAnillo);

        temporizadorHeartbeat = Random.Range(0f, intervaloHeartbeatEstado);

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

        comunicacion.OnInformRecibido       += protocolHandler.ManejarInform;
        comunicacion.OnRequestRecibido      += protocolHandler.ManejarRequest;
        comunicacion.OnQueryRecibido        += protocolHandler.ManejarQuery;
        comunicacion.OnCFPRecibido          += onCFP;
        comunicacion.OnPropuestaAceptada    += protocolHandler.ManejarPropuestaAceptada;
        comunicacion.OnPropuestaRechazada   += protocolHandler.ManejarPropuestaRechazada;
        comunicacion.OnDoneRecibido         += protocolHandler.ManejarDone;
        comunicacion.OnAgreeRecibido        += protocolHandler.ManejarAgree;
        comunicacion.OnRefuseRecibido       += protocolHandler.ManejarRefuse;

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
            comunicacion.OnInformRecibido     -= protocolHandler.ManejarInform;
            comunicacion.OnRequestRecibido    -= protocolHandler.ManejarRequest;
            comunicacion.OnQueryRecibido      -= protocolHandler.ManejarQuery;
            comunicacion.OnCFPRecibido        -= onCFP;
            comunicacion.OnPropuestaAceptada  -= protocolHandler.ManejarPropuestaAceptada;
            comunicacion.OnPropuestaRechazada -= protocolHandler.ManejarPropuestaRechazada;
            comunicacion.OnDoneRecibido       -= protocolHandler.ManejarDone;
            comunicacion.OnAgreeRecibido      -= protocolHandler.ManejarAgree;
            comunicacion.OnRefuseRecibido     -= protocolHandler.ManejarRefuse;
        }

        contractNetManager?.Limpiar();
    }

    // CICLO PRINCIPAL

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        creencias.MiPosicion = transform.position;
        creencias.EstadoActual = behaviorActivo_tipo;

        if (ComprobarCaptura()) return;

        comunicacion.ProcesarMensajes();
        ActualizarTemporizadorComprobacion();
        ActualizarHeartbeatEstado();

        if (Time.time - ultimaLimpiezaStale > 10f)
        {
            creencias.LimpiarGuardiasStale();
            ultimaLimpiezaStale = Time.time;
        }

        List<Desire> deseos = generadorDeseos.GenerarDeseos();
        selectorIntenciones.Seleccionar(deseos, creencias);

        if (selectorIntenciones.CambioDeIntencion)
            ActivarBehavior(selectorIntenciones.NombreIntencion);

        EjecutarBehaviorActivo();
        contractNetManager.Gestionar();

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

    // MANEJADORES DE SENSORES

    private void OnLadronVisto(Vector3 posicion)
    {
        Vector3 direccion = CalcularDireccionObservada(posicion);
        bool tieneDireccion = direccion.sqrMagnitude > 0.01f;

        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId, direccion, tieneDireccion);
        ComprobarSiLlevaAnillo();
        InformarAvistamiento(posicion, direccion, tieneDireccion, true);
    }

    private void OnLadronSigueVisible(Vector3 posicion)
    {
        Vector3 direccion = CalcularDireccionObservada(posicion);
        bool tieneDireccion = direccion.sqrMagnitude > 0.01f;

        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId, direccion, tieneDireccion);
        ComprobarSiLlevaAnillo();
        InformarAvistamiento(posicion, direccion, tieneDireccion, false);
    }

    private void OnLadronPerdido()
    {
        creencias.MarcarLadronPerdido();
        protocolHandler.InformarPredicado(PredicateType.THIEF_LOST);
        contractNetManager.IniciarDistribucionBusqueda();
    }

    private void OnAnilloDesaparecido()
    {
        creencias.MarcarAnilloRobado();
        creencias.DebeBuscarAlrededorPedestal = false;
        creencias.DebeComprobarPedestal = false;
        protocolHandler.InformarPredicado(PredicateType.RING_STOLEN);
        Debug.Log($"[{agentId}] Anillo robado detectado");
    }

    private void OnSonidoDetectado(Vector3 posicion)
    {
        creencias.ActualizarPosicionLadron(posicion, Time.time, false, agentId);
    }

    private void ComprobarSiLlevaAnillo()
    {
        if (sensorVision == null ||
            !sensorVision.ObjetivoVisibleConAnillo ||
            creencias.LadronVistoConAnillo)
        {
            return;
        }

        creencias.MarcarLadronConAnillo();
        protocolHandler.InformarPredicado(PredicateType.RING_STOLEN, "seen-carrying-ring");
        Debug.Log($"[{agentId}] Ladrón visto llevando el anillo");
    }

    private Vector3 CalcularDireccionObservada(Vector3 nuevaPosicion)
    {
        if (!creencias.TieneInfoReciente(2f))
            return Vector3.zero;

        Vector3 delta = nuevaPosicion - creencias.UltimaPosicionLadron;
        if (delta.sqrMagnitude < 0.04f)
            return Vector3.zero;

        delta.y = 0f;
        return delta.normalized;
    }

    private void InformarAvistamiento(Vector3 posicion, Vector3 direccion, bool tieneDireccion, bool forzar)
    {
        if (!forzar && Time.time - ultimoInformeAvistamiento < intervaloActualizacionAvistamiento)
            return;

        ultimoInformeAvistamiento = Time.time;
        protocolHandler.InformarAvistamiento(new ThiefSighting
        {
            Location     = new Position(posicion),
            Direction    = tieneDireccion ? new Position(direccion) : null,
            Timestamp    = Time.time,
            ReportedBy   = agentId,
            DirectVision = true
        });
    }

    // GESTIÓN DE BEHAVIORS

    private void ActivarBehavior(BehaviorType tipo)
    {
        if (behaviorActivo != null)
            behaviorActivo.Detener(actuador);

        if (behaviors.TryGetValue(tipo, out IBehavior nuevoBehavior))
        {
            behaviorActivo = nuevoBehavior;
            behaviorActivo_tipo = tipo;
            behaviorActivo.Iniciar(creencias, actuador);
            Debug.Log($"[{agentId}] Behavior activado: {tipo}");
        }
        else
        {
            behaviorActivo = behaviors[BehaviorType.Patrol];
            behaviorActivo_tipo = BehaviorType.Patrol;
            behaviorActivo.Iniciar(creencias, actuador);
            Debug.Log($"[{agentId}] Behavior no encontrado, fallback a Patrol");
        }

        BroadcastEstado();
    }

    private void BroadcastEstado()
    {
        GuardStatus miEstado = new GuardStatus
        {
            GuardId         = agentId,
            CurrentPosition = new Position(transform.position),
            CurrentState    = behaviorActivo_tipo.ToString(),
            IsAvailable     = selectorIntenciones.EstaDisponible(),
            CurrentZone     = creencias.TareaAsignada?.ZoneId ?? ""
        };
        ACLMessage statusMsg = new ACLMessage(ACLPerformative.INFORM, agentId, "");
        statusMsg.Content  = ContentLanguage.Encode(miEstado);
        statusMsg.Protocol = "fipa-inform";
        comunicacion.BroadcastATipo(statusMsg, "guard");
    }

    private void EjecutarBehaviorActivo()
    {
        if (behaviorActivo == null) return;

        bool terminado = behaviorActivo.Ejecutar(creencias, actuador);

        if (terminado)
        {
            Debug.Log($"[{agentId}] Behavior '{behaviorActivo_tipo}' completado");

            if (behaviorActivo_tipo == BehaviorType.SearchAssigned && creencias.TieneTareaAsignada)
            {
                creencias.RegistrarBusquedaCompletada(creencias.TareaAsignada.ZoneId);

                // Si la tarea vino de un Contract-Net, notificar al iniciador.
                // Las tareas auto-asignadas no tienen asignador y no requieren INFORM_DONE.
                if (!string.IsNullOrEmpty(creencias.AsignadorTarea))
                    protocolHandler.NotificarDone(creencias.ConversacionTareaAsignada, creencias.AsignadorTarea);

                protocolHandler.InformarPredicado(PredicateType.ZONE_CLEAR);
                creencias.LimpiarTarea();
            }

            if (creencias.TieneRequestPendiente)
            {
                protocolHandler.NotificarDone(creencias.ConversacionRequest, creencias.SolicitanteRequest);
                creencias.LimpiarRequest();
            }

            if (behaviorActivo_tipo == BehaviorType.CheckPedestal)
            {
                creencias.RegistrarChequeoPedestal();
                if (!creencias.AnilloRobado && creencias.TieneInfoReciente(12f))
                    creencias.DebeBuscarAlrededorPedestal = true;
            }

            // Tras una búsqueda libre (Search), si quedan zonas sin cubrir por el resto
            // del equipo, auto-asignarse la mejor candidata para mantenerse útil.
            if (behaviorActivo_tipo == BehaviorType.Search)
                IntentarAutoAsignacionDeZona();

            behaviorActivo.Detener(actuador);
            behaviorActivo = null;
            behaviorActivo_tipo = BehaviorType.None;
            selectorIntenciones.ForzarReset();
        }
    }

    // AUTO-ASIGNACIÓN DE ZONA

    // Tras una búsqueda libre (Search alrededor de la última posición), el guardia
    // mira qué zonas no están cubiertas por el resto del equipo (vía CurrentZone
    // difundido en el heartbeat) y se auto-asigna una. Es self-assignment local
    // basado en información compartida, no Contract-Net — no hay iniciador ni
    // INFORM_DONE asociado al terminar.
    private void IntentarAutoAsignacionDeZona()
    {
        string zonaLibre = creencias.ObtenerZonaSinCubrir(soloExit: creencias.AnilloRobado);
        if (string.IsNullOrEmpty(zonaLibre)) return;

        Vector3 centro = creencias.ObtenerCentroZona(zonaLibre);
        SearchTask tarea = new SearchTask
        {
            TaskId     = $"self-{agentId}-{zonaLibre}-{Time.time:F0}",
            ZoneId     = zonaLibre,
            TargetArea = new Position(centro),
            Radius     = 15f,
            Urgency    = 0.6f
        };

        // Asignador vacío indica auto-asignación: al completarse no se notificará a nadie.
        creencias.AsignarTarea(tarea, "", "");
        Debug.Log($"[{agentId}] Auto-asignación a zona libre: {zonaLibre}");
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
