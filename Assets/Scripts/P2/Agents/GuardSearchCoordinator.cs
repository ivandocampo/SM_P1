// =============================================================
// Fichero parcial de GuardAgent: coordinación de búsqueda distribuida.
// Cuando el ladrón se pierde de vista, determina qué guardia es el
// responsable de lanzar el Contract-Net y gestiona el retardo antes
// de iniciar la ronda. El responsable es el guardia más cercano a la
// última posición conocida; los empates se rompen por orden lexicográfico
// de ID para evitar rondas duplicadas sin coordinador central
// =============================================================

using UnityEngine;

public partial class GuardAgent
{
    private bool busquedaCoordinadaPendiente = false;
    private float tiempoPerdidaLadron = -100f;

    private const float RETARDO_BUSQUEDA_COORDINADA = BeliefBase.TIEMPO_INFO_TACTICA_LADRON;
    private const float DISTANCIA_MISMA_RONDA_BUSQUEDA = 10f;

    // Variables estáticas compartidas por todos los guardias para evitar rondas duplicadas
    private static float ultimaRondaBusquedaCoordinada = -100f;
    private static Vector3 ultimaPosicionRondaBusqueda = Vector3.positiveInfinity;
    private static string responsableBusquedaCoordinada = "";

    // Gestionar el flujo completo de la búsqueda coordinada: esperar, decidir responsable y lanzar Contract-Net
    private void GestionarBusquedaCoordinada(TacticalPhase fase, bool faseContactoTactico)
    {
        // Activar búsqueda coordinada si llegó un informe externo de ladrón perdido
        if (!busquedaCoordinadaPendiente &&
            creencias.PendienteBusquedaCoordinadaPorInformeExterno &&
            (fase == TacticalPhase.RingSafeThiefLost ||
             fase == TacticalPhase.RingStolenThiefLost))
        {
            busquedaCoordinadaPendiente = true;
            tiempoPerdidaLadron = creencias.TiempoUltimaDeteccion + BeliefBase.TIEMPO_INFO_TACTICA_LADRON;
            creencias.BuscarLocalAntesDeCoordinar = false;
        }

        // En fase de contacto táctico, hacer una búsqueda local antes de coordinar con el equipo
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

            // Si ya hay una ronda activa cerca de la misma posición, unirse sin lanzar otra
            if (EsMismaRondaBusquedaCoordinada())
            {
                creencias.ComprobarPedestalTrasBusquedaLocal = false;
                busquedaCoordinadaPendiente = false;
                creencias.PendienteBusquedaCoordinadaPorInformeExterno = false;
            }
            else if (SoyResponsableDeBusquedaCoordinada())
            {
                // Este guardia es el responsable: lanzar el Contract-Net
                bool contratoLanzado = contractNetManager.IniciarDistribucionBusqueda();
                if (contratoLanzado)
                {
                    ultimaRondaBusquedaCoordinada = Time.time;
                    ultimaPosicionRondaBusqueda = creencias.UltimaPosicionLadron;

                    // Si el anillo sigue seguro, el responsable va a comprobar el pedestal
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
                    // El Contract-Net no pudo lanzarse; reintentar en el siguiente frame
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

    // Marcar este guardia como candidato a responsable cuando detecta que el ladrón se perdió
    private void ReclamarResponsabilidadBusquedaCoordinada()
    {
        if (creencias.AnilloRobado) return;

        if (!busquedaCoordinadaPendiente)
            tiempoPerdidaLadron = Time.time;

        busquedaCoordinadaPendiente = true;
    }

    // Comprobar si ya existe una ronda de búsqueda activa cercana a la posición actual del ladrón
    private bool EsMismaRondaBusquedaCoordinada()
    {
        return Time.time - ultimaRondaBusquedaCoordinada < RETARDO_BUSQUEDA_COORDINADA &&
               Vector3.Distance(ultimaPosicionRondaBusqueda, creencias.UltimaPosicionLadron) < DISTANCIA_MISMA_RONDA_BUSQUEDA;
    }

    // Determinar si este guardia debe ser el iniciador del Contract-Net
    private bool SoyResponsableDeBusquedaCoordinada()
    {
        if (string.IsNullOrEmpty(responsableBusquedaCoordinada) ||
            !ResponsableBusquedaSigueValido(responsableBusquedaCoordinada))
        {
            responsableBusquedaCoordinada = CalcularResponsableBusquedaCoordinada();
        }

        return responsableBusquedaCoordinada == agentId;
    }

    // Verificar que el responsable previamente elegido sigue teniendo la búsqueda pendiente
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

    // Elegir el guardia más cercano a la última posición del ladrón como responsable del Contract-Net
    // En caso de empate de distancia, se desempata por orden lexicográfico del ID
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
