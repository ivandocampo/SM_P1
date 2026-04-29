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
        // Equipo de persecución emergente con tamaño máximo 2: si soy el más cercano
        // o aún hay hueco entre los que ya persiguen, me uno; si no, dejo que otros
        // mantengan la cobertura del mapa (búsqueda, bloqueo de salida, etc.).
        if (creencias.LadronVisible)
        {
            const int MAX_PERSEGUIDORES = 2;
            bool soyMejorPerseguidor = !creencias.AlguienPersiguiendo()
                || creencias.SoyElMasCercanoA(creencias.UltimaPosicionLadron);

            if (soyMejorPerseguidor || creencias.GuardiasPersiguiendo() < MAX_PERSEGUIDORES)
            {
                deseos.Add(new Desire(
                    BehaviorType.Pursuit,
                    100f,
                    creencias.UltimaPosicionLadron
                ));
            }
        }

        // === Tarea asignada vía Contract Net ===
        // Prioridad 85 para superar a CheckPedestal(82) — un guardia que acepta una zona
        // debe ejecutarla, no quedarse en CheckPedestal por ser el más cercano al pedestal.
        if (creencias.TieneTareaAsignada)
        {
            deseos.Add(new Desire(
                BehaviorType.SearchAssigned,
                85f,
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
                                 BehaviorType.Search;

            deseos.Add(new Desire(nombreDeseo, 75f, pos, creencias.ConversacionRequest));
        }

        // === Búsqueda activa (info reciente pero no vemos al ladrón) ===
        // Limitada: si ya hay 2+ guardias buscando, no añadir más buscadores
        int maxGuardiasBuscando = creencias.AnilloRobado ? 3 : 2;
        if (!creencias.LadronVisible && creencias.TieneInfoReciente(15f)
            && creencias.GuardiasBuscando() < maxGuardiasBuscando)
        {
            deseos.Add(new Desire(
                BehaviorType.Search,
                70f,
                creencias.UltimaPosicionLadron
            ));
        }

        // === Asegurar pedestal si vimos al ladrón sin anillo ===
        if (!creencias.AnilloRobado &&
            creencias.TienePosicionPedestal &&
            creencias.TieneInfoReciente(12f) &&
            !creencias.AlguienGuardandoPedestal() &&
            creencias.SoyElMasCercanoA(creencias.PosicionPedestal))
        {
            deseos.Add(new Desire(
                BehaviorType.CheckPedestal,
                82f,
                creencias.PosicionPedestal
            ));
        }

        // === Bloquear salida (anillo robado, hasta 2 bloqueadores) ===
        // Cuando el anillo está robado, la salida es el punto crítico: el ladrón
        // tiene que pasar obligatoriamente por ahí. Permitimos hasta 2 bloqueadores
        // para garantizar cobertura aunque uno se vea desplazado por la persecución.
        const int MAX_BLOQUEADORES = 2;
        if (creencias.AnilloRobado && creencias.GuardiasBloqueando() < MAX_BLOQUEADORES)
        {
            float prioridadBloqueo = creencias.LadronVisible ? 90f : 95f;
            deseos.Add(new Desire(BehaviorType.BlockExit, prioridadBloqueo));
        }

        // === Búsqueda con info menos reciente ===
        if (!creencias.LadronVisible && creencias.TieneInfoReciente(30f)
            && !creencias.TieneInfoReciente(15f))
        {
            deseos.Add(new Desire(
                BehaviorType.Search,
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
