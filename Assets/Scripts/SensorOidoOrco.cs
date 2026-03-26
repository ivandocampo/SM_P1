using UnityEngine;
using System;

/// <summary>
/// Sensor de audición event-driven para agentes (guardias y arañas).
/// Detecta sonidos producidos por el ladrón (caminar y correr)
/// y notifica al cerebro del agente mediante eventos C#.
/// 
/// Eventos disponibles:
///   OnSonidoDetectado(Vector3 posición) — Se ha oído al ladrón
/// </summary>
public class SensorOido : MonoBehaviour
{
    [Header("Configuración de Audición")]
    public Transform objetivo;                  // Referencia al transform de Frodo
    public float rangoOidoCaminar = 5f;
    public float rangoOidoCorrer = 15f;

    [Header("Frecuencia de Comprobación")]
    [Tooltip("Intervalo mínimo entre notificaciones de sonido (evita spam de eventos)")]
    public float intervaloNotificacion = 0.5f;

    // === EVENTOS ===
    public event Action<Vector3> OnSonidoDetectado;

    // === ESTADO ===
    /// <summary>Indica si se está detectando sonido en este momento.</summary>
    public bool OyendoAlgo { get; private set; } = false;

    private CerebroFrodo cerebroFrodo;
    private ActuadorMovimientoFrodo movimientoFrodo;
    private float ultimaNotificacion = 0f;

    void Start()
    {
        if (objetivo != null)
        {
            cerebroFrodo = objetivo.GetComponent<CerebroFrodo>();
            movimientoFrodo = objetivo.GetComponent<ActuadorMovimientoFrodo>();
        }
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;
        if (objetivo == null || cerebroFrodo == null || movimientoFrodo == null) return;

        bool oido = ComprobarAudicion();
        OyendoAlgo = oido;

        // Notificar con throttle para evitar eventos excesivos
        if (oido && Time.time - ultimaNotificacion >= intervaloNotificacion)
        {
            ultimaNotificacion = Time.time;
            OnSonidoDetectado?.Invoke(objetivo.position);
        }
    }

    
    private bool ComprobarAudicion()
    {
        float distancia = Vector3.Distance(transform.position, objetivo.position);
        float velocidad = movimientoFrodo.VelocidadActual();

        // Correr: audible a mayor distancia
        if (cerebroFrodo.estaCorriendo && distancia < rangoOidoCorrer)
            return true;

        // Caminar: audible solo a corta distancia
        if (!cerebroFrodo.estaCorriendo && velocidad > 0.5f && distancia < rangoOidoCaminar)
            return true;

        return false;
    }

  
    void OnDrawGizmos()
    {
        // Rango de detección de carrera (amarillo)
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, rangoOidoCorrer);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangoOidoCorrer);

        // Rango de detección de caminata (naranja)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, rangoOidoCaminar);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, rangoOidoCaminar);
    }
}