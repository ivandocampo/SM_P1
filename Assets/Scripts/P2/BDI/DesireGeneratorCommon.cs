// =============================================================
// Fichero parcial de DesireGenerator: utilidades comunes.
// Proporciona métodos auxiliares usados por el resto de ficheros
// parciales: obtener la política de fase, detectar si el guardia
// venía de un behavior táctico, y ajustar la prioridad de un deseo
// según la distancia al objetivo (el más cercano recibe más prioridad)
// =============================================================

using UnityEngine;

public partial class DesireGenerator
{
    // Obtener los parámetros tácticos (límites y prioridades) para la fase actual
    private TacticalPhasePolicy ObtenerPoliticaDeFase(TacticalPhase fase)
    {
        return TacticalPhasePolicy.For(fase);
    }

    // Comprobar si el guardia estaba en un behavior táctico antes de la deliberación actual
    private bool VeniaDeComportamientoTactico()
    {
        return creencias.EstadoActual == BehaviorType.Intercept ||
               creencias.EstadoActual == BehaviorType.Pursuit ||
               creencias.EstadoActual == BehaviorType.Search;
    }

    // Reducir la prioridad base según la distancia al objetivo: el guardia más cercano obtiene la prioridad más alta
    private float PrioridadPorCercania(float basePrioridad, Vector3 objetivo, float penalizacionPorMetro)
    {
        float distancia = Vector3.Distance(creencias.MiPosicion, objetivo);
        return basePrioridad - distancia * penalizacionPorMetro;
    }
}
