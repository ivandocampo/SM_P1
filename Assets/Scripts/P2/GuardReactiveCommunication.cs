using UnityEngine;

public partial class GuardAgent
{
    // CAPA DE COMUNICACION REACTIVA
    // Consume los flags pendientes y decide que mensajes FIPA enviar.

    private void GestionarComunicacionReactiva()
    {
        // Avistamiento continuo del ladron: con throttle, forzado en primera deteccion.
        if (creencias.LadronVisible)
            InformarAvistamientoSiProcede();

        // Ladron perdido de vista: comunicar al equipo e iniciar busqueda coordinada.
        if (creencias.PendienteComunicarLadronPerdido)
        {
            protocolHandler.InformarPredicado(PredicateType.THIEF_LOST);
            creencias.PendienteComunicarLadronPerdido = false;
        }

        TacticalPhase fase = creencias.FaseActual();
        bool faseContactoTactico = fase == TacticalPhase.RingSafeThiefKnown ||
                                   fase == TacticalPhase.RingStolenThiefKnown;

        GestionarBusquedaCoordinada(fase, faseContactoTactico);

        // Anillo desaparecido del pedestal: alertar al equipo.
        if (creencias.PendienteComunicarAnilloDesaparecido)
        {
            protocolHandler.InformarPredicado(PredicateType.RING_STOLEN);
            creencias.PendienteComunicarAnilloDesaparecido = false;
        }

        // Ladron visto portando el anillo: alertar al equipo con contexto adicional.
        if (creencias.PendienteComunicarLadronConAnillo)
        {
            protocolHandler.InformarPredicado(PredicateType.RING_STOLEN, GameConstants.PredicateExtras.SeenCarryingRing);
            creencias.PendienteComunicarLadronConAnillo = false;
        }
    }

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
