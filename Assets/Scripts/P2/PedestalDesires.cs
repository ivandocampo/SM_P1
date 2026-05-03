using System.Collections.Generic;
using UnityEngine;

public partial class DesireGenerator
{
    private void AgregarComprobacionPrioritariaPedestal(List<Desire> deseos)
    {
        if (!creencias.DebeComprobarPedestalPrioritario ||
            creencias.TieneTareaAsignada ||
            creencias.AnilloRobado ||
            !creencias.TienePosicionPedestal)
            return;

        deseos.Add(new Desire(
            BehaviorType.CheckPedestal,
            DesirePriorities.PriorityCheckPedestal,
            creencias.PosicionPedestal
        ));
    }

    private void AgregarDefensaPedestalSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        if (creencias.TieneTareaAsignada ||
            creencias.AnilloRobado ||
            !creencias.TienePosicionPedestal)
            return;

        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        if (!faseContactoTactico)
            return;

        if (!SoyCandidatoPedestal(fase))
            return;

        if (fase != TacticalPhase.RingSafeThiefKnown &&
            Time.time - creencias.UltimoChequeoPedestal < 6f)
            return;

        bool soyGuardaPedestal = creencias.EstadoActual == BehaviorType.CheckPedestal;
        if (!soyGuardaPedestal &&
            creencias.GuardiasEnEstado(BehaviorType.CheckPedestal) >= 1)
            return;

        float prioridad = faseContactoTactico ? DesirePriorities.TacticalCheckPedestal :
            PrioridadPorCercania(DesirePriorities.CheckPedestalByDistanceBase, creencias.PosicionPedestal, 0.18f);

        deseos.Add(new Desire(
            BehaviorType.CheckPedestal,
            prioridad,
            creencias.PosicionPedestal
        ));
    }

    private bool SoyCandidatoPedestal(TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefKnown)
            return creencias.SoyElMasCercanoA(creencias.PosicionPedestal);

        HashSet<string> excluir = new HashSet<string>();
        if (!string.IsNullOrEmpty(creencias.FuenteUltimaDeteccion))
            excluir.Add(creencias.FuenteUltimaDeteccion);

        if (creencias.TieneDeteccionPropiaReciente())
            excluir.Add(creencias.MiId);

        foreach (var par in creencias.EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.Pursuit.ToString())
                excluir.Add(par.Key);
        }

        return creencias
            .ObtenerIdsMasCercanosA(creencias.PosicionPedestal, 1, excluir)
            .Contains(creencias.MiId);
    }
}
