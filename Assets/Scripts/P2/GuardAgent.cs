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

    [Header("Temporizadores")]
    public float tiempoEntreComprobaciones = 30f;
    public float cooldownContractNet = 10f;

    

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
        behaviors[BehaviorType.Investigate]    = new InvestigateBehavior(3f);
        behaviors[BehaviorType.CheckPedestal]  = new CheckPedestalBehavior(pedestalAnillo);

        temporizadorComprobacion = Random.Range(15f, tiempoEntreComprobaciones);

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
            comunicacion.OnInformRecibido       -= protocolHandler.ManejarInform;
            comunicacion.OnRequestRecibido      -= protocolHandler.ManejarRequest;
            comunicacion.OnQueryRecibido        -= protocolHandler.ManejarQuery;
            comunicacion.OnCFPRecibido          -= onCFP;
            comunicacion.OnPropuestaAceptada    -= protocolHandler.ManejarPropuestaAceptada;
            comunicacion.OnPropuestaRechazada   -= protocolHandler.ManejarPropuestaRechazada;
            comunicacion.OnDoneRecibido         -= protocolHandler.ManejarDone;
            comunicacion.OnAgreeRecibido        -= protocolHandler.ManejarAgree;
            comunicacion.OnRefuseRecibido       -= protocolHandler.ManejarRefuse;
        }
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
        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId);
        comunicacion.InformarAvistamiento(new ThiefSighting
        {
            Location     = new Position(posicion),
            Timestamp    = Time.time,
            ReportedBy   = agentId,
            DirectVision = true
        });
    }

    private void OnLadronSigueVisible(Vector3 posicion)
    {
        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId);
    }

    private void OnLadronPerdido()
    {
        creencias.MarcarLadronPerdido();
        comunicacion.InformarPredicado(PredicateType.THIEF_LOST);
        contractNetManager.IniciarDistribucionBusqueda();
    }

    private void OnAnilloDesaparecido()
    {
        creencias.MarcarAnilloRobado();
        comunicacion.InformarPredicado(PredicateType.RING_STOLEN);
        Debug.Log($"[{agentId}] Anillo robado detectado");
    }

    private void OnSonidoDetectado(Vector3 posicion)
    {
        creencias.ActualizarPosicionLadron(posicion, Time.time, false, agentId);
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
            IsAvailable     = selectorIntenciones.EstaDisponible()
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
                comunicacion.NotificarDone(creencias.ConversacionTareaAsignada, creencias.AsignadorTarea);
                comunicacion.InformarPredicado(PredicateType.ZONE_CLEAR);
                creencias.LimpiarTarea();
            }

            if (creencias.TieneRequestPendiente)
            {
                comunicacion.NotificarDone(creencias.ConversacionRequest, creencias.SolicitanteRequest);
                creencias.LimpiarRequest();
            }

            if (behaviorActivo_tipo == BehaviorType.CheckPedestal)
                creencias.DebeComprobarPedestal = false;

            behaviorActivo.Detener(actuador);
            behaviorActivo = null;
            behaviorActivo_tipo = BehaviorType.None;
            selectorIntenciones.ForzarReset();
        }
    }

    // TEMPORIZADOR PEDESTAL

    private void ActualizarTemporizadorComprobacion()
    {
        if (creencias.AnilloRobado) return;
        if (behaviorActivo_tipo != BehaviorType.Patrol) return;

        temporizadorComprobacion -= Time.deltaTime;
        if (temporizadorComprobacion <= 0)
        {
            temporizadorComprobacion = tiempoEntreComprobaciones;
            creencias.DebeComprobarPedestal = true;
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