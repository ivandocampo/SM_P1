using UnityEngine;

public class SensorOlfatoOrco : MonoBehaviour
{
    [Header("Configuración de Olfato")]
    public Transform objetivoFrodo;
    public float rangoOlfatoBase = 3f;
    public float rangoOlfatoAnillo = 40f;

    private CerebroFrodo scriptFrodo;

    // Establece el vínculo con el cerebro del objetivo para monitorizar sus estados
    void Start()
    {
        if (objetivoFrodo != null)
        {
            scriptFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
        }
    }

    // Calcula si el rastro del objetivo es perceptible según el uso del anillo
    public bool OlerFrodo()
    {
        // Finaliza la comprobación si las referencias necesarias no están asignadas
        if (objetivoFrodo == null || scriptFrodo == null) return false;

        // Mide la separación física entre el agente y el objetivo
        float distancia = Vector3.Distance(transform.position, objetivoFrodo.position);

        // Incrementa drásticamente el rango de detección si el objetivo activa el anillo
        if (scriptFrodo.usandoAnillo)
        {
            return distancia < rangoOlfatoAnillo;
        }
        // Aplica el rango de proximidad estándar cuando el objetivo no emplea magia
        else
        {
            return distancia < rangoOlfatoBase;
        }
    }
}