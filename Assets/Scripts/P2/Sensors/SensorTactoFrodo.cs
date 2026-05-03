// =============================================================
// Sensor de tacto/proximidad de Frodo.
// Comprueba si el jugador esta suficientemente cerca del anillo para
// recogerlo o de la salida para completar la partida. No usa eventos:
// expone metodos simples que el cerebro de Frodo consulta cuando hace falta
// =============================================================

using UnityEngine;

public class SensorTactoFrodo : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform elAnillo;
    public Transform laSalida;

    // Evalúa la proximidad física del personaje con respecto al objeto del anillo
    public bool TocarAnillo()
    {
        // Verifica que el anillo exista y se encuentre activo en la jerarquía de la escena
        if (elAnillo != null && elAnillo.gameObject.activeSelf)
            // Calcula si la separación entre ambos es inferior al límite de interacción
            return Vector3.Distance(transform.position, elAnillo.position) < 1.5f;
            
        return false;
    }

    // Comprueba si el personaje ha ingresado en el área designada como meta o salida
    public bool TocarSalida()
    {
        // Valida la referencia del objeto de salida antes de realizar el cálculo
        if (laSalida != null)
            // Retorna verdadero si el personaje se encuentra dentro del radio de escape
            return Vector3.Distance(transform.position, laSalida.position) < 2.0f;
            
        return false;
    }
}
