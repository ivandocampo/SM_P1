// =============================================================
// Fichero parcial de GuardAgent: capa de percepción.
// Contiene los manejadores de eventos de los sensores (visión y oído).
// Importante: estos métodos SOLO actualizan creencias y activan flags;
// nunca llaman directamente a ProtocolHandler ni a ContractNetManager.
// La separación garantiza que percepción y comunicación estén desacopladas
// =============================================================

using UnityEngine;

public partial class GuardAgent
{
    // Manejador del evento OnObjetivoDetectado: Frodo pasa de no visible a visible
    private void OnLadronVisto(Vector3 posicion)
    {
        Vector3 direccion = CalcularDireccionObservada(posicion);
        creencias.ActualizarPosicionLadron(posicion, Time.time, true, agentId,
            direccion, direccion.sqrMagnitude > 0.01f);
        // Cancelar cualquier tarea o búsqueda coordinada en curso
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

    // Manejador del evento OnObjetivoVisible: Frodo sigue siendo visible (actualización continua)
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
        // No forzar deliberación: ya está en Pursuit, solo se actualiza la posición
    }

    // Manejador del evento OnObjetivoPerdido: Frodo desaparece del campo de visión
    private void OnLadronPerdido()
    {
        creencias.MarcarLadronPerdido();
        creencias.PendienteComunicarLadronPerdido = true;
        busquedaCoordinadaPendiente = true;
        creencias.BuscarLocalAntesDeCoordinar = false;
        // Si el anillo sigue en el pedestal, comprobar si aún está al llegar
        creencias.ComprobarPedestalTrasBusquedaLocal = !creencias.AnilloRobado;
        tiempoPerdidaLadron = Time.time;
        ReclamarResponsabilidadBusquedaCoordinada();
        deliberacionPendiente = true; // Pursuit desaparece, Search/BlockExit entran
    }

    // Manejador del evento OnAnilloDesaparecido: el pedestal está vacío
    private void OnAnilloDesaparecido()
    {
        creencias.MarcarAnilloRobado(); // activa NecesitaDeliberar internamente
        creencias.DebeComprobarPedestal = false;
        creencias.DebeComprobarPedestalPrioritario = false;
        creencias.PendienteComunicarAnilloDesaparecido = true;
        Debug.Log($"[{agentId}] Anillo robado detectado");
    }

    // Manejador del evento OnSonidoDetectado: actualizar posición del ladrón sin visión directa
    private void OnSonidoDetectado(Vector3 posicion)
    {
        creencias.ActualizarPosicionLadron(posicion, Time.time, false, agentId);
    }

    // Comprobar si Frodo es visible portando el anillo y marcar la creencia correspondiente
    private void ComprobarSiLlevaAnillo()
    {
        if (sensorVision == null ||
            !sensorVision.ObjetivoVisibleConAnillo ||
            creencias.LadronVistoConAnillo) return;

        creencias.MarcarLadronConAnillo();
        creencias.PendienteComunicarLadronConAnillo = true;
        Debug.Log($"[{agentId}] Ladrón visto llevando el anillo");
    }

    // Calcular la dirección de movimiento del ladrón comparando su posición actual con la anterior
    private Vector3 CalcularDireccionObservada(Vector3 nuevaPosicion)
    {
        if (!creencias.TieneInfoReciente(2f)) return Vector3.zero;

        Vector3 delta = nuevaPosicion - creencias.UltimaPosicionLadron;
        // Ignorar desplazamientos mínimos para no calcular direcciones con ruido
        if (delta.sqrMagnitude < 0.04f) return Vector3.zero;

        delta.y = 0f;
        return delta.normalized;
    }
}
