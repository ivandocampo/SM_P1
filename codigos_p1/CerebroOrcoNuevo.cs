using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement; 

public class CerebroOrco : MonoBehaviour
{
    [Header("Configuración de Patrulla")]
    public Transform[] puntosPatrulla;      // Lista de puntos iniciales
    public float velocidadPatrulla = 3.5f;  
    public float velocidadAlerta = 6.0f;    
    public float tiempoDeEsperaAlInvestigar = 3.0f; 
    public float radioPatrullaLocal = 10f;  // Distancia máxima a la que dará vueltas en la zona caliente

    [Header("Configuración de Ataque")]
    public Transform objetivoFrodo;         
    public float velocidadPersecucion = 8f; 
    public float distanciaAtaque = 1.2f;    

    private NavMeshAgent agent;             
    private int indiceDestino = 0;          

    // Órganos Sensoriales
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;
    private SensorOlfatoOrco sensorOlfato;

    // Estados de la IA
    private bool viendoAFrodo = false;      
    private bool investigando = false;      
    private bool estaAgresivo = false;      
    private bool patrullandoZonaLocal = false;
    private Vector3 ultimaPosicionConocida; 
    private float temporizadorBusqueda = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();
        sensorOlfato = GetComponent<SensorOlfatoOrco>();

        IrAlSiguientePunto();
    }

    void Update()
    {
        // El orco comprueba si, con sus ojos, se da cuenta de que falta el anillo
        if (!estaAgresivo && sensorVista != null && sensorVista.NoAnillo())
        {
            estaAgresivo = true;
            Debug.Log("El orco ha visto que falta el anillo. Aumenta su agresividad.");
        }

        Vector3 origenDelRuido;

        // Sentidos precisos (Olor fuerte o Visión directa)
        if ((sensorOlfato != null && sensorOlfato.OlerFrodo()) || 
            (sensorVista != null && sensorVista.VerFrodo()))
        {
            patrullandoZonaLocal = false; // Si te encuentra, deja la patrulla local y ataca
            Perseguir();
        }
        // Sentido zonal (Oído capta ruido)
        else if (sensorOido != null && sensorOido.OirFrodo(out origenDelRuido))
        {
            patrullandoZonaLocal = false; 
            ultimaPosicionConocida = origenDelRuido;
            investigando = true;
            Investigar();
        }
        // Ir al último punto conocido a mirar
        else if (investigando)
        {
            Investigar();
        }
        // Quedarse patrullando la zona caliente
        else if (patrullandoZonaLocal)
        {
            PatrullarLocalmente();
        }
        // Rutina inicial (Patrulla de puntos predefinidos)
        else
        {
            Patrullar();
        }
    }

    void Perseguir()
    {
        viendoAFrodo = true; 
        investigando = true; // Activa su memoria para cuando pierda el rastro
        
        agent.speed = velocidadPersecucion; 
        
        if(objetivoFrodo != null)
        {
            agent.destination = objetivoFrodo.position; 
            ultimaPosicionConocida = objetivoFrodo.position; 

            if (Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
            {
                Debug.Log("Frodo capturado.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    void Investigar()
    {
        viendoAFrodo = false; 
        agent.speed = velocidadPersecucion; 
        agent.destination = ultimaPosicionConocida; 

        if (Vector3.Distance(transform.position, ultimaPosicionConocida) < 1.0f)
        {
            agent.isStopped = true; 
            temporizadorBusqueda += Time.deltaTime;

            if (temporizadorBusqueda > tiempoDeEsperaAlInvestigar)
            {
                // En lugar de rendirse, ahora se queda patrullando esta zona
                investigando = false; 
                patrullandoZonaLocal = true; 
                agent.isStopped = false;
                temporizadorBusqueda = 0f;
                Debug.Log("El orco perdió el rastro visual, pero se queda vigilando la zona.");
                AsignarPuntoLocal(); // Le damos su primer destino aleatorio en la zona
            }
        }
        else
        {
            if(agent.isStopped) agent.isStopped = false;
        }
    }

    void PatrullarLocalmente()
    {
        viendoAFrodo = false;
        agent.speed = estaAgresivo ? velocidadAlerta : velocidadPatrulla; 
        
        if(agent.isStopped) agent.isStopped = false;

        // Si llega a su destino aleatorio, busca otro nuevo
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            AsignarPuntoLocal();
        }
    }

    void AsignarPuntoLocal()
    {
        // Calcula un punto al azar en una esfera de X metros alrededor del punto caliente
        Vector3 direccionAleatoria = Random.insideUnitSphere * radioPatrullaLocal;
        direccionAleatoria += ultimaPosicionConocida;
        
        NavMeshHit hit;
        // Se asegura de que ese punto aleatorio caiga dentro del suelo transitable (NavMesh)
        if (NavMesh.SamplePosition(direccionAleatoria, out hit, radioPatrullaLocal, NavMesh.AllAreas))
        {
            agent.destination = hit.position;
        }
    }

    void Patrullar()
    {
        viendoAFrodo = false;
        agent.speed = estaAgresivo ? velocidadAlerta : velocidadPatrulla; 
        
        if(agent.isStopped) agent.isStopped = false;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            IrAlSiguientePunto();
        }
    }

    void IrAlSiguientePunto()
    {
        if (puntosPatrulla.Length == 0) return;
        agent.destination = puntosPatrulla[indiceDestino].position;
        indiceDestino = (indiceDestino + 1) % puntosPatrulla.Length;
    }
}