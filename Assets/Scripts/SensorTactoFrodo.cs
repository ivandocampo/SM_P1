using UnityEngine;

public class SensorTactoFrodo : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform elAnillo;
    public Transform laSalida;

    // Detecta si Frodo está en proximidad del Anillo
    public bool TocarAnillo()
    {
        if (elAnillo != null && elAnillo.gameObject.activeSelf)
            return Vector3.Distance(transform.position, elAnillo.position) < 1.5f;
        return false;
    }

    // Detecta si Frodo está en la zona de salida
    public bool TocarSalida()
    {
        if (laSalida != null)
            return Vector3.Distance(transform.position, laSalida.position) < 2.0f;
        return false;
    }
}