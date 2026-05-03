// =============================================================
// Behavior de persecución directa del ladrón.
// Solo lo activa el guardia con detección propia reciente (ver TacticalDesires).
// Actualiza el destino cada vez que la posición del ladrón cambia más de
// UMBRAL_RECALCULO_DESTINO_SQR para evitar recálculos redundantes.
// Termina cuando el ladrón deja de ser visible y el guardia ha llegado al punto
// =============================================================

using UnityEngine;

[System.Serializable]
public class PursuitBehavior : IBehavior
{
    private Vector3 ultimoDestino;
    private bool tieneDestino;
    // Umbral al cuadrado para detectar cambio significativo de posición del ladrón
    private const float UMBRAL_RECALCULO_DESTINO_SQR = 0.36f;

    // Iniciar la persecución yendo directamente a la última posición conocida del ladrón
    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        ultimoDestino = creencias.UltimaPosicionLadron;
        tieneDestino = true;
        actuador.SetDestino(ultimoDestino, TipoVelocidad.Persecucion);
    }

    // Recalcular destino solo si el ladrón se ha movido lo suficiente; terminar si se pierde de vista al llegar
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

    // Reducir velocidad al alerta al terminar la persecución
    public void Detener(ActuadorMovimiento actuador)
    {
        actuador.CambiarVelocidad(TipoVelocidad.Alerta);
    }
}
