using UnityEngine;

public class ActuadorInteraccionFrodo : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform elAnillo;

    [Header("Efecto Visual Anillo")]
    public Renderer[] renderersFrodo;
    public float alphaInvisible = 0.2f;

    // Gestiona la recolección del anillo desactivando su presencia en la escena
    public void CogerAnillo()
    {
        // Verifica la existencia de la referencia antes de desactivar el objeto
        if (elAnillo != null)
            elAnillo.gameObject.SetActive(false);
    }

    // Modifica la apariencia visual del personaje para reflejar su estado de visibilidad
    public void CambiarTransparencia(bool hacerInvisible)
    {
        // Selecciona el valor de transparencia adecuado según el estado solicitado
        float valorAlpha = hacerInvisible ? alphaInvisible : 1f;

        // Itera sobre todos los componentes visuales asignados al personaje
        foreach (Renderer rend in renderersFrodo)
        {
            // Valida que el componente visual actual sea accesible
            if (rend != null)
            {
                // Obtiene el color del material actual para ajustar su canal de opacidad
                Color colorActual = rend.material.color;
                colorActual.a = valorAlpha;
                
                // Aplica el nuevo color con la transparencia actualizada al material
                rend.material.color = colorActual;
            }
        }
    }
}