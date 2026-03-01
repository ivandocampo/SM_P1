using UnityEngine;

public class CerebroFrodo : MonoBehaviour
{
    // Define el estado del personaje para que sea detectable por los enemigos
    public bool estaCorriendo { get; private set; } = false;
    public bool usandoAnillo { get; private set; } = false;

    [Header("Magia del Anillo")]
    public float duracionAnillo = 5f;        
    public float tiempoRecarga = 30f;        
    private float temporizadorUso = 0f;
    private float temporizadorRecarga = 0f;

    private ActuadorMovimientoFrodo actuadorMovimiento;
    private ActuadorInteraccionFrodo actuadorInteraccion;
    private SensorTactoFrodo sensorTacto;

    private bool tieneElAnillo = false;

    // Proporciona datos de estado para la interfaz de usuario y sistemas externos
    public bool TieneElAnillo => tieneElAnillo;
    public bool AnilloListo => tieneElAnillo && !usandoAnillo && temporizadorRecarga <= 0;
    public float ProgresoUso => usandoAnillo ? temporizadorUso / duracionAnillo : 0f;
    public float ProgresoRecarga => temporizadorRecarga > 0 ? 1f - (temporizadorRecarga / tiempoRecarga) : 1f;

    // Establece las referencias a los actuadores y sensores al iniciar
    void Start()
    {
        actuadorMovimiento = GetComponent<ActuadorMovimientoFrodo>();
        actuadorInteraccion = GetComponent<ActuadorInteraccionFrodo>();
        sensorTacto = GetComponent<SensorTactoFrodo>();
    }

    // Coordina la actualización constante de la lógica de juego
    void Update()
    {
        // Interrumpe la lógica si el estado global de la partida no es activo
        if (!GameManager.Instance.PartidaActiva) return;

        ManejarAnillo();
        LeerInput();
        ComprobarSensores();
    }

    // Procesa las entradas del jugador para el movimiento relativo a la cámara
    void LeerInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Calcula la orientación basada en la posición actual de la cámara
        Transform cam = Camera.main.transform;
        Vector3 adelante = cam.forward;
        Vector3 derecha = cam.right;
        
        // Aplana los vectores para evitar desplazamientos en el eje vertical
        adelante.y = 0f;
        derecha.y = 0f;
        adelante.Normalize();
        derecha.Normalize();

        // Combina las direcciones para obtener el vector de movimiento final
        Vector3 direccion = (-adelante * v - derecha * h).normalized;

        // Determina si el personaje debe correr basándose en la velocidad y el input
        estaCorriendo = Input.GetKey(KeyCode.LeftShift) && direccion.magnitude > 0.1f;
        
        // Envía la dirección final al actuador de movimiento
        actuadorMovimiento.Mover(direccion, estaCorriendo);
    }

    // Administra los estados de invisibilidad y los tiempos de enfriamiento
    void ManejarAnillo()
    {
        // Reduce el tiempo de espera para volver a usar el anillo
        if (!usandoAnillo && temporizadorRecarga > 0)
        {
            temporizadorRecarga -= Time.deltaTime;
        }

        // Activa el efecto de invisibilidad si se cumplen los requisitos y se pulsa el comando
        if (tieneElAnillo && Input.GetKeyDown(KeyCode.Space) && temporizadorRecarga <= 0 && !usandoAnillo)
        {
            usandoAnillo = true;
            temporizadorUso = duracionAnillo;
            
            // Notifica al actuador visual que debe aplicar la transparencia
            actuadorInteraccion.CambiarTransparencia(true);
            Debug.Log("¡Anillo activado! Eres invisible.");
        }

        // Gestiona el agotamiento del efecto una vez transcurrido el tiempo de uso
        if (usandoAnillo)
        {
            temporizadorUso -= Time.deltaTime;
            if (temporizadorUso <= 0)
            {
                usandoAnillo = false;
                temporizadorRecarga = tiempoRecarga;
                
                // Ordena al actuador restaurar la apariencia normal del personaje
                actuadorInteraccion.CambiarTransparencia(false);
                Debug.Log("El efecto del anillo ha terminado. Recargando...");
            }
        }
    }

    // Evalúa la información de los sensores táctiles para interactuar con el entorno
    void ComprobarSensores()
    {
        // Comprueba si el personaje ha recolectado el anillo
        if (!tieneElAnillo && sensorTacto.TocarAnillo())
        {
            tieneElAnillo = true;
            actuadorInteraccion.CogerAnillo();
            Debug.Log("¡Anillo recogido!");
        }

        // Verifica si el personaje ha alcanzado la zona de salida con el objetivo cumplido
        if (tieneElAnillo && sensorTacto.TocarSalida())
        {
            GameManager.Instance.FrodoEscapa();
        }
    }
}