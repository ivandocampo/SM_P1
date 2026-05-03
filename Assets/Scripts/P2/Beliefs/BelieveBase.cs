// =============================================================
// Base de creencias del guardia BDI.
// Guarda lo que el agente sabe sobre Frodo, el anillo, su propio
// estado, las tareas aceptadas y los eventos pendientes de comunicar.
// Esta clase es parcial: GuardTeamBeliefs y SearchZoneBeliefs amplian
// la informacion de equipo y de zonas de busqueda
// =============================================================

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
    // Ventanas de tiempo que separan informacion reciente, investigable y caducada.
    public const float TIEMPO_INFO_TACTICA_LADRON = TacticalConfig.ThiefInfoRecentSeconds;
    public const float TIEMPO_INVESTIGACION_LADRON = TacticalConfig.ThiefInvestigationSeconds;
    public const float TIEMPO_GRACIA_PERDIDA_LADRON = TacticalConfig.LostGraceSeconds;

    // Creencias sobre Frodo

    public bool LadronVisible { get; private set; } = false;

    public Vector3 UltimaPosicionLadron { get; private set; } = Vector3.zero;

    public float TiempoUltimaDeteccion { get; private set; } = -100f;

    public bool UltimaDeteccionDirecta { get; private set; } = false;

    public string FuenteUltimaDeteccion { get; private set; } = "";

    public bool TieneInfoReciente(float maxAge = 10f)
    {
        return (Time.time - TiempoUltimaDeteccion) < maxAge;
    }

    public float AntiguedadInfoLadron => Time.time - TiempoUltimaDeteccion;

    public bool TieneDeteccionPropiaReciente(float maxAge = 8f)
    {
        return FuenteUltimaDeteccion == MiId && TieneInfoReciente(maxAge);
    }

    public Vector3 UltimaDireccionLadron { get; private set; } = Vector3.zero;

    public bool TieneDireccionLadron { get; private set; } = false;

    // Creencias sobre el anillo

    public bool AnilloRobado { get; private set; } = false;

    public float TiempoAnilloRobado { get; private set; } = -100f;

    public bool LadronVistoConAnillo { get; private set; } = false;

    // Creencias sobre el propio guardia

    public string MiId { get; private set; }

    public Vector3 MiPosicion { get; set; }

    public Vector3 PosicionPedestal { get; set; } = Vector3.zero;

    public bool TienePosicionPedestal { get; set; } = false;

    public Vector3 PosicionSalida { get; set; } = Vector3.zero;

    public bool TienePosicionSalida { get; set; } = false;

    public BehaviorType EstadoActual { get; set; } = BehaviorType.None;

    public bool Disponible => EstadoActual == BehaviorType.Patrol || EstadoActual == BehaviorType.None;

    public bool DebeComprobarPedestal { get; set; } = false;
    public bool DebeComprobarPedestalPrioritario { get; set; } = false;
public bool BuscarLocalAntesDeCoordinar { get; set; } = false;
    public bool ComprobarPedestalTrasBusquedaLocal { get; set; } = false;
    public float UltimoChequeoPedestal { get; private set; } = -100f;

    // Eventos de comunicacion pendientes

    public bool PendienteComunicarLadronPerdido { get; set; } = false;

    public bool PendienteComunicarAnilloDesaparecido { get; set; } = false;

    public bool PendienteComunicarLadronConAnillo { get; set; } = false;

    public bool PendienteBusquedaCoordinadaPorInformeExterno { get; set; } = false;

    public bool PrimerAvistamiento { get; set; } = false;

    public bool NecesitaDeliberar { get; set; } = false;

    // Tareas asignadas por Contract-Net

    public SearchTask TareaAsignada { get; private set; } = null;

    public bool TieneTareaAsignada => TareaAsignada != null;

    public string ConversacionTareaAsignada { get; private set; } = "";

    public string AsignadorTarea { get; private set; } = "";

    public int VersionTareaAsignada { get; private set; } = 0;

    // REQUEST aceptados

    public ActionRequest RequestAceptado { get; private set; } = null;

    public bool TieneRequestPendiente => RequestAceptado != null;

    public string ConversacionRequest { get; private set; } = "";
    public string SolicitanteRequest { get; private set; } = "";

    // Construcción

    public BeliefBase(string agentId)
    {
        MiId = agentId;
    }

    // Actualizaciones llamadas desde GuardAgent y sensores

    public void ActualizarPosicionLadron(Vector3 posicion, float timestamp,
        bool visionDirecta, string fuente, Vector3 direccion = default(Vector3), bool tieneDireccion = false)
    {
        // Evita que un mensaje antiguo sobreescriba una deteccion mas nueva
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

        // Solo se marca visible si la deteccion directa procede de este guardia
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
            // Al cambiar el objetivo tactico hacia la salida, las zonas previas dejan de servir
            ultimaBusquedaPorZona.Clear();
            NecesitaDeliberar = true;
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

    // Resume el estado del mundo en una fase tactica
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
        // Cuando la informacion sobre Frodo caduca del todo, se vuelve a un estado limpio
        LadronVisible = false;
        UltimaDeteccionDirecta = false;
        FuenteUltimaDeteccion = "";
        TiempoUltimaDeteccion = -100f;
        UltimaDireccionLadron = Vector3.zero;
        TieneDireccionLadron = false;
        BuscarLocalAntesDeCoordinar = false;
        ComprobarPedestalTrasBusquedaLocal = false;
        DebeComprobarPedestalPrioritario = false;
        LimpiarTarea();
        LimpiarRequest();
        NecesitaDeliberar = true;
    }

    public Vector3 ObjetivoCriticoActual()
    {
        // Si Frodo robo el anillo, el objetivo critico pasa a ser la salida
        if (AnilloRobado && TienePosicionSalida)
            return PosicionSalida;

        // Si el anillo sigue seguro, el punto critico es el pedestal
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
        // Ordena los guardias activos para repartir carriles laterales de forma estable
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
        // Con el anillo seguro interesa cerrar sobre Frodo; en otras fases se corta su ruta
        if (FaseActual() == TacticalPhase.RingSafeThiefKnown)
            return CalcularPuntoCierreSobreLadron(carril, distanciaAdelante, distanciaLateral);

        return CalcularPuntoCorteRuta(carril, distanciaAdelante, distanciaLateral);
    }

    private Vector3 CalcularPuntoCierreSobreLadron(int carril, float distanciaAdelante, float distanciaLateral)
    {
        // Usa la direccion conocida de Frodo; si no existe, aproxima desde la posicion del guardia
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
        // Calcula la linea probable entre Frodo y su objetivo critico
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
        // La reserva global evita que dos guardias hagan la misma zona a la vez
        TareaAsignada = tarea;
        ConversacionTareaAsignada = conversacionId;
        AsignadorTarea = asignador;
        ReservarZonaGlobal(tarea?.ZoneId);
        VersionTareaAsignada++;
        NecesitaDeliberar = true;
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

    // Distancia estimada desde este guardia hasta la ultima posicion conocida de Frodo
    public float DistanciaEstimadaAlLadron()
    {
        return Vector3.Distance(MiPosicion, UltimaPosicionLadron);
    }


}
