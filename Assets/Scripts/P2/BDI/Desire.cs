// =============================================================
// Define la clase Desire (un deseo BDI) y la entrada principal
// del generador de deseos. Un deseo representa una intención
// candidata con su tipo, prioridad y posición objetivo.
// GenerarDeseos() orquesta todos los ficheros parciales para
// producir la lista completa de deseos según la fase táctica
// =============================================================

using System.Collections.Generic;
using UnityEngine;

// Un deseo representa un comportamiento candidato con su urgencia y destino
[System.Serializable]
public class Desire
{
    public BehaviorType Nombre;
    public float Prioridad;       // Mayor valor = más urgente
    public Vector3 PosicionObjetivo;
    public string DatosExtra;     // Datos adicionales de contexto

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

    // Generar la lista completa de deseos para el frame actual según la fase táctica
    public List<Desire> GenerarDeseos()
    {
        List<Desire> deseos = new List<Desire>();
        TacticalPhase fase = creencias.FaseActual();
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        // Orden de prioridad: persecución > tarea asignada > request > intercepción > pedestal > búsqueda > bloqueo
        AgregarPersecucionSiProcede(deseos, fase);
        AgregarTareaAsignada(deseos);

        // Si hay tarea asignada y el ladrón está perdido con el anillo seguro, solo patrullar como fallback
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

        // Fallback táctico: si venía de un behavior táctico y no hay nada mejor, buscar localmente
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

        // Comprobar pedestal tras búsqueda local si el ladrón se perdió con el anillo seguro
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

        // Con el anillo robado, añadir fallback defensivo y terminar
        if (creencias.AnilloRobado)
        {
            AgregarFallbackDefensaSalida(deseos, fase);
            return deseos;
        }

        deseos.Add(new Desire(BehaviorType.Patrol, DesirePriorities.Patrol));
        return deseos;
    }

}
