using UnityEngine;

// Gestiona la construccion y el manejo de mensajes FIPA-ACL para un guardia.
// Actua como intermediario entre el cerebro del agente (GuardAgent) y el buzon (ComunicacionAgente).
public class ProtocolHandler
{
    public static bool LogsDetallados = false;

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

    // CONSTRUCTORES DE MENSAJES

    public void InformarAvistamiento(ThiefSighting avistamiento)
    {
        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, agentId, "");
        msg.Content = ContentLanguage.Encode(avistamiento);
        msg.Protocol = "fipa-inform";
        comunicacion.Broadcast(msg);
    }

    public void InformarPredicado(PredicateType predicado, string extraData = "")
    {
        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, agentId, "");
        msg.Content = ContentLanguage.EncodePredicate(predicado, extraData);
        msg.Protocol = "fipa-inform";
        comunicacion.Broadcast(msg);
    }

    public void ResponderAgree(ACLMessage original)
        => comunicacion.Enviar(original.CreateReply(ACLPerformative.AGREE));

    public void ResponderRefuse(ACLMessage original, string motivo = "busy")
    {
        ACLMessage reply = original.CreateReply(ACLPerformative.REFUSE);
        reply.Content = motivo;
        comunicacion.Enviar(reply);
    }

    public void ResponderNotUnderstood(ACLMessage original, string motivo = "content-not-understood")
    {
        ACLMessage reply = original.CreateReply(ACLPerformative.NOT_UNDERSTOOD);
        reply.Content = motivo;
        comunicacion.Enviar(reply);
    }

    public void NotificarDone(string convId, string solicitante)
    {
        ACLMessage done = new ACLMessage(ACLPerformative.INFORM_DONE, agentId, solicitante);
        done.ConversationId = convId;
        done.Protocol = "fipa-request";
        comunicacion.Enviar(done);
    }

    public void ResponderQuery(ACLMessage query, string resultado)
    {
        ACLMessage reply = query.CreateReply(ACLPerformative.INFORM_RESULT);
        reply.Content = resultado;
        comunicacion.Enviar(reply);
    }

    public void ResponderCFPConPropuesta(ACLMessage cfp, ProposalData propuesta)
    {
        ACLMessage propose = cfp.CreateReply(ACLPerformative.PROPOSE);
        propose.Content = ContentLanguage.Encode(propuesta);
        comunicacion.Enviar(propose);
    }

    public void RechazarCFP(ACLMessage cfp, string motivo = "unavailable")
    {
        ACLMessage refuse = cfp.CreateReply(ACLPerformative.REFUSE);
        refuse.Content = motivo;
        comunicacion.Enviar(refuse);
    }

    // MANEJADORES

    public void ManejarInform(ACLMessage msg)
    {
        if (ContentLanguage.IsThiefSighting(msg.Content))
        {
            ThiefSighting avistamiento = ContentLanguage.DecodeThiefSighting(msg.Content);
            if (avistamiento?.Location != null)
            {
                creencias.ActualizarPosicionLadron(
                    avistamiento.Location.ToVector3(),
                    avistamiento.Timestamp,
                    avistamiento.DirectVision,
                    avistamiento.ReportedBy,
                    avistamiento.Direction != null ? avistamiento.Direction.ToVector3() : Vector3.zero,
                    avistamiento.Direction != null
                );
                creencias.BuscarLocalAntesDeCoordinar = false;
                creencias.ComprobarPedestalTrasBusquedaLocal = false;
                creencias.DebeComprobarPedestalPrioritario = false;
                creencias.NecesitaDeliberar = true;
                if (LogsDetallados)
                    Debug.Log($"[{agentId}] Avistamiento recibido de {msg.Sender} en {avistamiento.Location}");
            }
            return;
        }

        if (ContentLanguage.IsGuardStatus(msg.Content))
        {
            GuardStatus estado = ContentLanguage.DecodeGuardStatus(msg.Content);
            if (estado != null && estado.GuardId != agentId)
                creencias.ActualizarEstadoGuardia(estado);
            return;
        }

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
        if (!ContentLanguage.IsActionRequest(msg.Content))
        {
            ResponderNotUnderstood(msg, "content-not-understood");
            return;
        }

        if (creencias.EstadoActual == BehaviorType.Pursuit)
        {
            ResponderRefuse(msg, "in-pursuit");
            return;
        }

        ActionRequest solicitud = ContentLanguage.DecodeActionRequest(msg.Content);
        if (solicitud == null ||
            string.IsNullOrEmpty(solicitud.Action) ||
            !System.Enum.IsDefined(typeof(ActionType), solicitud.Action))
        {
            ResponderNotUnderstood(msg, "invalid-action");
            return;
        }

        bool puedoAceptar = selectorIntenciones.EstaDisponible() || solicitud.Urgency > 0.8f;

        if (puedoAceptar)
        {
            ResponderAgree(msg);
            creencias.AceptarRequest(solicitud, msg.ConversationId, msg.Sender);
            Debug.Log($"[{agentId}] REQUEST aceptado de {msg.Sender}: {solicitud.Action}");
        }
        else
        {
            ResponderRefuse(msg, "busy");
            Debug.Log($"[{agentId}] REQUEST rechazado de {msg.Sender}");
        }
    }

    public void ManejarQuery(ACLMessage msg)
    {
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
        if (ContentLanguage.IsPredicate(msg.Content))
        {
            PredicateInfo info = ContentLanguage.DecodePredicate(msg.Content);
            if (info == null || string.IsNullOrEmpty(info.Predicate))
            {
                ResponderNotUnderstood(msg, "invalid-predicate");
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
        if (ContentLanguage.IsGuardStatus(msg.Content))
        {
            GuardStatus estado = ContentLanguage.DecodeGuardStatus(msg.Content);
            if (estado != null && estado.GuardId != agentId)
                creencias.ActualizarEstadoGuardia(estado);
        }
    }

    public void ManejarCFP(ACLMessage msg, ActuadorMovimiento actuador)
    {
        if (creencias.EstadoActual == BehaviorType.Pursuit && creencias.LadronVisible)
        {
            RechazarCFP(msg, "in-pursuit");
            return;
        }

        SearchTask tarea = ContentLanguage.DecodeSearchTask(msg.Content);
        if (tarea == null)
        {
            RechazarCFP(msg, "invalid-task");
            return;
        }

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
        if (ContentLanguage.IsSearchTask(msg.Content))
        {
            SearchTask tarea = ContentLanguage.DecodeSearchTask(msg.Content);
            creencias.AsignarTarea(tarea, msg.ConversationId, msg.Sender);
            Debug.Log($"[{agentId}] Tarea adjudicada de {msg.Sender}: zona={tarea.ZoneId}");
        }
    }

    public void ManejarPropuestaRechazada(ACLMessage msg)
    {
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, true);
        if (LogsDetallados)
            Debug.Log($"[{agentId}] Propuesta rechazada por {msg.Sender}");
    }

    public void ManejarDone(ACLMessage msg)
    {
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, true, BehaviorType.Patrol.ToString());
        Debug.Log($"[{agentId}] Tarea completada confirmada por {msg.Sender}");
    }

    public void ManejarAgree(ACLMessage msg)
    {
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, false);
        Debug.Log($"[{agentId}] {msg.Sender} acepto nuestra solicitud");
    }

    public void ManejarRefuse(ACLMessage msg)
    {
        creencias.ActualizarDisponibilidadGuardia(msg.Sender, false);
        Debug.Log($"[{agentId}] {msg.Sender} rechazo nuestra solicitud: {msg.Content}");
    }

    public void ManejarCancel(ACLMessage msg)
    {
        bool cancelado = false;

        if (!string.IsNullOrEmpty(msg.ConversationId) &&
            msg.ConversationId == creencias.ConversacionRequest)
        {
            creencias.LimpiarRequest();
            cancelado = true;
        }

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
            ResponderRefuse(msg, "unknown-conversation");
        }
    }

    public void ManejarNotUnderstood(ACLMessage msg)
    {
        if (LogsDetallados)
            Debug.Log($"[{agentId}] NOT_UNDERSTOOD de {msg.Sender}: {msg.Content}");
    }

    private bool EvaluarPredicado(string predicado)
    {
        if (predicado == PredicateType.RING_STOLEN.ToString())
            return creencias.AnilloRobado;
        if (predicado == PredicateType.RING_ON_PEDESTAL.ToString())
            return !creencias.AnilloRobado;
        if (predicado == PredicateType.THIEF_SPOTTED.ToString())
            return creencias.TieneInfoReciente();

        return false;
    }
}
