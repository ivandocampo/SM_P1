using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class Desire
{
    public BehaviorType Nombre;

    /// <summary>Prioridad del deseo. Mayor = más urgente. Rango típico: 0-100.</summary>
    public float Prioridad;

    /// <summary>Posición objetivo asociada al deseo (si aplica).</summary>
    public Vector3 PosicionObjetivo;

    /// <summary>Datos adicionales del contexto (tarea asignada, request, etc.).</summary>
    public string DatosExtra;

    public Desire(BehaviorType nombre, float prioridad, Vector3 posicion = default, string datos = "")
    {
        Nombre = nombre;
        Prioridad = prioridad;
        PosicionObjetivo = posicion;
        DatosExtra = datos;
    }

    public override string ToString()
    {
        return $"{Nombre} (prioridad: {Prioridad:F0})";
    }
}


public class DesireGenerator
{
    private BeliefBase creencias;

    public DesireGenerator(BeliefBase creencias)
    {
        this.creencias = creencias;
    }

    
    public List<Desire> GenerarDeseos()
    {
        List<Desire> deseos = new List<Desire>();

        // === PRIORIDAD MÁXIMA: Persecución directa ===
        if (creencias.LadronVisible)
        {
            deseos.Add(new Desire(
                BehaviorType.Pursuit,
                100f,
                creencias.UltimaPosicionLadron
            ));
        }

        // === Persecución por reporte reciente de otro agente ===
        if (!creencias.LadronVisible && creencias.TieneInfoReciente(5f)
            && creencias.UltimaDeteccionDirecta
            && creencias.FuenteUltimaDeteccion != creencias.MiId)
        {
            // Alguien lo está viendo ahora — prioridad alta
            float distancia = creencias.DistanciaEstimadaAlLadron();
            float prioridad = Mathf.Lerp(90f, 70f, distancia / 50f);
            deseos.Add(new Desire(
                BehaviorType.Pursuit,
                prioridad,
                creencias.UltimaPosicionLadron
            ));
        }

        // === Tarea asignada vía Contract Net ===
        if (creencias.TieneTareaAsignada)
        {
            deseos.Add(new Desire(
                BehaviorType.SearchAssigned,
                80f,
                creencias.TareaAsignada.TargetArea.ToVector3(),
                creencias.ConversacionTareaAsignada
            ));
        }

        // === REQUEST aceptado pendiente ===
        if (creencias.TieneRequestPendiente)
        {
            string accion = creencias.RequestAceptado.Action;
            Vector3 pos = creencias.RequestAceptado.TargetPosition != null
                ? creencias.RequestAceptado.TargetPosition.ToVector3()
                : Vector3.zero;

            BehaviorType nombreDeseo = accion == ActionType.BLOCK_EXIT.ToString() ? BehaviorType.BlockExit :
                                 accion == ActionType.SEARCH_AREA.ToString() ? BehaviorType.SearchAssigned :
                                 BehaviorType.Investigate;

            deseos.Add(new Desire(nombreDeseo, 75f, pos, creencias.ConversacionRequest));
        }

        // === Búsqueda activa (info reciente pero no vemos al ladrón) ===
        if (!creencias.LadronVisible && creencias.TieneInfoReciente(15f))
        {
            deseos.Add(new Desire(
                BehaviorType.Search,
                70f,
                creencias.UltimaPosicionLadron
            ));
        }

        // === Bloquear salida (anillo robado, nadie bloqueando aún) ===
        if (creencias.AnilloRobado && !creencias.AlguienBloqueandoSalida())
        {
            deseos.Add(new Desire(BehaviorType.BlockExit, 60f));
        }

        // === Investigar posición reportada (info menos reciente) ===
        if (!creencias.LadronVisible && creencias.TieneInfoReciente(30f)
            && !creencias.TieneInfoReciente(15f))
        {
            deseos.Add(new Desire(
                BehaviorType.Investigate,
                50f,
                creencias.UltimaPosicionLadron
            ));
        }

        // === Comprobar pedestal (rutina periódica) ===
        if (creencias.DebeComprobarPedestal)
            deseos.Add(new Desire(BehaviorType.CheckPedestal, 20f));

        // === Patrullar (siempre disponible como fallback) ===
        deseos.Add(new Desire(BehaviorType.Patrol, 10f));

        return deseos;
    }
}