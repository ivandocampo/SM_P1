// =============================================================
// Fichero parcial de DesireGenerator: deseos tácticos principales.
// Genera los deseos de persecución (Pursuit), intercepción (Intercept),
// bloqueo de salida (BlockExit) y fallback defensivo. Aplica límites
// de coordinación: solo el guardia con detección propia persigue;
// los demás se organizan como interceptores o bloqueadores.
// =============================================================

using System.Collections.Generic;
using UnityEngine;

public partial class DesireGenerator
{
    // Generar deseo de persecución directa; solo para el guardia con detección propia reciente
    private void AgregarPersecucionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefKnown &&
            fase != TacticalPhase.RingStolenThiefKnown)
            return;

        // Solo el guardia que vio a Frodo directamente persigue;
        // los que reciben la posición por mensaje se convierten en interceptores o bloqueadores
        if (!creencias.TieneDeteccionPropiaReciente())
            return;

        // Si ya se inició la búsqueda local (1.5 s sin visual), transicionar a Search
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

    // Generar deseo de intercepción; el punto de corte varía según la fase táctica
    private void AgregarIntercepcionSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        TacticalPhasePolicy politica = ObtenerPoliticaDeFase(fase);
        int maxInterceptores;

        // Calcular el número máximo de interceptores según la fase
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

        // Calcular el punto de referencia para elegir candidatos interceptores
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

        // Respetar el límite de interceptores simultáneos
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

    // Generar deseo de bloqueo de salida cuando el anillo ha sido robado
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

        // Sticky: si ya estoy bloqueando, mantener BlockExit aunque haya exceso; solo ceder si no soy candidato estable
        if (soyBloqueadorActual && hayExcesoBloqueadores && !soyBloqueadorElegido)
            return;

        if (!soyBloqueadorActual &&
            !soyBloqueadorElegido)
            return;

        // Añadir +1 de prioridad si ya estoy bloqueando para reforzar la continuidad
        float prioridadBloqueo = politica.PrioridadBloqueoSalida;
        if (soyBloqueadorActual)
            prioridadBloqueo += 1f;

        deseos.Add(new Desire(BehaviorType.BlockExit, prioridadBloqueo));
    }

    // Determinar si este guardia es candidato a roles tácticos excluyendo a los bloqueadores fijos
    private bool SoyCandidatoTacticoAnilloRobado(Vector3 referencia, int maxAgentes)
    {
        HashSet<string> bloqueadores = creencias.ObtenerIdsBloqueadoresSalidaEstables(MAX_BLOQUEADORES_SALIDA);

        // Si este guardia ve a Frodo directamente, puede salir del rol de bloqueador
        if (creencias.LadronVisible && creencias.TieneDeteccionPropiaReciente())
            bloqueadores.Remove(creencias.MiId);

        return creencias
            .ObtenerIdsMasCercanosA(referencia, maxAgentes, bloqueadores)
            .Contains(creencias.MiId);
    }

    // Generar deseo de fallback defensivo si no hay ningún otro deseo operativo activo
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

        // Si no se conoce la posición de la salida, buscar en la última posición del ladrón
        deseos.Add(new Desire(
            BehaviorType.Search,
            DesirePriorities.FallbackDefense,
            creencias.UltimaPosicionLadron
        ));
    }
}
