// =============================================================
// Fichero parcial de GuardAgent: ciclo de vida de los behaviors.
// Gestiona la activación, ejecución y finalización de los behaviors
// BDI del guardia (patrulla, persecución, búsqueda, intercepción...).
// Cuando un behavior termina, decide si hay que notificar al equipo
// o auto-asignarse una nueva zona, y fuerza una nueva deliberación
// =============================================================

using System.Collections.Generic;
using UnityEngine;

public partial class GuardAgent
{
    // Diccionario con una instancia de cada behavior posible, indexado por tipo
    private Dictionary<BehaviorType, IBehavior> behaviors = new Dictionary<BehaviorType, IBehavior>();
    private IBehavior behaviorActivo = null;
    private BehaviorType behaviorActivo_tipo = BehaviorType.None;
    private int versionTareaProcesada = 0;

    // Detectar si llegó una nueva tarea asignada por Contract-Net e interrumpir si procede
    private void GestionarCambioTareaAsignada()
    {
        if (creencias.VersionTareaAsignada == versionTareaProcesada)
            return;

        versionTareaProcesada = creencias.VersionTareaAsignada;
        if (!creencias.TieneTareaAsignada)
            return;

        // Solo interrumpir si el guardia está en un behavior de baja prioridad
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

    // Activar un behavior: detener el anterior, iniciar el nuevo y notificar al equipo
    private void ActivarBehavior(BehaviorType tipo)
    {
        if (behaviorActivo != null)
            behaviorActivo.Detener(actuador);

        // Si se pasa a BlockExit con tarea asignada, liberar la tarea para no bloquear el Contract-Net
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
            // Fallback a patrulla si el tipo pedido no está registrado
            behaviorActivo = behaviors[BehaviorType.Patrol];
            behaviorActivo_tipo = BehaviorType.Patrol;
            behaviorActivo.Iniciar(creencias, actuador);
            Debug.Log($"[{agentId}] Behavior no encontrado, fallback a Patrol");
        }

        // Informar al equipo del nuevo estado mediante heartbeat inmediato
        BroadcastEstado();
    }

    // Emitir el estado actual del guardia a todos los guardias del equipo
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

    // Ejecutar el behavior activo cada frame y gestionar su finalización
    private void EjecutarBehaviorActivo()
    {
        if (behaviorActivo == null) return;

        bool terminado = behaviorActivo.Ejecutar(creencias, actuador);

        if (terminado)
        {
            Debug.Log($"[{agentId}] Behavior '{behaviorActivo_tipo}' completado");
            BehaviorType behaviorTerminado = behaviorActivo_tipo;

            // Si se pierde al ladrón tras perseguirlo o interceptarlo, reclamar coordinación de búsqueda
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

                // Notificar al iniciador del Contract-Net que la zona fue completada
                if (!string.IsNullOrEmpty(creencias.AsignadorTarea))
                    protocolHandler.NotificarDone(creencias.ConversacionTareaAsignada, creencias.AsignadorTarea);

                protocolHandler.InformarPredicado(PredicateType.ZONE_CLEAR);
                creencias.LimpiarTarea();

                // Si el anillo fue robado y este guardia no está cubriendo la salida, buscar otra zona
                if (creencias.AnilloRobado &&
                    !creencias.ObtenerIdsBloqueadoresSalidaEstables(2).Contains(agentId))
                {
                    IntentarAutoAsignacionDeZona(zonaCompletada);
                }
            }

            // Notificar al solicitante si este behavior fue activado por un REQUEST de otro guardia
            if (creencias.TieneRequestPendiente)
            {
                protocolHandler.NotificarDone(creencias.ConversacionRequest, creencias.SolicitanteRequest);
                creencias.LimpiarRequest();
            }

            if (behaviorTerminado == BehaviorType.CheckPedestal)
            {
                creencias.RegistrarChequeoPedestal();
                creencias.ComprobarPedestalTrasBusquedaLocal = false;
            }

            // Tras búsqueda libre, auto-asignarse la siguiente zona sin esperar un nuevo Contract-Net
            if (behaviorTerminado == BehaviorType.Search)
            {
                creencias.BuscarLocalAntesDeCoordinar = false;
                IntentarAutoAsignacionDeZona();
            }

            // Limpiar el behavior completado y forzar nueva deliberación BDI
            behaviorActivo.Detener(actuador);
            behaviorActivo = null;
            behaviorActivo_tipo = BehaviorType.None;
            selectorIntenciones.ForzarReset();
            deliberacionPendiente = true;
        }
    }
}
