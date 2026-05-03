// =============================================================
// Actuador de movimiento de los guardias (orcos) por el laberinto.
// Envuelve NavMeshAgent para que los comportamientos BDI (PatrolBehavior,
// PursuitBehavior, etc.) puedan mover al guardia sin acceder directamente
// al agente de navegación. Gestiona tres velocidades según el estado táctico
// =============================================================

using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(NavMeshAgent))]
public class ActuadorMovimiento : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadPatrulla = 4.5f;
    public float velocidadAlerta = 8.0f;
    public float velocidadPersecucion = 10.0f;

    private NavMeshAgent agent;

    // Comprobar si el guardia ha llegado al destino actual dentro del margen indicado
    public bool HaLlegado(float margen = 1.0f)
    {
        if (agent == null) return true;
        return !agent.pathPending && agent.remainingDistance < margen;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Asignar un nuevo destino al guardia con la velocidad correspondiente al estado táctico
    public void SetDestino(Vector3 destino, TipoVelocidad tipo = TipoVelocidad.Alerta)
    {
        if (agent == null) return;

        agent.speed = ObtenerVelocidad(tipo);
        agent.isStopped = false;
        agent.destination = destino;
    }

    // Detener al guardia en seco anulando su movimiento
    public void Detener()
    {
        if (agent == null) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    // Cambiar la velocidad del guardia sin modificar su destino actual
    public void CambiarVelocidad(TipoVelocidad tipo)
    {
        if (agent == null) return;
        agent.speed = ObtenerVelocidad(tipo);
    }

    // Verificar si existe una ruta completa y navegable hasta el punto indicado
    public bool PuntoAlcanzable(Vector3 punto)
    {
        if (agent == null) return false;

        NavMeshPath path = new NavMeshPath();
        return agent.CalculatePath(punto, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    // Generar un punto aleatorio navegable dentro de un radio, usado por los comportamientos de búsqueda
    public Vector3 GenerarPuntoAleatorio(Vector3 centro, float radio)
    {
        for (int i = 0; i < 15; i++)
        {
            Vector3 puntoRandom = centro + Random.insideUnitSphere * radio;
            puntoRandom.y = centro.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(puntoRandom, out hit, 2f, NavMesh.AllAreas))
            {
                // Descartar puntos demasiado cerca de obstáculos
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
                {
                    if (edgeHit.distance < 0.8f) continue;
                }

                // Verificar que haya ruta completa hasta el punto
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    return hit.position;
                }
            }
        }
        // Si no se encuentra ningún punto válido, devolver el centro como fallback
        return centro;
    }

    // Traducir el enum TipoVelocidad al valor numérico configurado en el Inspector
    private float ObtenerVelocidad(TipoVelocidad tipo)
    {
        switch (tipo)
        {
            case TipoVelocidad.Patrulla: return velocidadPatrulla;
            case TipoVelocidad.Alerta: return velocidadAlerta;
            case TipoVelocidad.Persecucion: return velocidadPersecucion;
            default: return velocidadAlerta;
        }
    }
}

// Estados de velocidad posibles para el guardia según su situación táctica
public enum TipoVelocidad
{
    Patrulla,
    Alerta,
    Persecucion
}
