using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class CerebroFrodo : MonoBehaviour
{
    [Header("UI")]
    public GameObject pantallaVictoria;

    [Header("Configuración de Movimiento")]
    public float velocidadCaminar = 5f; // Velocidad estándar sigilosa
    public float velocidadCorrer = 10f; // Velocidad rápida (genera ruido)

    [Header("Estado (Información para los Orcos)")]
    public bool estaCorriendo = false; // Indica a los enemigos si Frodo está haciendo ruido
    public bool usandoAnillo = false;  // Indica a los enemigos si Frodo emite olor

    private NavMeshAgent agent;         // Componente que gestiona el movimiento físico
    private SensorTactoFrodo sensorTacto; // Sentido para interactuar con objetos
    private bool tieneElAnillo = false; // Memoria interna: Frodo ya tiene el objetivo?
    private bool haEscapado = false;    // Estado final: Frodo ha ganado?

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        sensorTacto = GetComponent<SensorTactoFrodo>();
        agent.updateRotation = true; 
    }

    void Update()
    {
        Debug.Log("CerebroFrodo Update ejecutándose");
        
        if (haEscapado)
        {
            if (Input.GetKeyDown(KeyCode.Return))
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        MoverJugador();       
        GestionarAnillo();    
        ComprobarObjetivos(); 
    }

    void MoverJugador()
    {
        float inputHorizontal = Input.GetAxis("Horizontal");
        float inputVertical = Input.GetAxis("Vertical");

        Vector3 movimiento = new Vector3(-inputHorizontal, 0f, -inputVertical).normalized;

        if (Input.GetKey(KeyCode.LeftShift) && movimiento.magnitude > 0.1f)
            estaCorriendo = true;
        else
            estaCorriendo = false;

        float velocidadActual = estaCorriendo ? velocidadCorrer : velocidadCaminar;

        if (movimiento.magnitude >= 0.1f)
        {
            agent.Move(movimiento * velocidadActual * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(movimiento);
        }
    }

    void GestionarAnillo()
    {
        if (tieneElAnillo && Input.GetKeyDown(KeyCode.Space))
        {
            // Alternar el estado del anillo
            usandoAnillo = !usandoAnillo; 

            if (usandoAnillo)
            {
                Debug.Log("Anillo activado. Frodo es invisible a la vista, pero emite olor.");
            }
            else
            {
                Debug.Log("Anillo desactivado.");
            }
        }
    }

    void ComprobarObjetivos()
    {
        // El cerebro pregunta al tacto si está tocando el anillo
        if (!tieneElAnillo && sensorTacto != null && sensorTacto.TocarAnillo())
        {
            RecogerAnillo();
        }

        // El cerebro pregunta al tacto si está pisando la salida
        if (tieneElAnillo && sensorTacto != null && sensorTacto.TocarSalida())
        {
            Victoria();
        }
    }

    void RecogerAnillo()
    {
        tieneElAnillo = true;           
        Debug.Log("Anillo recogido. (Pulsa ESPACIO para usarlo)"); 
        
        // El cerebro ordena usar el tacto para agarrar el objeto
        if (sensorTacto != null) sensorTacto.CogerAnillo(); 
    }

    void Victoria()
    {
        haEscapado = true;               
        if (pantallaVictoria != null)
        {
            pantallaVictoria.SetActive(true);
        }
    }
}