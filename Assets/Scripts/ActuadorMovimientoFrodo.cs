using UnityEngine;
using UnityEngine.AI;

public class ActuadorMovimientoFrodo : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadCaminar = 5f;
    public float velocidadCorrer = 10f;

    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = true;
    }

    // Ejecuta el movimiento físico según el input recibido del Cerebro
    public void Mover(Vector3 direccion, bool corriendo)
    {
        if (!GameManager.Instance.PartidaActiva) return;
        if (direccion.magnitude < 0.1f) return;

        float velocidad = corriendo ? velocidadCorrer : velocidadCaminar;
        agent.Move(direccion * velocidad * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(direccion);
    }

    // Devuelve la velocidad actual del agente, usada por los sensores de los Orcos
    public float VelocidadActual()
    {
        return agent.velocity.magnitude;
    }
}