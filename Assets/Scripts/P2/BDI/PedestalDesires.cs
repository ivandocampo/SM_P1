// =============================================================
// Fichero parcial de DesireGenerator: deseos relacionados con
// la vigilancia del pedestal del Anillo. Genera tres variantes:
// comprobación prioritaria (el responsable del Contract-Net va al
// pedestal), defensa táctica cuando el ladrón está localizado
// (el guardia más cercano excluido del rol de perseguidor), y
// comprobación periódica en patrulla normal.
// =============================================================

using System.Collections.Generic;
using UnityEngine;

public partial class DesireGenerator
{
    // Generar deseo de comprobación urgente del pedestal tras lanzar un Contract-Net
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

    // Generar deseo de vigilancia del pedestal en fases de contacto táctico
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

        // Evitar vigilancias repetidas si el pedestal fue comprobado recientemente
        if (fase != TacticalPhase.RingSafeThiefKnown &&
            Time.time - creencias.UltimoChequeoPedestal < 6f)
            return;

        // No generar este deseo si otro guardia ya está vigilando el pedestal
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

    // Determinar si este guardia es el candidato para vigilar el pedestal en la fase actual
    private bool SoyCandidatoPedestal(TacticalPhase fase)
    {
        if (fase != TacticalPhase.RingSafeThiefKnown)
            return creencias.SoyElMasCercanoA(creencias.PosicionPedestal);

        // En RingSafeThiefKnown, excluir al perseguidor y al que detectó al ladrón
        HashSet<string> excluir = new HashSet<string>();
        if (!string.IsNullOrEmpty(creencias.FuenteUltimaDeteccion))
            excluir.Add(creencias.FuenteUltimaDeteccion);

        // Si este guardia tiene detección propia reciente, también se excluye (irá a perseguir)
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
