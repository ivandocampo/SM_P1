using UnityEngine;


[System.Serializable]
public class ACLMessage
{
    public ACLPerformative Performative;
    public string Sender;
    public string Receiver;

    public string Content;          // Contenido serializado (JSON via ContentLanguage)
    public string Language;         // Lenguaje del contenido ("fipa-sl")
    public string Ontology;         // Ontología utilizada ("guard-ontology")

    public string Protocol;         // "fipa-request", "fipa-query", "fipa-contract-net"
    public string ConversationId;   // Identificador único de la conversación
    public string ReplyWith;        // ID para que el receptor referencie en su respuesta
    public string InReplyTo;        // Referencia al ReplyWith del mensaje que se contesta

    // === Metadatos ===
    public float Timestamp;         // Time.time en el momento de la creación

    
    public ACLMessage(ACLPerformative performative, string sender, string receiver)
    {
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
        if (string.IsNullOrEmpty(Content)) return "";
        return Content.Length <= maxLength ? Content : Content.Substring(0, maxLength) + "...";
    }
}