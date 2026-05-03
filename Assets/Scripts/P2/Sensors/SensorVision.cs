using UnityEngine;
using System;
 
public class SensorVision : MonoBehaviour
{
    [Header("Configuración de Visión")]
    public float rangoVision = 15f;
    public float anguloVision = 60f;
    public LayerMask capasObstaculos;
    public LayerMask capasDetectables;    // capas a escanear con OverlapSphere
 
    [Header("Identificación")]
    public string tagObjetivo = "Player"; // tag de Frodo en la escena
 
    [Header("Altura de los Ojos")]
    public float alturaOjos = 1.5f;
    public float alturaObjetivo = 1.0f;
 
    [Header("Pedestal del Anillo")]
    public Transform objetoAnillo;
 
    [Header("Rendimiento")]
    public float intervaloChequeo = 0.1f; // segundos entre escaneos
 
    // === EVENTOS ===
    public event Action<Vector3> OnObjetivoDetectado;  // no visible → visible
    public event Action OnObjetivoPerdido;              // visible → no visible
    public event Action<Vector3> OnObjetivoVisible;    // sigue visible
    public event Action OnAnilloDesaparecido;           // anillo recogido y pedestal visible
 
    // === ESTADO ===
    public bool ObjetivoEsVisible { get; private set; } = false;
    public bool ObjetivoVisibleConAnillo { get; private set; } = false;
 
    private bool eraVisible = false;
    private bool anilloDesaparecidoNotificado = false;
    private Vector3 posicionOriginalAnillo;
    private CerebroFrodo cerebroFrodo;
    private float timerChequeo = 0f;
 
    void Start()
    {
        GameObject frodo = GameObject.FindWithTag(tagObjetivo);
        if (frodo != null)
            cerebroFrodo = frodo.GetComponent<CerebroFrodo>();
 
        if (objetoAnillo != null)
            posicionOriginalAnillo = objetoAnillo.position;
    }
 
    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;
 
        timerChequeo -= Time.deltaTime;
        if (timerChequeo > 0f) return;
        timerChequeo = intervaloChequeo;
 
        Transform objetivo = EscanearCono();
        bool esVisible = objetivo != null;
        ObjetivoEsVisible = esVisible;
        ObjetivoVisibleConAnillo = esVisible
            && cerebroFrodo != null
            && cerebroFrodo.TieneElAnillo
            && !cerebroFrodo.usandoAnillo;
 
        if (esVisible && !eraVisible)
            OnObjetivoDetectado?.Invoke(objetivo.position);
        else if (!esVisible && eraVisible)
            OnObjetivoPerdido?.Invoke();
        else if (esVisible)
            OnObjetivoVisible?.Invoke(objetivo.position);
        else
            ObjetivoVisibleConAnillo = false;
 
        eraVisible = esVisible;
 
        ComprobarAnillo();
    }
 
    // Lanza un OverlapSphere y filtra por cono + LOS. Devuelve el transform del primer
    // objetivo válido encontrado, o null si no hay ninguno en el cono.
    private Transform EscanearCono()
    {
        Collider[] candidatos = Physics.OverlapSphere(transform.position, rangoVision, capasDetectables);
 
        foreach (Collider col in candidatos)
        {
            if (!col.CompareTag(tagObjetivo)) continue;
 
            if (cerebroFrodo != null && cerebroFrodo.usandoAnillo) continue;
 
            Vector3 posOjos     = transform.position + Vector3.up * alturaOjos;
            Vector3 posObjetivo = col.transform.position + Vector3.up * alturaObjetivo;
            Vector3 direccion   = (posObjetivo - posOjos).normalized;
            float distancia     = Vector3.Distance(posOjos, posObjetivo);
 
            // Ángulo horizontal (proyección XZ para ignorar diferencia de altura)
            Vector3 dirFlat     = new Vector3(direccion.x, 0f, direccion.z).normalized;
            Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            if (Vector3.Angle(forwardFlat, dirFlat) > anguloVision) continue;
 
            if (Physics.Raycast(posOjos, direccion, distancia, capasObstaculos)) continue;
 
            return col.transform;
        }
 
        return null;
    }
 
    private void ComprobarAnillo()
    {
        if (anilloDesaparecidoNotificado) return;
        if (objetoAnillo == null) return;
        if (objetoAnillo.gameObject.activeSelf) return;
 
        float distAlPedestal = Vector3.Distance(transform.position, posicionOriginalAnillo);
        if (distAlPedestal > rangoVision) return;
 
        Vector3 posOjos     = transform.position + Vector3.up * alturaOjos;
        Vector3 posPedestal = posicionOriginalAnillo + Vector3.up * 0.5f;
        Vector3 direccion   = (posPedestal - posOjos).normalized;
 
        Vector3 dirFlat     = new Vector3(direccion.x, 0f, direccion.z).normalized;
        Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        if (Vector3.Angle(forwardFlat, dirFlat) > anguloVision) return;
 
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
        Vector3 bordeDer = Quaternion.Euler(0,  anguloVision, 0) * transform.forward;
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