using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TacticalPhase
{
    NormalPatrol,
    RingSafeThiefKnown,
    RingSafeThiefLost,
    RingStolenThiefKnown,
    RingStolenThiefLost
}

[System.Serializable]
public partial class BeliefBase
{
    public const float TIEMPO_INFO_TACTICA_LADRON = TacticalConfig.ThiefInfoRecentSeconds;
    public const float TIEMPO_INVESTIGACION_LADRON = TacticalConfig.ThiefInvestigationSeconds;
    public const float TIEMPO_GRACIA_PERDIDA_LADRON = TacticalConfig.LostGraceSeconds;

    // CREENCIAS SOBRE EL LADRÓN

    /// <summary>Si el ladrón es visible en este momento (por sensores propios).</summary>
    public bool LadronVisible { get; private set; } = false;

    /// <summary>Última posición conocida del ladrón (vista u oída, propia o reportada).</summary>
    public Vector3 UltimaPosicionLadron { get; private set; } = Vector3.zero;

    /// <summary>Timestamp de la última detección del ladrón.</summary>
    public float TiempoUltimaDeteccion { get; private set; } = -100f;

    /// <summary>Si la última detección fue por visión directa (true) u oído/reporte (false).</summary>
    public bool UltimaDeteccionDirecta { get; private set; } = false;

    /// <summary>ID del agente que reportó la última posición (puede ser el propio).</summary>
    public string FuenteUltimaDeteccion { get; private set; } = "";

    /// <summary>Si tenemos información reciente sobre el ladrón (menos de maxAge segundos).</summary>
    public bool TieneInfoReciente(float maxAge = 10f)
    {
        return (Time.time - TiempoUltimaDeteccion) < maxAge;
    }

    /// <summary>Antigüedad en segundos de la última información sobre el ladrón.</summary>
    public float AntiguedadInfoLadron => Time.time - TiempoUltimaDeteccion;

    /// <summary>Si la última detección reciente procede de un sensor propio, no de un mensaje.</summary>
    public bool TieneDeteccionPropiaReciente(float maxAge = 8f)
    {
        return FuenteUltimaDeteccion == MiId && TieneInfoReciente(maxAge);
    }

    /// <summary>Dirección más reciente conocida del ladrón.</summary>
    public Vector3 UltimaDireccionLadron { get; private set; } = Vector3.zero;

    public bool TieneDireccionLadron { get; private set; } = false;

    // CREENCIAS SOBRE EL ANILLO

    /// <summary>Si se sabe que el anillo ha sido robado.</summary>
    public bool AnilloRobado { get; private set; } = false;

    /// <summary>Timestamp de cuando se supo que el anillo fue robado.</summary>
    public float TiempoAnilloRobado { get; private set; } = -100f;

    /// <summary>Si algún guardia ha visto directamente al ladrón llevando el anillo.</summary>
    public bool LadronVistoConAnillo { get; private set; } = false;

    // CREENCIAS SOBRE EL PROPIO AGENTE

    /// <summary>ID del propio agente.</summary>
    public string MiId { get; private set; }

    /// <summary>Posición actual del agente (actualizada cada frame).</summary>
    public Vector3 MiPosicion { get; set; }

    /// <summary>Posición del pedestal, conocida por el propio agente.</summary>
    public Vector3 PosicionPedestal { get; set; } = Vector3.zero;

    public bool TienePosicionPedestal { get; set; } = false;

    /// <summary>Posición de la salida del mapa, conocida por el propio agente.</summary>
    public Vector3 PosicionSalida { get; set; } = Vector3.zero;

    public bool TienePosicionSalida { get; set; } = false;

    /// <summary>Nombre de la intención/estado actual.</summary>
    public BehaviorType EstadoActual { get; set; } = BehaviorType.None;

    /// <summary>Si el agente está disponible para aceptar tareas.</summary>
    public bool Disponible => EstadoActual == BehaviorType.Patrol || EstadoActual == BehaviorType.None;

    public bool DebeComprobarPedestal { get; set; } = false;
    public bool DebeComprobarPedestalPrioritario { get; set; } = false;
    public bool DebeBuscarAlrededorPedestal { get; set; } = false;
    public bool BuscarLocalAntesDeCoordinar { get; set; } = false;
    public bool ComprobarPedestalTrasBusquedaLocal { get; set; } = false;
    public float UltimoChequeoPedestal { get; private set; } = -100f;

    // EVENTOS DE COMUNICACIÓN PENDIENTES
    // La capa de percepción (sensores) los activa al detectar cambios relevantes.
    // La capa de comunicación (GestionarComunicacionReactiva en GuardAgent) los
    // consume cada frame, desacoplando ambas capas: añadir un nuevo sensor solo
    // requiere actualizar creencias y activar el flag correspondiente.

    /// <summary>El ladrón acaba de perderse de vista — comunicar THIEF_LOST e iniciar búsqueda coordinada.</summary>
    public bool PendienteComunicarLadronPerdido { get; set; } = false;

    /// <summary>El pedestal del anillo está vacío — comunicar RING_STOLEN al equipo.</summary>
    public bool PendienteComunicarAnilloDesaparecido { get; set; } = false;

    /// <summary>Se ha visto al ladrón portando el anillo — comunicar RING_STOLEN con contexto adicional.</summary>
    public bool PendienteComunicarLadronConAnillo { get; set; } = false;

    public bool PendienteBusquedaCoordinadaPorInformeExterno { get; set; } = false;

    /// <summary>Primera detección del ladrón en este ciclo — forzar avistamiento inmediato sin throttle.</summary>
    public bool PrimerAvistamiento { get; set; } = false;

    /// <summary>
    /// Se ha producido un cambio de creencias relevante que puede alterar la intención óptima
    /// (tarea asignada vía Contract-Net, anillo robado recibido por mensaje...).
    /// GuardAgent lo consume para forzar una ronda de deliberación inmediata.
    /// </summary>
    public bool NecesitaDeliberar { get; set; } = false;

    // TAREAS ASIGNADAS

    /// <summary>Tarea de búsqueda asignada vía Contract Net.</summary>
    public SearchTask TareaAsignada { get; private set; } = null;

    /// <summary>Si hay una tarea asignada pendiente de ejecutar.</summary>
    public bool TieneTareaAsignada => TareaAsignada != null;

    /// <summary>ID de la conversación de la tarea asignada (para INFORM_DONE).</summary>
    public string ConversacionTareaAsignada { get; private set; } = "";

    /// <summary>ID del agente que nos asignó la tarea.</summary>
    public string AsignadorTarea { get; private set; } = "";

    public int VersionTareaAsignada { get; private set; } = 0;

    // REQUEST PENDIENTES

    /// <summary>Solicitud de acción recibida vía REQUEST que aceptamos.</summary>
    public ActionRequest RequestAceptado { get; private set; } = null;

    /// <summary>Si hay un REQUEST aceptado pendiente.</summary>
    public bool TieneRequestPendiente => RequestAceptado != null;

    public string ConversacionRequest { get; private set; } = "";
    public string SolicitanteRequest { get; private set; } = "";

    // CONSTRUCTOR

    public BeliefBase(string agentId)
    {
        MiId = agentId;
    }

    // ACTUALIZACIONES — Llamadas desde el GuardAgent

    
    public void ActualizarPosicionLadron(Vector3 posicion, float timestamp,
        bool visionDirecta, string fuente, Vector3 direccion = default(Vector3), bool tieneDireccion = false)
    {
        // Solo aceptar información más reciente
        if (timestamp <= TiempoUltimaDeteccion) return;

        UltimaPosicionLadron = posicion;
        TiempoUltimaDeteccion = timestamp;
        UltimaDeteccionDirecta = visionDirecta;
        FuenteUltimaDeteccion = fuente;

        if (tieneDireccion && direccion.sqrMagnitude > 0.01f)
        {
            UltimaDireccionLadron = direccion.normalized;
            TieneDireccionLadron = true;
        }

        // Solo marcar como visible si es visión directa propia
        if (visionDirecta && fuente == MiId)
            LadronVisible = true;
    }

    
    public void MarcarLadronPerdido()
    {
        LadronVisible = false;
        NecesitaDeliberar = true;
    }

    
    public void MarcarAnilloRobado()
    {
        if (!AnilloRobado)
        {
            AnilloRobado = true;
            TiempoAnilloRobado = Time.time;
            // El robo cambia el escenario radicalmente: las zonas exit pasan a ser
            // las únicas relevantes, así que el tracking previo de zonas generales
            // ya no aporta y conviene partir de cero.
            ultimaBusquedaPorZona.Clear();
            NecesitaDeliberar = true; // BlockExit(95) entra en juego
        }
    }


    public void RegistrarChequeoPedestal()
    {
        UltimoChequeoPedestal = Time.time;
        DebeComprobarPedestal = false;
        DebeComprobarPedestalPrioritario = false;
        PendienteBusquedaCoordinadaPorInformeExterno = false;
    }


    public void MarcarLadronConAnillo()
    {
        LadronVistoConAnillo = true;
        MarcarAnilloRobado();
    }

    
    public TacticalPhase FaseActual()
    {
        if (AnilloRobado)
            return TieneInfoReciente(TIEMPO_INFO_TACTICA_LADRON)
                ? TacticalPhase.RingStolenThiefKnown
                : TacticalPhase.RingStolenThiefLost;

        if (TieneInfoReciente(TIEMPO_INFO_TACTICA_LADRON))
            return TacticalPhase.RingSafeThiefKnown;

        if (TieneInfoReciente(TIEMPO_INVESTIGACION_LADRON))
            return TacticalPhase.RingSafeThiefLost;

        return TacticalPhase.NormalPatrol;
    }

    public void DescartarHipotesisLadronCaducada()
    {
        LadronVisible = false;
        UltimaDeteccionDirecta = false;
        FuenteUltimaDeteccion = "";
        TiempoUltimaDeteccion = -100f;
        UltimaDireccionLadron = Vector3.zero;
        TieneDireccionLadron = false;
        BuscarLocalAntesDeCoordinar = false;
        ComprobarPedestalTrasBusquedaLocal = false;
        DebeBuscarAlrededorPedestal = false;
        DebeComprobarPedestalPrioritario = false;
        LimpiarTarea();
        LimpiarRequest();
        NecesitaDeliberar = true;
    }

    public Vector3 ObjetivoCriticoActual()
    {
        if (AnilloRobado && TienePosicionSalida)
            return PosicionSalida;

        if (!AnilloRobado && TienePosicionPedestal)
            return PosicionPedestal;

        return UltimaPosicionLadron;
    }

    public bool TieneObjetivoCriticoActual()
    {
        return AnilloRobado ? TienePosicionSalida : TienePosicionPedestal;
    }

    public int CarrilInterceptacion()
    {
        List<string> guardiasActivos = EstadosOtrosGuardias
            .Where(par => par.Value.CurrentState == BehaviorType.Intercept.ToString() ||
                          par.Value.CurrentState == BehaviorType.Pursuit.ToString())
            .Select(par => par.Key)
            .ToList();

        guardiasActivos.Add(MiId);
        guardiasActivos = guardiasActivos.Distinct().ToList();
        guardiasActivos.Sort(StringComparer.Ordinal);

        int modulo = guardiasActivos.IndexOf(MiId) % 3;
        if (modulo == 0) return -1;
        if (modulo == 1) return 0;
        return 1;
    }

    public Vector3 CalcularPuntoInterceptacion(float distanciaAdelante = 6f, float distanciaLateral = 4f)
    {
        return CalcularPuntoInterceptacion(CarrilInterceptacion(), distanciaAdelante, distanciaLateral);
    }

    public Vector3 CalcularPuntoInterceptacion(int carril, float distanciaAdelante = 6f, float distanciaLateral = 4f)
    {
        if (FaseActual() == TacticalPhase.RingSafeThiefKnown)
            return CalcularPuntoCierreSobreLadron(carril, distanciaAdelante, distanciaLateral);

        return CalcularPuntoCorteRuta(carril, distanciaAdelante, distanciaLateral);
    }

    private Vector3 CalcularPuntoCierreSobreLadron(int carril, float distanciaAdelante, float distanciaLateral)
    {
        Vector3 direccionCierre = TieneDireccionLadron
            ? UltimaDireccionLadron
            : (UltimaPosicionLadron - MiPosicion);
        direccionCierre.y = 0f;

        if (direccionCierre.sqrMagnitude < 0.01f)
            return UltimaPosicionLadron;

        direccionCierre.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, direccionCierre).normalized;

        return UltimaPosicionLadron
            + direccionCierre * distanciaAdelante
            + lateral * carril * distanciaLateral;
    }

    private Vector3 CalcularPuntoCorteRuta(int carril, float distanciaAdelante, float distanciaLateral)
    {
        Vector3 objetivo = ObjetivoCriticoActual();
        Vector3 direccionObjetivo = objetivo - UltimaPosicionLadron;
        direccionObjetivo.y = 0f;

        if (direccionObjetivo.sqrMagnitude < 0.01f)
        {
            direccionObjetivo = TieneDireccionLadron
                ? UltimaDireccionLadron
                : (UltimaPosicionLadron - MiPosicion);
            direccionObjetivo.y = 0f;
        }

        if (direccionObjetivo.sqrMagnitude < 0.01f)
            return UltimaPosicionLadron;

        direccionObjetivo.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, direccionObjetivo).normalized;

        return UltimaPosicionLadron
            + direccionObjetivo * distanciaAdelante
            + lateral * carril * distanciaLateral;
    }

    public void AsignarTarea(SearchTask tarea, string conversacionId, string asignador)
    {
        TareaAsignada = tarea;
        ConversacionTareaAsignada = conversacionId;
        AsignadorTarea = asignador;
        ReservarZonaGlobal(tarea?.ZoneId);
        VersionTareaAsignada++;
        NecesitaDeliberar = true; // SearchAssigned(85) entra en juego
    }


    public void LimpiarTarea()
    {
        LiberarReservaZonaGlobal(TareaAsignada?.ZoneId);
        TareaAsignada = null;
        ConversacionTareaAsignada = "";
        AsignadorTarea = "";
    }


    public void AceptarRequest(ActionRequest request, string conversacionId, string solicitante)
    {
        RequestAceptado = request;
        ConversacionRequest = conversacionId;
        SolicitanteRequest = solicitante;
    }

    public void LimpiarRequest()
    {
        RequestAceptado = null;
        ConversacionRequest = "";
        SolicitanteRequest = "";
    }

    // CONSULTAS DE ALTO NIVEL

    
    public float DistanciaEstimadaAlLadron()
    {
        return Vector3.Distance(MiPosicion, UltimaPosicionLadron);
    }


}
