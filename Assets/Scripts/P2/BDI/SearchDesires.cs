using System.Collections.Generic;

public partial class DesireGenerator
{
    private void AgregarBusquedaSiProcede(List<Desire> deseos, TacticalPhase fase)
    {
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        if (creencias.BuscarLocalAntesDeCoordinar && faseContactoTactico)
        {
            // Si llego informacion fresca (otro guardia ve a Frodo), abandonamos
            // la busqueda local para que Intercept/Pursuit tomen el relevo.
            // Solo generamos Search(98) cuando nadie ha visto al ladron en la
            // ventana de gracia (1.5s).
            if (creencias.AntiguedadInfoLadron < BeliefBase.TIEMPO_GRACIA_PERDIDA_LADRON)
                return;

            deseos.Add(new Desire(
                BehaviorType.Search,
                DesirePriorities.TacticalSearchAfterLoss,
                creencias.UltimaPosicionLadron
            ));
            return;
        }

        // En las fases de busqueda coordinada, la busqueda activa esta cubierta
        // por SearchAssigned. En Fase 5 no generamos Search libre porque rompe
        // el reparto 2 BlockExit + 3 zonas Exit_ durante pequenos huecos de
        // autoasignacion.
        if (creencias.AnilloRobado)
            return;

        return;
    }
}
