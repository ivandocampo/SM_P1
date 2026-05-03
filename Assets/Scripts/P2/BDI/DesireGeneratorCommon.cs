using UnityEngine;

public partial class DesireGenerator
{
    private TacticalPhasePolicy ObtenerPoliticaDeFase(TacticalPhase fase)
    {
        return TacticalPhasePolicy.For(fase);
    }

    private bool VeniaDeComportamientoTactico()
    {
        return creencias.EstadoActual == BehaviorType.Intercept ||
               creencias.EstadoActual == BehaviorType.Pursuit ||
               creencias.EstadoActual == BehaviorType.Search;
    }

    private float PrioridadPorCercania(float basePrioridad, Vector3 objetivo, float penalizacionPorMetro)
    {
        float distancia = Vector3.Distance(creencias.MiPosicion, objetivo);
        return basePrioridad - distancia * penalizacionPorMetro;
    }
}
