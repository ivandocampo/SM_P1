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
    private const int MAX_TACTICOS_ANILLO_SEGURO = 4;
    private const int MAX_TACTICOS_ANILLO_ROBADO = 3;
    private const int MAX_BLOQUEADORES_SALIDA = 2;

    public DesireGenerator(BeliefBase creencias)
    {
        this.creencias = creencias;
    }

    public List<Desire> GenerarDeseos()
    {
        List<Desire> deseos = new List<Desire>();
        TacticalPhase fase = creencias.FaseActual();
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        AgregarPersecucionSiProcede(deseos, fase);
        AgregarTareaAsignada(deseos);

        if (creencias.TieneTareaAsignada &&
            fase == TacticalPhase.RingSafeThiefLost)
        {
            deseos.Add(new Desire(BehaviorType.Patrol, 10f));
            return deseos;
        }

        AgregarRequestAceptado(deseos);
        AgregarIntercepcionSiProcede(deseos, fase);
        AgregarComprobacionPrioritariaPedestal(deseos);
        AgregarDefensaPedestalSiProcede(deseos, fase);
        AgregarBusquedaSiProcede(deseos, fase);
        AgregarBloqueoSalidaSiProcede(deseos, fase);

        if (faseContactoTactico &&
            !creencias.LadronVisible &&
            VeniaDeComportamientoTactico() &&
            !deseos.Exists(d => d.Nombre != BehaviorType.Patrol))
        {
            deseos.Add(new Desire(
                BehaviorType.Search,
                82f,
                creencias.UltimaPosicionLadron
            ));
        }

        if (creencias.ComprobarPedestalTrasBusquedaLocal &&
            fase == TacticalPhase.RingSafeThiefLost &&
            !creencias.TieneTareaAsignada &&
            !creencias.AnilloRobado &&
            creencias.TienePosicionPedestal)
        {
            deseos.Add(new Desire(BehaviorType.CheckPedestal, 88f));
        }
        else if (creencias.DebeComprobarPedestal &&
                 !creencias.TieneTareaAsignada &&
                 !faseContactoTactico &&
                 !creencias.AnilloRobado &&
                 creencias.TienePosicionPedestal &&
                 !creencias.AlguienGuardandoPedestal() &&
                 creencias.SoyElMasCercanoA(creencias.PosicionPedestal))
        {
            deseos.Add(new Desire(BehaviorType.CheckPedestal, 35f));
        }

        if (creencias.AnilloRobado)
        {
            AgregarFallbackDefensaSalida(deseos, fase);
            return deseos;
        }

        deseos.Add(new Desire(BehaviorType.Patrol, 10f));
        return deseos;
    }

    private bool VeniaDeComportamientoTactico()
    {
        return creencias.EstadoActual == BehaviorType.Intercept ||
               creencias.EstadoActual == BehaviorType.Pursuit ||
               creencias.EstadoActual == BehaviorType.Search;
    }

    private void AgregarPersecucionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefKnown &&
            fase != TacticalPhase.RingStolenThiefKnown)
            return;

        // Solo el guardia que mantiene contacto visual debe perseguir directamente.
        // Los guardias que conocen la posicion por comunicacion se organizan como
        // interceptores, bloqueadores o buscadores para evitar persecuciones redundantes.
        if (!creencias.LadronVisible || !creencias.TieneDeteccionPropiaReciente())
            return;

        int maxPerseguidores = creencias.AnilloRobado
            ? MAX_TACTICOS_ANILLO_ROBADO
            : MAX_TACTICOS_ANILLO_SEGURO;

        if (creencias.AnilloRobado)
        {
            if (!SoyCandidatoTacticoAnilloRobado(creencias.UltimaPosicionLadron, maxPerseguidores))
                return;
        }
        else if (!creencias.SoyEntreMasCercanosA(creencias.UltimaPosicionLadron, maxPerseguidores))
        {
            return;
        }

        deseos.Add(new Desire(
            BehaviorType.Pursuit,
            PrioridadPorCercania(100f, creencias.UltimaPosicionLadron, 0.12f),
            creencias.UltimaPosicionLadron
        ));
    }

    private void AgregarTareaAsignada(List<Desire> deseos)
    {
        if (!creencias.TieneTareaAsignada) return;

        TacticalPhase fase = creencias.FaseActual();
        if (fase != TacticalPhase.RingSafeThiefLost &&
            fase != TacticalPhase.RingStolenThiefLost)
            return;

        deseos.Add(new Desire(
            BehaviorType.SearchAssigned,
            95f,
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

    private void AgregarComprobacionPrioritariaPedestal(List<Desire> deseos)
    {
        if (!creencias.DebeComprobarPedestalPrioritario ||
            creencias.TieneTareaAsignada ||
            creencias.AnilloRobado ||
            !creencias.TienePosicionPedestal)
            return;

        deseos.Add(new Desire(
            BehaviorType.CheckPedestal,
            96f,
            creencias.PosicionPedestal
        ));
    }

    private void AgregarIntercepcionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        int maxInterceptores;
        float prioridadBase;

        switch (fase)
        {
            case TacticalPhase.RingSafeThiefKnown:
                maxInterceptores = Mathf.Max(0,
                    MAX_TACTICOS_ANILLO_SEGURO - creencias.GuardiasEnEstado(BehaviorType.Pursuit));
                prioridadBase = 92f;
                break;
            case TacticalPhase.RingSafeThiefLost:
                if (creencias.AntiguedadInfoLadron >= 3f)
                    return;
                maxInterceptores = 2;
                prioridadBase = 74f;
                break;
            case TacticalPhase.RingStolenThiefKnown:
                maxInterceptores = Mathf.Max(0,
                    MAX_TACTICOS_ANILLO_ROBADO - creencias.GuardiasEnEstado(BehaviorType.Pursuit));
                prioridadBase = 90f;
                break;
            case TacticalPhase.RingStolenThiefLost:
                if (creencias.AntiguedadInfoLadron >= 3f)
                    return;
                maxInterceptores = 2;
                prioridadBase = 76f;
                break;
            default:
                return;
        }

        bool faseConLadronLocalizado = fase == TacticalPhase.RingSafeThiefKnown ||
                                       fase == TacticalPhase.RingStolenThiefKnown;
        if (faseConLadronLocalizado && maxInterceptores <= 0)
            return;

        if (!faseConLadronLocalizado && !creencias.TieneObjetivoCriticoActual())
            return;

        Vector3 referenciaSeleccion = faseConLadronLocalizado
            ? creencias.UltimaPosicionLadron
            : creencias.CalcularPuntoInterceptacion(0);
        int maxCandidatos = faseConLadronLocalizado
            ? MAX_TACTICOS_ANILLO_ROBADO
            : maxInterceptores;

        bool soyInterceptorActual = creencias.EstadoActual == BehaviorType.Intercept;
        if (creencias.AnilloRobado)
        {
            if (!SoyCandidatoTacticoAnilloRobado(referenciaSeleccion, maxCandidatos))
                return;
        }
        else if (!creencias.SoyEntreMasCercanosA(referenciaSeleccion, maxCandidatos))
        {
            return;
        }

        if (faseConLadronLocalizado &&
            !soyInterceptorActual &&
            creencias.GuardiasEnEstado(BehaviorType.Intercept) >= maxInterceptores)
            return;

        if (!faseConLadronLocalizado &&
            !soyInterceptorActual &&
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
        if (creencias.TieneTareaAsignada ||
            creencias.AnilloRobado ||
            !creencias.TienePosicionPedestal)
            return;

        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        if (!faseContactoTactico)
            return;

        if (!creencias.SoyElMasCercanoA(creencias.PosicionPedestal))
            return;

        if (Time.time - creencias.UltimoChequeoPedestal < 6f)
            return;

        bool soyGuardaPedestal = creencias.EstadoActual == BehaviorType.CheckPedestal;
        if (!soyGuardaPedestal &&
            creencias.GuardiasEnEstado(BehaviorType.CheckPedestal) >= 1)
            return;

        float prioridad = faseContactoTactico ? 94f :
            PrioridadPorCercania(86f, creencias.PosicionPedestal, 0.18f);

        deseos.Add(new Desire(
            BehaviorType.CheckPedestal,
            prioridad,
            creencias.PosicionPedestal
        ));
    }

    private void AgregarBusquedaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        if (creencias.BuscarLocalAntesDeCoordinar && faseContactoTactico)
        {
            deseos.Add(new Desire(
                BehaviorType.Search,
                98f,
                creencias.UltimaPosicionLadron
            ));
            return;
        }

        if (fase != TacticalPhase.RingSafeThiefLost &&
            fase != TacticalPhase.RingStolenThiefLost)
            return;

        const int MAX_BUSCADORES = 5;
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
        if (!creencias.TienePosicionSalida) return;

        bool contactoDirectoPropio = creencias.LadronVisible && creencias.TieneDeteccionPropiaReciente();
        if (!creencias.SoyEntreMasCercanosParaBloquearSalida(MAX_BLOQUEADORES_SALIDA, contactoDirectoPropio))
            return;

        bool soyBloqueadorActual = creencias.EstadoActual == BehaviorType.BlockExit;

        float prioridadBloqueo = fase == TacticalPhase.RingStolenThiefKnown ? 98f : 99f;
        if (soyBloqueadorActual)
            prioridadBloqueo += 1f;

        deseos.Add(new Desire(BehaviorType.BlockExit, prioridadBloqueo));
    }

    private bool SoyCandidatoTacticoAnilloRobado(Vector3 referencia, int maxAgentes)
    {
        HashSet<string> bloqueadores = creencias.ObtenerIdsBloqueadoresSalida(MAX_BLOQUEADORES_SALIDA);

        if (creencias.LadronVisible && creencias.TieneDeteccionPropiaReciente())
            bloqueadores.Remove(creencias.MiId);

        return creencias
            .ObtenerIdsMasCercanosA(referencia, maxAgentes, bloqueadores)
            .Contains(creencias.MiId);
    }

    private void AgregarFallbackDefensaSalida(List<Desire> deseos, TacticalPhase fase)
    {
        bool tieneDeseoOperativo = deseos.Exists(d => d.Nombre != BehaviorType.Patrol);
        if (tieneDeseoOperativo)
            return;

        if (creencias.TienePosicionSalida)
        {
            deseos.Add(new Desire(
                BehaviorType.BlockExit,
                30f,
                creencias.PosicionSalida
            ));
            return;
        }

        deseos.Add(new Desire(
            BehaviorType.Search,
            30f,
            creencias.UltimaPosicionLadron
        ));
    }

    private float PrioridadPorCercania(float basePrioridad, Vector3 objetivo, float penalizacionPorMetro)
    {
        float distancia = Vector3.Distance(creencias.MiPosicion, objetivo);
        return basePrioridad - distancia * penalizacionPorMetro;
    }
}
