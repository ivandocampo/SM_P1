using UnityEngine;


public static class ContentLanguage
{
    // CODIFICACIÓN — Objeto → String (para enviar)

    public static string Encode(ThiefSighting sighting)
    {
        return JsonUtility.ToJson(sighting);
    }

    public static string Encode(SearchTask task)
    {
        return JsonUtility.ToJson(task);
    }

    public static string Encode(GuardStatus status)
    {
        return JsonUtility.ToJson(status);
    }

    public static string Encode(ProposalData proposal)
    {
        return JsonUtility.ToJson(proposal);
    }

    public static string Encode(ActionRequest request)
    {
        return JsonUtility.ToJson(request);
    }

    public static string EncodePredicate(PredicateType predicate, string extraData = "")
    {
        PredicateInfo info = new PredicateInfo
        {
            Predicate = predicate.ToString(),
            ExtraData = extraData
        };
        return JsonUtility.ToJson(info);
    }

    // DECODIFICACIÓN — String → Objeto (al recibir)

    public static ThiefSighting DecodeThiefSighting(string json)
    {
        return JsonUtility.FromJson<ThiefSighting>(json);
    }

    public static SearchTask DecodeSearchTask(string json)
    {
        return JsonUtility.FromJson<SearchTask>(json);
    }

    public static GuardStatus DecodeGuardStatus(string json)
    {
        return JsonUtility.FromJson<GuardStatus>(json);
    }

    public static ProposalData DecodeProposal(string json)
    {
        return JsonUtility.FromJson<ProposalData>(json);
    }

    public static ActionRequest DecodeActionRequest(string json)
    {
        return JsonUtility.FromJson<ActionRequest>(json);
    }

    public static PredicateInfo DecodePredicate(string json)
    {
        return JsonUtility.FromJson<PredicateInfo>(json);
    }

    // DETECCIÓN DE TIPO — Identificar qué contiene un mensaje

   
    public static bool IsPredicate(string json)
    {
        return !string.IsNullOrEmpty(json) && json.Contains("\"Predicate\"");
    }

    
    public static bool IsThiefSighting(string json)
    {
        return !string.IsNullOrEmpty(json) && json.Contains("\"ReportedBy\"");
    }

    
    public static bool IsSearchTask(string json)
    {
        return !string.IsNullOrEmpty(json) && json.Contains("\"TaskId\"");
    }

    
    public static bool IsProposal(string json)
    {
        return !string.IsNullOrEmpty(json) && json.Contains("\"Cost\"");
    }

    
    public static bool IsActionRequest(string json)
    {
        return !string.IsNullOrEmpty(json) && json.Contains("\"Action\"");
    }

    public static bool IsGuardStatus(string json)
    {
        // "CurrentState" es exclusivo de GuardStatus; ProposalData también tiene "GuardId"
        return !string.IsNullOrEmpty(json) && json.Contains("\"CurrentState\"");
    }

}