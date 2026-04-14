using System;
using System.Collections.Generic;
using UnityEngine;

// Buzón de mensajes del agente: recibe, encola y despacha mensajes FIPA-ACL.
// Cada frame se procesan como máximo maxMensajesPorFrame mensajes para evitar picos de CPU.
public class ComunicacionAgente : MonoBehaviour
{
    public string AgentId   { get; private set; }
    public string TipoAgente { get; private set; }

    [SerializeField] private int maxMensajesPorFrame = 2;

    private Queue<ACLMessage> buzonEntrada = new Queue<ACLMessage>();
    private int contadorConversaciones = 0;

    // Eventos — el cerebro del agente se suscribe a estos
    public event Action<ACLMessage> OnInformRecibido;
    public event Action<ACLMessage> OnRequestRecibido;
    public event Action<ACLMessage> OnAgreeRecibido;
    public event Action<ACLMessage> OnRefuseRecibido;
    public event Action<ACLMessage> OnDoneRecibido;
    public event Action<ACLMessage> OnFailureRecibido;
    public event Action<ACLMessage> OnQueryRecibido;
    public event Action<ACLMessage> OnCFPRecibido;
    public event Action<ACLMessage> OnPropuestaRecibida;
    public event Action<ACLMessage> OnPropuestaAceptada;
    public event Action<ACLMessage> OnPropuestaRechazada;

    public void Inicializar(string id, string tipo)
    {
        AgentId    = id;
        TipoAgente = tipo;
        AgentRegistry.Instance?.Registrar(AgentId, TipoAgente, this);
    }

    void OnDestroy() => AgentRegistry.Instance?.Desregistrar(AgentId);

    // Depositar un mensaje en el buzón (llamado por el remitente)
    public void RecibirMensaje(ACLMessage mensaje) => buzonEntrada.Enqueue(mensaje);

    // Procesar hasta maxMensajesPorFrame mensajes (llamar desde Update del agente)
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
            case ACLPerformative.REQUEST:         OnRequestRecibido?.Invoke(msg);    break;
            case ACLPerformative.AGREE:           OnAgreeRecibido?.Invoke(msg);      break;
            case ACLPerformative.REFUSE:          OnRefuseRecibido?.Invoke(msg);     break;
            case ACLPerformative.FAILURE:         OnFailureRecibido?.Invoke(msg);    break;
            case ACLPerformative.QUERY_IF:
            case ACLPerformative.QUERY_REF:       OnQueryRecibido?.Invoke(msg);      break;
            case ACLPerformative.CFP:             OnCFPRecibido?.Invoke(msg);        break;
            case ACLPerformative.PROPOSE:         OnPropuestaRecibida?.Invoke(msg);  break;
            case ACLPerformative.ACCEPT_PROPOSAL: OnPropuestaAceptada?.Invoke(msg);  break;
            case ACLPerformative.REJECT_PROPOSAL: OnPropuestaRechazada?.Invoke(msg); break;
        }
    }

    // Enviar un mensaje a un destinatario específico vía el registro de agentes
    public void Enviar(ACLMessage mensaje)
    {
        mensaje.Sender = AgentId;
        AgentRegistry.Instance?.ObtenerAgente(mensaje.Receiver)?.RecibirMensaje(mensaje);
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

    // Genera un identificador único para una conversación
    public string NuevaConversacion() => $"{AgentId}-conv-{contadorConversaciones++}";
}
