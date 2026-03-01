using UnityEngine;

public class SensorVistaOrco : MonoBehaviour
{
    [Header("Configuración de Visión")]
    public Transform objetivoFrodo;         
    public Transform elAnillo;              
    public float rangoVision = 15f;         
    public float anguloVision = 60f;        // 120 grados total (60 hacia cada lado)
    public LayerMask capasObstaculos;       // Solo paredes/obstáculos

    private Vector3 posicionOriginalAnillo;
    private CerebroOrco[] todosOrcos;

    void Start()
    {
        if (elAnillo != null)
        {
            posicionOriginalAnillo = elAnillo.position;
        }
        todosOrcos = FindObjectsByType<CerebroOrco>(FindObjectsSortMode.None);
    }

    public bool VerFrodo()
    {
        if (objetivoFrodo == null) return false;

        CerebroFrodo scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
        if (scriptFrodo != null && scriptFrodo.usandoAnillo)
        {
            return false;
        }

        float distancia = Vector3.Distance(transform.position, objetivoFrodo.position);
        if (distancia > rangoVision) return false;

        Vector3 direccionHaciaFrodo = (objetivoFrodo.position - transform.position).normalized;

        float anguloHaciaFrodo = Vector3.Angle(transform.forward, direccionHaciaFrodo);
        if (anguloHaciaFrodo > anguloVision) return false;

        RaycastHit hit;
        if (Physics.Raycast(transform.position, direccionHaciaFrodo, out hit, distancia, capasObstaculos))
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
                Vector3 direccion = (posicionOriginalAnillo - transform.position).normalized;
                
                float anguloHaciaPedestal = Vector3.Angle(transform.forward, direccion);
                if (anguloHaciaPedestal > anguloVision) return false;

                if (!Physics.Raycast(transform.position, direccion, distanciaAlPedestal, capasObstaculos))
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

    public bool VerCompañeroCerca(float rango = 5f)
    {
        foreach (CerebroOrco otro in todosOrcos)
        {
            if (otro.gameObject == this.gameObject) continue;

            float distancia = Vector3.Distance(transform.position, otro.transform.position);
            if (distancia < rango)
            {
                Vector3 direccion = (otro.transform.position - transform.position).normalized;
                if (!Physics.Raycast(transform.position, direccion, distancia, capasObstaculos))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ==================== GIZMOS ====================
    void OnDrawGizmos()
    {
        Color colorCono = (Application.isPlaying && VerFrodo()) ? Color.red : Color.green;
        Gizmos.color = colorCono;

        Gizmos.DrawRay(transform.position, transform.forward * rangoVision);

        Vector3 bordeIzq = Quaternion.Euler(0, -anguloVision, 0) * transform.forward;
        Vector3 bordeDer = Quaternion.Euler(0, anguloVision, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, bordeIzq * rangoVision);
        Gizmos.DrawRay(transform.position, bordeDer * rangoVision);

        int segmentos = 20;
        Vector3 puntoAnterior = transform.position + bordeIzq * rangoVision;
        for (int i = 1; i <= segmentos; i++)
        {
            float angulo = Mathf.Lerp(-anguloVision, anguloVision, i / (float)segmentos);
            Vector3 dir = Quaternion.Euler(0, angulo, 0) * transform.forward;
            Vector3 puntoActual = transform.position + dir * rangoVision;
            Gizmos.DrawLine(puntoAnterior, puntoActual);
            puntoAnterior = puntoActual;
        }
    }
}