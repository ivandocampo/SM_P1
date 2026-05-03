// =============================================================
// Agente araña: sensor estático del laberinto.
// A diferencia de los orcos, la araña no tiene arquitectura BDI
// ni se mueve. Su única función es observar y alertar: se suscribe
// a los eventos de SensorVision y SensorOido y reenvía todo lo que
// detecta a los guardias mediante mensajes FIPA-ACL INFORM en broadcast.
// Actúa como cámara de seguridad del sistema multiagente
// =============================================================

using UnityEngine;


public class SpiderAgent : MonoBehaviour
{
    [Header("Identificación")]
    public string agentId = "Spider_01";

    [Header("Configuración")]
    [Tooltip("Intervalo mínimo entre informes del mismo tipo (evita spam)")]
    public float cooldownInforme = 2f;

    private ComunicacionAgente comunicacion;
    private SensorVision sensorVision;
    private SensorOido sensorOido;

    private float ultimoInformeAvistamiento = -100f;
    // Flag para no enviar RING_STOLEN más de una vez
    private bool anilloReportado = false;

    void Start()
    {
        comunicacion = GetComponent<ComunicacionAgente>();
        if (comunicacion == null)
        {
            Debug.LogError($"[{agentId}] Falta componente ComunicacionAgente");
            return;
        }
        comunicacion.Inicializar(agentId, GameConstants.AgentTypes.Spider);

        // Obtener sensores; la araña puede tener visión, oído o ambos según la escena
        sensorVision = GetComponent<SensorVision>();
        sensorOido = GetComponent<SensorOido>();

        // Suscribirse a los eventos de los sensores para reaccionar cuando detecten algo
        if (sensorVision != null)
        {
            sensorVision.OnObjetivoDetectado += ManejarObjetivoVisto;
            sensorVision.OnObjetivoVisible += ManejarObjetivoVisible;
            sensorVision.OnObjetivoPerdido += ManejarObjetivoPerdido;
            sensorVision.OnAnilloDesaparecido += ManejarAnilloDesaparecido;
        }

        if (sensorOido != null)
        {
            sensorOido.OnSonidoDetectado += ManejarSonidoDetectado;
        }

        Debug.Log($"[{agentId}] Araña inicializada en posición {transform.position}");
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        // Procesar mensajes entrantes; las arañas pueden recibir QUERY de los guardias
        comunicacion.ProcesarMensajes();
    }

    // Desuscribirse de todos los eventos al destruir el objeto para evitar referencias nulas
    void OnDestroy()
    {
        if (sensorVision != null)
        {
            sensorVision.OnObjetivoDetectado -= ManejarObjetivoVisto;
            sensorVision.OnObjetivoVisible -= ManejarObjetivoVisible;
            sensorVision.OnObjetivoPerdido -= ManejarObjetivoPerdido;
            sensorVision.OnAnilloDesaparecido -= ManejarAnilloDesaparecido;
        }

        if (sensorOido != null)
        {
            sensorOido.OnSonidoDetectado -= ManejarSonidoDetectado;
        }
    }

    // Manejadores de eventos de sensores: cada uno delega en los métodos de comunicación

    // Frodo pasa a ser visible: enviar informe inmediato sin throttle
    private void ManejarObjetivoVisto(Vector3 posicion)
    {
        EnviarInformeAvistamiento(posicion, true);
    }

    // Frodo sigue visible: enviar informe con cooldown para no saturar el canal
    private void ManejarObjetivoVisible(Vector3 posicion)
    {
        if (Time.time - ultimoInformeAvistamiento >= cooldownInforme)
            EnviarInformeAvistamiento(posicion, true);
    }

    // Frodo desaparece del campo de visión: notificar THIEF_LOST al equipo
    private void ManejarObjetivoPerdido()
    {
        BroadcastPredicado(PredicateType.THIEF_LOST);
        Debug.Log($"[{agentId}] Ladrón perdido de vista — informando a guardias");
    }

    // El pedestal queda vacío: notificar RING_STOLEN una sola vez
    private void ManejarAnilloDesaparecido()
    {
        if (anilloReportado) return;
        anilloReportado = true;

        BroadcastPredicado(PredicateType.RING_STOLEN);
        Debug.Log($"[{agentId}] ¡Anillo desaparecido del pedestal! — informando a guardias");
    }

    // Frodo detectado por oído: enviar informe de posición con cooldown
    private void ManejarSonidoDetectado(Vector3 posicion)
    {
        if (Time.time - ultimoInformeAvistamiento >= cooldownInforme)
            EnviarInformeAvistamiento(posicion, false);
    }

    // Construir y emitir en broadcast un ThiefSighting con la posición detectada
    private void EnviarInformeAvistamiento(Vector3 posicion, bool visionDirecta)
    {
        ultimoInformeAvistamiento = Time.time;

        ThiefSighting avistamiento = new ThiefSighting
        {
            Location     = new Position(posicion),
            Timestamp    = Time.time,
            ReportedBy   = agentId,
            DirectVision = visionDirecta
        };

        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, agentId, "");
        msg.Content  = ContentLanguage.Encode(avistamiento);
        msg.Protocol = GameConstants.Protocols.Inform;
        comunicacion.Broadcast(msg);

        // Si Frodo es visible portando el anillo, emitir también RING_STOLEN sin esperar al pedestal
        if (visionDirecta &&
            sensorVision != null &&
            sensorVision.ObjetivoVisibleConAnillo &&
            !anilloReportado)
        {
            anilloReportado = true;
            BroadcastPredicado(PredicateType.RING_STOLEN);
            Debug.Log($"[{agentId}] Ladron visto con el anillo - informando a guardias");
        }

        string tipo = visionDirecta ? "VISTO" : "OÍDO";
        Debug.Log($"[{agentId}] Ladrón {tipo} en {posicion} — informando a guardias");
    }

    // Emitir en broadcast un predicado factual (THIEF_LOST, RING_STOLEN, etc.)
    private void BroadcastPredicado(PredicateType predicado)
    {
        ACLMessage msg = new ACLMessage(ACLPerformative.INFORM, agentId, "");
        msg.Content  = ContentLanguage.EncodePredicate(predicado);
        msg.Protocol = GameConstants.Protocols.Inform;
        comunicacion.Broadcast(msg);
    }
}
