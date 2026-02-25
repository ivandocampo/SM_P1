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

    [Header("Configuración de Puntos Clave")]
    public Transform puntoSalida;   // Dónde está la puerta para bloquearla
    public Transform pedestalAnillo; // Dónde está el anillo para ir a revisarlo
    public float tiempoMinVigilancia = 15f; // Tiempo mínimo entre vistazos
    public float tiempoMaxVigilancia = 40f; // Tiempo máximo entre vistazos

    private NavMeshAgent agent;             
    private int indiceDestino = 0;          

    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;
    private SensorOlfatoOrco sensorOlfato;

    private bool investigando = false;      
    private bool estaAgresivo = false;      
    private bool patrullandoZonaLocal = false;
    private bool yendoAComprobarAnillo = false; // Estado de ir a hacer la ronda
    private float temporizadorBusqueda = 0f;
    private float temporizadorVigilancia; // El reloj interno de cada orco
    private Vector3 ultimaPosicionConocida; 

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();
        sensorOlfato = GetComponent<SensorOlfatoOrco>();
        
        // Cada orco tira los dados para ver cuándo le toca hacer la primera ronda
        temporizadorVigilancia = Random.Range(tiempoMinVigilancia, tiempoMaxVigilancia);
        
        IrAlSiguientePunto();
    }

    void Update()
    {
        // 0. CAPA ABSOLUTA: El tacto.
        if (objetivoFrodo != null && Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            Debug.Log("Frodo capturado por contacto físico.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return; 
        }

        // Reloj interno de vigilancia del anillo
        if (!estaAgresivo && !investigando && !yendoAComprobarAnillo)
        {
            temporizadorVigilancia -= Time.deltaTime;
            if (temporizadorVigilancia <= 0f)
            {
                yendoAComprobarAnillo = true;
                Debug.Log("Un orco ha decidido ir a hacer la ronda al pedestal.");
            }
        }

        // Descubrimiento pasivo o activo con la vista
        if (!estaAgresivo && sensorVista != null && sensorVista.NoAnillo())
        {
            estaAgresivo = true;
            yendoAComprobarAnillo = false; // Cancela la ronda, ya sabe que no está
            Debug.Log("¡Han robado el anillo! El orco abandona la ruta y va a bloquear la salida.");
        }

        Vector3 origenDelRuido = Vector3.zero;

        // 1. Sentido mortal (Visión directa) -> Perseguir a muerte
        if (sensorVista != null && sensorVista.VerFrodo())
        {
            patrullandoZonaLocal = false; 
            Perseguir();
        }
        // 2. Sentidos de rastreo (Oler u Oir pasos) -> Ir a investigar
        else if ((sensorOlfato != null && sensorOlfato.OlerFrodo()) || 
                 (sensorOido != null && sensorOido.OirFrodo(out origenDelRuido)))
        {
            patrullandoZonaLocal = false; 
            if (sensorOlfato != null && sensorOlfato.OlerFrodo()) 
            {
                 if (!investigando) ultimaPosicionConocida = objetivoFrodo.position;
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
        // 4. Modo Pánico: Sabe que no hay anillo, va a la salida
        else if (estaAgresivo)
        {
            IrALaSalida();
        }
        // 5. NUEVA CAPA: Ir a comprobar el pedestal del anillo
        else if (yendoAComprobarAnillo)
        {
            IrAComprobarAnillo();
        }
        // 6. Quedarse patrullando la zona caliente
        else if (patrullandoZonaLocal)
        {
            PatrullarLocalmente();
        }
        // 7. Rutina inicial (Patrulla)
        else
        {
            Patrullar();
        }
    }

    void Perseguir()
    {
        investigando = true;
        agent.speed = velocidadPersecucion;

        if(objetivoFrodo != null)
        {
            agent.destination = objetivoFrodo.position;
            ultimaPosicionConocida = objetivoFrodo.position;
        }
    }
    
    void Investigar()
    {
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
    
    void IrALaSalida()
    {
        agent.speed = velocidadAlerta;
        if(agent.isStopped) agent.isStopped = false;

        if (puntoSalida != null)
        {
            agent.destination = puntoSalida.position;
        }
    }
    
    void PatrullarLocalmente()
    {
        agent.speed = velocidadPatrulla;
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
        agent.speed = velocidadPatrulla;
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

    // NUEVO COMPORTAMIENTO: Ir a ver el pedestal
    void IrAComprobarAnillo()
    {
        agent.speed = velocidadPatrulla; // Va caminando normal, no sabe si ha pasado algo
        if(agent.isStopped) agent.isStopped = false;

        if (pedestalAnillo != null)
        {
            agent.destination = pedestalAnillo.position;

            // Si llega al pedestal
            if (!agent.pathPending && agent.remainingDistance < 2.0f)
            {
                // La vista comprueba si está en el Update, así que si llegamos aquí 
                // y no ha saltado la alarma, es que el anillo sigue ahí.
                yendoAComprobarAnillo = false;
                
                // Vuelve a tirar los dados para su próxima ronda
                temporizadorVigilancia = Random.Range(tiempoMinVigilancia, tiempoMaxVigilancia);
                Debug.Log("El orco comprobó el anillo. Todo en orden. Vuelve a su patrulla.");
                
                IrAlSiguientePunto(); // Retoma su ruta
            }
        }
    }
}