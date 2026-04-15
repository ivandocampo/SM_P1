using UnityEngine;


[System.Serializable]
public class Position
{
    public float X;
    public float Y;
    public float Z;

    public Position() { }

    public Position(Vector3 v)
    {
        X = v.x;
        Y = v.y;
        Z = v.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }

    public float DistanceTo(Position other)
    {
        return Vector3.Distance(ToVector3(), other.ToVector3());
    }

    public override string ToString()
    {
        return $"({X:F1}, {Y:F1}, {Z:F1})";
    }
}


[System.Serializable]
public class ThiefSighting
{
    public Position Location;
    public float Timestamp;
    public string ReportedBy;      // ID del agente que lo detectó
    public bool DirectVision;      // true = visto, false = oído
}



[System.Serializable]
public class SearchTask
{
    public string TaskId;
    public Position TargetArea;     // Centro de la zona a buscar
    public float Radius;            // Radio de búsqueda
    public float Urgency;           // 0.0 - 1.0, influye en la prioridad
}


[System.Serializable]
public class GuardStatus
{
    public string GuardId;
    public Position CurrentPosition;
    public string CurrentState;     // "patrol", "pursuit", "search", "block", "investigate"
    public bool IsAvailable;        // Si puede aceptar nuevas tareas
}


[System.Serializable]
public class ProposalData
{
    public string GuardId;
    public float Cost;              // Menor = mejor (típicamente distancia)
    public float EstimatedTime;     // Segundos estimados para llegar y buscar
}

// PREDICADOS — Tipos de hechos sobre el mundo


public enum PredicateType
{
    THIEF_SPOTTED,              // Se ha visto o oído al ladrón
    THIEF_LOST,                 // Se ha perdido de vista al ladrón
    RING_STOLEN,                // El anillo ya no está en el pedestal
    RING_ON_PEDESTAL,           // El anillo sigue en su sitio
    ZONE_CLEAR,                 // Una zona ha sido rastreada sin resultado
    NEED_BACKUP,                // Se solicitan refuerzos en una posición
    EXIT_BLOCKED                // Un guardia confirma que está bloqueando la salida
}


[System.Serializable]
public class PredicateInfo
{
    public string Predicate;        // Nombre del predicado (PredicateType.ToString())
    public string ExtraData;        // Datos opcionales serializados
}

// ACCIONES — Tipos de acciones solicitables vía REQUEST


public enum ActionType
{
    PATROL_ZONE,                // Patrullar una zona específica
    SEARCH_AREA,                // Buscar al ladrón en un área
    PURSUE_THIEF,               // Perseguir al ladrón
    BLOCK_EXIT,                 // Ir a bloquear la salida
    INVESTIGATE_LOCATION,       // Investigar una posición concreta
    REPORT_STATUS,              // Reportar estado actual
    CHECK_PEDESTAL              // Ir a comprobar el pedestal del anillo
}


[System.Serializable]
public class ActionRequest
{
    public string Action;           // ActionType.ToString()
    public Position TargetPosition; // Posición objetivo (si aplica)
    public float Urgency;           // 0.0 - 1.0
}

