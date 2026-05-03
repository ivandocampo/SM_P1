using System.Collections.Generic;
using UnityEngine;

public partial class DesireGenerator
{
    private void AgregarTareaAsignada(List<Desire> deseos)
    {
        if (!creencias.TieneTareaAsignada) return;

        TacticalPhase fase = creencias.FaseActual();
        if (fase != TacticalPhase.RingSafeThiefLost &&
            fase != TacticalPhase.RingStolenThiefLost)
            return;

        deseos.Add(new Desire(
            BehaviorType.SearchAssigned,
            DesirePriorities.SearchAssigned,
            creencias.TareaAsignada.TargetArea.ToVector3(),
            creencias.ConversacionTareaAsignada
        ));
    }

    private void AgregarRequestAceptado(List<Desire> deseos)
    {
        if (!creencias.TieneRequestPendiente) return;

        string accion = creencias.RequestAceptado.Action;
        Vector3 pos = creencias.RequestAceptado.TargetPosition != null
            ? creencias.RequestAceptado.TargetPosition.ToVector3()
            : Vector3.zero;

        BehaviorType nombreDeseo =
            accion == ActionType.BLOCK_EXIT.ToString() ? BehaviorType.BlockExit :
            accion == ActionType.SEARCH_AREA.ToString() ? BehaviorType.SearchAssigned :
            BehaviorType.Search;

        deseos.Add(new Desire(nombreDeseo, DesirePriorities.AcceptedRequest, pos, creencias.ConversacionRequest));
    }
}
