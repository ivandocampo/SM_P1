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

    // private bool investigando = false;      
    // private bool estaAgresivo = false;      
    // private bool patrullandoZonaLocal = false;
    // private bool yendoAComprobarAnillo = false; // Estado de ir a hacer la ronda

    // Estados posibles del orco
    public enum EstadoOrco 
    { 
        PATRULLA, 
        PERSECUCION, 
        INVESTIGACION, 
        COMPROBAR_ANILLO, 
        BLOQUEAR_SALIDA 
    }

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA; // Estado inicial



    private float temporizadorBusqueda = 0f;
    private float temporizadorVigilancia; // El reloj interno de cada orco
    private Vector3 ultimaPosicionConocida; 

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();
        sensorOlfato = GetComponent<SensorOlfatoOrco>();

        // Estado inicial: Patrullando
        estadoActual = EstadoOrco.PATRULLA;
        
        // Arrancar el movimiento hacia el primer punto
        IrAlSiguientePunto();
    }

    void Update()
    {
        // Mantenemos lógica original: si está muy cerca, te atrapa.
        if (objetivoFrodo != null && Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            Debug.Log("Frodo capturado.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return; 
        }

        
        // Si ve a Frodo -> Persecucion
        if (sensorVista != null && sensorVista.VerFrodo())
        {
            estadoActual = EstadoOrco.PERSECUCION;
            ultimaPosicionConocida = objetivoFrodo.position; 
        }
        // Si ve que NO hay anillo -> Agresivo
        else if (sensorVista != null && sensorVista.NoAnillo() && estadoActual != EstadoOrco.BLOQUEAR_SALIDA)
        {
            estadoActual = EstadoOrco.BLOQUEAR_SALIDA;
        }
        // Si huele u oye a Frodo y NO lo está viendo ni persiguiendo ya -> INVESTIGAR
        else if (estadoActual != EstadoOrco.PERSECUCION && estadoActual != EstadoOrco.BLOQUEAR_SALIDA)
        {
            Vector3 origenRuido;
            if (sensorOlfato != null && sensorOlfato.OlerFrodo())
            {
                estadoActual = EstadoOrco.INVESTIGACION;
                ultimaPosicionConocida = objetivoFrodo.position;
            }
            else if (sensorOido != null && sensorOido.OirFrodo(out origenRuido))
            {
                estadoActual = EstadoOrco.INVESTIGACION;
                ultimaPosicionConocida = origenRuido;
            }
        }

        // Ejecuta la lógica correspondiente al estado actual
        switch (estadoActual)
        {
            case EstadoOrco.PATRULLA:
                Patrullar();
                break;
            case EstadoOrco.PERSECUCION:
                Perseguir();
                break;
            case EstadoOrco.INVESTIGACION:
                Investigar();
                break;
            case EstadoOrco.COMPROBAR_ANILLO:
                IrAComprobarAnillo();
                break;
            case EstadoOrco.BLOQUEAR_SALIDA:
                IrALaSalida();
                break;
        }
    }

    void Perseguir()
    {
        agent.speed = velocidadPersecucion;

        // Si lo sigo viendo, actualizo el destino
        if (sensorVista.VerFrodo())
        {   
            agent.destination = objetivoFrodo.position;
            ultimaPosicionConocida = objetivoFrodo.position;
        }
        else
        {
            // Si lo pierdo de vista, paso a INVESTIGAR donde lo vi por última vez
            Debug.Log("Lo he perdido. Voy a investigar.");
            estadoActual = EstadoOrco.INVESTIGACION;
        }
    }

    void Investigar()
    {
        agent.speed = velocidadAlerta;
        agent.destination = ultimaPosicionConocida;

        // Si el agente ha llegado al punto de investigación
        if (!agent.pathPending && agent.remainingDistance < 1.0f)
        {
            // Esperamos un rato mirando alrededor
            temporizadorBusqueda += Time.deltaTime;

            if (temporizadorBusqueda > tiempoDeEsperaAlInvestigar)
            {
                // "No está aquí. Voy a ver si el anillo sigue en su sitio."
                Debug.Log("Aquí no hay nadie. Voy a comprobar el anillo.");
                
                estadoActual = EstadoOrco.COMPROBAR_ANILLO; // Cambio de estado
                temporizadorBusqueda = 0f;
            }
        }
    }
    
    void IrAComprobarAnillo()
    {
        agent.speed = velocidadPatrulla; // Va tranquilo, no sabe nada aún

        if (pedestalAnillo != null)
        {
            agent.destination = pedestalAnillo.position;

            // Si llega al pedestal
            if (!agent.pathPending && agent.remainingDistance < 2.0f)
            {
                // Usamos el sensor para ver si el anillo está físicamente
                if (sensorVista != null && sensorVista.NoAnillo())
                {
                    Debug.Log("¡HAN ROBADO EL ANILLO! ¡BLOQUEAD LA SALIDA!");
                    estadoActual = EstadoOrco.BLOQUEAR_SALIDA;
                }
                else
                {
                    Debug.Log("El anillo sigue aquí. Falsa alarma. Vuelvo a la patrulla.");
                    estadoActual = EstadoOrco.PATRULLA;
                    IrAlSiguientePunto(); // Retoma la ruta para no quedarse quieto
                }
            }
        }
    }

    void IrALaSalida()
    {
        agent.speed = velocidadAlerta; // Va corriendo
        if (puntoSalida != null)
        {
            agent.destination = puntoSalida.position;
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

}