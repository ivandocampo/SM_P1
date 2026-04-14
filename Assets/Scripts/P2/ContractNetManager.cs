using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Gestiona el protocolo FIPA-Contract-Net desde el lado del manager (iniciador).
// Cuando el ladrón se pierde de vista, distribuye zonas de búsqueda entre los guardias
// disponibles eligiendo al candidato con menor coste (distancia).
public class ContractNetManager
{
    private BeliefBase creencias;
    private ComunicacionAgente comunicacion;
    private string agentId;
    private float cooldown;
    private float ultimoContractNet = -100f;

    private ContractNetEstado contratoActivo = null;

    public ContractNetManager(BeliefBase creencias, ComunicacionAgente comunicacion,
                               string id, float cooldown)
    {
        this.creencias    = creencias;
        this.comunicacion = comunicacion;
        this.agentId      = id;
        this.cooldown     = cooldown;

        // Suscribirse a propuestas y rechazos para alimentar el contrato activo
        comunicacion.OnPropuestaRecibida += AlimentarContrato;
        comunicacion.OnRefuseRecibido    += AlimentarContratoRefuse;
    }

    // Desuscribirse al destruir el agente
    public void Limpiar()
    {
        comunicacion.OnPropuestaRecibida -= AlimentarContrato;
        comunicacion.OnRefuseRecibido    -= AlimentarContratoRefuse;
    }

    // Inicia un Contract Net para distribuir la búsqueda entre los guardias disponibles
    public void IniciarDistribucionBusqueda()
    {
        if (Time.time - ultimoContractNet < cooldown) return;
        if (contratoActivo != null) return;

        ultimoContractNet = Time.time;

        List<string> otrosGuardias = AgentRegistry.Instance
            .ObtenerOtrosIdsPorTipo("guard", agentId);

        if (otrosGuardias.Count == 0) return;

        SearchTask tarea = new SearchTask
        {
            TaskId     = $"search-{agentId}-{Time.time:F0}",
            TargetArea = new Position(creencias.UltimaPosicionLadron),
            Radius     = 15f,
            Urgency    = 0.8f
        };

        string convId = comunicacion.NuevaConversacion();
        contratoActivo = new ContractNetEstado(convId, otrosGuardias, timeout: 2f);
        contratoActivo.SetContenidoTarea(ContentLanguage.Encode(tarea));

        foreach (string receptor in otrosGuardias)
        {
            ACLMessage cfp = new ACLMessage(ACLPerformative.CFP, agentId, receptor);
            cfp.Content        = ContentLanguage.Encode(tarea);
            cfp.Protocol       = "fipa-contract-net";
            cfp.ConversationId = convId;
            comunicacion.Enviar(cfp);
        }

        Debug.Log($"[{agentId}] Contract Net iniciado ({convId})");
    }

    // Evalúa y adjudica si el contrato activo está listo (llamar desde Update)
    public void Gestionar()
    {
        if (contratoActivo == null || !contratoActivo.ListoParaEvaluar()) return;

        List<ACLMessage> propuestas = contratoActivo.Propuestas;

        if (propuestas.Count == 0)
        {
            Debug.Log($"[{agentId}] Contract Net: sin propuestas recibidas");
            contratoActivo = null;
            return;
        }

        // Adjudicar al guardia con menor coste estimado
        ACLMessage mejor = propuestas
            .OrderBy(p => ContentLanguage.DecodeProposal(p.Content)?.Cost ?? float.MaxValue)
            .First();

        ACLMessage accept = mejor.CreateReply(ACLPerformative.ACCEPT_PROPOSAL);
        accept.Content = contratoActivo.ContenidoTarea;
        comunicacion.Enviar(accept);

        foreach (ACLMessage propuesta in propuestas)
        {
            if (propuesta.Sender != mejor.Sender)
                comunicacion.Enviar(propuesta.CreateReply(ACLPerformative.REJECT_PROPOSAL));
        }

        Debug.Log($"[{agentId}] Contract Net resuelto: {mejor.Sender} buscará la zona");
        contratoActivo = null;
    }

    private void AlimentarContrato(ACLMessage msg)
    {
        if (contratoActivo != null && msg.ConversationId == contratoActivo.ConversationId)
            contratoActivo.RecibirRespuesta(msg);
    }

    private void AlimentarContratoRefuse(ACLMessage msg)
    {
        if (contratoActivo != null && msg.ConversationId == contratoActivo.ConversationId)
            contratoActivo.RecibirRespuesta(msg);
    }
}

// Seguimiento del estado de una negociación Contract Net activa
public class ContractNetEstado
{
    public string ConversationId  { get; private set; }
    public List<ACLMessage> Propuestas { get; private set; } = new List<ACLMessage>();
    public string ContenidoTarea  { get; private set; }

    private List<string> pendientes;
    private float deadline;

    public ContractNetEstado(string convId, List<string> participantes, float timeout)
    {
        ConversationId = convId;
        pendientes     = new List<string>(participantes);
        deadline       = Time.time + timeout;
    }

    public void SetContenidoTarea(string contenido) => ContenidoTarea = contenido;

    public void RecibirRespuesta(ACLMessage msg)
    {
        if (msg.ConversationId != ConversationId) return;
        if (msg.Performative == ACLPerformative.PROPOSE)
            Propuestas.Add(msg);
        pendientes.Remove(msg.Sender);
    }

    // Listo cuando todos respondieron o se agotó el tiempo
    public bool ListoParaEvaluar() => pendientes.Count == 0 || Time.time > deadline;
}
