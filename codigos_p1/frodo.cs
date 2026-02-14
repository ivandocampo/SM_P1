using UnityEngine;
using UnityEngine.AI;

public class CerebroFrodo : MonoBehaviour
{

    [Header("Objetivos de la Misión")]
    public Transform elAnillo;   // Referencia al objeto que Frodo debe robar
    public Transform laSalida;   // Referencia al punto de escape
    
    [Header("Configuración de Movimiento")]
    public float velocidadCaminar = 5f; // Velocidad estándar sigilosa
    public float velocidadCorrer = 10f; // Velocidad rápida (genera ruido)

    [Header("Estado (Información para los Orcos)")]
    public bool estaCorriendo = false; // Indica a los enemigos si Frodo está haciendo ruido
    public bool usandoAnillo = false;  // Indica a los enemigos si Frodo es invisible pero detectable

    private NavMeshAgent agent;         // Componente que gestiona el movimiento físico
    private bool tieneElAnillo = false; // Memoria interna: Frodo ya tiene el objetivo?
    private bool haEscapado = false;    // Estado final: Frodo ha ganado?

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = true; 
    }

    void Update()
    {
        // Si Frodo ya ha escapado, el cerebro deja de procesar decisiones
        if (haEscapado) return;

        // Frodo procesa el teclado y se mueve
        MoverJugador();       
        
        // Frodo decide si usa el Anillo Único
        GestionarAnillo();    
        
        // Frodo comprueba su entorno (distancia a objetivos)
        ComprobarObjetivos(); 
    }

    void MoverJugador()
    {
        float inputHorizontal = Input.GetAxis("Horizontal"); 
        float inputVertical = Input.GetAxis("Vertical");     

        Vector3 movimiento = new Vector3(inputHorizontal, 0f, inputVertical).normalized;

        if (Input.GetKey(KeyCode.LeftShift) && movimiento.magnitude > 0.1f)
        {
            estaCorriendo = true; // Frodo corre
        }
        else
        {
            estaCorriendo = false; // Frodo camina sigilosamente
        }

        // Seleccionar la velocidad adecuada según su estado
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
            // Alternar el estado del anillo (Invisible vs Visible)
            usandoAnillo = !usandoAnillo; 

            if (usandoAnillo)
            {
                Debug.Log("Anillo activado.");
                // Aquí se podría añadir lógica visual (hacer transparente a Frodo)
            }
            else
            {
                Debug.Log("Anillo desactivado.");
            }
        }
    }

    void ComprobarObjetivos()
    {
        // Distancia al Anillo
        if (!tieneElAnillo && elAnillo != null && Vector3.Distance(transform.position, elAnillo.position) < 1.5f)
        {
            RecogerAnillo();
        }

        // Distancia a la Salida (si ya tiene el Anillo)
        if (tieneElAnillo && Vector3.Distance(transform.position, laSalida.position) < 2.0f)
        {
            Victoria();
        }
    }

    void RecogerAnillo()
    {
        tieneElAnillo = true;           
        Debug.Log("Anillo recogido. (Pulsa ESPACIO para usarlo)"); 
        
        // Desaparecer el anillo del mundo físico
        if(elAnillo != null) elAnillo.gameObject.SetActive(false); 
    }

    void Victoria()
    {
        haEscapado = true;               
        Debug.Log("Frodo ha escapado con el Anillo."); 
    }
}