// =============================================================
// Fichero parcial de GuardAgent: capa de comunicación reactiva.
// Consume los flags pendientes que activó la capa de percepción
// y decide qué mensajes FIPA-ACL enviar ese frame al equipo.
// Separa la percepción de la comunicación: los sensores nunca
// llaman directamente a ProtocolHandler, solo activan flags
// =============================================================

using UnityEngine;

public partial class GuardAgent
{
    // Revisar los flags de comunicación pendiente y enviar los mensajes correspondientes
    private void GestionarComunicacionReactiva()
    {
        // Informar avistamiento continuo del ladrón con throttle para no saturar el canal
        if (creencias.LadronVisible)
            InformarAvistamientoSiProcede();

        // Comunicar al equipo que el ladrón se perdió de vista e iniciar búsqueda coordinada
        if (creencias.PendienteComunicarLadronPerdido)
        {
            protocolHandler.InformarPredicado(PredicateType.THIEF_LOST);
            creencias.PendienteComunicarLadronPerdido = false;
        }

        TacticalPhase fase = creencias.FaseActual();
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        GestionarBusquedaCoordinada(fase, faseContactoTactico);

        // Comunicar al equipo que el anillo ha desaparecido del pedestal
        if (creencias.PendienteComunicarAnilloDesaparecido)
        {
            protocolHandler.InformarPredicado(PredicateType.RING_STOLEN);
            creencias.PendienteComunicarAnilloDesaparecido = false;
        }

        // Comunicar al equipo que se ha visto al ladrón portando el anillo
        if (creencias.PendienteComunicarLadronConAnillo)
        {
            protocolHandler.InformarPredicado(PredicateType.RING_STOLEN, GameConstants.PredicateExtras.SeenCarryingRing);
            creencias.PendienteComunicarLadronConAnillo = false;
        }
    }

    // Enviar un informe de avistamiento al equipo
    private void InformarAvistamientoSiProcede()
    {
        bool forzar = creencias.PrimerAvistamiento;
        if (!forzar && Time.time - ultimoInformeAvistamiento < intervaloActualizacionAvistamiento)
            return;

        ultimoInformeAvistamiento = Time.time;
        creencias.PrimerAvistamiento = false;

        protocolHandler.InformarAvistamiento(new ThiefSighting
        {
            Location     = new Position(creencias.UltimaPosicionLadron),
            Direction    = creencias.TieneDireccionLadron
                           ? new Position(creencias.UltimaDireccionLadron) : null,
            Timestamp    = Time.time,
            ReportedBy   = agentId,
            DirectVision = true
        });
    }
}
