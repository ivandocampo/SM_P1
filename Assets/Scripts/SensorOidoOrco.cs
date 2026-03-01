using UnityEngine;

public class SensorOidoOrco : MonoBehaviour
{
    [Header("Audición - Frodo")]
    public Transform objetivoFrodo;
    public float rangoOidoCaminar = 5f;
    public float rangoOidoCorrer = 15f;

    private CerebroFrodo cerebroFrodo;
    private ActuadorMovimientoFrodo movimientoFrodo;

    // Obtiene las referencias necesarias del objetivo al iniciar el script
    void Start()
    {
        if (objetivoFrodo != null)
        {
            cerebroFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
            movimientoFrodo = objetivoFrodo.GetComponent<ActuadorMovimientoFrodo>();
        }
    }

    // Evalúa si el agente percibe el sonido del objetivo basándose en su velocidad y distancia
    public bool OirFrodo(out Vector3 posicionRuido)
    {
        posicionRuido = Vector3.zero;

        if (objetivoFrodo != null && cerebroFrodo != null && movimientoFrodo != null)
        {
            float distanciaFrodo = Vector3.Distance(transform.position, objetivoFrodo.position);
            float velocidadFrodo = movimientoFrodo.VelocidadActual();

            // Detecta si el objetivo está corriendo dentro del rango de audición extendido
            if (cerebroFrodo.estaCorriendo && distanciaFrodo < rangoOidoCorrer)
            {
                posicionRuido = objetivoFrodo.position;
                return true;
            }
            
            // Detecta si el objetivo camina dentro del rango de audición reducido
            if (!cerebroFrodo.estaCorriendo && velocidadFrodo > 0.5f && distanciaFrodo < rangoOidoCaminar)
            {
                posicionRuido = objetivoFrodo.position;
                return true;
            }
        }

        return false;
    }

    // Representa visualmente los rangos de audición mediante esferas de colores en el editor
    void OnDrawGizmos()
    {
        // Dibuja el radio de detección para el estado de carrera en amarillo
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, rangoOidoCorrer);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangoOidoCorrer);

        // Dibuja el radio de detección para el estado de caminata en naranja
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, rangoOidoCaminar);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, rangoOidoCaminar);
    }
}