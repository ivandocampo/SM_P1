using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Desire
{
    public BehaviorType Nombre;

    /// <summary>Prioridad del deseo. Mayor = mas urgente. Rango tipico: 0-100.</summary>
    public float Prioridad;

    /// <summary>Posicion objetivo asociada al deseo (si aplica).</summary>
    public Vector3 PosicionObjetivo;

    /// <summary>Datos adicionales del contexto (tarea asignada, request, etc.).</summary>
    public string DatosExtra;

    public Desire(BehaviorType nombre, float prioridad, Vector3 posicion = default, string datos = "")
    {
        Nombre = nombre;
        Prioridad = prioridad;
        PosicionObjetivo = posicion;
        DatosExtra = datos;
    }

    public override string ToString()
    {
        return $"{Nombre} (prioridad: {Prioridad:F0})";
    }
}

public partial class DesireGenerator
{
    private BeliefBase creencias;
    private const int MAX_BLOQUEADORES_SALIDA = TacticalConfig.MaxExitBlockers;

    public DesireGenerator(BeliefBase creencias)
    {
        this.creencias = creencias;
    }

    public List<Desire> GenerarDeseos()
    {
        List<Desire> deseos = new List<Desire>();
        TacticalPhase fase = creencias.FaseActual();
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        AgregarPersecucionSiProcede(deseos, fase);
        AgregarTareaAsignada(deseos);

        if (creencias.TieneTareaAsignada &&
            fase == TacticalPhase.RingSafeThiefLost)
        {
            deseos.Add(new Desire(BehaviorType.Patrol, DesirePriorities.Patrol));
            return deseos;
        }

        AgregarRequestAceptado(deseos);
        AgregarIntercepcionSiProcede(deseos, fase);
        AgregarComprobacionPrioritariaPedestal(deseos);
        AgregarDefensaPedestalSiProcede(deseos, fase);
        AgregarBusquedaSiProcede(deseos, fase);
        AgregarBloqueoSalidaSiProcede(deseos, fase);

        if (faseContactoTactico &&
            !creencias.LadronVisible &&
            VeniaDeComportamientoTactico() &&
            !deseos.Exists(d => d.Nombre != BehaviorType.Patrol))
        {
            deseos.Add(new Desire(
                BehaviorType.Search,
                DesirePriorities.TacticalFallbackSearch,
                creencias.UltimaPosicionLadron
            ));
        }

        if (creencias.ComprobarPedestalTrasBusquedaLocal &&
            fase == TacticalPhase.RingSafeThiefLost &&
            !creencias.TieneTareaAsignada &&
            !creencias.AnilloRobado &&
            creencias.TienePosicionPedestal)
        {
            deseos.Add(new Desire(BehaviorType.CheckPedestal, DesirePriorities.CheckPedestalAfterLocalSearch));
        }
        else if (creencias.DebeComprobarPedestal &&
                 !creencias.TieneTareaAsignada &&
                 !faseContactoTactico &&
                 !creencias.AnilloRobado &&
                 creencias.TienePosicionPedestal &&
                 !creencias.AlguienGuardandoPedestal() &&
                 creencias.SoyElMasCercanoA(creencias.PosicionPedestal))
        {
            deseos.Add(new Desire(BehaviorType.CheckPedestal, DesirePriorities.PeriodicCheckPedestal));
        }

        if (creencias.AnilloRobado)
        {
            AgregarFallbackDefensaSalida(deseos, fase);
            return deseos;
        }

        deseos.Add(new Desire(BehaviorType.Patrol, DesirePriorities.Patrol));
        return deseos;
    }

}
