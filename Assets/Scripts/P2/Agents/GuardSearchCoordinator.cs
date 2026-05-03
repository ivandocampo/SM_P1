using UnityEngine;

public partial class GuardAgent
{
    private bool busquedaCoordinadaPendiente = false;
    private float tiempoPerdidaLadron = -100f;

    private const float RETARDO_BUSQUEDA_COORDINADA = BeliefBase.TIEMPO_INFO_TACTICA_LADRON;
    private const float DISTANCIA_MISMA_RONDA_BUSQUEDA = 10f;
    private static float ultimaRondaBusquedaCoordinada = -100f;
    private static Vector3 ultimaPosicionRondaBusqueda = Vector3.positiveInfinity;
    private static string responsableBusquedaCoordinada = "";

    private void GestionarBusquedaCoordinada(TacticalPhase fase, bool faseContactoTactico)
    {
        if (!busquedaCoordinadaPendiente &&
            creencias.PendienteBusquedaCoordinadaPorInformeExterno &&
            (fase == TacticalPhase.RingSafeThiefLost ||
             fase == TacticalPhase.RingStolenThiefLost))
        {
            busquedaCoordinadaPendiente = true;
            tiempoPerdidaLadron = creencias.TiempoUltimaDeteccion + BeliefBase.TIEMPO_INFO_TACTICA_LADRON;
            creencias.BuscarLocalAntesDeCoordinar = false;
        }

        if (busquedaCoordinadaPendiente &&
            faseContactoTactico &&
            !creencias.BuscarLocalAntesDeCoordinar &&
            Time.time - tiempoPerdidaLadron >= BeliefBase.TIEMPO_GRACIA_PERDIDA_LADRON)
        {
            creencias.BuscarLocalAntesDeCoordinar = true;
            creencias.NecesitaDeliberar = true;
        }

        if (busquedaCoordinadaPendiente &&
            (fase == TacticalPhase.RingSafeThiefLost ||
             fase == TacticalPhase.RingStolenThiefLost))
        {
            creencias.BuscarLocalAntesDeCoordinar = false;

            if (EsMismaRondaBusquedaCoordinada())
            {
                creencias.ComprobarPedestalTrasBusquedaLocal = false;
                busquedaCoordinadaPendiente = false;
                creencias.PendienteBusquedaCoordinadaPorInformeExterno = false;
            }
            else if (SoyResponsableDeBusquedaCoordinada())
            {
                bool contratoLanzado = contractNetManager.IniciarDistribucionBusqueda();
                if (contratoLanzado)
                {
                    ultimaRondaBusquedaCoordinada = Time.time;
                    ultimaPosicionRondaBusqueda = creencias.UltimaPosicionLadron;

                    if (!creencias.AnilloRobado)
                    {
                        responsableBusquedaCoordinada = "";
                        creencias.DebeComprobarPedestalPrioritario = true;
                        creencias.ComprobarPedestalTrasBusquedaLocal = false;
                        selectorIntenciones.ForzarReset();
                        deliberacionPendiente = true;
                    }

                    busquedaCoordinadaPendiente = false;
                    creencias.PendienteBusquedaCoordinadaPorInformeExterno = false;
                }
                else
                {
                    responsableBusquedaCoordinada = "";
                    busquedaCoordinadaPendiente = true;
                    tiempoPerdidaLadron = Time.time - BeliefBase.TIEMPO_INFO_TACTICA_LADRON;
                    return;
                }
            }
            else
            {
                creencias.ComprobarPedestalTrasBusquedaLocal = false;
            }
        }
    }

    private void ReclamarResponsabilidadBusquedaCoordinada()
    {
        if (creencias.AnilloRobado) return;

        if (!busquedaCoordinadaPendiente)
            tiempoPerdidaLadron = Time.time;

        busquedaCoordinadaPendiente = true;
    }

    private bool EsMismaRondaBusquedaCoordinada()
    {
        return Time.time - ultimaRondaBusquedaCoordinada < RETARDO_BUSQUEDA_COORDINADA &&
               Vector3.Distance(ultimaPosicionRondaBusqueda, creencias.UltimaPosicionLadron) < DISTANCIA_MISMA_RONDA_BUSQUEDA;
    }

    private bool SoyResponsableDeBusquedaCoordinada()
    {
        if (string.IsNullOrEmpty(responsableBusquedaCoordinada) ||
            !ResponsableBusquedaSigueValido(responsableBusquedaCoordinada))
        {
            responsableBusquedaCoordinada = CalcularResponsableBusquedaCoordinada();
        }

        return responsableBusquedaCoordinada == agentId;
    }

    private bool ResponsableBusquedaSigueValido(string responsableId)
    {
        if (string.IsNullOrEmpty(responsableId))
            return false;

        if (responsableId == agentId)
            return busquedaCoordinadaPendiente;

        ComunicacionAgente comunicacionResponsable = AgentRegistry.Instance.ObtenerAgente(responsableId);
        if (comunicacionResponsable == null)
            return false;

        GuardAgent guardiaResponsable = comunicacionResponsable.GetComponent<GuardAgent>();
        return guardiaResponsable != null && guardiaResponsable.busquedaCoordinadaPendiente;
    }

    private string CalcularResponsableBusquedaCoordinada()
    {
        string mejorId = agentId;
        float miDistancia = Vector3.Distance(transform.position, creencias.UltimaPosicionLadron);
        float mejorDistancia = miDistancia;

        foreach (var par in AgentRegistry.Instance.ObtenerIdsPorTipo(GameConstants.AgentTypes.Guard))
        {
            if (par == agentId) continue;

            ComunicacionAgente otro = AgentRegistry.Instance.ObtenerAgente(par);
            if (otro == null) continue;

            GuardAgent otroGuardia = otro.GetComponent<GuardAgent>();
            if (otroGuardia == null || !otroGuardia.busquedaCoordinadaPendiente)
                continue;

            float suDistancia = Vector3.Distance(otroGuardia.transform.position, creencias.UltimaPosicionLadron);
            bool estaMasCerca = suDistancia < mejorDistancia - 0.25f;
            bool empataConMejorId = Mathf.Abs(suDistancia - mejorDistancia) <= 0.25f &&
                                    string.Compare(otroGuardia.agentId, mejorId, System.StringComparison.Ordinal) < 0;

            if (estaMasCerca || empataConMejorId)
            {
                mejorId = otroGuardia.agentId;
                mejorDistancia = suDistancia;
            }
        }

        Debug.Log($"[{agentId}] Responsable CN fase anillo elegido: {mejorId}");
        return mejorId;
    }
}
