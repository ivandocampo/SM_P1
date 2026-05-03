// =============================================================
// Actuador de movimiento de Frodo (personaje controlado por el jugador).
// Recibe la dirección calculada desde CerebroFrodo y mueve al personaje
// por el laberinto usando NavMeshAgent. Expone la velocidad actual para
// que SensorOido pueda determinar si Frodo está caminando o corriendo
// =============================================================

using UnityEngine;
using UnityEngine.AI;

public class ActuadorMovimientoFrodo : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadCaminar = 5f;
    public float velocidadCorrer = 10f;

    private NavMeshAgent agent;

    // Inicializar el NavMeshAgent y activar la rotación automática
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = true;
    }

    // Mover a Frodo en la dirección indicada, a velocidad de caminar o correr
    public void Mover(Vector3 direccion, bool corriendo)
    {
        // No mover si la partida no está activa
        if (!GameManager.Instance.PartidaActiva) return;
        // Ignorar inputs demasiado pequeños
        if (direccion.magnitude < 0.1f) return;

        float velocidad = corriendo ? velocidadCorrer : velocidadCaminar;
        agent.Move(direccion * velocidad * Time.deltaTime);

        // Orientar el personaje hacia donde se mueve
        transform.rotation = Quaternion.LookRotation(direccion);
    }

    // Devolver la velocidad actual; SensorOido la consulta para saber si Frodo hace ruido
    public float VelocidadActual()
    {
        return agent.velocity.magnitude;
    }
}
