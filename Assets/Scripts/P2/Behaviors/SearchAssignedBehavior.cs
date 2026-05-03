using UnityEngine;

[System.Serializable]
public class SearchAssignedBehavior : IBehavior
{
    private Vector3[] puntosBusqueda;
    private int indicePunto = 0;
    private const int PUNTOS_FALLBACK = 4;

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        indicePunto = 0;

        SearchTask tarea = creencias.TareaAsignada;
        if (tarea == null)
        {
            puntosBusqueda = new Vector3[0];
            return;
        }

        Vector3[] puntosZona = creencias.ObtenerPuntosZona(tarea.ZoneId);
        if (puntosZona != null && puntosZona.Length > 0)
        {
            puntosBusqueda = puntosZona;
        }
        else
        {
            Vector3 centro = tarea.TargetArea != null
                ? tarea.TargetArea.ToVector3()
                : creencias.UltimaPosicionLadron;
            float radio = Mathf.Max(tarea.Radius, 8f);

            puntosBusqueda = new Vector3[PUNTOS_FALLBACK];
            for (int i = 0; i < puntosBusqueda.Length; i++)
                puntosBusqueda[i] = actuador.GenerarPuntoAleatorio(centro, radio);

            Debug.LogWarning($"[SearchAssigned] Zona '{tarea.ZoneId}' sin puntos locales; usando fallback alrededor de {centro}");
        }

        if (puntosBusqueda.Length > 0)
            actuador.SetDestino(puntosBusqueda[0], TipoVelocidad.Alerta);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosBusqueda == null || puntosBusqueda.Length == 0) return true;

        if (actuador.HaLlegado(1.5f))
        {
            indicePunto++;
        }

        if (indicePunto >= puntosBusqueda.Length)
            return true;

        actuador.SetDestino(puntosBusqueda[indicePunto], TipoVelocidad.Alerta);
        return false;
    }

    public void Detener(ActuadorMovimiento actuador) { }
}
