using UnityEngine;


public class SpiderAgent : MonoBehaviour
{
    [Header("Identificación")]
    public string agentId = "Spider_01";

    [Header("Configuración")]
    [Tooltip("Intervalo mínimo entre informes del mismo tipo (evita spam)")]
    public float cooldownInforme = 2f;

    // === COMPONENTES ===
    private ComunicacionAgente comunicacion;
    private SensorVision sensorVision;
    private SensorOido sensorOido;

    // === ESTADO ===
    private float ultimoInformeAvistamiento = -100f;
    //private float ultimoInformeAnillo = -100f;
    private bool anilloReportado = false;

    void Start()
    {
        // Inicializar comunicación
        comunicacion = GetComponent<ComunicacionAgente>();
        if (comunicacion == null)
        {
            Debug.LogError($"[{agentId}] Falta componente ComunicacionAgente");
            return;
        }
        comunicacion.Inicializar(agentId, "spider");

        // Obtener sensores (puede tener uno o ambos)
        sensorVision = GetComponent<SensorVision>();
        sensorOido = GetComponent<SensorOido>();

        // Suscribirse a los eventos de los sensores
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

        // Procesar mensajes recibidos (las arañas pueden recibir QUERY de los guardias)
        comunicacion.ProcesarMensajes();
    }

    void OnDestroy()
    {
        // Desuscribirse de los eventos
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

    // MANEJADORES DE EVENTOS DE SENSORES

    
    private void ManejarObjetivoVisto(Vector3 posicion)
    {
        EnviarInformeAvistamiento(posicion, true);
    }

    
    private void ManejarObjetivoVisible(Vector3 posicion)
    {
        // Solo enviar actualización si ha pasado suficiente tiempo
        if (Time.time - ultimoInformeAvistamiento >= cooldownInforme)
        {
            EnviarInformeAvistamiento(posicion, true);
        }
    }

   
    private void ManejarObjetivoPerdido()
    {
        comunicacion.InformarPredicado(PredicateType.THIEF_LOST);
        Debug.Log($"[{agentId}] Ladrón perdido de vista — informando a guardias");
    }

    
    private void ManejarAnilloDesaparecido()
    {
        if (anilloReportado) return;
        anilloReportado = true;

        comunicacion.InformarPredicado(PredicateType.RING_STOLEN);
        Debug.Log($"[{agentId}] ¡Anillo desaparecido del pedestal! — informando a guardias");
    }

    
    private void ManejarSonidoDetectado(Vector3 posicion)
    {
        if (Time.time - ultimoInformeAvistamiento >= cooldownInforme)
        {
            EnviarInformeAvistamiento(posicion, false);
        }
    }

    // COMUNICACIÓN


    private void EnviarInformeAvistamiento(Vector3 posicion, bool visionDirecta)
    {
        ultimoInformeAvistamiento = Time.time;

        ThiefSighting avistamiento = new ThiefSighting
        {
            Location = new Position(posicion),
            Timestamp = Time.time,
            ReportedBy = agentId,
            DirectVision = visionDirecta
        };

        comunicacion.InformarAvistamiento(avistamiento);

        string tipo = visionDirecta ? "VISTO" : "OÍDO";
        Debug.Log($"[{agentId}] Ladrón {tipo} en {posicion} — informando a guardias");
    }
}