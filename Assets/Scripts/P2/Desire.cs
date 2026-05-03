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

    private struct PhasePolicy
    {
        public int MaxTacticos;
        public int MaxInterceptoresTrasPerdida;
        public int MaxBloqueadoresSalida;
        public float PrioridadIntercept;
        public float PrioridadBloqueoSalida;
    }

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

    private PhasePolicy ObtenerPoliticaDeFase(TacticalPhase fase)
    {
        switch (fase)
        {
            case TacticalPhase.RingSafeThiefKnown:
                return new PhasePolicy
                {
                    MaxTacticos = MAX_TACTICOS_ANILLO_SEGURO,
                    MaxInterceptoresTrasPerdida = 2,
                    MaxBloqueadoresSalida = 0,
                    PrioridadIntercept = 92f,
                    PrioridadBloqueoSalida = 0f
                };
            case TacticalPhase.RingSafeThiefLost:
                return new PhasePolicy
                {
                    MaxTacticos = MAX_TACTICOS_ANILLO_SEGURO,
                    MaxInterceptoresTrasPerdida = 2,
                    MaxBloqueadoresSalida = 0,
                    PrioridadIntercept = 74f,
                    PrioridadBloqueoSalida = 0f
                };
            case TacticalPhase.RingStolenThiefKnown:
                return new PhasePolicy
                {
                    MaxTacticos = MAX_TACTICOS_ANILLO_ROBADO,
                    MaxInterceptoresTrasPerdida = 2,
                    MaxBloqueadoresSalida = MAX_BLOQUEADORES_SALIDA,
                    PrioridadIntercept = 90f,
                    PrioridadBloqueoSalida = 98f
                };
            case TacticalPhase.RingStolenThiefLost:
                return new PhasePolicy
                {
                    MaxTacticos = MAX_TACTICOS_ANILLO_ROBADO,
                    MaxInterceptoresTrasPerdida = 2,
                    MaxBloqueadoresSalida = MAX_BLOQUEADORES_SALIDA,
                    PrioridadIntercept = 76f,
                    PrioridadBloqueoSalida = 99f
                };
            default:
                return new PhasePolicy
                {
                    MaxTacticos = MAX_TACTICOS_ANILLO_SEGURO,
                    MaxInterceptoresTrasPerdida = 0,
                    MaxBloqueadoresSalida = 0,
                    PrioridadIntercept = 0f,
                    PrioridadBloqueoSalida = 0f
                };
        }
    }

    private void AgregarPersecucionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefKnown &&
            fase != TacticalPhase.RingStolenThiefKnown)
            return;

        // Solo el guardia que tuvo deteccion propia reciente debe perseguir directamente.
        // Los guardias que conocen la posicion por comunicacion se organizan como
        // interceptores, bloqueadores o buscadores para evitar persecuciones redundantes.
        if (!creencias.TieneDeteccionPropiaReciente())
            return;

        // Si ya entramos en busqueda local (1.5s sin visual), abandonamos Pursuit
        // para que el pursuer transicione a Search igual que hacen los interceptores.
        if (creencias.BuscarLocalAntesDeCoordinar)
            return;

        PhasePolicy politica = ObtenerPoliticaDeFase(fase);
        int maxPerseguidores = politica.MaxTacticos;

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
        PhasePolicy politica = ObtenerPoliticaDeFase(fase);
        int maxInterceptores;

        switch (fase)
        {
            case TacticalPhase.RingSafeThiefKnown:
                maxInterceptores = Mathf.Max(0,
                    politica.MaxTacticos - creencias.GuardiasEnEstado(BehaviorType.Pursuit));
                break;
            case TacticalPhase.RingSafeThiefLost:
                if (creencias.AntiguedadInfoLadron >= 3f)
                    return;
                maxInterceptores = politica.MaxInterceptoresTrasPerdida;
                break;
            case TacticalPhase.RingStolenThiefKnown:
                maxInterceptores = Mathf.Max(0,
                    politica.MaxTacticos - creencias.GuardiasEnEstado(BehaviorType.Pursuit));
                break;
            case TacticalPhase.RingStolenThiefLost:
                if (creencias.AntiguedadInfoLadron >= 3f)
                    return;
                maxInterceptores = politica.MaxInterceptoresTrasPerdida;
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
        int reservaPedestal = fase == TacticalPhase.RingSafeThiefKnown ? 1 : 0;
        int maxCandidatos = faseConLadronLocalizado
            ? maxInterceptores + creencias.GuardiasEnEstado(BehaviorType.Pursuit) + reservaPedestal
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
            PrioridadPorCercania(politica.PrioridadIntercept, punto, 0.12f),
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

        if (!SoyCandidatoPedestal(fase))
            return;

        if (fase != TacticalPhase.RingSafeThiefKnown &&
            Time.time - creencias.UltimoChequeoPedestal < 6f)
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

    private bool SoyCandidatoPedestal(TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefKnown)
            return creencias.SoyElMasCercanoA(creencias.PosicionPedestal);

        HashSet<string> excluir = new HashSet<string>();
        if (!string.IsNullOrEmpty(creencias.FuenteUltimaDeteccion))
            excluir.Add(creencias.FuenteUltimaDeteccion);

        if (creencias.TieneDeteccionPropiaReciente())
            excluir.Add(creencias.MiId);

        foreach (var par in creencias.EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.Pursuit.ToString())
                excluir.Add(par.Key);
        }

        return creencias
            .ObtenerIdsMasCercanosA(creencias.PosicionPedestal, 1, excluir)
            .Contains(creencias.MiId);
    }

    private void AgregarBusquedaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        if (creencias.BuscarLocalAntesDeCoordinar && faseContactoTactico)
        {
            // Si llego informacion fresca (otro guardia ve a Frodo), abandonamos
            // la busqueda local para que Intercept/Pursuit tomen el relevo.
            // Solo generamos Search(98) cuando nadie ha visto al ladron en la
            // ventana de gracia (1.5s).
            if (creencias.AntiguedadInfoLadron < BeliefBase.TIEMPO_GRACIA_PERDIDA_LADRON)
                return;

            deseos.Add(new Desire(
                BehaviorType.Search,
                98f,
                creencias.UltimaPosicionLadron
            ));
            return;
        }

        // En las fases de busqueda coordinada, la busqueda activa esta cubierta
        // por SearchAssigned. En Fase 5 no generamos Search libre porque rompe
        // el reparto 2 BlockExit + 3 zonas Exit_ durante pequenos huecos de
        // autoasignacion.
        if (creencias.AnilloRobado)
            return;

        return;
    }

    private void AgregarBloqueoSalidaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (!creencias.AnilloRobado) return;
        if (!creencias.TienePosicionSalida) return;

        PhasePolicy politica = ObtenerPoliticaDeFase(fase);

        bool contactoDirectoPropio = creencias.LadronVisible && creencias.TieneDeteccionPropiaReciente();
        bool soyBloqueadorActual = creencias.EstadoActual == BehaviorType.BlockExit;
        bool soyBloqueadorElegido = creencias.ObtenerIdsBloqueadoresSalidaEstables(
            politica.MaxBloqueadoresSalida,
            contactoDirectoPropio
        ).Contains(creencias.MiId);
        bool hayExcesoBloqueadores = creencias.GuardiasEnEstado(BehaviorType.BlockExit) >
                                     politica.MaxBloqueadoresSalida;

        // Sticky: si ya estoy bloqueando, mantengo BlockExit aunque me haya alejado
        // de la salida visitando puntos de bloqueo. Si hay mas de dos bloqueadores,
        // solo se quedan los dos candidatos reales.
        if (soyBloqueadorActual && hayExcesoBloqueadores && !soyBloqueadorElegido)
            return;

        if (!soyBloqueadorActual &&
            !soyBloqueadorElegido)
            return;

        float prioridadBloqueo = politica.PrioridadBloqueoSalida;
        if (soyBloqueadorActual)
            prioridadBloqueo += 1f;

        deseos.Add(new Desire(BehaviorType.BlockExit, prioridadBloqueo));
    }

    private bool SoyCandidatoTacticoAnilloRobado(Vector3 referencia, int maxAgentes)
    {
        HashSet<string> bloqueadores = creencias.ObtenerIdsBloqueadoresSalidaEstables(MAX_BLOQUEADORES_SALIDA);

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
            bool coberturaInsuficiente = creencias.GuardiasEnEstado(BehaviorType.BlockExit) < MAX_BLOQUEADORES_SALIDA;
            bool soyCandidato = creencias.ObtenerIdsBloqueadoresSalidaEstables(MAX_BLOQUEADORES_SALIDA)
                .Contains(creencias.MiId);

            if (!coberturaInsuficiente || !soyCandidato)
                return;

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
