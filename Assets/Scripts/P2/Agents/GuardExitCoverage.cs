// =============================================================
// Fichero parcial de GuardAgent: cobertura de la salida y auto-asignación de zonas.
// Cuando el anillo es robado, garantiza que al menos 2 guardias bloqueen
// la salida. Si hay hueco, fuerza una nueva deliberación para que algún
// guardia libre tome el rol de BlockExit. También gestiona la auto-asignación
// de zonas de búsqueda sin pasar por Contract-Net, usando un diccionario
// estático compartido para evitar que dos guardias elijan la misma zona
// =============================================================

using UnityEngine;

public partial class GuardAgent
{
    private float tiempoInicioCoberturaSalidaInsuficiente = -100f;
    private const float RETARDO_RELLENO_BLOQUEO_SALIDA = 2.5f;

    // Vigilar que la salida tenga siempre 2 guardias bloqueándola tras robar el anillo
    private void GestionarCoberturaSalidaRobada()
    {
        if (!creencias.AnilloRobado || !creencias.TienePosicionSalida)
        {
            tiempoInicioCoberturaSalidaInsuficiente = -100f;
            return;
        }

        // Si ya hay 2 bloqueadores, no hacer nada
        if (creencias.GuardiasEnEstado(BehaviorType.BlockExit) >= 2)
        {
            tiempoInicioCoberturaSalidaInsuficiente = -100f;
            return;
        }

        // Esperar un pequeño retardo antes de forzar el relevo para evitar cambios bruscos
        if (tiempoInicioCoberturaSalidaInsuficiente < 0f)
        {
            tiempoInicioCoberturaSalidaInsuficiente = Time.time;
            return;
        }

        if (Time.time - tiempoInicioCoberturaSalidaInsuficiente < RETARDO_RELLENO_BLOQUEO_SALIDA)
            return;

        // No forzar el relevo si este guardia ya está bloqueando o no es candidato estable
        if (behaviorActivo_tipo == BehaviorType.BlockExit ||
            !creencias.ObtenerIdsBloqueadoresSalidaEstables(2).Contains(agentId))
            return;

        // Forzar deliberación para que el guardia adopte BlockExit
        selectorIntenciones.ForzarReset();
        creencias.NecesitaDeliberar = true;
        deliberacionPendiente = true;
        tiempoInicioCoberturaSalidaInsuficiente = Time.time;
        Debug.Log($"[{agentId}] Cobertura de salida insuficiente; forzando relevo a BlockExit");
    }

    // Auto-asignar una zona de búsqueda cuando el anillo está robado y el ladrón se perdió
    private void GestionarAutoAsignacionSalidaRobada()
    {
        if (!creencias.AnilloRobado ||
            creencias.FaseActual() != TacticalPhase.RingStolenThiefLost ||
            creencias.TieneTareaAsignada)
            return;

        // No interrumpir behaviors de alta prioridad
        if (behaviorActivo_tipo == BehaviorType.BlockExit ||
            behaviorActivo_tipo == BehaviorType.SearchAssigned ||
            behaviorActivo_tipo == BehaviorType.Pursuit ||
            behaviorActivo_tipo == BehaviorType.Intercept)
            return;

        bool faltaCoberturaSalida = creencias.GuardiasEnEstado(BehaviorType.BlockExit) < 2;
        if (faltaCoberturaSalida &&
            creencias.ObtenerIdsBloqueadoresSalidaEstables(2).Contains(agentId))
            return;

        IntentarAutoAsignacionDeZona("", respetarRolBloqueador: faltaCoberturaSalida);
    }

    // Buscar una zona libre no cubierta por el equipo y auto-asignársela sin Contract-Net
    private void IntentarAutoAsignacionDeZona(string zonaAnterior = "", bool respetarRolBloqueador = true)
    {
        // No auto-asignarse si este guardia ya está fijo como bloqueador de salida
        if (respetarRolBloqueador &&
            creencias.AnilloRobado &&
            creencias.ObtenerIdsBloqueadoresSalidaEstables(2).Contains(agentId))
            return;

        if (!creencias.AnilloRobado &&
            creencias.FaseActual() == TacticalPhase.RingSafeThiefLost)
            return;

        // Si toca comprobar el pedestal antes de coordinar, forzar deliberación y salir
        if (creencias.ComprobarPedestalTrasBusquedaLocal)
        {
            if (creencias.FaseActual() == TacticalPhase.RingSafeThiefKnown)
                creencias.BuscarLocalAntesDeCoordinar = true;

            creencias.NecesitaDeliberar = true;
            return;
        }

        // Obtener la siguiente zona libre según la fase táctica
        string zonaLibre = creencias.AnilloRobado
            ? creencias.ObtenerSiguienteZonaExitSecuencialSinCubrir(zonaAnterior)
            : creencias.ObtenerZonaSinCubrir(soloExit: false);
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

        // Asignador vacío indica auto-asignación: al completarse no se notificará a nadie
        creencias.AsignarTarea(tarea, "", "");
        Debug.Log($"[{agentId}] Auto-asignacion a zona libre: {zonaLibre}");
    }
}
