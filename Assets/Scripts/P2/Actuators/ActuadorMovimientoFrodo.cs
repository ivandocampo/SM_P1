using UnityEngine;
using UnityEngine.AI;

public class ActuadorMovimientoFrodo : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadCaminar = 5f;
    public float velocidadCorrer = 10f;

    private NavMeshAgent agent;

    // Inicializa las referencias a los componentes al inicio de la ejecución
    void Start()
    {
        // Obtiene el componente de navegación NavMeshAgent del objeto
        agent = GetComponent<NavMeshAgent>();
        
        // Permite que el sistema de navegación gestione la orientación automática
        agent.updateRotation = true;
    }

    // Gestiona el desplazamiento físico del personaje basado en la dirección recibida
    public void Mover(Vector3 direccion, bool corriendo)
    {
        // Interrumpe el movimiento si la partida no se encuentra activa en el GameManager
        if (!GameManager.Instance.PartidaActiva) return;
        
        // Evita procesar el movimiento si la intensidad del input es insignificante
        if (direccion.magnitude < 0.1f) return;

        // Determina la velocidad de desplazamiento según el estado de carrera
        float velocidad = corriendo ? velocidadCorrer : velocidadCaminar;
        
        // Aplica el desplazamiento al agente utilizando la dirección y el tiempo transcurrido
        agent.Move(direccion * velocidad * Time.deltaTime);
        
        // Orienta el transform del personaje hacia la dirección de movimiento actual
        transform.rotation = Quaternion.LookRotation(direccion);
    }

    // Proporciona la magnitud de la velocidad actual para el uso de sensores externos
    public float VelocidadActual()
    {
        // Retorna el valor escalar de la velocidad registrada por el agente de navegación
        return agent.velocity.magnitude;
    }
}