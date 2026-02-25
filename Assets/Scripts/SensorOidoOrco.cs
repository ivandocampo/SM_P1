using UnityEngine;
using UnityEngine.AI; // Necesario para leer la velocidad del NavMesh

public class SensorOidoOrco : MonoBehaviour
{
    [Header("Configuración de Audición")]
    public Transform objetivoFrodo;         
    public float rangoOido = 15f;           
    public float umbralVelocidadRuido = 4.0f; // A partir de qué velocidad hace ruido (ajusta según tu Frodo)

    private NavMeshAgent agenteFrodo;       // Referencia física, no al script lógico

    void Start()
    {
        if (objetivoFrodo != null)
        {
            // Obtenemos el componente de navegación para medir su velocidad real
            agenteFrodo = objetivoFrodo.GetComponent<NavMeshAgent>();
        }
    }

    public bool OirFrodo(out Vector3 posicionRuido)
    {
        posicionRuido = Vector3.zero;

        if (objetivoFrodo == null) return false;

        float distancia = Vector3.Distance(transform.position, objetivoFrodo.position);

        // 1. Si está fuera de rango, no oímos nada
        if (distancia > rangoOido) return false;

        // 2. Calculamos la velocidad real física
        float velocidadActual = 0f;

        if (agenteFrodo != null)
        {
            velocidadActual = agenteFrodo.velocity.magnitude;
        }
        else
        {
            // Si Frodo no usa NavMesh (es humano puro), estimamos velocidad manual (opcional)
            // velocidadActual = (objetivoFrodo.position - ultimaPosicion).magnitude / Time.deltaTime;
        }

        // 3. Evaluación Física: ¿Se mueve lo suficientemente rápido para hacer ruido?
        // Ya no preguntamos "bool estaCorriendo", sino que medimos el hecho.
        if (velocidadActual > umbralVelocidadRuido)
        {
            posicionRuido = objetivoFrodo.position;
            return true; // ¡Oído!
        }

        return false;
    }
}