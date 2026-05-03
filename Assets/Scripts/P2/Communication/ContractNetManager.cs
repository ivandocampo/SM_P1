// =============================================================
// Gestor del protocolo FIPA-Contract-Net desde el lado iniciador.
// Cuando Frodo se pierde, abre contratos para repartir zonas de
// busqueda entre guardias disponibles. Cada contrato corresponde
// a una zona y se resuelve por propuestas de coste, aplicando un
// matching greedy para asignar como maximo una zona a cada guardia
// =============================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ContractNetManager
{
    // Referencias necesarias para leer el estado BDI y enviar mensajes ACL
    private BeliefBase creencias;
    private ComunicacionAgente comunicacion;
    private string agentId;
    // Cooldown configurado desde fuera; se conserva aunque el cooldown efectivo depende de la fase
    private float cooldown;
    private float ultimoContractNet = -100f;

    // Contratos abiertos en la ronda actual (uno por zona)
    private List<ContractNetEstado> contratosActivos = new List<ContractNetEstado>();

    public ContractNetManager(BeliefBase creencias, ComunicacionAgente comunicacion,
                               string id, float cooldown)
    {
        // El manager no es MonoBehaviour: recibe sus dependencias desde GuardAgent
        this.creencias    = creencias;
        this.comunicacion = comunicacion;
        this.agentId      = id;
        this.cooldown     = cooldown;

        // Escucha respuestas de Contract-Net para alimentar el contrato correcto
        comunicacion.OnPropuestaRecibida += AlimentarContrato;
        comunicacion.OnRefuseRecibido    += AlimentarContrato;
    }

    public void Limpiar()
    {
        // Desuscribe eventos cuando el guardia deja de usar este manager
        comunicacion.OnPropuestaRecibida -= AlimentarContrato;
        comunicacion.OnRefuseRecibido    -= AlimentarContrato;
    }

    // Inicia una ronda de Contract Net distribuyendo todas las zonas candidatas entre los guardias disponibles
    public bool IniciarDistribucionBusqueda()
    {
        // Si el anillo fue robado se evita relanzar rondas demasiado seguidas
        float cooldownEfectivo = creencias.AnilloRobado ? 3f : 0f;
        if (Time.time - ultimoContractNet < cooldownEfectivo) return false;
        // No se abre una nueva ronda si todavia quedan contratos activos
        if (contratosActivos.Count > 0) return false;

        // Selecciona que guardias pueden participar en la ronda actual
        List<string> participantes = SeleccionarParticipantes();

        if (participantes.Count == 0) return false;

        // Como cada guardia puede recibir como maximo una zona, no se contratan mas zonas que participantes
        List<string> zonas = SeleccionarZonasParaContratar(participantes.Count);
        if (zonas.Count == 0)
        {
            Debug.LogWarning($"[{agentId}] Contract Net sin zonas candidatas para la fase actual");
            return false;
        }

        ultimoContractNet = Time.time;

        // Cada zona abre un contrato independiente con su propio ConversationId
        foreach (string zoneId in zonas)
        {
            AbrirContratoParaZona(zoneId, participantes);
        }

        Debug.Log($"[{agentId}] Contract Net iniciado: zonas=[{string.Join(", ", zonas)}], participantes=[{string.Join(", ", participantes)}]");
        return true;
    }

    private List<string> SeleccionarParticipantes()
    {
        // Parte de todos los guardias registrados en el mundo
        List<string> participantes = AgentRegistry.Instance.ObtenerIdsPorTipo(GameConstants.AgentTypes.Guard);

        if (!creencias.AnilloRobado)
        {
            // El iniciador coordina la busqueda y va al pedestal
            participantes.Remove(agentId);
            return participantes;
        }

        // Los dos bloqueadores preservan la salida; el iniciador solo participa si no es uno de ellos
        HashSet<string> bloqueadoresSalida = creencias.ObtenerIdsBloqueadoresSalidaEstables(2);
        // Se excluyen guardias que ya estan cubriendo salida para no romper el bloqueo
        participantes.RemoveAll(id => bloqueadoresSalida.Contains(id));
        return participantes;
    }

    // Evalúa los contratos abiertos cuando todos están listos y hace el matching
    public void Gestionar()
    {
        // Gestionar se llama periodicamente, pero solo actua si hay contratos abiertos
        if (contratosActivos.Count == 0) return;
        // Espera a que todos los contratos esten listos: respuestas completas o timeout
        if (contratosActivos.Any(c => !c.ListoParaEvaluar())) return;

        // Matching greedy: cada guardia recibe como máximo una zona
        HashSet<string> guardiasYaAsignados = new HashSet<string>();

        // Recorre las zonas ya ordenadas por prioridad y asigna la mejor propuesta disponible
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

        // La ronda queda cerrada tras adjudicar o descartar todos los contratos
        contratosActivos.Clear();
    }

    // Devuelve hasta maxZonas zonas a contratar, filtradas y ordenadas
    private List<string> SeleccionarZonasParaContratar(int maxZonas)
    {
        // Limpia duplicados y nombres vacios antes de aplicar prioridad
        List<string> zonasDisponibles = creencias.ObtenerIdsZonasBusqueda()
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Select(z => z.Trim())
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (zonasDisponibles.Count == 0) return new List<string>();

        IEnumerable<string> candidatas = zonasDisponibles;
        Vector3 referencia = creencias.UltimaPosicionLadron;

        if (creencias.AnilloRobado)
        {
            // Si Frodo tiene el anillo, interesan antes las zonas de salida
            var exitZones = zonasDisponibles
                .Where(EsZonaSalida)
                .ToList();
            if (exitZones.Count > 0) candidatas = exitZones;

            if (creencias.TienePosicionSalida)
                referencia = creencias.PosicionSalida;
        }
        else
        {
            // Si el anillo sigue seguro, se priorizan las zonas relacionadas con el pedestal
            var ringZones = zonasDisponibles
                .Where(EsZonaAnillo)
                .ToList();

            if (creencias.TienePosicionPedestal)
                referencia = creencias.PosicionPedestal;

            candidatas = ringZones;
        }

        // Primero zonas menos recientes, luego zonas mas cercanas a la referencia tactica
        return candidatas
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(z => creencias.ObtenerTiempoUltimaBusqueda(z))
            .ThenBy(z => Vector3.Distance(creencias.ObtenerCentroZona(z), referencia))
            .Take(maxZonas)
            .ToList();
    }

    private bool EsZonaSalida(string zoneId)
    {
        // Acepta tanto prefijo en ingles como en castellano por compatibilidad con nombres de escena
        return !string.IsNullOrEmpty(zoneId) &&
               (zoneId.StartsWith(GameConstants.ZonePrefixes.Exit, System.StringComparison.OrdinalIgnoreCase) ||
                zoneId.StartsWith(GameConstants.ZonePrefixes.ExitAlt, System.StringComparison.OrdinalIgnoreCase));
    }

    private bool EsZonaAnillo(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return false;

        return zoneId.StartsWith(GameConstants.ZonePrefixes.Ring, System.StringComparison.OrdinalIgnoreCase);
    }

    private void AbrirContratoParaZona(string zoneId, List<string> participantes)
    {
        // La tarea apunta al centro de la zona para que los participantes calculen su coste
        Vector3 centroZona = creencias.ObtenerCentroZona(zoneId);

        SearchTask tarea = new SearchTask
        {
            TaskId     = $"search-{agentId}-{zoneId}-{Time.time:F0}",
            ZoneId     = zoneId,
            TargetArea = new Position(centroZona),
            Radius     = 15f,
            Urgency    = 0.8f
        };

        // Todas las respuestas a esta zona se agrupan bajo la misma conversacion
        string convId = comunicacion.NuevaConversacion();
        ContractNetEstado estado = new ContractNetEstado(convId, participantes, timeout: 2f);
        estado.SetContenidoTarea(ContentLanguage.Encode(tarea));
        contratosActivos.Add(estado);

        // El mismo CFP se envia a todos los participantes, cambiando solo el receptor
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
        // Ignora guardias ya asignados en esta ronda y escoge el menor coste declarado
        return propuestas
            .Where(p => !excluir.Contains(p.Sender))
            .OrderBy(p => ContentLanguage.DecodeProposal(p.Content)?.Cost ?? float.MaxValue)
            .FirstOrDefault();
    }

    private void AdjudicarZona(ACLMessage ganador, string contenidoTarea)
    {
        // Acepta la propuesta enviando de vuelta la SearchTask asignada
        ACLMessage accept = ganador.CreateReply(ACLPerformative.ACCEPT_PROPOSAL);
        accept.Content = contenidoTarea;
        comunicacion.Enviar(accept);
        Debug.Log($"[{agentId}] Zona adjudicada a {ganador.Sender}");
    }

    private void RechazarPerdedores(List<ACLMessage> propuestas, ACLMessage ganador)
    {
        // Toda propuesta no ganadora recibe REJECT_PROPOSAL
        foreach (ACLMessage propuesta in propuestas)
        {
            if (ganador == null || propuesta.Sender != ganador.Sender)
                comunicacion.Enviar(propuesta.CreateReply(ACLPerformative.REJECT_PROPOSAL));
        }
    }

    // Se enrutan las propuestas al contrato correspondiente por ConversationId
    private void AlimentarContrato(ACLMessage msg)
    {
        // Busca el contrato cuyo ConversationId coincide con el mensaje recibido
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

// Seguimiento del estado de una negociación Contract Net activa para una zona
public class ContractNetEstado
{
    // Identificador comun para todos los mensajes de este contrato
    public string ConversationId  { get; private set; }
    // Solo se guardan las respuestas PROPOSE; los REFUSE solo eliminan pendientes
    public List<ACLMessage> Propuestas { get; private set; } = new List<ACLMessage>();
    // SearchTask serializada que se reutiliza al aceptar una propuesta
    public string ContenidoTarea  { get; private set; }

    // Participantes que todavia no respondieron y limite temporal de la subasta
    private List<string> pendientes;
    private float deadline;

    public ContractNetEstado(string convId, List<string> participantes, float timeout)
    {
        // Copia la lista de participantes para poder ir marcando respuestas recibidas
        ConversationId = convId;
        pendientes     = new List<string>(participantes);
        deadline       = Time.time + timeout;
    }

    public void SetContenidoTarea(string contenido) => ContenidoTarea = contenido;

    public void RecibirRespuesta(ACLMessage msg)
    {
        // Descarta mensajes de otra conversacion
        if (msg.ConversationId != ConversationId) return;
        // Solo las propuestas compiten en el matching; los rechazos no tienen coste
        if (msg.Performative == ACLPerformative.PROPOSE)
            Propuestas.Add(msg);
        // Tanto PROPOSE como REFUSE cuentan como respuesta recibida
        pendientes.Remove(msg.Sender);
    }

    // Listo cuando todos respondieron o se agotó el tiempo
    public bool ListoParaEvaluar() => pendientes.Count == 0 || Time.time > deadline;
}
