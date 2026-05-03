// =============================================================
// Lenguaje de contenido para los mensajes entre agentes.
// Convierte los objetos de la ontologia compartida a JSON antes de
// enviarlos en ACLMessage.Content y los reconstruye al recibirlos.
// Tambien ofrece detectores sencillos de tipo basados en campos
// caracteristicos del JSON recibido
// =============================================================

using UnityEngine;


public static class ContentLanguage
{

    public static string Encode(ThiefSighting sighting)
    {
        // Serializa un avistamiento del ladron para enviarlo en Content
        return JsonUtility.ToJson(sighting);
    }

    public static string Encode(SearchTask task)
    {
        // Serializa una tarea de busqueda asignable por Contract-Net
        return JsonUtility.ToJson(task);
    }

    public static string Encode(GuardStatus status)
    {
        // Serializa el estado actual de un guardia para los heartbeats
        return JsonUtility.ToJson(status);
    }

    public static string Encode(ProposalData proposal)
    {
        // Serializa una propuesta de coste para responder a un CFP
        return JsonUtility.ToJson(proposal);
    }

    public static string Encode(ActionRequest request)
    {
        // Serializa una peticion directa de accion
        return JsonUtility.ToJson(request);
    }

    public static string EncodePredicate(PredicateType predicate, string extraData = "")
    {
        // Envuelve un predicado factual en una estructura comun
        PredicateInfo info = new PredicateInfo
        {
            Predicate = predicate.ToString(),
            ExtraData = extraData
        };
        return JsonUtility.ToJson(info);
    }

    public static ThiefSighting DecodeThiefSighting(string json)
    {
        // Reconstruye un avistamiento recibido en un INFORM
        return JsonUtility.FromJson<ThiefSighting>(json);
    }

    public static SearchTask DecodeSearchTask(string json)
    {
        // Reconstruye una tarea de busqueda recibida por CFP o ACCEPT_PROPOSAL
        return JsonUtility.FromJson<SearchTask>(json);
    }

    public static GuardStatus DecodeGuardStatus(string json)
    {
        // Reconstruye el estado de un guardia recibido por heartbeat o query
        return JsonUtility.FromJson<GuardStatus>(json);
    }

    public static ProposalData DecodeProposal(string json)
    {
        // Reconstruye una propuesta enviada por un participante del Contract-Net
        return JsonUtility.FromJson<ProposalData>(json);
    }

    public static ActionRequest DecodeActionRequest(string json)
    {
        // Reconstruye una solicitud directa de accion
        return JsonUtility.FromJson<ActionRequest>(json);
    }

    public static PredicateInfo DecodePredicate(string json)
    {
        // Reconstruye un hecho factual difundido entre agentes
        return JsonUtility.FromJson<PredicateInfo>(json);
    }

   
    public static bool IsPredicate(string json)
    {
        // Predicate es exclusivo de PredicateInfo
        return !string.IsNullOrEmpty(json) && json.Contains("\"Predicate\"");
    }

    
    public static bool IsThiefSighting(string json)
    {
        // ReportedBy identifica mensajes de avistamiento
        return !string.IsNullOrEmpty(json) && json.Contains("\"ReportedBy\"");
    }

    
    public static bool IsSearchTask(string json)
    {
        // TaskId identifica tareas de busqueda
        return !string.IsNullOrEmpty(json) && json.Contains("\"TaskId\"");
    }

    
    public static bool IsProposal(string json)
    {
        // Cost identifica propuestas de Contract-Net
        return !string.IsNullOrEmpty(json) && json.Contains("\"Cost\"");
    }

    
    public static bool IsActionRequest(string json)
    {
        // Action identifica solicitudes REQUEST
        return !string.IsNullOrEmpty(json) && json.Contains("\"Action\"");
    }

    public static bool IsGuardStatus(string json)
    {
        return !string.IsNullOrEmpty(json) && json.Contains("\"CurrentState\"");
    }

}
