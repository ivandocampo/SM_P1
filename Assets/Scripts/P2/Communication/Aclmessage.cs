// =============================================================
// Mensaje ACL usado por la capa de comunicacion.
// Representa una unidad generica de intercambio entre agentes:
// performativa FIPA, emisor, receptor, contenido, protocolo y
// campos de conversacion. Tambien ofrece utilidades para crear
// respuestas y clonar mensajes en envios a multiples receptores
// =============================================================

using UnityEngine;


[System.Serializable]
public class ACLMessage
{
    // Performativa FIPA que indica la intencion comunicativa del mensaje
    public ACLPerformative Performative;

    // Identidad del emisor y receptor dentro del AgentRegistry
    public string Sender;
    public string Receiver;

    public string Content;          // Contenido serializado
    public string Language;         // Lenguaje del contenido
    public string Ontology;         // Ontología utilizada

    public string Protocol;         
    public string ConversationId;   // Identificador único de la conversación
    public string ReplyWith;        // ID para que el receptor referencie en su respuesta
    public string InReplyTo;        // Referencia al ReplyWith del mensaje que se contesta

    public float Timestamp;         // Time.time en el momento de la creación

    
    public ACLMessage(ACLPerformative performative, string sender, string receiver)
    {
        // Inicializa un mensaje generico con los campos FIPA comunes
        Performative = performative;
        Sender = sender;
        Receiver = receiver;
        Content = "";
        Language = "fipa-sl";
        Ontology = "guard-ontology";
        Protocol = "";
        ConversationId = "";
        ReplyWith = "";
        InReplyTo = "";
        Timestamp = Time.time;
    }

    
    public ACLMessage CreateReply(ACLPerformative performative)
    {
        // Crea una respuesta invirtiendo emisor/receptor y conservando la conversacion
        ACLMessage reply = new ACLMessage(performative, Receiver, Sender);
        reply.ConversationId = ConversationId;
        reply.InReplyTo = ReplyWith;
        reply.Protocol = Protocol;
        reply.Ontology = Ontology;
        reply.Language = Language;
        return reply;
    }

    
    public ACLMessage CloneForReceiver(string newReceiver)
    {
        // Duplica una plantilla de mensaje para enviarla a otro receptor
        ACLMessage copy = new ACLMessage(Performative, Sender, newReceiver);
        copy.Content = Content;
        copy.Language = Language;
        copy.Ontology = Ontology;
        copy.Protocol = Protocol;
        copy.ConversationId = ConversationId;
        copy.ReplyWith = "";  // Se asignará al enviar
        copy.InReplyTo = InReplyTo;
        return copy;
    }

    
    public override string ToString()
    {
        return $"({Performative} " +
               $":sender {Sender} " +
               $":receiver {Receiver} " +
               $":content \"{TruncateContent(60)}\" " +
               $":protocol {Protocol} " +
               $":conversation-id {ConversationId})";
    }

    private string TruncateContent(int maxLength)
    {
        // Evita que el log de un mensaje sea demasiado largo.
        if (string.IsNullOrEmpty(Content)) return "";
        return Content.Length <= maxLength ? Content : Content.Substring(0, maxLength) + "...";
    }
}
