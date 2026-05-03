using UnityEngine;

[System.Serializable]
public class PursuitBehavior : IBehavior
{
    private Vector3 ultimoDestino;
    private bool tieneDestino;
    private const float UMBRAL_RECALCULO_DESTINO_SQR = 0.36f;

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        ultimoDestino = creencias.UltimaPosicionLadron;
        tieneDestino = true;
        actuador.SetDestino(ultimoDestino, TipoVelocidad.Persecucion);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        Vector3 nuevoDestino = creencias.UltimaPosicionLadron;
        if (!tieneDestino ||
            (nuevoDestino - ultimoDestino).sqrMagnitude > UMBRAL_RECALCULO_DESTINO_SQR)
        {
            ultimoDestino = nuevoDestino;
            tieneDestino = true;
            actuador.SetDestino(ultimoDestino, TipoVelocidad.Persecucion);
        }

        if (!creencias.LadronVisible && actuador.HaLlegado(1.5f))
            return true;

        return false;
    }

    public void Detener(ActuadorMovimiento actuador)
    {
        actuador.CambiarVelocidad(TipoVelocidad.Alerta);
    }
}
