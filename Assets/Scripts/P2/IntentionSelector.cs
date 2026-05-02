using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class IntentionSelector
{
    /// <summary>Intención actualmente activa.</summary>
    public Desire IntencionActual { get; private set; } = null;

    public BehaviorType NombreIntencion => IntencionActual?.Nombre ?? BehaviorType.None;

    /// <summary>Si se ha producido un cambio de intención en la última selección.</summary>
    public bool CambioDeIntencion { get; private set; } = false;

    [Header("Configuración")]
    /// <summary>
    /// Diferencia mínima de prioridad para cambiar de intención.
    /// Evita oscilaciones entre deseos de prioridad similar.
    /// </summary>
    private float umbralCambio = 15f;

    /// <summary>Tiempo mínimo antes de poder reconsiderar la intención.</summary>
    private float tiempoMinReconsideracion = 1.5f;

    private float tiempoUltimoCambio = 0f;
    
    /// <summary>Cooldown adicional después de cambiar de behavior para evitar oscilación rápida.</summary>
    private float cooldownPostCambio = 2.0f;
    private float tiempoUltimoBehaviorChange = 0f;

    public IntentionSelector(float umbral = 15f, float tiempoMin = 1.5f)
    {
        umbralCambio = umbral;
        tiempoMinReconsideracion = tiempoMin;
    }

    
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

        // Verificar cooldown post-cambio para evitar oscilación
        if (IntencionActual != null &&
            Time.time - tiempoUltimoBehaviorChange < cooldownPostCambio)
        {
            // Excepción: persecución directa siempre se acepta inmediatamente
            Desire candidato = deseos[0];
            if (candidato.Nombre == BehaviorType.Pursuit && candidato.Prioridad >= 100f
                && IntencionActual?.Nombre != BehaviorType.Pursuit)
            {
                // Permitir cambio solo para persecución de alta prioridad
            }
            else
            {
                return; // En cooldown, mantener intención actual
            }
        }

        // Ordenar deseos por prioridad descendente
        deseos.Sort((a, b) => b.Prioridad.CompareTo(a.Prioridad));
        Desire mejorDeseo = deseos[0];

        // === CASO 1: No hay intención actual → adoptar la mejor ===
        if (IntencionActual == null)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // === CASO 2: La intención actual ya no está entre los deseos posibles ===
        bool intencionSigueValida = deseos.Any(d => d.Nombre == IntencionActual.Nombre);
        if (!intencionSigueValida)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // === CASO 3: Reconsideración periódica ===
        if (Time.time - tiempoUltimoCambio < tiempoMinReconsideracion)
        {
            // Excepción: persecución directa siempre se acepta inmediatamente
            if (mejorDeseo.Nombre == BehaviorType.Pursuit && mejorDeseo.Prioridad >= 100f
                && IntencionActual.Nombre != BehaviorType.Pursuit)
            {
                CambiarIntencion(mejorDeseo);
            }
            return;
        }

        // === CASO 4: Hay un deseo significativamente mejor ===
        // Buscar la prioridad actual del deseo que coincide con la intención
        float prioridadActual = deseos
            .Where(d => d.Nombre == IntencionActual.Nombre)
            .Max(d => d.Prioridad);

        if (mejorDeseo.Prioridad > prioridadActual + umbralCambio)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // === CASO 5: Mantener intención actual, pero actualizar datos ===
        // (ej: posición del ladrón puede haber cambiado)
        Desire actualizado = deseos.FirstOrDefault(d => d.Nombre == IntencionActual.Nombre);
        if (actualizado != null)
        {
            IntencionActual.PosicionObjetivo = actualizado.PosicionObjetivo;
            IntencionActual.Prioridad = actualizado.Prioridad;
        }
    }

    
    public void ForzarReset()
    {
        IntencionActual = null;
        CambioDeIntencion = true;
        tiempoUltimoCambio = 0f;
    }

    
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
        tiempoUltimoBehaviorChange = Time.time; // Registrar cambio de behavior

        Debug.Log($"[BDI] Intención cambiada: {anterior} → {nuevaIntencion.Nombre} " +
                  $"(prioridad: {nuevaIntencion.Prioridad:F0})");
    }
}
