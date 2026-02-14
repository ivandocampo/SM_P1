using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement; 

public class CerebroOrco : MonoBehaviour
{

    [Header("Configuración de Patrulla")]
    public Transform[] puntosPatrulla;      // Lista de puntos que el orco debe vigilar
    public float velocidadPatrulla = 3.5f;  // Velocidad calmada
    public float tiempoDeEsperaAlInvestigar = 3.0f; // Tiempo que el orco busca antes de rendirse

    [Header("Sensores y Percepción")]
    public Transform objetivoFrodo;         // Referencia a la presa (Frodo)
    public float rangoVisionCaminando = 6f; // Rango corto (Frodo sigiloso)
    public float rangoVisionCorriendo = 15f;// Rango largo (Frodo ruidoso)
    public float rangoPercepcionAnillo = 50f; // Rango enorme (Presencia mágica)
    public float velocidadPersecucion = 6f; // Velocidad rápida (Carrera)
    public float distanciaAtaque = 1.2f;    // Distancia para capturar
    public LayerMask capasVision;           // Qué objetos bloquean la vista (paredes)

    private NavMeshAgent agent;             // Motor de movimiento
    private int indiceDestino = 0;          // Índice del siguiente punto de ruta
    private CerebroFrodo scriptFrodo;       // Enlace al cerebro de Frodo para leer su estado

    // Estados de la IA
    private bool viendoAFrodo = false;      // Estado: Tiene contacto visual directo?
    private bool investigando = false;      // Estado: Está buscando en la última posición?
    private Vector3 ultimaPosicionConocida; // Memoria: Dónde vio a Frodo por última vez
    private float temporizadorBusqueda = 0f;// Reloj interno para medir el tiempo de búsqueda

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = velocidadPatrulla;
        
        // El orco intenta conectar con la mente de Frodo para detectar sus estados (ruido/anillo)
        if(objetivoFrodo != null)
            scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();

        // El orco comienza su ruta de patrulla
        IrAlSiguientePunto();
    }

    void Update()
    {
        // Si no hay objetivo asignado, el orco no hace nada
        if (objetivoFrodo == null) return;

        // Distancia física hasta el objetivo
        float distanciaAFrodo = Vector3.Distance(transform.position, objetivoFrodo.position);
        
        // Qué tan agudos son sus sentidos dependiendo de lo que haga Frodo
        float rangoActual = rangoVisionCaminando;

        if (scriptFrodo != null && scriptFrodo.estaCorriendo)
            rangoActual = rangoVisionCorriendo; // El orco oye ruido y amplia su atención

        if (scriptFrodo != null && scriptFrodo.usandoAnillo)
            rangoActual = rangoPercepcionAnillo; // El orco siente la presencia del anillo
        
        // El orco detecta a Frodo -> Perseguir
        if (distanciaAFrodo < rangoActual && ComprobarDeteccion(rangoActual))
        {
            Perseguir();
        }
        // El orco no lo ve, pero recuerda dónde estaba -> Investigar
        else if (investigando)
        {
            Investigar();
        }
        // El orco no tiene rastro de Frodo -> Patrullar
        else
        {
            Patrullar();
        }
    }

    bool ComprobarDeteccion(float rangoDinamico)
    {
        // Comprobar si el orco siente la magia del Anillo (ignora paredes)
        if (scriptFrodo != null && scriptFrodo.usandoAnillo) return true; 

        // Ver si hay paredes en medio
        Vector3 direccion = objetivoFrodo.position - transform.position;
        RaycastHit hit;
        
        if (Physics.Raycast(transform.position, direccion, out hit, rangoDinamico, capasVision))
        {
            if (hit.transform == objetivoFrodo) return true;
        }
        return false;
    }

    void Perseguir()
    {
        viendoAFrodo = true; 
        investigando = true; // El orco activa su memoria para cuando pierda de vista al objetivo
        
        // El orco corre hacia la posición actual de Frodo
        agent.speed = velocidadPersecucion; 
        agent.destination = objetivoFrodo.position; 
        
        // El orco actualiza constantemente dónde ha visto a Frodo
        ultimaPosicionConocida = objetivoFrodo.position;

        // Si está muy cerca, atrapa al jugador
        if (Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            Debug.Log("Frodo capturado!");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    void Investigar()
    {
        viendoAFrodo = false; // Ya no tiene contacto visual directo
        agent.speed = velocidadPersecucion; // Mantiene la prisa hasta llegar al punto
        agent.destination = ultimaPosicionConocida; // El orco va a la última posición registrada en su memoria

        // Si el orco llega al lugar donde creía que estaba Frodo
        if (Vector3.Distance(transform.position, ultimaPosicionConocida) < 1.0f)
        {
            agent.isStopped = true; // El orco se detiene para mirar alrededor
            temporizadorBusqueda += Time.deltaTime;

            // El orco espera unos segundos buscando pistas
            if (temporizadorBusqueda > tiempoDeEsperaAlInvestigar)
            {
                // Si pasa el tiempo y no encuentra nada, el orco se rinde
                investigando = false; 
                agent.isStopped = false;
                temporizadorBusqueda = 0f;
                Debug.Log("El orco ha perdido el rastro. Volviendo a patrulla.");
            }
        }
    }

    void Patrullar()
    {
        viendoAFrodo = false;
        agent.speed = velocidadPatrulla; // El orco vuelve a velocidad de paseo
        
        // Si el orco estaba parado por haber investigado, se reactiva
        if(agent.isStopped) agent.isStopped = false;

        // Si el orco llega al punto de ruta actual, selecciona el siguiente
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            IrAlSiguientePunto();
        }
    }

    void IrAlSiguientePunto()
    {
        // Si la lista está vacía, no hace nada
        if (puntosPatrulla.Length == 0) return;
        
        // Establecer su destino al siguiente punto
        agent.destination = puntosPatrulla[indiceDestino].position;
        // Matemáticas para recorrer la lista en círculo
        indiceDestino = (indiceDestino + 1) % puntosPatrulla.Length;
    }
}