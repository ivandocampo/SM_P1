// =============================================================
// Actuador de interacción del personaje Frodo (controlado por el jugador)
//
// En la práctica, Frodo puede recoger el Anillo del pedestal y usarlo
// para volverse invisible a los sensores de visión de guardias y arañas.
// Este script gestiona las dos consecuencias físicas de esas acciones:
//   - Eliminar el anillo de la escena al ser recogido.
//   - Cambiar la apariencia visual de Frodo para reflejar su invisibilidad
//
// Es llamado por CerebroFrodo cuando el jugador recoge el anillo o activa
// el poder de invisibilidad con la barra espaciadora
// =============================================================

using UnityEngine;

public class ActuadorInteraccionFrodo : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform elAnillo;          // Referencia al GameObject del Anillo en la escena

    [Header("Efecto Visual Anillo")]
    public Renderer[] renderersFrodo;   // Todos los Renderer del modelo de Frodo
    public float alphaInvisible = 0.2f; // Transparencia aplicada al activar la invisibilidad

    // Desactivar el Anillo de la escena al ser recogido por Frodo
    public void CogerAnillo()
    {
        if (elAnillo != null)
            elAnillo.gameObject.SetActive(false);
    }

    // Aplicar o quitar la transparencia del modelo de Frodo al activar/desactivar el Anillo
    public void CambiarTransparencia(bool hacerInvisible)
    {
        float valorAlpha = hacerInvisible ? alphaInvisible : 1f;

        foreach (Renderer rend in renderersFrodo)
        {
            if (rend != null)
            {
                // Modificar solo el canal alpha, manteniendo el color original
                Color colorActual = rend.material.color;
                colorActual.a = valorAlpha;
                rend.material.color = colorActual;
            }
        }
    }
}
