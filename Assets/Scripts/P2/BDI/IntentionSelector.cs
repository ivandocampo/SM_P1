// =============================================================
// Selector de intenciones BDI del guardia.
// Recibe la lista de deseos generados y elige la intención activa
// aplicando mecanismos anti-oscilación: un umbral mínimo de diferencia
// de prioridad para cambiar (15 puntos) y un cooldown de 1.5 s entre
// reconsideraciones. La única excepción que salta estos filtros es
// la activación de Pursuit cuando el guardia ve a Frodo por primera vez
// =============================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class IntentionSelector
{
    public Desire IntencionActual { get; private set; } = null;

    // Nombre del behavior activo; BehaviorType.None si no hay intención
    public BehaviorType NombreIntencion => IntencionActual?.Nombre ?? BehaviorType.None;

    public bool CambioDeIntencion { get; private set; } = false;

    // Umbral mínimo de diferencia de prioridad para cambiar de intención (anti-oscilación)
    private float umbralCambio = 15f;
    private float tiempoMinReconsideracion = 1.5f;

    private float tiempoUltimoCambio = 0f;
    private float cooldownPostCambio = 2.0f;
    private float tiempoUltimoBehaviorChange = 0f;

    public IntentionSelector(float umbral = 15f, float tiempoMin = 1.5f)
    {
        umbralCambio = umbral;
        tiempoMinReconsideracion = tiempoMin;
    }

    // Seleccionar la intención activa a partir de la lista de deseos generados
    public void Seleccionar(List<Desire> deseos, BeliefBase creencias)
    {
        CambioDeIntencion = false;

        if (deseos == null || deseos.Count == 0)
        {
            if (IntencionActual != null)
            {
                IntencionActual = null;
                CambioDeIntencion = true;
            }
            return;
        }

        // Ordenar por prioridad descendente; el mejor deseo es el candidato al cambio
        deseos.Sort((a, b) => b.Prioridad.CompareTo(a.Prioridad));
        Desire mejorDeseo = deseos[0];

        // Detectar si Pursuit entra por primera vez (caso urgente que salta filtros)
        bool persecucionDirectaNueva =
            mejorDeseo.Nombre == BehaviorType.Pursuit &&
            IntencionActual?.Nombre != BehaviorType.Pursuit;

        // Respetar el cooldown post-cambio excepto para Pursuit urgente
        if (IntencionActual != null &&
            Time.time - tiempoUltimoBehaviorChange < cooldownPostCambio &&
            !persecucionDirectaNueva)
        {
            return;
        }

        // Sin intención previa: adoptar el mejor deseo directamente
        if (IntencionActual == null)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // Si la intención actual ya no está en la lista de deseos, cambiar inmediatamente
        bool intencionSigueValida = deseos.Any(d => d.Nombre == IntencionActual.Nombre);
        if (!intencionSigueValida)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // Ver al ladrón rompe intercept/search/pedestal aunque no supere el umbral numérico
        if (persecucionDirectaNueva)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // Respetar el tiempo mínimo entre reconsideraciones
        if (Time.time - tiempoUltimoCambio < tiempoMinReconsideracion)
            return;

        float prioridadActual = deseos
            .Where(d => d.Nombre == IntencionActual.Nombre)
            .Max(d => d.Prioridad);

        // Solo cambiar si el nuevo deseo supera la prioridad actual en más del umbral
        if (mejorDeseo.Prioridad > prioridadActual + umbralCambio)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // Si no hay cambio, actualizar la posición objetivo de la intención actual
        Desire actualizado = deseos.FirstOrDefault(d => d.Nombre == IntencionActual.Nombre);
        if (actualizado != null)
        {
            IntencionActual.PosicionObjetivo = actualizado.PosicionObjetivo;
            IntencionActual.Prioridad = actualizado.Prioridad;
        }
    }

    // Limpiar la intención actual para forzar una nueva elección en la próxima deliberación
    public void ForzarReset()
    {
        IntencionActual = null;
        CambioDeIntencion = true;
        tiempoUltimoCambio = 0f;
    }

    // Indicar si el guardia está libre para aceptar tareas o requests externos
    public bool EstaDisponible()
    {
        return IntencionActual == null
            || IntencionActual.Nombre == BehaviorType.Patrol
            || IntencionActual.Nombre == BehaviorType.CheckPedestal;
    }

    private void CambiarIntencion(Desire nuevaIntencion)
    {
        BehaviorType anterior = IntencionActual?.Nombre ?? BehaviorType.None;
        IntencionActual = nuevaIntencion;
        CambioDeIntencion = true;
        tiempoUltimoCambio = Time.time;
        tiempoUltimoBehaviorChange = Time.time;

        Debug.Log($"[BDI] Intencion cambiada: {anterior} -> {nuevaIntencion.Nombre} " +
                  $"(prioridad: {nuevaIntencion.Prioridad:F0})");
    }
}
