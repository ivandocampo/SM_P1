using UnityEngine;

public class ActuadorInteraccionFrodo : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform elAnillo;

    [Header("Efecto Visual Anillo")]
    public Renderer[] renderersFrodo;        // Arrastra aquí el modelo 3D de Frodo
    public float alphaInvisible = 0.2f;      // Nivel de transparencia (0.2 es casi fantasma)

    // Elimina el Anillo del mundo cuando Frodo lo recoge
    public void CogerAnillo()
    {
        if (elAnillo != null)
            elAnillo.gameObject.SetActive(false);
    }

    // Cambia el color/transparencia del modelo 3D
    public void CambiarTransparencia(bool hacerInvisible)
    {
        float valorAlpha = hacerInvisible ? alphaInvisible : 1f;

        foreach (Renderer rend in renderersFrodo)
        {
            if (rend != null)
            {
                Color colorActual = rend.material.color;
                colorActual.a = valorAlpha;
                rend.material.color = colorActual;
            }
        }
    }
}