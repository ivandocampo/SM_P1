using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement; 

public class CerebroOrco : MonoBehaviour
{
    [Header("Configuración de Patrulla")]
    public Transform[] puntosPatrulla;      
    public float velocidadPatrulla = 3.5f;  
    public float velocidadAlerta = 6.0f;    
    public float tiempoDeEsperaAlInvestigar = 3.0f; 
    public float radioPatrullaLocal = 10f;  

    [Header("Configuración de Ataque")]
    public Transform objetivoFrodo;         
    public float velocidadPersecucion = 8f; 
    public float distanciaAtaque = 1.2f;    

    private NavMeshAgent agent;             
    private int indiceDestino = 0;          

    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;
    private SensorOlfatoOrco sensorOlfato;

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
        // 0. CAPA ABSOLUTA: El tacto. Si choco con él físicamente, lo atrapo siempre.
        if (objetivoFrodo != null && Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            Debug.Log("Frodo capturado por contacto físico.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return; // Cortamos el código aquí, ya te han matado
        }

        if (!estaAgresivo && sensorVista != null && sensorVista.NoAnillo())
        {
            estaAgresivo = true;
            Debug.Log("El orco ha visto que falta el anillo. Aumenta su agresividad.");
        }

        Vector3 origenDelRuido = Vector3.zero;

        // 1. Sentido mortal (Visión directa) -> Perseguir a muerte
        if (sensorVista != null && sensorVista.VerFrodo())
        {
            patrullandoZonaLocal = false; 
            Perseguir();
        }
        // 2. Sentidos de rastreo (Oler el anillo u Oir pasos) -> Ir a investigar
        else if ((sensorOlfato != null && sensorOlfato.OlerFrodo()) || 
                 (sensorOido != null && sensorOido.OirFrodo(out origenDelRuido)))
        {
            patrullandoZonaLocal = false; 
            
            if (sensorOlfato != null && sensorOlfato.OlerFrodo()) 
            {
                 // ARREGLO DEL IMÁN: Solo guarda tu posición si acaba de empezar a olerte.
                 // Si ya estaba yendo hacia un olor, no actualiza hasta que llegue allí.
                 if (!investigando) 
                 {
                     ultimaPosicionConocida = objetivoFrodo.position;
                 }
            } 
            else 
            {
                 ultimaPosicionConocida = origenDelRuido;
            }

            investigando = true;
            Investigar();
        }
        // 3. Ir al último punto conocido a mirar
        else if (investigando)
        {
            Investigar();
        }
        // 4. Quedarse patrullando la zona caliente
        else if (patrullandoZonaLocal)
        {
            PatrullarLocalmente();
        }
        // 5. Rutina inicial (Patrulla)
        else
        {
            Patrullar();
        }
    }

    void Perseguir()
    {
        viendoAFrodo = true; 
        investigando = true; 
        agent.speed = velocidadPersecucion; 
        
        if(objetivoFrodo != null)
        {
            agent.destination = objetivoFrodo.position; 
            ultimaPosicionConocida = objetivoFrodo.position; 
            // La muerte ya no está aquí, está arriba del todo en el Update
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
                investigando = false; 
                patrullandoZonaLocal = true; 
                agent.isStopped = false;
                temporizadorBusqueda = 0f;
                Debug.Log("El orco perdió el rastro, pero se queda vigilando la zona.");
                AsignarPuntoLocal(); 
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

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            AsignarPuntoLocal();
        }
    }

    void AsignarPuntoLocal()
    {
        Vector3 direccionAleatoria = Random.insideUnitSphere * radioPatrullaLocal;
        direccionAleatoria += ultimaPosicionConocida;
        NavMeshHit hit;
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