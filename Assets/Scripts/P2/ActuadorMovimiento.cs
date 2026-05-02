using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(NavMeshAgent))]
public class ActuadorMovimiento : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadPatrulla = 3.5f;
    public float velocidadAlerta = 7.0f;
    public float velocidadPersecucion = 9.0f;

    private NavMeshAgent agent;

    public float VelocidadActual => agent != null ? agent.velocity.magnitude : 0f;

    public bool HaLlegado(float margen = 1.0f)
    {
        if (agent == null) return true;
        return !agent.pathPending && agent.remainingDistance < margen;
    }

    public float DistanciaRestante => agent != null ? agent.remainingDistance : 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    
    public void SetDestino(Vector3 destino, TipoVelocidad tipo = TipoVelocidad.Alerta)
    {
        if (agent == null) return;

        agent.speed = ObtenerVelocidad(tipo);
        agent.isStopped = false;
        agent.destination = destino;
    }

    
    public void Detener()
    {
        if (agent == null) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    
    public void CambiarVelocidad(TipoVelocidad tipo)
    {
        if (agent == null) return;
        agent.speed = ObtenerVelocidad(tipo);
    }

    
    public bool PuntoAlcanzable(Vector3 punto)
    {
        if (agent == null) return false;

        NavMeshPath path = new NavMeshPath();
        return agent.CalculatePath(punto, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    
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

                // Verificar que haya ruta completa
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    return hit.position;
                }
            }
        }
        return centro;
    }

    
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


public enum TipoVelocidad
{
    Patrulla,
    Alerta,
    Persecucion
}
