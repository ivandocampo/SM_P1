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
    public float alturaOjosOrco = 1.5f;      // Desde dónde lanza el rayo el orco
    public float alturaObjetivoFrodo = 1.0f;  // A qué altura de Frodo apunta

    private Vector3 posicionOriginalAnillo;

    void Start()
    {
        if (elAnillo != null)
        {
            posicionOriginalAnillo = elAnillo.position;
        }
    }

    public bool VerFrodo()
    {
        if (objetivoFrodo == null) return false;

        CerebroFrodo scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
        if (scriptFrodo != null && scriptFrodo.usandoAnillo)
        {
            return false;
        }

        // FIX: Usar posiciones a la altura de los ojos, no de los pies.
        // Evita que irregularidades del suelo bloqueen el raycast.
        Vector3 posOjos = transform.position + Vector3.up * alturaOjosOrco;
        Vector3 posFrodo = objetivoFrodo.position + Vector3.up * alturaObjetivoFrodo;

        float distancia = Vector3.Distance(posOjos, posFrodo);
        if (distancia > rangoVision) return false;

        Vector3 direccionHaciaFrodo = (posFrodo - posOjos).normalized;

        float anguloHaciaFrodo = Vector3.Angle(transform.forward, direccionHaciaFrodo);
        if (anguloHaciaFrodo > anguloVision) return false;

        RaycastHit hit;
        if (Physics.Raycast(posOjos, direccionHaciaFrodo, out hit, distancia, capasObstaculos))
        {
            return false;
        }
        return true;
    }

    public bool NoAnillo()
    {
        if (elAnillo != null && !elAnillo.gameObject.activeSelf)
        {
            float distanciaAlPedestal = Vector3.Distance(transform.position, posicionOriginalAnillo);

            if (distanciaAlPedestal < rangoVision)
            {
                // FIX: Raycast también desde los ojos
                Vector3 posOjos = transform.position + Vector3.up * alturaOjosOrco;
                Vector3 posPedestal = posicionOriginalAnillo + Vector3.up * 0.5f;
                Vector3 direccion = (posPedestal - posOjos).normalized;
                float dist = Vector3.Distance(posOjos, posPedestal);

                float anguloHaciaPedestal = Vector3.Angle(transform.forward, direccion);
                if (anguloHaciaPedestal > anguloVision) return false;

                if (!Physics.Raycast(posOjos, direccion, dist, capasObstaculos))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool AnilloEnPedestal()
    {
        return elAnillo != null && elAnillo.gameObject.activeSelf;
    }


    void OnDrawGizmos()
    {
        // Cono de visión: verde normal, rojo si ve a Frodo
        bool veFrodo = Application.isPlaying && VerFrodo();
        Gizmos.color = veFrodo ? Color.red : Color.green;

        Vector3 posOjos = transform.position + Vector3.up * alturaOjosOrco;

        // Línea frontal
        Gizmos.DrawRay(posOjos, transform.forward * rangoVision);

        // Bordes del cono
        Vector3 bordeIzq = Quaternion.Euler(0, -anguloVision, 0) * transform.forward;
        Vector3 bordeDer = Quaternion.Euler(0, anguloVision, 0) * transform.forward;
        Gizmos.DrawRay(posOjos, bordeIzq * rangoVision);
        Gizmos.DrawRay(posOjos, bordeDer * rangoVision);

        // Arco
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