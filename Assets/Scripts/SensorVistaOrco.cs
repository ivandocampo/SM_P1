using UnityEngine;

public class SensorVistaOrco : MonoBehaviour
{
    [Header("Configuración de Visión")]
    public Transform objetivoFrodo;         // Referencia a la presa
    public Transform elAnillo;              // Referencia al tesoro
    public float rangoVision = 10f;         // Distancia máxima visual fija
    public LayerMask capasVision;           // Qué objetos bloquean la vista (paredes)

    private Vector3 posicionOriginalAnillo; // Memoria de dónde estaba el tesoro inicialmente

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
            return false; // Es invisible, la vista se anula directamente
        }

        float distancia = Vector3.Distance(transform.position, objetivoFrodo.position);
        
        // El orco no ve a Frodo si está demasiado lejos
        if (distancia > rangoVision) return false;

        // El orco comprueba si hay paredes bloqueando su visión
        Vector3 direccion = objetivoFrodo.position - transform.position;
        RaycastHit hit;
        
        if (Physics.Raycast(transform.position, direccion, out hit, rangoVision, capasVision))
        {
            if (hit.transform == objetivoFrodo) return true;
        }
        return false;
    }

    public bool NoAnillo()
    {
        // El ojo comprueba si el objeto ha desaparecido físicamente del mundo
        if (elAnillo != null && !elAnillo.gameObject.activeSelf)
        {
            float distanciaAlPedestal = Vector3.Distance(transform.position, posicionOriginalAnillo);
            
            // El orco lanza una mirada al pedestal si pasa cerca
            if (distanciaAlPedestal < rangoVision)
            {
                Vector3 direccion = posicionOriginalAnillo - transform.position;
                
                // Si no hay paredes tapando el pedestal, confirma con sus ojos que no está
                if (!Physics.Raycast(transform.position, direccion, distanciaAlPedestal, capasVision))
                {
                    return true;
                }
            }
        }
        return false;
    }
}