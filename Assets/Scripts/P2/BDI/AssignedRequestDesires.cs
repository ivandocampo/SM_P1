// =============================================================
// Fichero parcial de DesireGenerator: deseos procedentes de
// coordinación entre guardias. Genera deseos de SearchAssigned
// cuando el Contract-Net asigna una zona a este guardia, y deseos
// derivados de un REQUEST aceptado de otro guardia (bloquear salida,
// buscar zona o búsqueda libre según la acción solicitada)
// =============================================================

using System.Collections.Generic;
using UnityEngine;

public partial class DesireGenerator
{
    // Generar deseo de búsqueda en zona asignada por Contract-Net
    private void AgregarTareaAsignada(List<Desire> deseos)
    {
        if (!creencias.TieneTareaAsignada) return;

        // Solo es relevante en fases de ladrón perdido
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

    // Generar deseo a partir de un REQUEST aceptado de otro guardia
    private void AgregarRequestAceptado(List<Desire> deseos)
    {
        if (!creencias.TieneRequestPendiente) return;

        string accion = creencias.RequestAceptado.Action;
        Vector3 pos = creencias.RequestAceptado.TargetPosition != null
            ? creencias.RequestAceptado.TargetPosition.ToVector3()
            : Vector3.zero;

        // Traducir la acción del REQUEST al BehaviorType correspondiente
        BehaviorType nombreDeseo =
            accion == ActionType.BLOCK_EXIT.ToString() ? BehaviorType.BlockExit :
            accion == ActionType.SEARCH_AREA.ToString() ? BehaviorType.SearchAssigned :
            BehaviorType.Search;

        deseos.Add(new Desire(nombreDeseo, DesirePriorities.AcceptedRequest, pos, creencias.ConversacionRequest));
    }
}
