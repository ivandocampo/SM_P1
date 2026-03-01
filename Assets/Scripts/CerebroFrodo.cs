using UnityEngine;

public class CerebroFrodo : MonoBehaviour
{
    // Estado perceptible por los sensores de los Orcos
    public bool estaCorriendo { get; private set; } = false;
    public bool usandoAnillo { get; private set; } = false;

    [Header("Magia del Anillo")]
    public float duracionAnillo = 5f;        // Segundos que dura la invisibilidad
    public float tiempoRecarga = 30f;        // Segundos que tarda en volver a usarse
    private float temporizadorUso = 0f;
    private float temporizadorRecarga = 0f;

    private ActuadorMovimientoFrodo actuadorMovimiento;
    private ActuadorInteraccionFrodo actuadorInteraccion;
    private SensorTactoFrodo sensorTacto;

    private bool tieneElAnillo = false;

    // Propiedades de solo lectura para la UI del anillo
    public bool TieneElAnillo => tieneElAnillo;
    public bool AnilloListo => tieneElAnillo && !usandoAnillo && temporizadorRecarga <= 0;
    public float ProgresoUso => usandoAnillo ? temporizadorUso / duracionAnillo : 0f;          // 1→0 mientras está activo
    public float ProgresoRecarga => temporizadorRecarga > 0 ? 1f - (temporizadorRecarga / tiempoRecarga) : 1f; // 0→1 mientras recarga

    void Start()
    {
        actuadorMovimiento = GetComponent<ActuadorMovimientoFrodo>();
        actuadorInteraccion = GetComponent<ActuadorInteraccionFrodo>();
        sensorTacto = GetComponent<SensorTactoFrodo>();
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        ManejarAnillo();
        LeerInput();
        ComprobarSensores();
    }

    void LeerInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Movimiento relativo a la cámara: "adelante" siempre es donde mira la cámara
        Transform cam = Camera.main.transform;
        Vector3 adelante = cam.forward;
        Vector3 derecha = cam.right;
        adelante.y = 0f;
        derecha.y = 0f;
        adelante.Normalize();
        derecha.Normalize();

        Vector3 direccion = (-adelante * v - derecha * h).normalized;

        estaCorriendo = Input.GetKey(KeyCode.LeftShift) && direccion.magnitude > 0.1f;
        actuadorMovimiento.Mover(direccion, estaCorriendo);
    }

    // Controla los tiempos y ordena al actuador cambiar la visibilidad
    void ManejarAnillo()
    {
        // Restar tiempo de recarga si está en cooldown
        if (!usandoAnillo && temporizadorRecarga > 0)
        {
            temporizadorRecarga -= Time.deltaTime;
        }

        // Activar el anillo (Pulsando Espacio)
        if (tieneElAnillo && Input.GetKeyDown(KeyCode.Space) && temporizadorRecarga <= 0 && !usandoAnillo)
        {
            usandoAnillo = true;
            temporizadorUso = duracionAnillo;
            actuadorInteraccion.CambiarTransparencia(true); // Ordena al actuador
            Debug.Log("¡Anillo activado! Eres invisible.");
        }

        // Desactivar el anillo cuando se acaba el tiempo
        if (usandoAnillo)
        {
            temporizadorUso -= Time.deltaTime;
            if (temporizadorUso <= 0)
            {
                usandoAnillo = false;
                temporizadorRecarga = tiempoRecarga; // Comienza el cooldown
                actuadorInteraccion.CambiarTransparencia(false); // Ordena al actuador
                Debug.Log("El efecto del anillo ha terminado. Recargando...");
            }
        }
    }

    void ComprobarSensores()
    {
        if (!tieneElAnillo && sensorTacto.TocarAnillo())
        {
            tieneElAnillo = true;
            actuadorInteraccion.CogerAnillo();
            Debug.Log("¡Anillo recogido!");
        }

        if (tieneElAnillo && sensorTacto.TocarSalida())
        {
            GameManager.Instance.FrodoEscapa();
        }
    }
}