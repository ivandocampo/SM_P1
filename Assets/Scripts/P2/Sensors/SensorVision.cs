// =============================================================
// Sensor de vision usado por guardias y aranas.
// Detecta objetivos dentro de un cono de vision configurable, valida
// obstaculos mediante raycast y emite eventos cuando Frodo aparece,
// permanece visible, se pierde de vista o el anillo desaparece del pedestal
// =============================================================

using UnityEngine;
using System;
 
public class SensorVision : MonoBehaviour
{
    [Header("Configuración de Visión")]
    // Rango maximo y semiangulo del cono de vision
    public float rangoVision = 15f;
    public float anguloVision = 60f;
    // Capas que bloquean la vision y capas en las que se buscan objetivos
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
 
    // Eventos que consumen los agentes para actualizar sus creencias
    public event Action<Vector3> OnObjetivoDetectado;  // no visible a visible
    public event Action OnObjetivoPerdido;              // visible a no visible
    public event Action<Vector3> OnObjetivoVisible;    // sigue visible
    public event Action OnAnilloDesaparecido;           // anillo recogido y pedestal visible
 
    // Estado publico de lectura para saber si el sensor esta viendo a Frodo.
    public bool ObjetivoEsVisible { get; private set; } = false;
    public bool ObjetivoVisibleConAnillo { get; private set; } = false;
 
    // Estado interno para detectar cambios entre visible/no visible
    private bool eraVisible = false;
    private bool anilloDesaparecidoNotificado = false;
    private Vector3 posicionOriginalAnillo;
    private CerebroFrodo cerebroFrodo;
    private float timerChequeo = 0f;
 
    void Start()
    {
        // Busca a Frodo por tag para consultar si esta usando o portando el anillo
        GameObject frodo = GameObject.FindWithTag(tagObjetivo);
        if (frodo != null)
            cerebroFrodo = frodo.GetComponent<CerebroFrodo>();
 
        // Guarda la posicion inicial del anillo, que actua como posicion del pedestal
        if (objetoAnillo != null)
            posicionOriginalAnillo = objetoAnillo.position;
    }
 
    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;
 
        // El sensor no escanea en todos los frames: espera al siguiente intervalo
        timerChequeo -= Time.deltaTime;
        if (timerChequeo > 0f) return;
        timerChequeo = intervaloChequeo;
 
        // Escaneo principal del cono de vision
        Transform objetivo = EscanearCono();
        bool esVisible = objetivo != null;
        ObjetivoEsVisible = esVisible;
        // Solo se considera "visible con anillo" si Frodo lo lleva y no esta invisible
        ObjetivoVisibleConAnillo = esVisible
            && cerebroFrodo != null
            && cerebroFrodo.TieneElAnillo
            && !cerebroFrodo.usandoAnillo;
 
        // Se emite un evento distinto segun el cambio de estado visual
        if (esVisible && !eraVisible)
            OnObjetivoDetectado?.Invoke(objetivo.position);
        else if (!esVisible && eraVisible)
            OnObjetivoPerdido?.Invoke();
        else if (esVisible)
            OnObjetivoVisible?.Invoke(objetivo.position);
        else
            ObjetivoVisibleConAnillo = false;
 
        eraVisible = esVisible;
 
        // Ademas de mirar a Frodo, comprueba si el pedestal visible quedo vacío
        ComprobarAnillo();
    }
 
    // Lanza un OverlapSphere y filtra candidatos por tag, anillo, angulo y obstaculos
    private Transform EscanearCono()
    {
        // Primer filtro barato: candidatos cercanos dentro del rango del sensor
        Collider[] candidatos = Physics.OverlapSphere(transform.position, rangoVision, capasDetectables);
 
        foreach (Collider col in candidatos)
        {
            // Solo interesa Frodo u otro objetivo con el tag configurado
            if (!col.CompareTag(tagObjetivo)) continue;
 
            // Si Frodo esta usando el anillo, la vision no puede detectarlo
            if (cerebroFrodo != null && cerebroFrodo.usandoAnillo) continue;
 
            // Se usan alturas separadas para simular ojos del agente y centro del objetivo
            Vector3 posOjos     = transform.position + Vector3.up * alturaOjos;
            Vector3 posObjetivo = col.transform.position + Vector3.up * alturaObjetivo;
            Vector3 direccion   = (posObjetivo - posOjos).normalized;
            float distancia     = Vector3.Distance(posOjos, posObjetivo);
 
            // Ángulo horizontal (proyección XZ para ignorar diferencia de altura)
            Vector3 dirFlat     = new Vector3(direccion.x, 0f, direccion.z).normalized;
            Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            if (Vector3.Angle(forwardFlat, dirFlat) > anguloVision) continue;
 
            // Si hay una pared u obstaculo entre los ojos y Frodo, no hay vision
            if (Physics.Raycast(posOjos, direccion, distancia, capasObstaculos)) continue;
 
            return col.transform;
        }
 
        return null;
    }
 
    private void ComprobarAnillo()
    {
        // La desaparicion del anillo solo se notifica una vez
        if (anilloDesaparecidoNotificado) return;
        if (objetoAnillo == null) return;
        // Si el objeto sigue activo, el anillo continua en el pedestal
        if (objetoAnillo.gameObject.activeSelf) return;
 
        // El agente solo detecta el pedestal vacio si esta dentro de su rango visual
        float distAlPedestal = Vector3.Distance(transform.position, posicionOriginalAnillo);
        if (distAlPedestal > rangoVision) return;
 
        Vector3 posOjos     = transform.position + Vector3.up * alturaOjos;
        Vector3 posPedestal = posicionOriginalAnillo + Vector3.up * 0.5f;
        Vector3 direccion   = (posPedestal - posOjos).normalized;
 
        Vector3 dirFlat     = new Vector3(direccion.x, 0f, direccion.z).normalized;
        Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        if (Vector3.Angle(forwardFlat, dirFlat) > anguloVision) return;
 
        // Si no hay obstaculos hasta el pedestal, se informa de que el anillo desapareció
        if (!Physics.Raycast(posOjos, direccion, distAlPedestal, capasObstaculos))
        {
            anilloDesaparecidoNotificado = true;
            OnAnilloDesaparecido?.Invoke();
        }
    }
 
    public bool AnilloEnPedestal()
    {
        // Metodo auxiliar para consultas directas sobre el estado del pedestal
        return objetoAnillo != null && objetoAnillo.gameObject.activeSelf;
    }
 
    void OnDrawGizmos()
    {
        // Dibuja el cono en escena para ajustar rango y angulo desde el editor
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
