using UnityEngine;

[System.Serializable]
public class PatrolBehavior : IBehavior
{
    private Transform[] puntosPatrulla;
    private int indiceActual = 0;

    public PatrolBehavior(Transform[] puntos)
    {
        puntosPatrulla = puntos;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosPatrulla == null || puntosPatrulla.Length == 0) return;
        actuador.SetDestino(puntosPatrulla[indiceActual].position, TipoVelocidad.Patrulla);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosPatrulla == null || puntosPatrulla.Length == 0) return false;

        if (actuador.HaLlegado(0.5f))
        {
            indiceActual = (indiceActual + 1) % puntosPatrulla.Length;
            actuador.SetDestino(puntosPatrulla[indiceActual].position, TipoVelocidad.Patrulla);
        }

        return false;
    }

    public void Detener(ActuadorMovimiento actuador)
    {
        // No detenemos al agente; simplemente dejara de ir al siguiente waypoint.
    }
}
