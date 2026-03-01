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

    private Vector3[] puntosBusqueda;
    private int indiceBusqueda = 0;

    // Inicializa el componente de navegación al comenzar
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Actualiza la referencia de la última ubicación donde se detectó al objetivo
    public void SetUltimaPosicionConocida(Vector3 posicion)
    {
        ultimaPosicionConocida = posicion;
    }

    // Verifica si el agente ha alcanzado su destino actual dentro de un margen
    public bool HaLlegado(float margen = 1.0f)
    {
        return !agent.pathPending && agent.remainingDistance < margen;
    }

    // Gestiona el movimiento cíclico entre los puntos de patrulla establecidos
    public void EjecutarPatrulla()
    {
        agent.speed = velocidadPatrulla;
        if (puntosPatrulla.Length == 0) return;

        agent.destination = puntosPatrulla[indicePatrulla].position;
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            indicePatrulla = (indicePatrulla + 1) % puntosPatrulla.Length;
    }

    // Dirige al agente hacia la posición actual del objetivo o su último rastro
    public void EjecutarPersecucion(bool viendoAFrodo)
    {
        agent.speed = velocidadPersecucion;
        if (viendoAFrodo && objetivoFrodo != null)
            agent.destination = objetivoFrodo.position;
        else
            agent.destination = ultimaPosicionConocida;
    }

    private float tiempoAtascado = 0f;
    private Vector3 posicionAnterior;

    // Calcula una serie de puntos aleatorios cercanos para iniciar el rastreo
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

    // Controla el desplazamiento entre los puntos de inspección generados
    public void EjecutarBusqueda()
    {
        agent.speed = velocidadAlerta;

        if (puntosBusqueda == null || puntosBusqueda.Length == 0) return;

        agent.destination = puntosBusqueda[indiceBusqueda];

        // Cambia al siguiente punto si detecta que el agente no logra avanzar
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

        // Avanza en la lista de puntos al llegar a cada destino individual
        if (!agent.pathPending && agent.remainingDistance < 1.5f)
        {
            indiceBusqueda++;
            tiempoAtascado = 0f;
        }

        // Reinicia el ciclo de búsqueda si se han visitado todos los puntos
        if (indiceBusqueda >= puntosBusqueda.Length)
        {
            IniciarBusqueda();
        }
    }

    // Busca una coordenada válida y accesible dentro de la malla de navegación
    Vector3 GenerarPuntoAlcanzable(Vector3 centro, float radio)
    {
        for (int i = 0; i < 15; i++)
        {
            Vector3 puntoRandom = centro + Random.insideUnitSphere * radio;
            puntoRandom.y = centro.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(puntoRandom, out hit, 2f, NavMesh.AllAreas))
            {
                // Descarta puntos que se encuentren demasiado próximos a obstáculos
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
                {
                    if (edgeHit.distance < 0.8f) continue;
                }

                // Asegura que exista una ruta completa y válida hasta el punto generado
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    return hit.position;
                }
            }
        }
        return centro;
    }

    // Dirige al agente al pedestal para verificar la presencia del objeto
    public bool EjecutarComprobarAnillo()
    {
        agent.speed = velocidadAlerta;
        if (pedestalAnillo != null)
            agent.destination = pedestalAnillo.position;
        return !agent.pathPending && agent.remainingDistance < 2.0f;
    }

    // Desplaza al agente hacia la zona de salida para interceptar al objetivo
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