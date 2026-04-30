using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Desire
{
    public BehaviorType Nombre;

    /// <summary>Prioridad del deseo. Mayor = mas urgente. Rango tipico: 0-100.</summary>
    public float Prioridad;

    /// <summary>Posicion objetivo asociada al deseo (si aplica).</summary>
    public Vector3 PosicionObjetivo;

    /// <summary>Datos adicionales del contexto (tarea asignada, request, etc.).</summary>
    public string DatosExtra;

    public Desire(BehaviorType nombre, float prioridad, Vector3 posicion = default, string datos = "")
    {
        Nombre = nombre;
        Prioridad = prioridad;
        PosicionObjetivo = posicion;
        DatosExtra = datos;
    }

    public override string ToString()
    {
        return $"{Nombre} (prioridad: {Prioridad:F0})";
    }
}

public class DesireGenerator
{
    private BeliefBase creencias;

    public DesireGenerator(BeliefBase creencias)
    {
        this.creencias = creencias;
    }

    public List<Desire> GenerarDeseos()
    {
        List<Desire> deseos = new List<Desire>();
        TacticalPhase fase = creencias.FaseActual();

        AgregarPersecucionSiProcede(deseos, fase);
        AgregarTareaAsignada(deseos);
        AgregarRequestAceptado(deseos);
        AgregarIntercepcionSiProcede(deseos, fase);
        AgregarDefensaPedestalSiProcede(deseos, fase);
        AgregarBusquedaSiProcede(deseos, fase);
        AgregarBloqueoSalidaSiProcede(deseos, fase);

        if (creencias.DebeComprobarPedestal &&
            !creencias.AnilloRobado &&
            creencias.TienePosicionPedestal &&
            !creencias.AlguienGuardandoPedestal() &&
            creencias.SoyElMasCercanoA(creencias.PosicionPedestal))
        {
            deseos.Add(new Desire(BehaviorType.CheckPedestal, 35f));
        }

        deseos.Add(new Desire(BehaviorType.Patrol, 10f));
        return deseos;
    }

    private void AgregarPersecucionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefKnown &&
            fase != TacticalPhase.RingStolenThiefKnown)
            return;

        const int MAX_PERSEGUIDORES = 1;
        bool soyPerseguidorActual = creencias.EstadoActual == BehaviorType.Pursuit;
        bool hayHueco = creencias.GuardiasEnEstado(BehaviorType.Pursuit) < MAX_PERSEGUIDORES;
        bool soyMejorPerseguidor = creencias.SoyElMasCercanoA(creencias.UltimaPosicionLadron);

        if (!soyPerseguidorActual && !hayHueco && !soyMejorPerseguidor)
            return;

        deseos.Add(new Desire(
            BehaviorType.Pursuit,
            PrioridadPorCercania(100f, creencias.UltimaPosicionLadron, 0.12f),
            creencias.UltimaPosicionLadron
        ));
    }

    private void AgregarTareaAsignada(List<Desire> deseos)
    {
        if (!creencias.TieneTareaAsignada) return;

        deseos.Add(new Desire(
            BehaviorType.SearchAssigned,
            85f,
            creencias.TareaAsignada.TargetArea.ToVector3(),
            creencias.ConversacionTareaAsignada
        ));
    }

    private void AgregarRequestAceptado(List<Desire> deseos)
    {
        if (!creencias.TieneRequestPendiente) return;

        string accion = creencias.RequestAceptado.Action;
        Vector3 pos = creencias.RequestAceptado.TargetPosition != null
            ? creencias.RequestAceptado.TargetPosition.ToVector3()
            : Vector3.zero;

        BehaviorType nombreDeseo =
            accion == ActionType.BLOCK_EXIT.ToString() ? BehaviorType.BlockExit :
            accion == ActionType.SEARCH_AREA.ToString() ? BehaviorType.SearchAssigned :
            BehaviorType.Search;

        deseos.Add(new Desire(nombreDeseo, 75f, pos, creencias.ConversacionRequest));
    }

    private void AgregarIntercepcionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        int maxInterceptores;
        float prioridadBase;

        switch (fase)
        {
            case TacticalPhase.RingSafeThiefKnown:
                maxInterceptores = 2;
                prioridadBase = 92f;
                break;
            case TacticalPhase.RingSafeThiefLost:
                maxInterceptores = 2;
                prioridadBase = 74f;
                break;
            case TacticalPhase.RingStolenThiefKnown:
                maxInterceptores = 2;
                prioridadBase = 90f;
                break;
            case TacticalPhase.RingStolenThiefLost:
                maxInterceptores = 2;
                prioridadBase = 76f;
                break;
            default:
                return;
        }

        bool faseConLadronLocalizado = fase == TacticalPhase.RingSafeThiefKnown ||
                                       fase == TacticalPhase.RingStolenThiefKnown;
        if (!faseConLadronLocalizado && !creencias.TieneObjetivoCriticoActual())
            return;

        Vector3 referenciaSeleccion = faseConLadronLocalizado
            ? creencias.UltimaPosicionLadron
            : creencias.CalcularPuntoInterceptacion(0);
        int maxCandidatos = maxInterceptores + (faseConLadronLocalizado ? 1 : 0);

        bool soyInterceptorActual = creencias.EstadoActual == BehaviorType.Intercept;
        if (!soyInterceptorActual &&
            !creencias.SoyEntreMasCercanosA(referenciaSeleccion, maxCandidatos))
            return;

        if (!soyInterceptorActual &&
            creencias.GuardiasEnEstado(BehaviorType.Intercept) >= maxInterceptores)
            return;

        Vector3 punto = creencias.CalcularPuntoInterceptacion();
        deseos.Add(new Desire(
            BehaviorType.Intercept,
            PrioridadPorCercania(prioridadBase, punto, 0.12f),
            punto
        ));
    }

    private void AgregarDefensaPedestalSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (creencias.AnilloRobado || !creencias.TienePosicionPedestal)
            return;

        if (fase != TacticalPhase.RingSafeThiefKnown &&
            fase != TacticalPhase.RingSafeThiefLost)
            return;

        bool soyGuardaPedestal = creencias.EstadoActual == BehaviorType.CheckPedestal;
        if (!soyGuardaPedestal &&
            creencias.GuardiasEnEstado(BehaviorType.CheckPedestal) >= 1)
            return;

        deseos.Add(new Desire(
            BehaviorType.CheckPedestal,
            PrioridadPorCercania(86f, creencias.PosicionPedestal, 0.18f),
            creencias.PosicionPedestal
        ));
    }

    private void AgregarBusquedaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefLost &&
            fase != TacticalPhase.RingStolenThiefLost)
            return;

        const int MAX_BUSCADORES = 2;
        bool soyBuscadorActual = creencias.EstadoActual == BehaviorType.Search;
        if (!soyBuscadorActual &&
            creencias.GuardiasBuscando() >= MAX_BUSCADORES)
            return;

        deseos.Add(new Desire(
            BehaviorType.Search,
            PrioridadPorCercania(70f, creencias.UltimaPosicionLadron, 0.1f),
            creencias.UltimaPosicionLadron
        ));
    }

    private void AgregarBloqueoSalidaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (!creencias.AnilloRobado) return;

        const int MAX_BLOQUEADORES = 2;
        bool soyBloqueadorActual = creencias.EstadoActual == BehaviorType.BlockExit;
        if (!soyBloqueadorActual &&
            creencias.GuardiasEnEstado(BehaviorType.BlockExit) >= MAX_BLOQUEADORES)
            return;

        float prioridadBloqueo = fase == TacticalPhase.RingStolenThiefKnown ? 94f : 96f;
        deseos.Add(new Desire(BehaviorType.BlockExit, prioridadBloqueo));
    }

    private float PrioridadPorCercania(float basePrioridad, Vector3 objetivo, float penalizacionPorMetro)
    {
        float distancia = Vector3.Distance(creencias.MiPosicion, objetivo);
        return basePrioridad - distancia * penalizacionPorMetro;
    }
}
