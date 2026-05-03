using System;
using System.Collections.Generic;
using UnityEngine;

// Buzon de mensajes del agente: recibe, encola y despacha mensajes FIPA-ACL.
// Cada frame se procesan como maximo maxMensajesPorFrame mensajes para evitar picos de CPU.
public class ComunicacionAgente : MonoBehaviour
{
    public string AgentId { get; private set; }
    public string TipoAgente { get; private set; }

    [SerializeField] private int maxMensajesPorFrame = 2;
    [SerializeField] private int maxBuzonSize = 100;
    [SerializeField] private int maxConversaciones = 50;

    private Queue<ACLMessage> buzonEntrada = new Queue<ACLMessage>();
    private int contadorConversaciones = 0;

    // Historial de conversaciones completas.
    private Dictionary<string, List<ACLMessage>> historialConversaciones =
        new Dictionary<string, List<ACLMessage>>();
    private Queue<string> ordenConversaciones = new Queue<string>();

    // Eventos: el cerebro del agente se suscribe a estos.
    public event Action<ACLMessage> OnInformRecibido;
    public event Action<ACLMessage> OnInformResultRecibido;
    public event Action<ACLMessage> OnRequestRecibido;
    public event Action<ACLMessage> OnAgreeRecibido;
    public event Action<ACLMessage> OnRefuseRecibido;
    public event Action<ACLMessage> OnDoneRecibido;
    public event Action<ACLMessage> OnFailureRecibido;
    public event Action<ACLMessage> OnQueryRecibido;
    public event Action<ACLMessage> OnQueryIfRecibido;
    public event Action<ACLMessage> OnQueryRefRecibido;
    public event Action<ACLMessage> OnCFPRecibido;
    public event Action<ACLMessage> OnPropuestaRecibida;
    public event Action<ACLMessage> OnPropuestaAceptada;
    public event Action<ACLMessage> OnPropuestaRechazada;
    public event Action<ACLMessage> OnCancelRecibido;
    public event Action<ACLMessage> OnNotUnderstoodRecibido;

    public void Inicializar(string id, string tipo)
    {
        if (!string.IsNullOrEmpty(AgentId))
        {
            if (AgentId == id && TipoAgente == tipo)
                return;

            Debug.LogWarning($"[{AgentId}] ComunicacionAgente ya inicializado; ignorando reinicializacion como {id}.");
            return;
        }

        AgentId = id;
        TipoAgente = tipo;
        AgentRegistry.Instance?.Registrar(AgentId, TipoAgente, this);
    }

    void OnDestroy()
    {
        AgentRegistry.Instance?.Desregistrar(AgentId);
        historialConversaciones.Clear();
    }

    // Depositar un mensaje en el buzon (llamado por el remitente).
    public void RecibirMensaje(ACLMessage mensaje)
    {
        if (buzonEntrada.Count >= maxBuzonSize)
        {
            ACLMessage descartado = buzonEntrada.Dequeue();
            Debug.LogWarning(
                $"[{AgentId}] Buzon lleno ({maxBuzonSize}). Descartando {descartado.Performative} de {descartado.Sender}.");
        }

        buzonEntrada.Enqueue(mensaje);
        RegistrarEnConversacionSiAplica(mensaje);
    }

    private void RegistrarEnConversacionSiAplica(ACLMessage mensaje)
    {
        if (!string.IsNullOrEmpty(mensaje.ConversationId))
            RegistrarEnConversacion(mensaje);
    }

    private void RegistrarEnConversacion(ACLMessage mensaje)
    {
        string convId = mensaje.ConversationId;

        if (!historialConversaciones.ContainsKey(convId) &&
            historialConversaciones.Count >= maxConversaciones)
        {
            string masAntigua = ordenConversaciones.Dequeue();
            historialConversaciones.Remove(masAntigua);
        }

        if (!historialConversaciones.ContainsKey(convId))
        {
            historialConversaciones[convId] = new List<ACLMessage>();
            ordenConversaciones.Enqueue(convId);
        }

        historialConversaciones[convId].Add(mensaje);
    }

    // Obtener historial completo de una conversacion.
    public List<ACLMessage> ObtenerHistorialConversacion(string conversationId)
    {
        if (historialConversaciones.TryGetValue(conversationId, out var historial))
            return new List<ACLMessage>(historial);
        return new List<ACLMessage>();
    }

    public List<string> ObtenerConversacionesActivas()
    {
        return new List<string>(historialConversaciones.Keys);
    }

    public bool TieneConversacion(string conversationId)
    {
        return historialConversaciones.ContainsKey(conversationId);
    }

    // Procesar hasta maxMensajesPorFrame mensajes (llamar desde Update del agente).
    public void ProcesarMensajes()
    {
        int procesados = 0;
        while (buzonEntrada.Count > 0 && procesados < maxMensajesPorFrame)
        {
            DespacharMensaje(buzonEntrada.Dequeue());
            procesados++;
        }
    }

    private void DespacharMensaje(ACLMessage msg)
    {
        switch (msg.Performative)
        {
            case ACLPerformative.INFORM:          OnInformRecibido?.Invoke(msg);     break;
            case ACLPerformative.INFORM_DONE:     OnDoneRecibido?.Invoke(msg);       break;
            case ACLPerformative.INFORM_RESULT:   OnInformResultRecibido?.Invoke(msg); break;
            case ACLPerformative.REQUEST:         OnRequestRecibido?.Invoke(msg);    break;
            case ACLPerformative.AGREE:           OnAgreeRecibido?.Invoke(msg);      break;
            case ACLPerformative.REFUSE:          OnRefuseRecibido?.Invoke(msg);     break;
            case ACLPerformative.FAILURE:         OnFailureRecibido?.Invoke(msg);    break;
            case ACLPerformative.QUERY_IF:        OnQueryIfRecibido?.Invoke(msg); OnQueryRecibido?.Invoke(msg); break;
            case ACLPerformative.QUERY_REF:       OnQueryRefRecibido?.Invoke(msg); OnQueryRecibido?.Invoke(msg); break;
            case ACLPerformative.CFP:             OnCFPRecibido?.Invoke(msg);        break;
            case ACLPerformative.PROPOSE:         OnPropuestaRecibida?.Invoke(msg);  break;
            case ACLPerformative.ACCEPT_PROPOSAL: OnPropuestaAceptada?.Invoke(msg);  break;
            case ACLPerformative.REJECT_PROPOSAL: OnPropuestaRechazada?.Invoke(msg); break;
            case ACLPerformative.CANCEL:          OnCancelRecibido?.Invoke(msg);     break;
            case ACLPerformative.NOT_UNDERSTOOD:  OnNotUnderstoodRecibido?.Invoke(msg); break;
            default:
                Debug.LogWarning($"[{AgentId}] Performativa no manejada: {msg.Performative} de {msg.Sender}");
                break;
        }
    }

    // Enviar un mensaje a un destinatario especifico via el registro de agentes.
    public void Enviar(ACLMessage mensaje)
    {
        mensaje.Sender = AgentId;
        RegistrarEnConversacionSiAplica(mensaje);
        ComunicacionAgente receptor = AgentRegistry.Instance?.ObtenerAgente(mensaje.Receiver);
        if (receptor == null)
        {
            AgentRegistry.Instance?.Desregistrar(mensaje.Receiver);
            Debug.LogWarning($"[{AgentId}] No se pudo enviar {mensaje.Performative}: receptor {mensaje.Receiver} no registrado.");
            return;
        }

        receptor.RecibirMensaje(mensaje);
    }

    public void EnviarATodos(ACLMessage plantilla, List<string> receptores)
    {
        foreach (string id in receptores)
            Enviar(plantilla.CloneForReceiver(id));
    }

    public void Broadcast(ACLMessage plantilla)
        => EnviarATodos(plantilla, AgentRegistry.Instance.ObtenerOtrosIds(AgentId));

    public void BroadcastATipo(ACLMessage plantilla, string tipo)
        => EnviarATodos(plantilla, AgentRegistry.Instance.ObtenerOtrosIdsPorTipo(tipo, AgentId));

    // Genera un identificador unico para una conversacion.
    public string NuevaConversacion() => $"{AgentId}-conv-{contadorConversaciones++}";
}
