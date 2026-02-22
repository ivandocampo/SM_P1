using UnityEngine;

public class SensorOlfatoOrco : MonoBehaviour
{
    [Header("Configuración de Olfato")]
    public Transform objetivoFrodo;         // Referencia a la presa
    public float rangoOlfatoBase = 3f;      // Distancia a la que lo huele de forma natural (muy cerca)
    public float rangoOlfatoAnillo = 40f;   // Distancia a la que huele la magia del anillo (muy lejos)

    private CerebroFrodo scriptFrodo;       // Enlace al objetivo para medir estímulos

    void Start()
    {
        if (objetivoFrodo != null)
        {
            scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
        }
    }

    public bool OlerFrodo()
    {
        if (objetivoFrodo == null || scriptFrodo == null) return false;

        float distancia = Vector3.Distance(transform.position, objetivoFrodo.position);

        // Si usa el anillo, el orco lo huele desde muchísimo más lejos
        if (scriptFrodo.usandoAnillo)
        {
            return distancia < rangoOlfatoAnillo;
        }
        // Si NO usa el anillo, el orco solo lo huele si está casi pegado a él
        else
        {
            return distancia < rangoOlfatoBase;
        }
    }
}