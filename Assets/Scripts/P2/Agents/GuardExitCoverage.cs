using UnityEngine;

public partial class GuardAgent
{
    private float tiempoInicioCoberturaSalidaInsuficiente = -100f;
    private const float RETARDO_RELLENO_BLOQUEO_SALIDA = 2.5f;

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
            !creencias.ObtenerIdsBloqueadoresSalidaEstables(2).Contains(agentId))
            return;

        selectorIntenciones.ForzarReset();
        creencias.NecesitaDeliberar = true;
        deliberacionPendiente = true;
        tiempoInicioCoberturaSalidaInsuficiente = Time.time;
        Debug.Log($"[{agentId}] Cobertura de salida insuficiente; forzando relevo a BlockExit");
    }

    private void GestionarAutoAsignacionSalidaRobada()
    {
        if (!creencias.AnilloRobado ||
            creencias.FaseActual() != TacticalPhase.RingStolenThiefLost ||
            creencias.TieneTareaAsignada)
            return;

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

    // Tras una busqueda libre (Search alrededor de la ultima posicion), el guardia
    // mira que zonas no estan cubiertas por el resto del equipo y se auto-asigna una.
    private void IntentarAutoAsignacionDeZona(string zonaAnterior = "", bool respetarRolBloqueador = true)
    {
        if (respetarRolBloqueador &&
            creencias.AnilloRobado &&
            creencias.ObtenerIdsBloqueadoresSalidaEstables(2).Contains(agentId))
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

        // Asignador vacio indica auto-asignacion: al completarse no se notificara a nadie.
        creencias.AsignarTarea(tarea, "", "");
        Debug.Log($"[{agentId}] Auto-asignacion a zona libre: {zonaLibre}");
    }
}
