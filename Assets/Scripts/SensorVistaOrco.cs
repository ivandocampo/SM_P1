using UnityEngine;

public class SensorVistaOrco : MonoBehaviour
{
    [Header("Configuración de Visión")]
    public Transform objetivoFrodo;         
    public Transform elAnillo;              
    public float rangoVision = 15f;         
    public float anguloVision = 60f;        // 120 grados total (60 hacia cada lado) - visión periférica de guardia alerta
    public LayerMask capasObstaculos;       // Solo paredes/obstáculos, no incluir la layer de Frodo)

    private Vector3 posicionOriginalAnillo;
    private CerebroOrco[] todosOrcos;  // Cacheado para no buscar cada vez

    void Start()
    {
        if (elAnillo != null)
        {
            posicionOriginalAnillo = elAnillo.position;
        }
        todosOrcos = FindObjectsOfType<CerebroOrco>();
    }

    public bool VerFrodo()
    {
        if (objetivoFrodo == null) return false;

        CerebroFrodo scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
        if (scriptFrodo != null && scriptFrodo.usandoAnillo)
        {
            return false; // Es invisible
        }

        float distancia = Vector3.Distance(transform.position, objetivoFrodo.position);
        
        // 1. Si está demasiado lejos, no lo ve
        if (distancia > rangoVision) return false;

        Vector3 direccionHaciaFrodo = (objetivoFrodo.position - transform.position).normalized;

        // 2. NUEVO: Comprobar si Frodo está dentro del cono de visión frontal del Orco
        float anguloHaciaFrodo = Vector3.Angle(transform.forward, direccionHaciaFrodo);
        if (anguloHaciaFrodo > anguloVision) return false; // Está a su espalda o muy a los lados

        // 3. Comprobar si hay paredes bloqueando la línea de visión
        // Lanzamos el ray solo contra obstáculos: si impacta una pared antes de llegar a Frodo, no lo ve
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direccionHaciaFrodo, out hit, distancia, capasObstaculos))
        {
            // Hay una pared entre el orco y Frodo
            return false;
        }
        // No hay obstáculos en medio: lo ve
        return true;
    }

    // Usado durante patrulla: detecta el anillo ausente a distancia con cono de visión
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

    // Usado al llegar al pedestal: el orco está al lado y mira directamente si el anillo sigue ahí
    // No necesita cono de visión ni rango largo porque está justo delante
    public bool AnilloEnPedestal()
    {
        return elAnillo != null && elAnillo.gameObject.activeSelf;
    }

    // Al investigar un ruido, comprueba si lo que hay cerca es un compañero orco
    // Usa un rango corto porque el orco ya llegó al origen del ruido
    public bool VerCompañeroCerca(float rango = 5f)
    {
        foreach (CerebroOrco otro in todosOrcos)
        {
            if (otro.gameObject == this.gameObject) continue; // Ignorarse a sí mismo

            float distancia = Vector3.Distance(transform.position, otro.transform.position);
            if (distancia < rango)
            {
                // Comprobar que no hay pared entre ellos (lo ve realmente)
                Vector3 direccion = (otro.transform.position - transform.position).normalized;
                if (!Physics.Raycast(transform.position, direccion, distancia, capasObstaculos))
                {
                    return true; // Ve a un compañero: el ruido era de uno de los suyos
                }
            }
        }
        return false;
    }
}