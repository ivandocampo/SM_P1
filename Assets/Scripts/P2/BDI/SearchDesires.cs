// =============================================================
// Fichero parcial de DesireGenerator: deseos de búsqueda libre.
// Genera el deseo Search(98) cuando el guardia venía de un behavior
// táctico y acaba de perder al ladrón (búsqueda local antes de
// coordinar con el equipo). Si el anillo ya fue robado, no genera
// búsqueda libre: la cobertura está repartida mediante Contract-Net
// y auto-asignación para no romper el esquema de bloqueo de salida
// =============================================================

using System.Collections.Generic;

public partial class DesireGenerator
{
    private void AgregarBusquedaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        if (creencias.BuscarLocalAntesDeCoordinar && faseContactoTactico)
        {
            // Si llegó información fresca de otro guardia, ceder el turno a Intercept/Pursuit
            if (creencias.AntiguedadInfoLadron < BeliefBase.TIEMPO_GRACIA_PERDIDA_LADRON)
                return;

            deseos.Add(new Desire(
                BehaviorType.Search,
                DesirePriorities.TacticalSearchAfterLoss,
                creencias.UltimaPosicionLadron
            ));
            return;
        }

        // Con el anillo robado, la búsqueda está cubierta por SearchAssigned vía Contract-Net
        if (creencias.AnilloRobado)
            return;

        return;
    }
}
