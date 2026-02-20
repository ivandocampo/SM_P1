using UnityEngine;

public class SensorOidoOrco : MonoBehaviour
{
    [Header("Configuración de Audición")]
    public Transform objetivoFrodo;         // Referencia a la presa
    public float rangoOido = 15f;           // Distancia a la que oye pisadas fuertes

    private CerebroFrodo scriptFrodo;       // Enlace al objetivo para medir estímulos

    void Start()
    {
        if (objetivoFrodo != null)
        {
            scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
        }
    }

    public bool OirFrodo(out Vector3 posicionRuido)
    {
        posicionRuido = Vector3.zero;

        // El orco solo escucha si Frodo está corriendo
        if (scriptFrodo != null && scriptFrodo.estaCorriendo)
        {
            if (Vector3.Distance(transform.position, objetivoFrodo.position) < rangoOido)
            {
                // El oído capta las coordenadas del sonido
                posicionRuido = objetivoFrodo.position;
                return true;
            }
        }
        return false;
    }
}