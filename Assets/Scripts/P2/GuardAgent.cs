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

    // Deliberación desacoplada del frame rate: se ejecuta cada INTERVALO_DELIBERACION
    // segundos o cuando un evento relevante lo fuerza. La ejecución del behavior
    // activo sí corre cada frame para garantizar movimiento suave.
    private float tiempoUltimaDeliberacion = 0f;
    private bool deliberacionPendiente = false;
    private const float INTERVALO_DELIBERACION = 0.5f;
    private bool busquedaCoordinadaPendiente = false;
    private float tiempoPerdidaLadron = -100f;
    private int versionTareaProcesada = 0;
    private const float RETARDO_BUSQUEDA_COORDINADA = BeliefBase.TIEMPO_INFO_TACTICA_LADRON;
    private static float ultimaRondaBusquedaCoordinada = -100f;
    private static Vector3 ultimaPosicionRondaBusqueda = Vector3.positiveInfinity;
    private static string responsableBusquedaCoordinada = "";
    private const float DISTANCIA_MISMA_RONDA_BUSQUEDA = 2f;
    private float tiempoInicioCoberturaSalidaInsuficiente = -100f;
    private const float RETARDO_RELLENO_BLOQUEO_SALIDA = 2.5f;
    private static GUIStyle estiloDebugPanel;
    private static GUIStyle estiloDebugTitulo;
    private static List<GuardAgent> guardiasDebug = new List<GuardAgent>();

    // INICIALIZACIÓN

    void Start()
    {
        comunicacion = GetComponent<ComunicacionAgente>();
        sensorVision = GetComponent<SensorVision>();
        sensorOido = GetComponent<SensorOido>();
        actuador = GetComponent<ActuadorMovimiento>();

        comunicacion.Inicializar(agentId, "guard");
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

    private Color ColorPorBehavior(BehaviorType tipo)
    {
        switch (tipo)
        {
            case BehaviorType.Pursuit: return new Color(0.85f, 0.12f, 0.12f);
            case BehaviorType.Intercept: return new Color(0.95f, 0.65f, 0.08f);
            case BehaviorType.Search:
            case BehaviorType.SearchAssigned: return new Color(0.1f, 0.35f, 0.85f);
            case BehaviorType.BlockExit: return new Color(0.45f, 0.15f, 0.75f);
            case BehaviorType.CheckPedestal: return new Color(0.9f, 0.9f, 0.9f);
            case BehaviorType.Patrol: return new Color(0.1f, 0.55f, 0.2f);
            default: return new Color(0.25f, 0.25f, 0.25f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!mostrarMarcadorDebug) return;

        Gizmos.color = Application.isPlaying ? ColorPorBehavior(behaviorActivo_tipo) : Color.cyan;
        Vector3 centro = transform.position + Vector3.up * 1.8f;
        Gizmos.DrawSphere(centro, 0.45f);
        Gizmos.DrawLine(transform.position, centro);
    }

    private void DibujarPanelDebug()
    {
        if (!mostrarDebugEnPantalla || guardiasDebug.Count == 0) return;

        List<GuardAgent> ordenados = new List<GuardAgent>(guardiasDebug);
        ordenados.RemoveAll(g => g == null);
        ordenados.Sort((a, b) => string.Compare(a.agentId, b.agentId, System.StringComparison.Ordinal));
        if (ordenados.Count == 0 || ordenados[0] != this) return;

        if (estiloDebugPanel == null)
        {
            estiloDebugPanel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            estiloDebugPanel.normal.textColor = Color.white;
        }

        if (estiloDebugTitulo == null)
        {
            estiloDebugTitulo = new GUIStyle(estiloDebugPanel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            estiloDebugTitulo.normal.textColor = Color.white;
        }

        float x = posicionPanelDebug.x;
        float y = posicionPanelDebug.y;
        float ancho = anchoPanelDebug;
        float alto = 34f + ordenados.Count * 23f;

        Color anterior = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.95f);
        GUI.Box(new Rect(x, y, ancho, alto), GUIContent.none);
        GUI.color = anterior;

        GUI.Label(new Rect(x + 10f, y + 6f, ancho - 20f, 22f), "DEBUG GUARDIAS", estiloDebugTitulo);

        for (int i = 0; i < ordenados.Count; i++)
        {
            GuardAgent guardia = ordenados[i];
            if (guardia == null || guardia.creencias == null) continue;

            string zona = guardia.creencias.TareaAsignada != null ? guardia.creencias.TareaAsignada.ZoneId : "-";
            string linea = $"{AbreviarAgente(guardia.agentId),-4} {AbreviarBehavior(guardia.behaviorActivo_tipo),-10} {AbreviarFase(guardia.creencias.FaseActual()),-13} Z:{zona}";

            GUI.color = guardia.ColorPorBehavior(guardia.behaviorActivo_tipo);
            GUI.DrawTexture(new Rect(x + 10f, y + 38f + i * 23f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 32f, y + 31f + i * 23f, ancho - 42f, 22f), linea, estiloDebugPanel);
        }

        GUI.color = anterior;
    }

    private static string AbreviarFase(TacticalPhase fase)
    {
        switch (fase)
        {
            case TacticalPhase.NormalPatrol: return "Normal";
            case TacticalPhase.RingSafeThiefKnown: return "SinAnillo/V";
            case TacticalPhase.RingSafeThiefLost: return "SinAnillo/P";
            case TacticalPhase.RingStolenThiefKnown: return "Anillo/V";
            case TacticalPhase.RingStolenThiefLost: return "Anillo/P";
            default: return fase.ToString();
        }
    }

    private static string AbreviarBehavior(BehaviorType tipo)
    {
        switch (tipo)
        {
            case BehaviorType.Patrol: return "Patrol";
            case BehaviorType.Pursuit: return "Pursuit";
            case BehaviorType.Search: return "Search";
            case BehaviorType.SearchAssigned: return "SearchZone";
            case BehaviorType.Intercept: return "Intercept";
            case BehaviorType.BlockExit: return "BlockExit";
            case BehaviorType.CheckPedestal: return "Pedestal";
            case BehaviorType.None: return "Deciding";
            default: return tipo.ToString();
        }
    }

    private static string AbreviarAgente(string id)
    {
        if (string.IsNullOrEmpty(id)) return "G?";

        int guion = id.LastIndexOf('_');
        if (guion >= 0 && guion < id.Length - 1)
            return "G" + id.Substring(guion + 1);

        return id.Length <= 4 ? id : id.Substring(0, 4);
    }

    // CAPA DE PERCEPCIÓN — Manejadores de sensores
    // Solo actualizan creencias y activan flags de comunicación pendiente.
    // Ningún sensor llama directamente a ProtocolHandler ni a ContractNetManager.

    private void OnLadronVisto(Vector3 posicion)
    {
        Vector3 direccion = CalcularDireccionObservada(posicion);
        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId,
            direccion, direccion.sqrMagnitude > 0.01f);
        creencias.PrimerAvistamiento = true;
        busquedaCoordinadaPendiente = false;
        creencias.BuscarLocalAntesDeCoordinar = false;
        creencias.ComprobarPedestalTrasBusquedaLocal = false;
        creencias.DebeComprobarPedestalPrioritario = false;
        deliberacionPendiente = true; // Pursuit(100) puede entrar en juego
        ComprobarSiLlevaAnillo();
    }

    private void OnLadronSigueVisible(Vector3 posicion)
    {
        Vector3 direccion = CalcularDireccionObservada(posicion);
        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId,
            direccion, direccion.sqrMagnitude > 0.01f);
        busquedaCoordinadaPendiente = false;
        creencias.BuscarLocalAntesDeCoordinar = false;
        creencias.ComprobarPedestalTrasBusquedaLocal = false;
        creencias.DebeComprobarPedestalPrioritario = false;
        ComprobarSiLlevaAnillo();
        // No fuerza deliberación: ya estamos en Pursuit, solo actualizamos posición
    }

    private void OnLadronPerdido()
    {
        creencias.MarcarLadronPerdido();
        creencias.PendienteComunicarLadronPerdido = true;
        busquedaCoordinadaPendiente = true;
        creencias.BuscarLocalAntesDeCoordinar = false;
        creencias.ComprobarPedestalTrasBusquedaLocal = !creencias.AnilloRobado;
        tiempoPerdidaLadron = Time.time;
        ReclamarResponsabilidadBusquedaCoordinada();
        deliberacionPendiente = true; // Pursuit desaparece, Search/BlockExit entran
    }

    private void OnAnilloDesaparecido()
    {
        creencias.MarcarAnilloRobado(); // activa NecesitaDeliberar internamente
        creencias.DebeBuscarAlrededorPedestal = false;
        creencias.DebeComprobarPedestal = false;
        creencias.DebeComprobarPedestalPrioritario = false;
        creencias.PendienteComunicarAnilloDesaparecido = true;
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
            creencias.LadronVistoConAnillo) return;

        creencias.MarcarLadronConAnillo();
        creencias.PendienteComunicarLadronConAnillo = true;
        Debug.Log($"[{agentId}] Ladrón visto llevando el anillo");
    }

    private Vector3 CalcularDireccionObservada(Vector3 nuevaPosicion)
    {
        if (!creencias.TieneInfoReciente(2f)) return Vector3.zero;

        Vector3 delta = nuevaPosicion - creencias.UltimaPosicionLadron;
        if (delta.sqrMagnitude < 0.04f) return Vector3.zero;

        delta.y = 0f;
        return delta.normalized;
    }

    // CAPA DE COMUNICACIÓN REACTIVA
    // Consume los flags de comunicación pendiente y decide qué mensajes FIPA
    // enviar. Se ejecuta cada frame desde Update(), después de procesar el buzón
    // y antes del ciclo BDI. Esta capa actúa como puente explícito entre
    // percepción y comportamiento, manteniendo ambas capas desacopladas:
    // añadir un nuevo sensor solo requiere actualizar creencias + activar un flag.
    private void GestionarComunicacionReactiva()
    {
        // Avistamiento continuo del ladrón — con throttle, forzado en primera detección
        if (creencias.LadronVisible)
            InformarAvistamientoSiProcede();

        // Ladrón perdido de vista — comunicar al equipo e iniciar búsqueda coordinada
        if (creencias.PendienteComunicarLadronPerdido)
        {
            protocolHandler.InformarPredicado(PredicateType.THIEF_LOST);
            creencias.PendienteComunicarLadronPerdido = false;
        }

        TacticalPhase fase = creencias.FaseActual();
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;
        if (busquedaCoordinadaPendiente &&
            faseContactoTactico &&
            !creencias.BuscarLocalAntesDeCoordinar &&
            Time.time - tiempoPerdidaLadron >= BeliefBase.TIEMPO_GRACIA_PERDIDA_LADRON)
        {
            creencias.BuscarLocalAntesDeCoordinar = true;
            creencias.NecesitaDeliberar = true;
        }

        if (busquedaCoordinadaPendiente &&
            (fase == TacticalPhase.RingSafeThiefLost ||
             fase == TacticalPhase.RingStolenThiefLost))
        {
            creencias.BuscarLocalAntesDeCoordinar = false;

            if (PuedeIniciarRondaBusquedaCoordinada())
            {
                bool contratoLanzado = contractNetManager.IniciarDistribucionBusqueda();
                if (contratoLanzado)
                {
                    ultimaRondaBusquedaCoordinada = Time.time;
                    ultimaPosicionRondaBusqueda = creencias.UltimaPosicionLadron;

                    if (!creencias.AnilloRobado)
                    {
                        responsableBusquedaCoordinada = "";
                        creencias.DebeComprobarPedestalPrioritario = true;
                        creencias.ComprobarPedestalTrasBusquedaLocal = false;
                        selectorIntenciones.ForzarReset();
                        deliberacionPendiente = true;
                    }
                }
                else if (!contratoLanzado)
                {
                    responsableBusquedaCoordinada = "";
                    busquedaCoordinadaPendiente = true;
                    tiempoPerdidaLadron = Time.time - BeliefBase.TIEMPO_INFO_TACTICA_LADRON;
                    return;
                }
            }
            else
            {
                creencias.ComprobarPedestalTrasBusquedaLocal = false;
            }

            busquedaCoordinadaPendiente = false;
        }

        // Anillo desaparecido del pedestal — alertar al equipo
        if (creencias.PendienteComunicarAnilloDesaparecido)
        {
            protocolHandler.InformarPredicado(PredicateType.RING_STOLEN);
            creencias.PendienteComunicarAnilloDesaparecido = false;
        }

        // Ladrón visto portando el anillo — alertar al equipo con contexto adicional
        if (creencias.PendienteComunicarLadronConAnillo)
        {
            protocolHandler.InformarPredicado(PredicateType.RING_STOLEN, "seen-carrying-ring");
            creencias.PendienteComunicarLadronConAnillo = false;
        }
    }

    private void ReclamarResponsabilidadBusquedaCoordinada()
    {
        if (creencias.AnilloRobado) return;

        busquedaCoordinadaPendiente = true;
        tiempoPerdidaLadron = Time.time;
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

    private void GestionarCambioTareaAsignada()
    {
        if (creencias.VersionTareaAsignada == versionTareaProcesada)
            return;

        versionTareaProcesada = creencias.VersionTareaAsignada;
        if (!creencias.TieneTareaAsignada)
            return;

        bool puedeInterrumpir =
            behaviorActivo_tipo == BehaviorType.Search ||
            behaviorActivo_tipo == BehaviorType.SearchAssigned ||
            behaviorActivo_tipo == BehaviorType.Patrol ||
            behaviorActivo_tipo == BehaviorType.None;

        if (!puedeInterrumpir)
            return;

        selectorIntenciones.ForzarReset();
        deliberacionPendiente = true;
    }

    private void GestionarCoberturaSalidaRobada()
    {
        if (!creencias.AnilloRobado || !creencias.TienePosicionSalida)
        {
            tiempoInicioCoberturaSalidaInsuficiente = -100f;
            return;
        }

        if (creencias.GuardiasEnEstado(BehaviorType.BlockExit) >= 2)
        {
            tiempoInicioCoberturaSalidaInsuficiente = -100f;
            return;
        }

        if (tiempoInicioCoberturaSalidaInsuficiente < 0f)
        {
            tiempoInicioCoberturaSalidaInsuficiente = Time.time;
            return;
        }

        if (Time.time - tiempoInicioCoberturaSalidaInsuficiente < RETARDO_RELLENO_BLOQUEO_SALIDA)
            return;

        if (behaviorActivo_tipo == BehaviorType.BlockExit ||
            !creencias.SoyEntreMasCercanosParaBloquearSalida(2, false))
            return;

        selectorIntenciones.ForzarReset();
        creencias.NecesitaDeliberar = true;
        deliberacionPendiente = true;
        tiempoInicioCoberturaSalidaInsuficiente = Time.time;
        Debug.Log($"[{agentId}] Cobertura de salida insuficiente; forzando relevo a BlockExit");
    }

    private void InformarAvistamientoSiProcede()
    {
        bool forzar = creencias.PrimerAvistamiento;
        if (!forzar && Time.time - ultimoInformeAvistamiento < intervaloActualizacionAvistamiento)
            return;

        ultimoInformeAvistamiento = Time.time;
        creencias.PrimerAvistamiento = false;

        protocolHandler.InformarAvistamiento(new ThiefSighting
        {
            Location     = new Position(creencias.UltimaPosicionLadron),
            Direction    = creencias.TieneDireccionLadron
                           ? new Position(creencias.UltimaDireccionLadron) : null,
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

        if (tipo == BehaviorType.BlockExit && creencias.AnilloRobado && creencias.TieneTareaAsignada)
            creencias.LimpiarTarea();

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
            BehaviorType behaviorTerminado = behaviorActivo_tipo;

            if ((behaviorTerminado == BehaviorType.Pursuit ||
                 behaviorTerminado == BehaviorType.Intercept) &&
                !creencias.LadronVisible &&
                !creencias.AnilloRobado &&
                creencias.FaseActual() == TacticalPhase.RingSafeThiefKnown)
            {
                ReclamarResponsabilidadBusquedaCoordinada();
            }

            if (behaviorTerminado == BehaviorType.SearchAssigned && creencias.TieneTareaAsignada)
            {
                creencias.RegistrarBusquedaCompletada(creencias.TareaAsignada.ZoneId);

                // Si la tarea vino de un Contract-Net, notificar al iniciador.
                // Las tareas auto-asignadas no tienen asignador y no requieren INFORM_DONE.
                if (!string.IsNullOrEmpty(creencias.AsignadorTarea))
                    protocolHandler.NotificarDone(creencias.ConversacionTareaAsignada, creencias.AsignadorTarea);

                protocolHandler.InformarPredicado(PredicateType.ZONE_CLEAR);
                creencias.LimpiarTarea();

                if (creencias.AnilloRobado &&
                    !creencias.SoyEntreMasCercanosParaBloquearSalida(2, false))
                {
                    IntentarAutoAsignacionDeZona();
                }
            }

            if (creencias.TieneRequestPendiente)
            {
                protocolHandler.NotificarDone(creencias.ConversacionRequest, creencias.SolicitanteRequest);
                creencias.LimpiarRequest();
            }

            if (behaviorTerminado == BehaviorType.CheckPedestal)
            {
                creencias.RegistrarChequeoPedestal();
                creencias.ComprobarPedestalTrasBusquedaLocal = false;
                if (!creencias.AnilloRobado && creencias.TieneInfoReciente(12f))
                    creencias.DebeBuscarAlrededorPedestal = true;
            }

            // Tras una búsqueda libre (Search), si quedan zonas sin cubrir por el resto
            // del equipo, auto-asignarse la mejor candidata para mantenerse útil.
            if (behaviorTerminado == BehaviorType.Search)
            {
                creencias.BuscarLocalAntesDeCoordinar = false;
                IntentarAutoAsignacionDeZona();
            }

            behaviorActivo.Detener(actuador);
            behaviorActivo = null;
            behaviorActivo_tipo = BehaviorType.None;
            selectorIntenciones.ForzarReset();
            deliberacionPendiente = true; // behavior completado, elegir siguiente
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
        if (creencias.AnilloRobado &&
            creencias.SoyEntreMasCercanosParaBloquearSalida(2, false))
            return;

        if (!creencias.AnilloRobado &&
            creencias.FaseActual() == TacticalPhase.RingSafeThiefLost)
            return;

        if (creencias.ComprobarPedestalTrasBusquedaLocal)
        {
            if (creencias.FaseActual() == TacticalPhase.RingSafeThiefKnown)
                creencias.BuscarLocalAntesDeCoordinar = true;

            creencias.NecesitaDeliberar = true;
            return;
        }

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

    private bool PuedeIniciarRondaBusquedaCoordinada()
    {
        bool mismaRonda =
            Time.time - ultimaRondaBusquedaCoordinada < RETARDO_BUSQUEDA_COORDINADA &&
            Vector3.Distance(ultimaPosicionRondaBusqueda, creencias.UltimaPosicionLadron) < DISTANCIA_MISMA_RONDA_BUSQUEDA;

        if (mismaRonda)
            return false;

        if (!SoyResponsableDeBusquedaCoordinada())
            return false;

        return true;
    }

    private bool SoyResponsableDeBusquedaCoordinada()
    {
        if (string.IsNullOrEmpty(responsableBusquedaCoordinada))
            responsableBusquedaCoordinada = CalcularResponsableBusquedaCoordinada();

        return responsableBusquedaCoordinada == agentId;
    }

    private string CalcularResponsableBusquedaCoordinada()
    {
        string mejorId = agentId;
        float miDistancia = Vector3.Distance(transform.position, creencias.UltimaPosicionLadron);
        float mejorDistancia = miDistancia;

        foreach (var par in AgentRegistry.Instance.ObtenerIdsPorTipo("guard"))
        {
            if (par == agentId) continue;

            ComunicacionAgente otro = AgentRegistry.Instance.ObtenerAgente(par);
            if (otro == null) continue;

            GuardAgent otroGuardia = otro.GetComponent<GuardAgent>();
            if (otroGuardia == null || !otroGuardia.busquedaCoordinadaPendiente)
                continue;

            float suDistancia = Vector3.Distance(otroGuardia.transform.position, creencias.UltimaPosicionLadron);
            bool estaMasCerca = suDistancia < mejorDistancia - 0.25f;
            bool empataConMejorId = Mathf.Abs(suDistancia - mejorDistancia) <= 0.25f &&
                                    string.Compare(otroGuardia.agentId, mejorId, System.StringComparison.Ordinal) < 0;

            if (estaMasCerca || empataConMejorId)
            {
                mejorId = otroGuardia.agentId;
                mejorDistancia = suDistancia;
            }
        }

        Debug.Log($"[{agentId}] Responsable CN fase anillo elegido: {mejorId}");
        return mejorId;
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
