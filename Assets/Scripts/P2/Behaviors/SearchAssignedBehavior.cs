// =============================================================
// Behavior de búsqueda en zona asignada vía Contract-Net.
// Lee la tarea de SearchTask en BeliefBase y obtiene los puntos
// registrados para esa zona. Si la zona no tiene puntos locales,
// genera un fallback aleatorio alrededor del centro de la tarea.
// Termina cuando todos los puntos han sido visitados
// =============================================================

using UnityEngine;

[System.Serializable]
public class SearchAssignedBehavior : IBehavior
{
    private Vector3[] puntosBusqueda;
    private int indicePunto = 0;
    // Número de puntos aleatorios a generar cuando la zona no tiene waypoints registrados
    private const int PUNTOS_FALLBACK = 4;

    // Cargar los puntos de la zona asignada; generar fallback si no hay waypoints registrados
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

    // Avanzar al siguiente punto de la zona al llegar al actual; terminar al agotar todos
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
