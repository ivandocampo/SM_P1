// =============================================================
// Manejador de protocolos FIPA-ACL para los guardias.
// Construye mensajes de respuesta o difusion y procesa los mensajes
// recibidos segun su contenido: avistamientos, predicados, estados,
// REQUEST, QUERY y fases del Contract-Net. Actua como puente entre
// ComunicacionAgente y la base de creencias BDI del guardia
// =============================================================

using UnityEngine;

public class ProtocolHandler
{
    // Permite activar logs extra sin llenar la consola durante ejecucion normal.
    public static bool LogsDetallados = false;

    // Referencias principales: creencias BDI, buzon de comunicacion y selector de intenciones.
    private BeliefBase creencias;
    private ComunicacionAgente comunicacion;
    private IntentionSelector selectorIntenciones;
    private string agentId;

    public ProtocolHandler(BeliefBase creencias, ComunicacionAgente comunicacion,
                           IntentionSelector selector, string id)
    {
        this.creencias = creencias;
        this.comunicacion = comunicacion;
        this.selectorIntenciones = selector;
        this.agentId = id;
    }

    // Difunde al equipo un avistamiento de Frodo.
    // El contenido viaja como ThiefSighting serializado por ContentLanguage.
    public void InformarAvistamiento(ThiefSighting avistamiento)
    {
        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, agentId, "");
        msg.Content = ContentLanguage.Encode(avistamiento);
        msg.Protocol = GameConstants.Protocols.Inform;
        comunicacion.Broadcast(msg);
    }

    // Difunde un hecho simple del mundo, por ejemplo anillo robado o ladron perdido.
    public void InformarPredicado(PredicateType predicado, string extraData = "")
    {
        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, agentId, "");
        msg.Content = ContentLanguage.EncodePredicate(predicado, extraData);
        msg.Protocol = GameConstants.Protocols.Inform;
        comunicacion.Broadcast(msg);
    }

    // Respuesta positiva a una solicitud recibida.
    public void ResponderAgree(ACLMessage original)
        => comunicacion.Enviar(original.CreateReply(ACLPerformative.AGREE));

    // Respuesta negativa a una solicitud o CFP, indicando el motivo en Content.
    public void ResponderRefuse(ACLMessage original, string motivo = GameConstants.RefusalReasons.Busy)
    {
        ACLMessage reply = original.CreateReply(ACLPerformative.REFUSE);
        reply.Content = motivo;
        comunicacion.Enviar(reply);
    }

    // Respuesta cuando el mensaje recibido no tiene un contenido reconocible.
    public void ResponderNotUnderstood(ACLMessage original, string motivo = GameConstants.RefusalReasons.ContentNotUnderstood)
    {
        ACLMessage reply = original.CreateReply(ACLPerformative.NOT_UNDERSTOOD);
        reply.Content = motivo;
        comunicacion.Enviar(reply);
    }

    // Notifica que una tarea o solicitud asociada a una conversacion ha terminado.
    public void NotificarDone(string convId, string solicitante)
    {
        ACLMessage done = new ACLMessage(ACLPerformative.INFORM_DONE, agentId, solicitante);
        done.ConversationId = convId;
        done.Protocol = GameConstants.Protocols.Request;
        comunicacion.Enviar(done);
    }

    // Responde a consultas QUERY con un resultado ya codificado como string.
    public void ResponderQuery(ACLMessage query, string resultado)
    {
        ACLMessage reply = query.CreateReply(ACLPerformative.INFORM_RESULT);
        reply.Content = resultado;
        comunicacion.Enviar(reply);
    }

    // Envia una propuesta de coste como respuesta a un CFP de Contract-Net.
    public void ResponderCFPConPropuesta(ACLMessage cfp, ProposalData propuesta)
    {
        ACLMessage propose = cfp.CreateReply(ACLPerformative.PROPOSE);
        propose.Content = ContentLanguage.Encode(propuesta);
        comunicacion.Enviar(propose);
    }

    // Rechaza un CFP cuando el guardia no puede participar en la tarea.
    public void RechazarCFP(ACLMessage cfp, string motivo = GameConstants.RefusalReasons.Unavailable)
    {
        ACLMessage refuse = cfp.CreateReply(ACLPerformative.REFUSE);
        refuse.Content = motivo;
        comunicacion.Enviar(refuse);
    }


    public void ManejarInform(ACLMessage msg)
    {
        // INFORM con ThiefSighting: actualiza la ultima informacion conocida sobre Frodo.
        if (ContentLanguage.IsThiefSighting(msg.Content))
        {
            ThiefSighting avistamiento = ContentLanguage.DecodeThiefSighting(msg.Content);
            if (avistamiento?.Location != null)
            {
                // Solo se limpian tareas y se fuerza deliberacion si el informe es mas reciente.
                bool informacionNueva = avistamiento.Timestamp > creencias.TiempoUltimaDeteccion;
                creencias.ActualizarPosicionLadron(
                    avistamiento.Location.ToVector3(),
                    avistamiento.Timestamp,
                    avistamiento.DirectVision,
                    avistamiento.ReportedBy,
                    avistamiento.Direction != null ? avistamiento.Direction.ToVector3() : Vector3.zero,
                    avistamiento.Direction != null
                );
                if (informacionNueva)
                {
                    creencias.LimpiarTarea();
                    creencias.LimpiarRequest();
                    creencias.BuscarLocalAntesDeCoordinar = false;
                    creencias.ComprobarPedestalTrasBusquedaLocal = false;
                    creencias.DebeComprobarPedestalPrioritario = false;
                    if (avistamiento.ReportedBy != agentId)
                        creencias.PendienteBusquedaCoordinadaPorInformeExterno = true;
                    // El BDI debe reconsiderar su intencion con la nueva informacion.
                    creencias.NecesitaDeliberar = true;
                }
                if (LogsDetallados)
                    Debug.Log($"[{agentId}] Avistamiento recibido de {msg.Sender} en {avistamiento.Location}");
            }
            return;
        }

        // INFORM con GuardStatus: actualiza la tabla de estados del equipo.
        if (ContentLanguage.IsGuardStatus(msg.Content))
        {
            GuardStatus estado = ContentLanguage.DecodeGuardStatus(msg.Content);
            if (estado != null && estado.GuardId != agentId)
                creencias.ActualizarEstadoGuardia(estado);
            return;
        }

        // INFORM con PredicateInfo: representa hechos simples del mundo.
        if (ContentLanguage.IsPredicate(msg.Content))
        {
            PredicateInfo info = ContentLanguage.DecodePredicate(msg.Content);
            if (info.Predicate == PredicateType.RING_STOLEN.ToString())
            {
                creencias.MarcarAnilloRobado();
                Debug.Log($"[{agentId}] Anillo robado - informado por {msg.Sender}");
            }
            else if (info.Predicate == PredicateType.THIEF_LOST.ToString())
            {
                // El emisor ha perdido el rastro: ya no está persiguiendo.
                // Actualizar su estado para que otros guardias no crean que alguien sigue en persecución
                // (evita falsos positivos en AlguienPersiguiendo() mientras llega el próximo heartbeat).
                creencias.ActualizarDisponibilidadGuardia(msg.Sender, true, BehaviorType.Search.ToString());
                if (LogsDetallados)
                    Debug.Log($"[{agentId}] {msg.Sender} perdió al ladrón");
            }
        }
    }

    public void ManejarRequest(ACLMessage msg)
    {
        // Primero valida que el contenido sea una ActionRequest reconocible.
        if (!ContentLanguage.IsActionRequest(msg.Content))
        {
            ResponderNotUnderstood(msg, GameConstants.RefusalReasons.ContentNotUnderstood);
            return;
        }

        // Si el guardia esta persiguiendo, no acepta ordenes secundarias.
        if (creencias.EstadoActual == BehaviorType.Pursuit)
        {
            ResponderRefuse(msg, GameConstants.RefusalReasons.InPursuit);
            return;
        }

        // Se comprueba que la accion exista en la ontologia de acciones.
        ActionRequest solicitud = ContentLanguage.DecodeActionRequest(msg.Content);
        if (solicitud == null ||
            string.IsNullOrEmpty(solicitud.Action) ||
            !System.Enum.IsDefined(typeof(ActionType), solicitud.Action))
        {
            ResponderNotUnderstood(msg, GameConstants.RefusalReasons.InvalidAction);
            return;
        }

        // Una solicitud urgente puede aceptarse aunque el agente no este totalmente libre.
        bool puedoAceptar = selectorIntenciones.EstaDisponible() || solicitud.Urgency > 0.8f;

        if (puedoAceptar)
        {
            ResponderAgree(msg);
            creencias.AceptarRequest(solicitud, msg.ConversationId, msg.Sender);
            Debug.Log($"[{agentId}] REQUEST aceptado de {msg.Sender}: {solicitud.Action}");
        }
        else
        {
            ResponderRefuse(msg, GameConstants.RefusalReasons.Busy);
            Debug.Log($"[{agentId}] REQUEST rechazado de {msg.Sender}");
        }
    }

    public void ManejarQuery(ACLMessage msg)
    {
        // QUERY_REF generica: responde con el estado actual del guardia.
        GuardStatus estado = new GuardStatus
        {
            GuardId = agentId,
            CurrentPosition = new Position(creencias.MiPosicion),
            CurrentState = creencias.EstadoActual.ToString(),
            IsAvailable = selectorIntenciones.EstaDisponible()
        };
        ResponderQuery(msg, ContentLanguage.Encode(estado));
    }

    public void ManejarQueryIf(ACLMessage msg)
    {
        // QUERY_IF puede preguntar por predicados booleanos del mundo.
        if (ContentLanguage.IsPredicate(msg.Content))
        {
            PredicateInfo info = ContentLanguage.DecodePredicate(msg.Content);
            if (info == null || string.IsNullOrEmpty(info.Predicate))
            {
                ResponderNotUnderstood(msg, GameConstants.RefusalReasons.InvalidPredicate);
                return;
            }

            bool resultado = EvaluarPredicado(info.Predicate);
            ResponderQuery(msg, resultado ? "true" : "false");
            return;
        }

        ManejarQuery(msg);
    }

    public void ManejarQueryRef(ACLMessage msg)
    {
        ManejarQuery(msg);
    }

    public void ManejarInformResult(ACLMessage msg)
    {
        // Resultado de una query: si trae estado de guardia, se integra en creencias.
        if (ContentLanguage.IsGuardStatus(msg.Content))
        {
            GuardStatus estado = ContentLanguage.DecodeGuardStatus(msg.Content);
            if (estado != null && estado.GuardId != agentId)
                creencias.ActualizarEstadoGuardia(estado);
        }
    }

    public void ManejarCFP(ACLMessage msg, ActuadorMovimiento actuador)
    {
        // Un guardia ya asignado a una zona o bloqueando salida no participa en otra subasta.
        if (creencias.TieneTareaAsignada ||
            creencias.EstadoActual == BehaviorType.SearchAssigned ||
            creencias.EstadoActual == BehaviorType.BlockExit)
        {
            RechazarCFP(msg, GameConstants.RefusalReasons.Busy);
            return;
        }

        // Si tiene que revisar el pedestal, esa tarea tiene prioridad sobre aceptar un CFP.
        if (creencias.DebeComprobarPedestalPrioritario ||
            creencias.EstadoActual == BehaviorType.CheckPedestal)
        {
            RechazarCFP(msg, GameConstants.RefusalReasons.CheckingPedestal);
            return;
        }

        // Si esta persiguiendo visualmente a Frodo, no abandona la persecucion.
        if (creencias.EstadoActual == BehaviorType.Pursuit && creencias.LadronVisible)
        {
            RechazarCFP(msg, GameConstants.RefusalReasons.InPursuit);
            return;
        }

        // El CFP debe contener una SearchTask valida.
        SearchTask tarea = ContentLanguage.DecodeSearchTask(msg.Content);
        if (tarea == null)
        {
            RechazarCFP(msg, GameConstants.RefusalReasons.InvalidTask);
            return;
        }

        // El coste de la propuesta se calcula como distancia hasta la zona objetivo.
        Vector3 objetivoTarea = tarea.TargetArea.ToVector3();
        Vector3[] puntosZona = creencias.ObtenerPuntosZona(tarea.ZoneId);
        if (puntosZona != null && puntosZona.Length > 0)
            objetivoTarea = creencias.ObtenerCentroZona(tarea.ZoneId);

        float distancia = Vector3.Distance(creencias.MiPosicion, objetivoTarea);
        ProposalData propuesta = new ProposalData
        {
            GuardId = agentId,
            Cost = distancia,
            EstimatedTime = distancia / actuador.velocidadAlerta
        };
        ResponderCFPConPropuesta(msg, propuesta);
        if (LogsDetallados)
            Debug.Log($"[{agentId}] Propuesta enviada: coste={distancia:F1}");
    }

    public void ManejarPropuestaAceptada(ACLMessage msg)
    {
        // ACCEPT_PROPOSAL asigna formalmente una zona de busqueda al guardia.
        if (ContentLanguage.IsSearchTask(msg.Content))
        {
            if (creencias.TieneTareaAsignada &&
                msg.ConversationId != creencias.ConversacionTareaAsignada)
            {
                Debug.Log($"[{agentId}] ACCEPT_PROPOSAL ignorado: ya tengo zona {creencias.TareaAsignada.ZoneId}");
                return;
            }

            SearchTask tarea = ContentLanguage.DecodeSearchTask(msg.Content);
            if (tarea == null)
                return;

            // Evita aceptar una zona que otro guardia ya haya reservado.
            if (creencias.ZonaReservadaPorOtro(tarea.ZoneId))
            {
                Debug.Log($"[{agentId}] ACCEPT_PROPOSAL ignorado: zona {tarea.ZoneId} ya reservada por otro guardia");
                return;
            }

            creencias.AsignarTarea(tarea, msg.ConversationId, msg.Sender);
            Debug.Log($"[{agentId}] Tarea adjudicada de {msg.Sender}: zona={tarea.ZoneId}");
            comunicacion.LoguearConversacion(msg.ConversationId);
        }
    }

    public void ManejarPropuestaRechazada(ACLMessage msg)
    {
        // Si rechazan nuestra propuesta, el otro agente queda marcado como disponible.
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, true);
        if (LogsDetallados)
            Debug.Log($"[{agentId}] Propuesta rechazada por {msg.Sender}");
    }

    public void ManejarDone(ACLMessage msg)
    {
        // INFORM_DONE indica que otro agente completo la tarea que tenia asignada.
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, true, BehaviorType.Patrol.ToString());
        Debug.Log($"[{agentId}] Tarea completada confirmada por {msg.Sender}");
        comunicacion.LoguearConversacion(msg.ConversationId);
    }

    public void ManejarAgree(ACLMessage msg)
    {
        // AGREE marca al emisor como ocupado porque acepto una solicitud.
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, false);
        Debug.Log($"[{agentId}] {msg.Sender} acepto nuestra solicitud");
    }

    public void ManejarRefuse(ACLMessage msg)
    {
        // REFUSE guarda que el emisor no va a ejecutar la solicitud.
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, false);
        Debug.Log($"[{agentId}] {msg.Sender} rechazo nuestra solicitud: {msg.Content}");
    }

    public void ManejarCancel(ACLMessage msg)
    {
        bool cancelado = false;

        // Cancela una REQUEST aceptada si coincide la conversacion.
        if (!string.IsNullOrEmpty(msg.ConversationId) &&
            msg.ConversationId == creencias.ConversacionRequest)
        {
            creencias.LimpiarRequest();
            cancelado = true;
        }

        // Cancela una tarea asignada por Contract-Net si coincide la conversacion.
        if (!string.IsNullOrEmpty(msg.ConversationId) &&
            msg.ConversationId == creencias.ConversacionTareaAsignada)
        {
            creencias.LimpiarTarea();
            cancelado = true;
        }

        if (cancelado)
        {
            creencias.NecesitaDeliberar = true;
            ResponderAgree(msg);
        }
        else
        {
            ResponderRefuse(msg, GameConstants.RefusalReasons.UnknownConversation);
        }
    }

    public void ManejarNotUnderstood(ACLMessage msg)
    {
        if (LogsDetallados)
            Debug.Log($"[{agentId}] NOT_UNDERSTOOD de {msg.Sender}: {msg.Content}");
    }

    private bool EvaluarPredicado(string predicado)
    {
        // Traduce predicados de la ontologia a valores booleanos de la base de creencias.
        if (predicado == PredicateType.RING_STOLEN.ToString())
            return creencias.AnilloRobado;
        if (predicado == PredicateType.RING_ON_PEDESTAL.ToString())
            return !creencias.AnilloRobado;
        if (predicado == PredicateType.THIEF_SPOTTED.ToString())
            return creencias.TieneInfoReciente();

        return false;
    }
}
