using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Gestiona el protocolo FIPA-Contract-Net desde el lado del manager (iniciador).
// Cuando el ladrón se pierde de vista, distribuye zonas de búsqueda entre los guardias
// disponibles eligiendo al candidato con menor coste (distancia).
//
// Distribución multi-zona: en una misma ronda se abren tantos contratos como zonas
// candidatas haya (limitado por el número de guardias disponibles). Cuando todos
// los contratos están listos para evaluar, se hace un matching greedy global:
// la zona más prioritaria recibe al guardia más barato, la siguiente al siguiente
// más barato no asignado, y así sucesivamente. Cada guardia recibe como máximo
// una zona por ronda.
public class ContractNetManager
{
    private BeliefBase creencias;
    private ComunicacionAgente comunicacion;
    private string agentId;
    private float cooldown;
    private float ultimoContractNet = -100f;

    // Contratos abiertos en la ronda actual (uno por zona).
    private List<ContractNetEstado> contratosActivos = new List<ContractNetEstado>();

    public ContractNetManager(BeliefBase creencias, ComunicacionAgente comunicacion,
                               string id, float cooldown)
    {
        this.creencias    = creencias;
        this.comunicacion = comunicacion;
        this.agentId      = id;
        this.cooldown     = cooldown;

        comunicacion.OnPropuestaRecibida += AlimentarContrato;
        comunicacion.OnRefuseRecibido    += AlimentarContrato;
    }

    public void Limpiar()
    {
        comunicacion.OnPropuestaRecibida -= AlimentarContrato;
        comunicacion.OnRefuseRecibido    -= AlimentarContrato;
    }

    // Inicia una ronda de Contract Net distribuyendo todas las zonas candidatas
    // entre los guardias disponibles. Solo lo lanza el guardia más cercano al
    // ladrón (con tiebreaker lexicográfico por ID), evitando rondas simultáneas
    // sin coordinador central.
    public bool IniciarDistribucionBusqueda()
    {
        float cooldownEfectivo = creencias.AnilloRobado ? 3f : 0f;
        if (Time.time - ultimoContractNet < cooldownEfectivo) return false;
        if (contratosActivos.Count > 0) return false;

        List<string> participantes = SeleccionarParticipantes();

        if (participantes.Count == 0) return false;

        List<string> zonas = SeleccionarZonasParaContratar(participantes.Count);
        if (zonas.Count == 0)
        {
            Debug.LogWarning($"[{agentId}] Contract Net sin zonas candidatas para la fase actual");
            return false;
        }

        ultimoContractNet = Time.time;

        foreach (string zoneId in zonas)
        {
            AbrirContratoParaZona(zoneId, participantes);
        }

        Debug.Log($"[{agentId}] Contract Net iniciado: zonas=[{string.Join(", ", zonas)}], participantes=[{string.Join(", ", participantes)}]");
        return true;
    }

    private List<string> SeleccionarParticipantes()
    {
        List<string> participantes = AgentRegistry.Instance.ObtenerIdsPorTipo(GameConstants.AgentTypes.Guard);

        if (!creencias.AnilloRobado)
        {
            // Fase 3: el iniciador coordina la busqueda y va al pedestal.
            participantes.Remove(agentId);
            return participantes;
        }

        // Fase 5: los dos bloqueadores preservan la salida; el iniciador solo
        // participa si no es uno de ellos.
        HashSet<string> bloqueadoresSalida = creencias.ObtenerIdsBloqueadoresSalidaEstables(2);
        participantes.RemoveAll(id => bloqueadoresSalida.Contains(id));
        return participantes;
    }

    // Evalúa los contratos abiertos cuando todos están listos y hace el matching
    // global. Llamar desde Update().
    public void Gestionar()
    {
        if (contratosActivos.Count == 0) return;
        if (contratosActivos.Any(c => !c.ListoParaEvaluar())) return;

        // Matching greedy: cada guardia recibe como máximo una zona.
        // Las zonas se procesan en el orden en que se seleccionaron (más
        // prioritarias primero), así la zona crítica obtiene su mejor postor.
        HashSet<string> guardiasYaAsignados = new HashSet<string>();

        foreach (ContractNetEstado contrato in contratosActivos)
        {
            ACLMessage ganador = ElegirMejorPropuesta(contrato.Propuestas, guardiasYaAsignados);

            if (ganador != null)
            {
                AdjudicarZona(ganador, contrato.ContenidoTarea);
                guardiasYaAsignados.Add(ganador.Sender);
            }
            else
            {
                Debug.LogWarning($"[{agentId}] Contract Net sin ganador para tarea {contrato.ContenidoTarea}");
            }

            RechazarPerdedores(contrato.Propuestas, ganador);
        }

        contratosActivos.Clear();
    }

    // SELECCIÓN DE ZONAS

    // Devuelve hasta maxZonas zonas a contratar, filtradas y ordenadas según
    // el estado del mundo:
    //   - Anillo robado: prioriza zonas "Exit_*" cercanas a la salida.
    //   - Anillo en pedestal: cualquier zona, ordenada por cercanía al ladrón.
    // Si el filtrado por prefijo deja la lista vacía, recurre a todas las zonas
    // para no bloquearse por convención de nombres.
    private List<string> SeleccionarZonasParaContratar(int maxZonas)
    {
        List<string> zonasDisponibles = creencias.ObtenerIdsZonasBusqueda();
        if (zonasDisponibles.Count == 0) return new List<string>();

        IEnumerable<string> candidatas = zonasDisponibles;
        Vector3 referencia = creencias.UltimaPosicionLadron;

        if (creencias.AnilloRobado)
        {
            var exitZones = zonasDisponibles
                .Where(EsZonaSalida)
                .ToList();
            if (exitZones.Count > 0) candidatas = exitZones;

            if (creencias.TienePosicionSalida)
                referencia = creencias.PosicionSalida;
        }
        else
        {
            var ringZones = zonasDisponibles
                .Where(EsZonaAnillo)
                .ToList();

            if (creencias.TienePosicionPedestal)
                referencia = creencias.PosicionPedestal;

            candidatas = ringZones;
        }

        // Orden primario: zona más antigua sin barrer (rotación natural).
        // Orden secundario: cercanía al punto de referencia (ladrón o salida).
        return candidatas
            .OrderBy(z => creencias.ObtenerTiempoUltimaBusqueda(z))
            .ThenBy(z => Vector3.Distance(creencias.ObtenerCentroZona(z), referencia))
            .Take(maxZonas)
            .ToList();
    }

    private bool EsZonaSalida(string zoneId)
    {
        return !string.IsNullOrEmpty(zoneId) &&
               (zoneId.StartsWith(GameConstants.ZonePrefixes.Exit, System.StringComparison.OrdinalIgnoreCase) ||
                zoneId.StartsWith(GameConstants.ZonePrefixes.ExitAlt, System.StringComparison.OrdinalIgnoreCase));
    }

    private bool EsZonaAnillo(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return false;

        return zoneId.StartsWith(GameConstants.ZonePrefixes.Ring, System.StringComparison.OrdinalIgnoreCase);
    }

    // APERTURA DE CONTRATO Y ENVÍO DE CFP

    private void AbrirContratoParaZona(string zoneId, List<string> participantes)
    {
        Vector3 centroZona = creencias.ObtenerCentroZona(zoneId);

        SearchTask tarea = new SearchTask
        {
            TaskId     = $"search-{agentId}-{zoneId}-{Time.time:F0}",
            ZoneId     = zoneId,
            TargetArea = new Position(centroZona),
            Radius     = 15f,
            Urgency    = 0.8f
        };

        string convId = comunicacion.NuevaConversacion();
        ContractNetEstado estado = new ContractNetEstado(convId, participantes, timeout: 2f);
        estado.SetContenidoTarea(ContentLanguage.Encode(tarea));
        contratosActivos.Add(estado);

        foreach (string receptor in participantes)
        {
            ACLMessage cfp = new ACLMessage(ACLPerformative.CFP, agentId, receptor);
            cfp.Content        = ContentLanguage.Encode(tarea);
            cfp.Protocol       = GameConstants.Protocols.ContractNet;
            cfp.ConversationId = convId;
            comunicacion.Enviar(cfp);
        }
    }

    // MATCHING

    private ACLMessage ElegirMejorPropuesta(List<ACLMessage> propuestas, HashSet<string> excluir)
    {
        return propuestas
            .Where(p => !excluir.Contains(p.Sender))
            .OrderBy(p => ContentLanguage.DecodeProposal(p.Content)?.Cost ?? float.MaxValue)
            .FirstOrDefault();
    }

    private void AdjudicarZona(ACLMessage ganador, string contenidoTarea)
    {
        ACLMessage accept = ganador.CreateReply(ACLPerformative.ACCEPT_PROPOSAL);
        accept.Content = contenidoTarea;
        comunicacion.Enviar(accept);
        Debug.Log($"[{agentId}] Zona adjudicada a {ganador.Sender}");
    }

    private void RechazarPerdedores(List<ACLMessage> propuestas, ACLMessage ganador)
    {
        foreach (ACLMessage propuesta in propuestas)
        {
            if (ganador == null || propuesta.Sender != ganador.Sender)
                comunicacion.Enviar(propuesta.CreateReply(ACLPerformative.REJECT_PROPOSAL));
        }
    }

    // ENRUTADO DE RESPUESTAS

    // Las propuestas y rechazos llegan como eventos del buzón. Se enrutan al
    // contrato correspondiente por ConversationId — un mismo guardia puede haber
    // recibido CFPs de varias zonas en la misma ronda y responder a cada una.
    private void AlimentarContrato(ACLMessage msg)
    {
        foreach (ContractNetEstado contrato in contratosActivos)
        {
            if (msg.ConversationId == contrato.ConversationId)
            {
                contrato.RecibirRespuesta(msg);
                return;
            }
        }
    }

}

// Seguimiento del estado de una negociación Contract Net activa para una zona.
// Una ronda multi-zona contiene varios estados, uno por zona contratada.
public class ContractNetEstado
{
    public string ConversationId  { get; private set; }
    public List<ACLMessage> Propuestas { get; private set; } = new List<ACLMessage>();
    public string ContenidoTarea  { get; private set; }

    private List<string> pendientes;
    private float deadline;

    public ContractNetEstado(string convId, List<string> participantes, float timeout)
    {
        ConversationId = convId;
        pendientes     = new List<string>(participantes);
        deadline       = Time.time + timeout;
    }

    public void SetContenidoTarea(string contenido) => ContenidoTarea = contenido;

    public void RecibirRespuesta(ACLMessage msg)
    {
        if (msg.ConversationId != ConversationId) return;
        if (msg.Performative == ACLPerformative.PROPOSE)
            Propuestas.Add(msg);
        pendientes.Remove(msg.Sender);
    }

    // Listo cuando todos respondieron o se agotó el tiempo
    public bool ListoParaEvaluar() => pendientes.Count == 0 || Time.time > deadline;
}
