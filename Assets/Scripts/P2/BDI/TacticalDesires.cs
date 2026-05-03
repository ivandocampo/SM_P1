using System.Collections.Generic;
using UnityEngine;

public partial class DesireGenerator
{
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

        TacticalPhasePolicy politica = ObtenerPoliticaDeFase(fase);
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
            PrioridadPorCercania(DesirePriorities.PursuitBase, creencias.UltimaPosicionLadron, 0.12f),
            creencias.UltimaPosicionLadron
        ));
    }

    private void AgregarIntercepcionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        TacticalPhasePolicy politica = ObtenerPoliticaDeFase(fase);
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

    private void AgregarBloqueoSalidaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (!creencias.AnilloRobado) return;
        if (!creencias.TienePosicionSalida) return;

        TacticalPhasePolicy politica = ObtenerPoliticaDeFase(fase);

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
                DesirePriorities.FallbackDefense,
                creencias.PosicionSalida
            ));
            return;
        }

        deseos.Add(new Desire(
            BehaviorType.Search,
            DesirePriorities.FallbackDefense,
            creencias.UltimaPosicionLadron
        ));
    }
}
