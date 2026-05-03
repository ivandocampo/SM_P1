using UnityEngine;

[System.Serializable]
public class CheckPedestalBehavior : IBehavior
{
    private Transform pedestal;

    public CheckPedestalBehavior(Transform pedestal)
    {
        this.pedestal = pedestal;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (pedestal != null)
            actuador.SetDestino(pedestal.position, TipoVelocidad.Alerta);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        return actuador.HaLlegado(2f);
    }

    public void Detener(ActuadorMovimiento actuador) { }
}
