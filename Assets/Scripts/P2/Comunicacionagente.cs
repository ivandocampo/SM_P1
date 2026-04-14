using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class ComunicacionAgente : MonoBehaviour
{
    
    [Header("Identificación")]
    [Tooltip("Se asigna programáticamente desde el cerebro del agente")]
    public string AgentId { get; private set; }
    public string TipoAgente { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool mostrarLogs = true;

    // BUZÓN DE MENSAJES (MAILBOX)

    private Queue<ACLMessage> buzonEntrada = new Queue<ACLMessage>();
    private List<ACLMessage> registroEnviados = new List<ACLMessage>();
    private int contadorMensajes = 0;

    /// <summary>Número de mensajes pendientes de procesar.</summary>
    public int MensajesPendientes => buzonEntrada.Count;
    public bool HayMensajes => buzonEntrada.Count > 0;

    // GESTIÓN DE CONVERSACIONES

    private Dictionary<string, Conversacion> conversaciones =
        new Dictionary<string, Conversacion>();
    private int contadorConversaciones = 0;

    // ESTADO DE PROTOCOLOS ACTIVOS

    // Contract Net — estado cuando este agente actúa como manager
    private Dictionary<string, ContractNetEstado> contractNetsActivos = new Dictionary<string, ContractNetEstado>();


    // EVENTOS — El cerebro del agente se suscribe a estos

    /// <summary>Se recibió un INFORM (avistamiento, predicado, etc.)</summary>
    public event Action<ACLMessage> OnInformRecibido;

    /// <summary>Se recibió un REQUEST (solicitud de acción)</summary>
    public event Action<ACLMessage> OnRequestRecibido;

    /// <summary>Se recibió un AGREE en respuesta a un REQUEST que enviamos</summary>
    public event Action<ACLMessage> OnAgreeRecibido;

    /// <summary>Se recibió un REFUSE en respuesta a un REQUEST que enviamos</summary>
    public event Action<ACLMessage> OnRefuseRecibido;

    /// <summary>Se recibió un INFORM_DONE confirmando una tarea completada</summary>
    public event Action<ACLMessage> OnDoneRecibido;

    /// <summary>Se recibió un FAILURE indicando que la tarea falló</summary>
    public event Action<ACLMessage> OnFailureRecibido;

    /// <summary>Se recibió un QUERY (IF o REF)</summary>
    public event Action<ACLMessage> OnQueryRecibido;

    /// <summary>Se recibió un INFORM_RESULT en respuesta a un QUERY que enviamos</summary>
    public event Action<ACLMessage> OnResultadoQueryRecibido;

    /// <summary>Se recibió un CFP (Call For Proposals)</summary>
    public event Action<ACLMessage> OnCFPRecibido;

    /// <summary>Se recibió un PROPOSE en respuesta a un CFP que enviamos</summary>
    public event Action<ACLMessage> OnPropuestaRecibida;

    /// <summary>Se recibió ACCEPT_PROPOSAL — nos adjudicaron una tarea</summary>
    public event Action<ACLMessage> OnPropuestaAceptada;

    /// <summary>Se recibió REJECT_PROPOSAL — nuestra propuesta fue rechazada</summary>
    public event Action<ACLMessage> OnPropuestaRechazada;

    // INICIALIZACIÓN

    
    public void Inicializar(string id, string tipo)
    {
        AgentId = id;
        TipoAgente = tipo;

        if (AgentRegistry.Instance != null)
        {
            AgentRegistry.Instance.Registrar(AgentId, TipoAgente, this);
        }
        else
        {
            Debug.LogError($"[{AgentId}] AgentRegistry no encontrado en la escena.");
        }
    }

    void OnDestroy()
    {
        if (AgentRegistry.Instance != null)
        {
            AgentRegistry.Instance.Desregistrar(AgentId);
        }
    }

    // RECEPCIÓN — Llamado por otros agentes para depositar mensajes

    
    public void RecibirMensaje(ACLMessage mensaje)
    {
        buzonEntrada.Enqueue(mensaje);

        if (mostrarLogs)
            Debug.Log($"[{AgentId}] Mensaje recibido: {mensaje}");
    }

    // PROCESAMIENTO — Llamar desde Update() del cerebro

    
    public void ProcesarMensajes()
    {
        while (buzonEntrada.Count > 0)
        {
            ACLMessage msg = buzonEntrada.Dequeue();
            RegistrarEnConversacion(msg);
            DespacharMensaje(msg);
        }

        // Gestionar timeouts de Contract Net activo
        ActualizarContractNet();
    }

    
    private void DespacharMensaje(ACLMessage msg)
    {
        switch (msg.Performative)
        {
            case ACLPerformative.INFORM:
            case ACLPerformative.INFORM_DONE:
                if (msg.Performative == ACLPerformative.INFORM_DONE)
                    OnDoneRecibido?.Invoke(msg);
                else
                    OnInformRecibido?.Invoke(msg);
                break;

            case ACLPerformative.INFORM_RESULT:
                OnResultadoQueryRecibido?.Invoke(msg);
                break;

            case ACLPerformative.REQUEST:
                OnRequestRecibido?.Invoke(msg);
                break;

            case ACLPerformative.AGREE:
                OnAgreeRecibido?.Invoke(msg);
                break;

            case ACLPerformative.REFUSE:
                OnRefuseRecibido?.Invoke(msg);
                if (contractNetsActivos.TryGetValue(msg.ConversationId, out ContractNetEstado cnRefuse))
                    cnRefuse.RecibirRespuesta(msg);
                break;

            case ACLPerformative.FAILURE:
                OnFailureRecibido?.Invoke(msg);
                break;

            case ACLPerformative.QUERY_IF:
            case ACLPerformative.QUERY_REF:
                OnQueryRecibido?.Invoke(msg);
                break;

            case ACLPerformative.CFP:
                OnCFPRecibido?.Invoke(msg);
                break;

            case ACLPerformative.PROPOSE:
                OnPropuestaRecibida?.Invoke(msg);
                // También alimentar al Contract Net si estamos gestionando uno
                if (contractNetsActivos.TryGetValue(msg.ConversationId, out ContractNetEstado cnPropose))
                    cnPropose.RecibirRespuesta(msg);
                break;

            case ACLPerformative.ACCEPT_PROPOSAL:
                OnPropuestaAceptada?.Invoke(msg);
                break;

            case ACLPerformative.REJECT_PROPOSAL:
                OnPropuestaRechazada?.Invoke(msg);
                break;

            default:
                if (mostrarLogs)
                    Debug.LogWarning($"[{AgentId}] Performativa no manejada: {msg.Performative}");
                break;
        }
    }

    // ENVÍO DE MENSAJES

    
    public void Enviar(ACLMessage mensaje)
    {
        mensaje.Sender = AgentId;

        if (string.IsNullOrEmpty(mensaje.ReplyWith))
            mensaje.ReplyWith = GenerarIdMensaje();

        registroEnviados.Add(mensaje);
        RegistrarEnConversacion(mensaje);

        // Buscar al destinatario en el registro y depositar el mensaje
        ComunicacionAgente receptor = AgentRegistry.Instance?.ObtenerAgente(mensaje.Receiver);
        if (receptor != null)
        {
            receptor.RecibirMensaje(mensaje);
            if (mostrarLogs)
                Debug.Log($"[{AgentId}] Mensaje enviado: {mensaje}");
        }
        else
        {
            if (mostrarLogs)
                Debug.LogWarning($"[{AgentId}] Destinatario no encontrado: {mensaje.Receiver}");
        }
    }

    
    public void EnviarATodos(ACLMessage plantilla, List<string> receptores)
    {
        foreach (string receptorId in receptores)
        {
            ACLMessage copia = plantilla.CloneForReceiver(receptorId);
            Enviar(copia);
        }
    }

    
    public void Broadcast(ACLMessage plantilla)
    {
        List<string> todos = AgentRegistry.Instance.ObtenerOtrosIds(AgentId);
        EnviarATodos(plantilla, todos);
    }

    
    public void BroadcastATipo(ACLMessage plantilla, string tipo)
    {
        List<string> destinos = AgentRegistry.Instance.ObtenerOtrosIdsPorTipo(tipo, AgentId);
        EnviarATodos(plantilla, destinos);
    }

    // PROTOCOLO FIPA-REQUEST (helpers)

    
    public string EnviarRequest(string receptor, ActionRequest accion)
    {
        string convId = IniciarConversacion("fipa-request");

        ACLMessage request = new ACLMessage(ACLPerformative.REQUEST, AgentId, receptor);
        request.Content = ContentLanguage.Encode(accion);
        request.Protocol = "fipa-request";
        request.ConversationId = convId;

        Enviar(request);
        return convId;
    }

    
    public void ResponderAgree(ACLMessage requestOriginal)
    {
        ACLMessage reply = requestOriginal.CreateReply(ACLPerformative.AGREE);
        Enviar(reply);
    }

    
    public void ResponderRefuse(ACLMessage requestOriginal, string motivo = "busy")
    {
        ACLMessage reply = requestOriginal.CreateReply(ACLPerformative.REFUSE);
        reply.Content = motivo;
        Enviar(reply);
    }

    
    public void NotificarDone(string conversationId, string solicitanteId)
    {
        ACLMessage done = new ACLMessage(ACLPerformative.INFORM_DONE, AgentId, solicitanteId);
        done.ConversationId = conversationId;
        done.Protocol = "fipa-request";
        Enviar(done);
        CerrarConversacion(conversationId);
    }

   
    public void NotificarFailure(string conversationId, string solicitanteId, string motivo = "")
    {
        ACLMessage failure = new ACLMessage(ACLPerformative.FAILURE, AgentId, solicitanteId);
        failure.ConversationId = conversationId;
        failure.Protocol = "fipa-request";
        failure.Content = motivo;
        Enviar(failure);
        CerrarConversacion(conversationId, "failed");
    }

    // PROTOCOLO FIPA-QUERY (helpers)

    
    public string EnviarQueryIf(string receptor, PredicateType predicado)
    {
        string convId = IniciarConversacion("fipa-query");

        ACLMessage query = new ACLMessage(ACLPerformative.QUERY_IF, AgentId, receptor);
        query.Content = ContentLanguage.EncodePredicate(predicado);
        query.Protocol = "fipa-query";
        query.ConversationId = convId;

        Enviar(query);
        return convId;
    }

    
    public void ResponderQuery(ACLMessage queryOriginal, string resultado)
    {
        ACLMessage reply = queryOriginal.CreateReply(ACLPerformative.INFORM_RESULT);
        reply.Content = resultado;
        Enviar(reply);
        CerrarConversacion(queryOriginal.ConversationId);
    }

    // PROTOCOLO FIPA-INFORM (helpers)

    
    public void InformarAvistamiento(ThiefSighting avistamiento)
    {
        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, AgentId, "");
        msg.Content = ContentLanguage.Encode(avistamiento);
        msg.Protocol = "fipa-inform";
        Broadcast(msg);
    }

    
    public void InformarPredicado(PredicateType predicado, string extraData = "")
    {
        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, AgentId, "");
        msg.Content = ContentLanguage.EncodePredicate(predicado, extraData);
        msg.Protocol = "fipa-inform";
        Broadcast(msg);
    }

    // PROTOCOLO CONTRACT NET (como Manager / Iniciador)

    public string IniciarContractNet(SearchTask tarea, List<string> destinatarios, float timeout = 3f)
    {
        string convId = IniciarConversacion("fipa-contract-net");

        ContractNetEstado nuevoContractNet = new ContractNetEstado(convId, destinatarios, timeout);
        nuevoContractNet.SetContenidoTarea(ContentLanguage.Encode(tarea));
        contractNetsActivos[convId] = nuevoContractNet;

        foreach (string receptor in destinatarios)
        {
            ACLMessage cfp = new ACLMessage(ACLPerformative.CFP, AgentId, receptor);
            cfp.Content = ContentLanguage.Encode(tarea);
            cfp.Protocol = "fipa-contract-net";
            cfp.ConversationId = convId;
            Enviar(cfp);
        }

        if (mostrarLogs)
            Debug.Log($"[{AgentId}] Contract Net iniciado: {convId} con {destinatarios.Count} participantes");

        return convId;
    }

    
    public bool ContractNetListoParaEvaluar()
    {
        foreach (var cn in contractNetsActivos.Values)
            if (cn.ListoParaEvaluar()) return true;
        return false;
    }
    
    
    public ACLMessage EvaluarYAdjudicar()
    {
        ContractNetEstado listo = null;
        foreach (var cn in contractNetsActivos.Values)
        {
            if (cn.ListoParaEvaluar()) { listo = cn; break; }
        }
        if (listo == null) return null;

        List<ACLMessage> propuestas = listo.Propuestas;

        if (propuestas.Count == 0)
        {
            if (mostrarLogs)
                Debug.Log($"[{AgentId}] Contract Net: sin propuestas recibidas");
            contractNetsActivos.Remove(listo.ConversationId);
            return null;
        }

        // Seleccionar la mejor propuesta (menor coste)
        ACLMessage mejorPropuesta = propuestas
            .OrderBy(p =>
            {
                ProposalData data = ContentLanguage.DecodeProposal(p.Content);
                return data != null ? data.Cost : float.MaxValue;
            })
            .First();

        // Aceptar la mejor
        ACLMessage accept = mejorPropuesta.CreateReply(ACLPerformative.ACCEPT_PROPOSAL);
        accept.Content = listo.ContenidoTarea;
        Enviar(accept);

        // Rechazar las demás
        foreach (ACLMessage propuesta in propuestas)
        {
            if (propuesta.Sender != mejorPropuesta.Sender)
            {
                ACLMessage reject = propuesta.CreateReply(ACLPerformative.REJECT_PROPOSAL);
                Enviar(reject);
            }
        }

        if (mostrarLogs)
        {
            ProposalData ganador = ContentLanguage.DecodeProposal(mejorPropuesta.Content);
            Debug.Log($"[{AgentId}] Contract Net adjudicado a {mejorPropuesta.Sender} " +
                    $"(coste: {ganador?.Cost:F1})");
        }

        contractNetsActivos.Remove(listo.ConversationId);
        CerrarConversacion(listo.ConversationId);

        return mejorPropuesta;
    }



    // PROTOCOLO CONTRACT NET (como Contractor / Respondedor)

    public void ResponderCFPConPropuesta(ACLMessage cfp, ProposalData propuesta)
    {
        ACLMessage propose = cfp.CreateReply(ACLPerformative.PROPOSE);
        propose.Content = ContentLanguage.Encode(propuesta);
        Enviar(propose);
    }

    
    public void RechazarCFP(ACLMessage cfp, string motivo = "unavailable")
    {
        ACLMessage refuse = cfp.CreateReply(ACLPerformative.REFUSE);
        refuse.Content = motivo;
        Enviar(refuse);
    }

    // GESTIÓN DE CONVERSACIONES

    public string IniciarConversacion(string protocolo)
    {
        string convId = $"{AgentId}-conv-{contadorConversaciones++}";
        Conversacion conv = new Conversacion
        {
            ConversationId = convId,
            Protocolo = protocolo,
            Estado = "active",
            TiempoInicio = Time.time
        };
        conversaciones[convId] = conv;
        return convId;
    }

    
    private void RegistrarEnConversacion(ACLMessage msg)
    {
        if (string.IsNullOrEmpty(msg.ConversationId)) return;

        if (!conversaciones.ContainsKey(msg.ConversationId))
        {
            conversaciones[msg.ConversationId] = new Conversacion
            {
                ConversationId = msg.ConversationId,
                Protocolo = msg.Protocol,
                Estado = "active",
                TiempoInicio = Time.time
            };
        }

        conversaciones[msg.ConversationId].Mensajes.Add(msg);
    }

    
    public void CerrarConversacion(string convId, string estadoFinal = "completed")
    {
        if (conversaciones.ContainsKey(convId))
        {
            conversaciones[convId].Estado = estadoFinal;
        }
    }

    
    public Conversacion ObtenerConversacion(string convId)
    {
        conversaciones.TryGetValue(convId, out Conversacion conv);
        return conv;
    }

    
    public List<Conversacion> ConversacionesActivas()
    {
        return conversaciones.Values
            .Where(c => c.Estado == "active")
            .ToList();
    }

    // UTILIDADES INTERNAS

    private string GenerarIdMensaje()
    {
        return $"{AgentId}-msg-{contadorMensajes++}";
    }

    
    private void ActualizarContractNet()
    {
        foreach (var cn in contractNetsActivos.Values)
        {
            if (cn.HaExpirado() && cn.ListoParaEvaluar())
            {
                if (mostrarLogs)
                    Debug.Log($"[{AgentId}] Contract Net timeout — evaluando propuestas disponibles");
                EvaluarYAdjudicar();
                return; // una evaluación por frame es suficiente
            }
        }
    }

    // DEBUG
    public string ResumenEstado()
    {
        int activas = conversaciones.Values.Count(c => c.Estado == "active");
        return $"[{AgentId}] Mensajes enviados: {registroEnviados.Count}, " +
               $"Pendientes: {buzonEntrada.Count}, " +
               $"Conversaciones activas: {activas}";
    }
}

// CLASES AUXILIARES

[System.Serializable]
public class Conversacion
{
    public string ConversationId;
    public string Protocolo;
    public string Estado;           // "active", "completed", "failed", "refused"
    public float TiempoInicio;
    public List<ACLMessage> Mensajes = new List<ACLMessage>();
}


public class ContractNetEstado
{
    public string ConversationId { get; private set; }
    public List<ACLMessage> Propuestas { get; private set; } = new List<ACLMessage>();
    public string ContenidoTarea { get; private set; }

    private List<string> pendientes;
    private float deadline;
    private bool evaluado = false;

    public ContractNetEstado(string convId, List<string> participantes, float timeout)
    {
        ConversationId = convId;
        pendientes = new List<string>(participantes);
        deadline = Time.time + timeout;
    }

    
    public void RecibirRespuesta(ACLMessage msg)
    {
        if (evaluado) return;
        if (msg.ConversationId != ConversationId) return;

        if (msg.Performative == ACLPerformative.PROPOSE)
        {
            Propuestas.Add(msg);
            if (string.IsNullOrEmpty(ContenidoTarea))
                ContenidoTarea = msg.Content; // Fallback; normalmente se guarda al enviar CFP
        }

        pendientes.Remove(msg.Sender);
    }

    
    public void SetContenidoTarea(string contenido)
    {
        ContenidoTarea = contenido;
    }

    
    public bool ListoParaEvaluar()
    {
        return !evaluado && (pendientes.Count == 0 || Time.time > deadline);
    }

    public bool HaExpirado()
    {
        return Time.time > deadline;
    }
}