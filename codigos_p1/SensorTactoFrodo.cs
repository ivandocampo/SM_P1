using UnityEngine;

public class SensorTactoFrodo : MonoBehaviour
{
    [Header("Objetivos Físicos")]
    public Transform elAnillo;   // Referencia al objeto que Frodo debe robar
    public Transform laSalida;   // Referencia al punto de escape

    public bool TocarAnillo()
    {
        // El sentido del tacto comprueba si el objeto está al alcance de la mano
        if (elAnillo != null && elAnillo.gameObject.activeSelf)
        {
            return Vector3.Distance(transform.position, elAnillo.position) < 1.5f;
        }
        return false;
    }

    public bool TocarSalida()
    {
        // El sentido del tacto comprueba si Frodo está pisando la zona de escape
        if (laSalida != null)
        {
            return Vector3.Distance(transform.position, laSalida.position) < 2.0f;
        }
        return false;
    }

    public void CogerAnillo()
    {
        // Interacción física con el entorno al coger el objeto
        if (elAnillo != null)
        {
            elAnillo.gameObject.SetActive(false);
        }
    }
}