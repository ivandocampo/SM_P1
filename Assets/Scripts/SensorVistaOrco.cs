using UnityEngine;

public class SensorVistaOrco : MonoBehaviour
{
    [Header("Configuración de Visión")]
    public Transform objetivoFrodo;
    public Transform elAnillo;
    public float rangoVision = 15f;
    public float anguloVision = 60f;
    public LayerMask capasObstaculos;

    [Header("Altura de los ojos")]
    public float alturaOjosOrco = 1.5f;
    public float alturaObjetivoFrodo = 1.0f;

    private Vector3 posicionOriginalAnillo;

    // Registra la ubicación inicial del anillo para monitorizar su presencia posterior
    void Start()
    {
        if (elAnillo != null)
        {
            posicionOriginalAnillo = elAnillo.position;
        }
    }

    // Determina si el objetivo es visible basándose en distancia, ángulo y obstáculos
    public bool VerFrodo()
    {
        if (objetivoFrodo == null) return false;

        // Comprueba si el objetivo se encuentra en estado de invisibilidad por el anillo
        CerebroFrodo scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
        if (scriptFrodo != null && scriptFrodo.usandoAnillo)
        {
            return false;
        }

        // Calcula las posiciones ajustadas a la altura de los ojos para evitar colisiones con el suelo
        Vector3 posOjos = transform.position + Vector3.up * alturaOjosOrco;
        Vector3 posFrodo = objetivoFrodo.position + Vector3.up * alturaObjetivoFrodo;

        // Valida si el objetivo se encuentra dentro del radio de alcance visual
        float distancia = Vector3.Distance(posOjos, posFrodo);
        if (distancia > rangoVision) return false;

        // Calcula el ángulo entre el frente del agente y la posición del objetivo
        Vector3 direccionHaciaFrodo = (posFrodo - posOjos).normalized;
        float anguloHaciaFrodo = Vector3.Angle(transform.forward, direccionHaciaFrodo);
        
        // Verifica si el objetivo cae dentro del cono de visión frontal
        if (anguloHaciaFrodo > anguloVision) return false;

        // Realiza un Raycast para confirmar que no existen muros u obstáculos bloqueando la vista
        RaycastHit hit;
        if (Physics.Raycast(posOjos, direccionHaciaFrodo, out hit, distancia, capasObstaculos))
        {
            return false;
        }
        
        return true;
    }

    // Identifica visualmente si el objeto del anillo ha sido sustraído de su posición original
    public bool NoAnillo()
    {
        if (elAnillo != null && !elAnillo.gameObject.activeSelf)
        {
            float distanciaAlPedestal = Vector3.Distance(transform.position, posicionOriginalAnillo);

            if (distanciaAlPedestal < rangoVision)
            {
                // Calcula la trayectoria visual hacia el pedestal vacío
                Vector3 posOjos = transform.position + Vector3.up * alturaOjosOrco;
                Vector3 posPedestal = posicionOriginalAnillo + Vector3.up * 0.5f;
                Vector3 direccion = (posPedestal - posOjos).normalized;
                float dist = Vector3.Distance(posOjos, posPedestal);

                // Comprueba si el pedestal está en el campo de visión actual
                float anguloHaciaPedestal = Vector3.Angle(transform.forward, direccion);
                if (anguloHaciaPedestal > anguloVision) return false;

                // Valida que no haya obstáculos ocultando la vista del pedestal vacío
                if (!Physics.Raycast(posOjos, direccion, dist, capasObstaculos))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Indica si el anillo sigue presente físicamente en el entorno
    public bool AnilloEnPedestal()
    {
        return elAnillo != null && elAnillo.gameObject.activeSelf;
    }

    // Dibuja el cono de visión en el editor de Unity para facilitar el depurado
    void OnDrawGizmos()
    {
        // Cambia el color del cono según si el agente está detectando al objetivo
        bool veFrodo = Application.isPlaying && VerFrodo();
        Gizmos.color = veFrodo ? Color.red : Color.green;

        Vector3 posOjos = transform.position + Vector3.up * alturaOjosOrco;

        // Representa la línea central de la mirada
        Gizmos.DrawRay(posOjos, transform.forward * rangoVision);

        // Representa los límites laterales del campo de visión
        Vector3 bordeIzq = Quaternion.Euler(0, -anguloVision, 0) * transform.forward;
        Vector3 bordeDer = Quaternion.Euler(0, anguloVision, 0) * transform.forward;
        Gizmos.DrawRay(posOjos, bordeIzq * rangoVision);
        Gizmos.DrawRay(posOjos, bordeDer * rangoVision);

        // Genera un arco visual para delimitar el final del rango de visión
        int segmentos = 20;
        Vector3 puntoAnterior = posOjos + bordeIzq * rangoVision;
        for (int i = 1; i <= segmentos; i++)
        {
            float angulo = Mathf.Lerp(-anguloVision, anguloVision, i / (float)segmentos);
            Vector3 dir = Quaternion.Euler(0, angulo, 0) * transform.forward;
            Vector3 puntoActual = posOjos + dir * rangoVision;
            Gizmos.DrawLine(puntoAnterior, puntoActual);
            puntoAnterior = puntoActual;
        }
    }
}