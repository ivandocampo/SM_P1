// =============================================================
// Cerebro de Frodo: lógica del personaje controlado por el jugador.
// Coordina input, movimiento, uso del Anillo y comprobación de sensores.
// Frodo no es un agente del sistema multiagente; su objetivo es recoger
// el Anillo del pedestal y llevarlo a la salida sin ser capturado por los orcos.
// Cuando activa el Anillo con Espacio se vuelve invisible a los sensores
// de visión durante 5 s, con una recarga de 30 s
// =============================================================

using UnityEngine;

public class CerebroFrodo : MonoBehaviour
{
    // Estado del personaje, consultado por SensorOido y SensorVision
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

    // Propiedades de estado para la UI y sistemas externos
    public bool TieneElAnillo => tieneElAnillo;
    public bool AnilloListo => tieneElAnillo && !usandoAnillo && temporizadorRecarga <= 0;
    public float ProgresoUso => usandoAnillo ? temporizadorUso / duracionAnillo : 0f;
    public float ProgresoRecarga => temporizadorRecarga > 0 ? 1f - (temporizadorRecarga / tiempoRecarga) : 1f;

    // Obtener referencias a actuadores y sensor táctil
    void Start()
    {
        actuadorMovimiento = GetComponent<ActuadorMovimientoFrodo>();
        actuadorInteraccion = GetComponent<ActuadorInteraccionFrodo>();
        sensorTacto = GetComponent<SensorTactoFrodo>();
    }

    // Ejecutar cada frame: gestionar anillo, leer input y comprobar sensores
    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        ManejarAnillo();
        LeerInput();
        ComprobarSensores();
    }

    // Leer el input del teclado y mover a Frodo relativo a la orientación de la cámara
    void LeerInput()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Calcular adelante y derecha a partir de la cámara actual
        Transform cam = Camera.main.transform;
        Vector3 adelante = cam.forward;
        Vector3 derecha = cam.right;

        // Aplanar los vectores para ignorar la inclinación vertical de la cámara
        adelante.y = 0f;
        derecha.y = 0f;
        adelante.Normalize();
        derecha.Normalize();

        Vector3 direccion = (-adelante * v - derecha * h).normalized;

        estaCorriendo = Input.GetKey(KeyCode.LeftShift) && direccion.magnitude > 0.1f;
        actuadorMovimiento.Mover(direccion, estaCorriendo);
    }

    // Gestionar la activación, duración y recarga del poder de invisibilidad
    void ManejarAnillo()
    {
        // Reducir el cooldown mientras el anillo no esté en uso
        if (!usandoAnillo && temporizadorRecarga > 0)
        {
            temporizadorRecarga -= Time.deltaTime;
        }

        // Activar invisibilidad al pulsar Espacio si el anillo está disponible
        if (tieneElAnillo && Input.GetKeyDown(KeyCode.Space) && temporizadorRecarga <= 0 && !usandoAnillo)
        {
            usandoAnillo = true;
            temporizadorUso = duracionAnillo;
            actuadorInteraccion.CambiarTransparencia(true);
            Debug.Log("¡Anillo activado! Eres invisible.");
        }

        // Desactivar la invisibilidad al agotarse el tiempo de uso
        if (usandoAnillo)
        {
            temporizadorUso -= Time.deltaTime;
            if (temporizadorUso <= 0)
            {
                usandoAnillo = false;
                temporizadorRecarga = tiempoRecarga;
                actuadorInteraccion.CambiarTransparencia(false);
                Debug.Log("El efecto del anillo ha terminado. Recargando...");
            }
        }
    }

    // Comprobar sensores táctiles: recoger el Anillo o alcanzar la salida
    void ComprobarSensores()
    {
        // Recoger el Anillo del pedestal si Frodo está suficientemente cerca
        if (!tieneElAnillo && sensorTacto.TocarAnillo())
        {
            tieneElAnillo = true;
            actuadorInteraccion.CogerAnillo();
            Debug.Log("¡Anillo recogido!");
        }

        // Notificar la victoria al GameManager cuando Frodo llega a la salida con el Anillo
        if (tieneElAnillo && sensorTacto.TocarSalida())
        {
            GameManager.Instance.FrodoEscapa();
        }
    }
}
