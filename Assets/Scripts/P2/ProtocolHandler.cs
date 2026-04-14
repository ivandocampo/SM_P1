using UnityEngine;

public class ProtocolHandler
{
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

    public void ManejarInform(ACLMessage msg)
    {
        if (ContentLanguage.IsThiefSighting(msg.Content))
        {
            ThiefSighting avistamiento = ContentLanguage.DecodeThiefSighting(msg.Content);
            if (avistamiento != null && avistamiento.Location != null)
            {
                creencias.ActualizarPosicionLadron(
                    avistamiento.Location.ToVector3(),
                    avistamiento.Timestamp,
                    avistamiento.DirectVision,
                    avistamiento.ReportedBy
                );
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
                Debug.Log($"[{agentId}] Anillo robado — informado por {msg.Sender}");
            }
            else if (info.Predicate == PredicateType.THIEF_LOST.ToString())
            {
                Debug.Log($"[{agentId}] {msg.Sender} perdió de vista al ladrón");
            }
        }
    }

    public void ManejarRequest(ACLMessage msg)
    {
        if (!ContentLanguage.IsActionRequest(msg.Content))
        {
            comunicacion.ResponderRefuse(msg, "content-not-understood");
            return;
        }

        ActionRequest solicitud = ContentLanguage.DecodeActionRequest(msg.Content);
        bool puedoAceptar = selectorIntenciones.EstaDisponible() || solicitud.Urgency > 0.8f;

        if (puedoAceptar)
        {
            comunicacion.ResponderAgree(msg);
            creencias.AceptarRequest(solicitud, msg.ConversationId, msg.Sender);
            Debug.Log($"[{agentId}] REQUEST aceptado de {msg.Sender}: {solicitud.Action}");
        }
        else
        {
            comunicacion.ResponderRefuse(msg, "busy");
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
        comunicacion.ResponderQuery(msg, ContentLanguage.Encode(estado));
    }

    public void ManejarCFP(ACLMessage msg, ActuadorMovimiento actuador)
    {
        if (creencias.EstadoActual == BehaviorType.Pursuit)
        {
            comunicacion.RechazarCFP(msg, "in-pursuit");
            return;
        }

        SearchTask tarea = ContentLanguage.DecodeSearchTask(msg.Content);
        if (tarea == null)
        {
            comunicacion.RechazarCFP(msg, "invalid-task");
            return;
        }

        float distancia = Vector3.Distance(creencias.MiPosicion, tarea.TargetArea.ToVector3());

        ProposalData propuesta = new ProposalData
        {
            GuardId = agentId,
            Cost = distancia,
            EstimatedTime = distancia / actuador.velocidadAlerta
        };

        comunicacion.ResponderCFPConPropuesta(msg, propuesta);
        Debug.Log($"[{agentId}] Propuesta enviada: coste={distancia:F1}");
    }

    public void ManejarPropuestaAceptada(ACLMessage msg)
    {
        if (ContentLanguage.IsSearchTask(msg.Content))
        {
            SearchTask tarea = ContentLanguage.DecodeSearchTask(msg.Content);
            creencias.AsignarTarea(tarea, msg.ConversationId, msg.Sender);
            Debug.Log($"[{agentId}] Tarea adjudicada de {msg.Sender}");
        }
    }

    public void ManejarPropuestaRechazada(ACLMessage msg)
    {
        Debug.Log($"[{agentId}] Propuesta rechazada por {msg.Sender}");
    }

    public void ManejarDone(ACLMessage msg)
    {
        Debug.Log($"[{agentId}] Tarea completada confirmada por {msg.Sender}");
    }

    public void ManejarAgree(ACLMessage msg)
    {
        Debug.Log($"[{agentId}] {msg.Sender} aceptó nuestra solicitud");
    }

    public void ManejarRefuse(ACLMessage msg)
    {
        Debug.Log($"[{agentId}] {msg.Sender} rechazó nuestra solicitud: {msg.Content}");
    }
}