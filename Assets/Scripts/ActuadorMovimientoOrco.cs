using UnityEngine;
using UnityEngine.AI;

public class ActuadorMovimientoOrco : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadPatrulla = 3.5f;
    public float velocidadAlerta = 6.0f;
    public float velocidadPersecucion = 8f;

    [Header("Patrulla")]
    public Transform[] puntosPatrulla;

    [Header("Patrulla Bloqueo Salida")]
    public Transform[] puntosBloqueoSalida;

    [Header("Puntos Clave")]
    public Transform pedestalAnillo;
    public Transform puntoSalida;
    public Transform objetivoFrodo;

    [Header("Búsqueda Activa")]
    public float radioBusqueda = 10f;
    public int puntosBusquedaMax = 4;

    private NavMeshAgent agent;
    private int indicePatrulla = 0;
    private int indiceBloqueo = 0;
    private Vector3 ultimaPosicionConocida;

    // Búsqueda activa
    private Vector3[] puntosBusqueda;
    private int indiceBusqueda = 0;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void SetUltimaPosicionConocida(Vector3 posicion)
    {
        ultimaPosicionConocida = posicion;
    }

    // Comprueba si el agente ha llegado a su destino
    public bool HaLlegado(float margen = 1.0f)
    {
        return !agent.pathPending && agent.remainingDistance < margen;
    }

    // ==================== PATRULLA ====================
    public void EjecutarPatrulla()
    {
        agent.speed = velocidadPatrulla;
        if (puntosPatrulla.Length == 0) return;

        agent.destination = puntosPatrulla[indicePatrulla].position;
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            indicePatrulla = (indicePatrulla + 1) % puntosPatrulla.Length;
    }

    // ==================== PERSECUCIÓN ====================
    public void EjecutarPersecucion(bool viendoAFrodo)
    {
        agent.speed = velocidadPersecucion;
        if (viendoAFrodo && objetivoFrodo != null)
            agent.destination = objetivoFrodo.position;
        else
            agent.destination = ultimaPosicionConocida;
    }

    // ==================== BÚSQUEDA ACTIVA ====================
    private float tiempoAtascado = 0f;
    private Vector3 posicionAnterior;

    public void IniciarBusqueda()
    {
        puntosBusqueda = new Vector3[puntosBusquedaMax];
        indiceBusqueda = 0;
        tiempoAtascado = 0f;
        posicionAnterior = transform.position;

        for (int i = 0; i < puntosBusquedaMax; i++)
        {
            puntosBusqueda[i] = GenerarPuntoAlcanzable(ultimaPosicionConocida, radioBusqueda);
        }
    }

    public void EjecutarBusqueda()
    {
        agent.speed = velocidadAlerta;

        if (puntosBusqueda == null || puntosBusqueda.Length == 0) return;

        agent.destination = puntosBusqueda[indiceBusqueda];

        // Detección de atasco: si lleva 2s casi sin moverse, saltar al siguiente punto
        if (Vector3.Distance(transform.position, posicionAnterior) < 0.3f)
        {
            tiempoAtascado += Time.deltaTime;
            if (tiempoAtascado > 2f)
            {
                indiceBusqueda++;
                tiempoAtascado = 0f;
            }
        }
        else
        {
            tiempoAtascado = 0f;
            posicionAnterior = transform.position;
        }

        if (!agent.pathPending && agent.remainingDistance < 1.5f)
        {
            indiceBusqueda++;
            tiempoAtascado = 0f;
        }

        if (indiceBusqueda >= puntosBusqueda.Length)
        {
            IniciarBusqueda();
        }
    }

    // Genera un punto aleatorio que sea ALCANZABLE por el NavMesh
    Vector3 GenerarPuntoAlcanzable(Vector3 centro, float radio)
    {
        for (int i = 0; i < 15; i++)
        {
            Vector3 puntoRandom = centro + Random.insideUnitSphere * radio;
            puntoRandom.y = centro.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(puntoRandom, out hit, 2f, NavMesh.AllAreas))
            {
                // Comprobar que no está pegado a una pared
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
                {
                    if (edgeHit.distance < 0.8f) continue; // Demasiado cerca de pared, intentar otro
                }

                // Comprobar que hay un camino completo hasta ahí
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    return hit.position;
                }
            }
        }
        return centro;
    }

    // ==================== COMPROBAR ANILLO ====================
    public bool EjecutarComprobarAnillo()
    {
        agent.speed = velocidadAlerta;
        if (pedestalAnillo != null)
            agent.destination = pedestalAnillo.position;
        return !agent.pathPending && agent.remainingDistance < 2.0f;
    }

    // ==================== BLOQUEAR SALIDA ====================
    public void EjecutarBloquearSalida()
    {
        agent.speed = velocidadAlerta;

        if (puntosBloqueoSalida != null && puntosBloqueoSalida.Length > 0)
        {
            agent.destination = puntosBloqueoSalida[indiceBloqueo].position;
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
                indiceBloqueo = (indiceBloqueo + 1) % puntosBloqueoSalida.Length;
        }
        else if (puntoSalida != null)
        {
            agent.destination = puntoSalida.position;
        }
    }
}