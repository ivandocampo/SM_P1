using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class IntentionSelector
{
    public Desire IntencionActual { get; private set; } = null;

    public BehaviorType NombreIntencion => IntencionActual?.Nombre ?? BehaviorType.None;

    public bool CambioDeIntencion { get; private set; } = false;

    [Header("Configuracion")]
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

        deseos.Sort((a, b) => b.Prioridad.CompareTo(a.Prioridad));
        Desire mejorDeseo = deseos[0];
        bool persecucionDirectaNueva =
            mejorDeseo.Nombre == BehaviorType.Pursuit &&
            IntencionActual?.Nombre != BehaviorType.Pursuit;

        if (IntencionActual != null &&
            Time.time - tiempoUltimoBehaviorChange < cooldownPostCambio &&
            !persecucionDirectaNueva)
        {
            return;
        }

        if (IntencionActual == null)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        bool intencionSigueValida = deseos.Any(d => d.Nombre == IntencionActual.Nombre);
        if (!intencionSigueValida)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        // Ver al ladron debe romper intercept/search/pedestal aunque la prioridad
        // numerica no supere el umbral anti-oscilacion.
        if (persecucionDirectaNueva)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

        if (Time.time - tiempoUltimoCambio < tiempoMinReconsideracion)
            return;

        float prioridadActual = deseos
            .Where(d => d.Nombre == IntencionActual.Nombre)
            .Max(d => d.Prioridad);

        if (mejorDeseo.Prioridad > prioridadActual + umbralCambio)
        {
            CambiarIntencion(mejorDeseo);
            return;
        }

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
        tiempoUltimoBehaviorChange = Time.time;

        Debug.Log($"[BDI] Intencion cambiada: {anterior} -> {nuevaIntencion.Nombre} " +
                  $"(prioridad: {nuevaIntencion.Prioridad:F0})");
    }
}
