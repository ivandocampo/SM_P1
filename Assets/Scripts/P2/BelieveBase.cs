using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BeliefBase
{
    // CREENCIAS SOBRE EL LADRÓN

    /// <summary>Si el ladrón es visible en este momento (por sensores propios).</summary>
    public bool LadronVisible { get; private set; } = false;

    /// <summary>Última posición conocida del ladrón (vista u oída, propia o reportada).</summary>
    public Vector3 UltimaPosicionLadron { get; private set; } = Vector3.zero;

    /// <summary>Timestamp de la última detección del ladrón.</summary>
    public float TiempoUltimaDeteccion { get; private set; } = -100f;

    /// <summary>Si la última detección fue por visión directa (true) u oído/reporte (false).</summary>
    public bool UltimaDeteccionDirecta { get; private set; } = false;

    /// <summary>ID del agente que reportó la última posición (puede ser el propio).</summary>
    public string FuenteUltimaDeteccion { get; private set; } = "";

    /// <summary>Si tenemos información reciente sobre el ladrón (menos de maxAge segundos).</summary>
    public bool TieneInfoReciente(float maxAge = 10f)
    {
        return (Time.time - TiempoUltimaDeteccion) < maxAge;
    }

    /// <summary>Antigüedad en segundos de la última información sobre el ladrón.</summary>
    public float AntiguedadInfoLadron => Time.time - TiempoUltimaDeteccion;

    // CREENCIAS SOBRE EL ANILLO

    /// <summary>Si se sabe que el anillo ha sido robado.</summary>
    public bool AnilloRobado { get; private set; } = false;

    /// <summary>Timestamp de cuando se supo que el anillo fue robado.</summary>
    public float TiempoAnilloRobado { get; private set; } = -100f;

    // CREENCIAS SOBRE EL PROPIO AGENTE

    /// <summary>ID del propio agente.</summary>
    public string MiId { get; private set; }

    /// <summary>Posición actual del agente (actualizada cada frame).</summary>
    public Vector3 MiPosicion { get; set; }

    /// <summary>Nombre de la intención/estado actual.</summary>
    public BehaviorType EstadoActual { get; set; } = BehaviorType.None;

    /// <summary>Si el agente está disponible para aceptar tareas.</summary>
    public bool Disponible => EstadoActual == BehaviorType.Patrol || EstadoActual == BehaviorType.None;

    public bool DebeComprobarPedestal { get; set; } = false;

    // CREENCIAS SOBRE OTROS AGENTES

    /// <summary>Estados reportados por otros guardias.</summary>
    public Dictionary<string, GuardStatus> EstadosOtrosGuardias { get; private set; }
        = new Dictionary<string, GuardStatus>();

    // TAREAS ASIGNADAS

    /// <summary>Tarea de búsqueda asignada vía Contract Net.</summary>
    public SearchTask TareaAsignada { get; private set; } = null;

    /// <summary>Si hay una tarea asignada pendiente de ejecutar.</summary>
    public bool TieneTareaAsignada => TareaAsignada != null;

    /// <summary>ID de la conversación de la tarea asignada (para INFORM_DONE).</summary>
    public string ConversacionTareaAsignada { get; private set; } = "";

    /// <summary>ID del agente que nos asignó la tarea.</summary>
    public string AsignadorTarea { get; private set; } = "";

    // REQUEST PENDIENTES

    /// <summary>Solicitud de acción recibida vía REQUEST que aceptamos.</summary>
    public ActionRequest RequestAceptado { get; private set; } = null;

    /// <summary>Si hay un REQUEST aceptado pendiente.</summary>
    public bool TieneRequestPendiente => RequestAceptado != null;

    public string ConversacionRequest { get; private set; } = "";
    public string SolicitanteRequest { get; private set; } = "";

    // CONSTRUCTOR

    public BeliefBase(string agentId)
    {
        MiId = agentId;
    }

    // ACTUALIZACIONES — Llamadas desde el GuardAgent

    
    public void ActualizarPosicionLadron(Vector3 posicion, float timestamp,
        bool visionDirecta, string fuente)
    {
        // Solo aceptar información más reciente
        if (timestamp <= TiempoUltimaDeteccion) return;

        UltimaPosicionLadron = posicion;
        TiempoUltimaDeteccion = timestamp;
        UltimaDeteccionDirecta = visionDirecta;
        FuenteUltimaDeteccion = fuente;

        // Solo marcar como visible si es visión directa propia
        if (visionDirecta && fuente == MiId)
            LadronVisible = true;
    }

    
    public void MarcarLadronPerdido()
    {
        LadronVisible = false;
    }

    
    public void MarcarAnilloRobado()
    {
        if (!AnilloRobado)
        {
            AnilloRobado = true;
            TiempoAnilloRobado = Time.time;
        }
    }

    
    public void ActualizarEstadoGuardia(GuardStatus estado)
    {
        if (estado != null && !string.IsNullOrEmpty(estado.GuardId))
        {
            EstadosOtrosGuardias[estado.GuardId] = estado;
        }
    }

    
    public void AsignarTarea(SearchTask tarea, string conversacionId, string asignador)
    {
        TareaAsignada = tarea;
        ConversacionTareaAsignada = conversacionId;
        AsignadorTarea = asignador;
    }


    public void LimpiarTarea()
    {
        TareaAsignada = null;
        ConversacionTareaAsignada = "";
        AsignadorTarea = "";
    }


    public void AceptarRequest(ActionRequest request, string conversacionId, string solicitante)
    {
        RequestAceptado = request;
        ConversacionRequest = conversacionId;
        SolicitanteRequest = solicitante;
    }

    public void LimpiarRequest()
    {
        RequestAceptado = null;
        ConversacionRequest = "";
        SolicitanteRequest = "";
    }

    // CONSULTAS DE ALTO NIVEL

    
    public float DistanciaEstimadaAlLadron()
    {
        return Vector3.Distance(MiPosicion, UltimaPosicionLadron);
    }

    
    public bool AlguienPersiguiendo()
    {
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == "pursuit")
                return true;
        }
        return false;
    }

   
    public bool AlguienBloqueandoSalida()
    {
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == "block")
                return true;
        }
        return false;
    }

    
    public int GuardiasBuscando()
    {
        int count = 0;
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == "search" || par.Value.CurrentState == "investigate")
                count++;
        }
        return count;
    }
}