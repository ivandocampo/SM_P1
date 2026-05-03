using UnityEngine;

public partial class GuardAgent
{
    // CAPA DE PERCEPCION - Manejadores de sensores
    // Solo actualizan creencias y activan flags de comunicacion pendiente.
    // Ningun sensor llama directamente a ProtocolHandler ni a ContractNetManager.

    private void OnLadronVisto(Vector3 posicion)
    {
        Vector3 direccion = CalcularDireccionObservada(posicion);
        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId,
            direccion, direccion.sqrMagnitude > 0.01f);
        creencias.LimpiarTarea();
        creencias.LimpiarRequest();
        creencias.PrimerAvistamiento = true;
        busquedaCoordinadaPendiente = false;
        creencias.PendienteBusquedaCoordinadaPorInformeExterno = false;
        creencias.BuscarLocalAntesDeCoordinar = false;
        creencias.ComprobarPedestalTrasBusquedaLocal = false;
        creencias.DebeComprobarPedestalPrioritario = false;
        deliberacionPendiente = true; // Pursuit(100) puede entrar en juego
        ComprobarSiLlevaAnillo();
    }

    private void OnLadronSigueVisible(Vector3 posicion)
    {
        Vector3 direccion = CalcularDireccionObservada(posicion);
        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId,
            direccion, direccion.sqrMagnitude > 0.01f);
        creencias.LimpiarTarea();
        creencias.LimpiarRequest();
        busquedaCoordinadaPendiente = false;
        creencias.PendienteBusquedaCoordinadaPorInformeExterno = false;
        creencias.BuscarLocalAntesDeCoordinar = false;
        creencias.ComprobarPedestalTrasBusquedaLocal = false;
        creencias.DebeComprobarPedestalPrioritario = false;
        ComprobarSiLlevaAnillo();
        // No fuerza deliberacion: ya estamos en Pursuit, solo actualizamos posicion
    }

    private void OnLadronPerdido()
    {
        creencias.MarcarLadronPerdido();
        creencias.PendienteComunicarLadronPerdido = true;
        busquedaCoordinadaPendiente = true;
        creencias.BuscarLocalAntesDeCoordinar = false;
        creencias.ComprobarPedestalTrasBusquedaLocal = !creencias.AnilloRobado;
        tiempoPerdidaLadron = Time.time;
        ReclamarResponsabilidadBusquedaCoordinada();
        deliberacionPendiente = true; // Pursuit desaparece, Search/BlockExit entran
    }

    private void OnAnilloDesaparecido()
    {
        creencias.MarcarAnilloRobado(); // activa NecesitaDeliberar internamente
        creencias.DebeComprobarPedestal = false;
        creencias.DebeComprobarPedestalPrioritario = false;
        creencias.PendienteComunicarAnilloDesaparecido = true;
        Debug.Log($"[{agentId}] Anillo robado detectado");
    }

    private void OnSonidoDetectado(Vector3 posicion)
    {
        creencias.ActualizarPosicionLadron(posicion, Time.time, false, agentId);
    }

    private void ComprobarSiLlevaAnillo()
    {
        if (sensorVision == null ||
            !sensorVision.ObjetivoVisibleConAnillo ||
            creencias.LadronVistoConAnillo) return;

        creencias.MarcarLadronConAnillo();
        creencias.PendienteComunicarLadronConAnillo = true;
        Debug.Log($"[{agentId}] Ladrón visto llevando el anillo");
    }

    private Vector3 CalcularDireccionObservada(Vector3 nuevaPosicion)
    {
        if (!creencias.TieneInfoReciente(2f)) return Vector3.zero;

        Vector3 delta = nuevaPosicion - creencias.UltimaPosicionLadron;
        if (delta.sqrMagnitude < 0.04f) return Vector3.zero;

        delta.y = 0f;
        return delta.normalized;
    }
}
