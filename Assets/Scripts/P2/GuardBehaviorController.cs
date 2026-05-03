using System.Collections.Generic;
using UnityEngine;

public partial class GuardAgent
{
    private Dictionary<BehaviorType, IBehavior> behaviors = new Dictionary<BehaviorType, IBehavior>();
    private IBehavior behaviorActivo = null;
    private BehaviorType behaviorActivo_tipo = BehaviorType.None;
    private int versionTareaProcesada = 0;

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
        statusMsg.Protocol = GameConstants.Protocols.Inform;
        comunicacion.BroadcastATipo(statusMsg, GameConstants.AgentTypes.Guard);
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
                string zonaCompletada = creencias.TareaAsignada.ZoneId;
                creencias.RegistrarBusquedaCompletada(zonaCompletada);

                // Si la tarea vino de un Contract-Net, notificar al iniciador.
                // Las tareas auto-asignadas no tienen asignador y no requieren INFORM_DONE.
                if (!string.IsNullOrEmpty(creencias.AsignadorTarea))
                    protocolHandler.NotificarDone(creencias.ConversacionTareaAsignada, creencias.AsignadorTarea);

                protocolHandler.InformarPredicado(PredicateType.ZONE_CLEAR);
                creencias.LimpiarTarea();

                if (creencias.AnilloRobado &&
                    !creencias.ObtenerIdsBloqueadoresSalidaEstables(2).Contains(agentId))
                {
                    IntentarAutoAsignacionDeZona(zonaCompletada);
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

            // Tras una busqueda libre (Search), si quedan zonas sin cubrir por el resto
            // del equipo, auto-asignarse la mejor candidata para mantenerse util.
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
}
