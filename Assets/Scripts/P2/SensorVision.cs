using UnityEngine;
using System;


public class SensorVision : MonoBehaviour
{
    [Header("Configuración de Visión")]
    public Transform objetivo;                  // Referencia al transform de Frodo
    public float rangoVision = 15f;
    public float anguloVision = 60f;            // Ángulo desde el frente (total = 2x)
    public LayerMask capasObstaculos;

    [Header("Altura de los Ojos")]
    public float alturaOjos = 1.5f;
    public float alturaObjetivo = 1.0f;

    [Header("Pedestal del Anillo")]
    public Transform objetoAnillo;              // Referencia al GameObject del anillo

    // === EVENTOS ===
    public event Action<Vector3> OnObjetivoDetectado;   // Transición: no visible → visible
    public event Action OnObjetivoPerdido;               // Transición: visible → no visible
    public event Action<Vector3> OnObjetivoVisible;      // Mientras sigue visible
    public event Action OnAnilloDesaparecido;            // Primera detección de anillo ausente

    // === ESTADO ===
    /// <summary>Indica si el objetivo es visible en este momento.</summary>
    public bool ObjetivoEsVisible { get; private set; } = false;

    private bool eraVisible = false;
    private bool anilloDesaparecidoNotificado = false;
    private Vector3 posicionOriginalAnillo;
    private CerebroFrodo cerebroFrodo;

    void Start()
    {
        if (objetivo != null)
            cerebroFrodo = objetivo.GetComponent<CerebroFrodo>();

        if (objetoAnillo != null)
            posicionOriginalAnillo = objetoAnillo.position;
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        bool esVisible = ComprobarVisibilidad();
        ObjetivoEsVisible = esVisible;

        // Transición: no visible → visible (primera detección)
        if (esVisible && !eraVisible)
        {
            OnObjetivoDetectado?.Invoke(objetivo.position);
        }
        // Transición: visible → no visible (se perdió de vista)
        else if (!esVisible && eraVisible)
        {
            OnObjetivoPerdido?.Invoke();
        }
        // Se mantiene visible (actualización continua de posición)
        else if (esVisible)
        {
            OnObjetivoVisible?.Invoke(objetivo.position);
        }

        eraVisible = esVisible;

        // Comprobar estado del anillo
        ComprobarAnillo();
    }

    
    private bool ComprobarVisibilidad()
    {
        if (objetivo == null) return false;

        // El ladrón es invisible si está usando el anillo
        if (cerebroFrodo != null && cerebroFrodo.usandoAnillo)
            return false;

        Vector3 posOjos = transform.position + Vector3.up * alturaOjos;
        Vector3 posObjetivo = objetivo.position + Vector3.up * alturaObjetivo;

        // Comprobar distancia
        float distancia = Vector3.Distance(posOjos, posObjetivo);
        if (distancia > rangoVision) return false;

        // Comprobar ángulo del campo de visión
        Vector3 direccion = (posObjetivo - posOjos).normalized;
        float angulo = Vector3.Angle(transform.forward, direccion);
        if (angulo > anguloVision) return false;

        // Comprobar línea de visión (obstáculos)
        if (Physics.Raycast(posOjos, direccion, distancia, capasObstaculos))
            return false;

        return true;
    }

    
    private void ComprobarAnillo()
    {
        if (anilloDesaparecidoNotificado) return;
        if (objetoAnillo == null) return;
        if (objetoAnillo.gameObject.activeSelf) return;

        // El anillo ha sido recogido — comprobar si podemos ver el pedestal vacío
        float distAlPedestal = Vector3.Distance(transform.position, posicionOriginalAnillo);
        if (distAlPedestal > rangoVision) return;

        Vector3 posOjos = transform.position + Vector3.up * alturaOjos;
        Vector3 posPedestal = posicionOriginalAnillo + Vector3.up * 0.5f;
        Vector3 direccion = (posPedestal - posOjos).normalized;

        float angulo = Vector3.Angle(transform.forward, direccion);
        if (angulo > anguloVision) return;

        if (!Physics.Raycast(posOjos, direccion, distAlPedestal, capasObstaculos))
        {
            anilloDesaparecidoNotificado = true;
            OnAnilloDesaparecido?.Invoke();
        }
    }

    
    public bool AnilloEnPedestal()
    {
        return objetoAnillo != null && objetoAnillo.gameObject.activeSelf;
    }

  
    void OnDrawGizmos()
    {
        bool viendo = Application.isPlaying && ObjetivoEsVisible;
        Gizmos.color = viendo ? Color.red : Color.green;

        Vector3 posOjos = transform.position + Vector3.up * alturaOjos;
        Gizmos.DrawRay(posOjos, transform.forward * rangoVision);

        Vector3 bordeIzq = Quaternion.Euler(0, -anguloVision, 0) * transform.forward;
        Vector3 bordeDer = Quaternion.Euler(0, anguloVision, 0) * transform.forward;
        Gizmos.DrawRay(posOjos, bordeIzq * rangoVision);
        Gizmos.DrawRay(posOjos, bordeDer * rangoVision);

        int segmentos = 20;
        Vector3 puntoAnterior = posOjos + bordeIzq * rangoVision;
        for (int i = 1; i <= segmentos; i++)
        {
            float a = Mathf.Lerp(-anguloVision, anguloVision, i / (float)segmentos);
            Vector3 dir = Quaternion.Euler(0, a, 0) * transform.forward;
            Vector3 puntoActual = posOjos + dir * rangoVision;
            Gizmos.DrawLine(puntoAnterior, puntoActual);
            puntoAnterior = puntoActual;
        }
    }
}