// =============================================================
// Behavior de comprobación del pedestal del Anillo Único.
// El guardia se dirige al pedestal y termina al llegar (radio 2 m).
// Lo activa el DesireGenerator cuando el guardia es candidato a
// vigilar el pedestal o tras lanzar un Contract-Net de búsqueda
// =============================================================

using UnityEngine;

[System.Serializable]
public class CheckPedestalBehavior : IBehavior
{
    private Transform pedestal;

    public CheckPedestalBehavior(Transform pedestal)
    {
        this.pedestal = pedestal;
    }

    // Enviar al guardia al pedestal al activar el behavior
    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (pedestal != null)
            actuador.SetDestino(pedestal.position, TipoVelocidad.Alerta);
    }

    // Terminar en cuanto el guardia está a menos de 2 m del pedestal
    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        return actuador.HaLlegado(2f);
    }

    public void Detener(ActuadorMovimiento actuador) { }
}
