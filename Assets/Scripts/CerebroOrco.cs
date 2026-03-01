using UnityEngine;

public class CerebroOrco : MonoBehaviour
{
    public enum EstadoOrco
    {
        PATRULLA,
        PERSECUCION,
        BUSQUEDA,
        COMPROBAR_ANILLO,
        BLOQUEAR_SALIDA
    }

    [Header("Referencias")]
    public Transform objetivoFrodo;
    public float distanciaAtaque = 1.2f;

    [Header("Temporizadores")]
    public float tiempoGraciaPersecucion = 3f;
    public float tiempoBusqueda = 7f;
    public float tiempoEntreComprobaciones = 30f; 

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA;
    private EstadoOrco estadoPrevio = EstadoOrco.PATRULLA;
    private ActuadorMovimientoOrco actuador;
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;

    private float temporizadorPersecucion = 0f;
    private float temporizadorBusqueda = 0f;
    private float temporizadorComprobacion = 0f;

    private bool frodoVisible;

    // Controla que solo un agente realice la verificación del anillo simultáneamente
    private static bool alguienComprobando = false;

    // Configura las referencias iniciales y establece un desfase aleatorio para las comprobaciones
    void Start()
    {
        actuador = GetComponent<ActuadorMovimientoOrco>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();

        temporizadorComprobacion = Random.Range(10f, tiempoEntreComprobaciones);
    }

    // Gestiona el ciclo de vida principal y la detección de proximidad para la captura
    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        // Almacena el estado de visibilidad actual del objetivo
        frodoVisible = sensorVista.VerFrodo();

        // Finaliza la partida si el agente se encuentra dentro de la distancia de ataque
        if (Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            GameManager.Instance.FrodoCapturado();
            return;
        }

        // Cambia al estado de bloqueo si detecta visualmente la ausencia del anillo
        if (sensorVista.NoAnillo() && estadoActual == EstadoOrco.PATRULLA)
        {
            estadoActual = EstadoOrco.BLOQUEAR_SALIDA;
        }

        // Maneja la lógica de inspección periódica del pedestal durante la patrulla
        if (estadoActual == EstadoOrco.PATRULLA)
        {
            temporizadorComprobacion -= Time.deltaTime;
            if (temporizadorComprobacion <= 0 && !alguienComprobando)
            {
                temporizadorComprobacion = tiempoEntreComprobaciones;
                estadoPrevio = EstadoOrco.PATRULLA;
                estadoActual = EstadoOrco.COMPROBAR_ANILLO;
                alguienComprobando = true;
            }
            else if (temporizadorComprobacion <= 0)
            {
                // Establece un tiempo de espera breve si otro agente ya está comprobando
                temporizadorComprobacion = Random.Range(5f, 15f);
            }
        }

        DecidirEstado();
        EjecutarEstado();
    }

    // Evalúa los estímulos sensoriales para determinar el comportamiento jerárquico
    void DecidirEstado()
    {
        Vector3 origenRuido;

        // Prioridad máxima: Inicia la persecución inmediata si el objetivo es visible
        if (frodoVisible)
        {
            actuador.SetUltimaPosicionConocida(objetivoFrodo.position);
            if (estadoActual != EstadoOrco.PERSECUCION)
            {
                if (estadoActual == EstadoOrco.COMPROBAR_ANILLO)
                    alguienComprobando = false;
                
                if (estadoActual == EstadoOrco.PATRULLA || estadoActual == EstadoOrco.BLOQUEAR_SALIDA)
                    estadoPrevio = estadoActual;
            }
            estadoActual = EstadoOrco.PERSECUCION;
            temporizadorPersecucion = tiempoGraciaPersecucion;
            return;
        }

        // Mantiene la persecución activa basándose en rastros sonoros si se pierde la visión
        if (estadoActual == EstadoOrco.PERSECUCION)
        {
            temporizadorPersecucion -= Time.deltaTime;

            if (sensorOido.OirFrodo(out origenRuido))
            {
                actuador.SetUltimaPosicionConocida(origenRuido);
                temporizadorPersecucion = tiempoGraciaPersecucion;
            }

            // Transiciona a búsqueda si se agota el tiempo de gracia o llega al último rastro
            if (temporizadorPersecucion <= 0 || actuador.HaLlegado(1.5f))
            {
                estadoActual = EstadoOrco.BUSQUEDA;
                temporizadorBusqueda = tiempoBusqueda;
                actuador.IniciarBusqueda();
            }
            return;
        }

        // Gestiona el estado de búsqueda intensiva durante un tiempo limitado
        if (estadoActual == EstadoOrco.BUSQUEDA)
        {
            temporizadorBusqueda -= Time.deltaTime;

            if (temporizadorBusqueda <= 0)
            {
                if (estadoPrevio == EstadoOrco.BLOQUEAR_SALIDA)
                    estadoActual = EstadoOrco.BLOQUEAR_SALIDA;
                else
                    estadoActual = EstadoOrco.COMPROBAR_ANILLO;
            }
            return;
        }

        // Reacciona a estímulos auditivos para iniciar una investigación desde estados pasivos
        if (sensorOido.OirFrodo(out origenRuido))
        {
            actuador.SetUltimaPosicionConocida(origenRuido);

            if (estadoActual == EstadoOrco.PATRULLA
                || estadoActual == EstadoOrco.BLOQUEAR_SALIDA
                || estadoActual == EstadoOrco.COMPROBAR_ANILLO)
            {
                if (estadoActual == EstadoOrco.COMPROBAR_ANILLO)
                    alguienComprobando = false;
                
                if (estadoActual == EstadoOrco.PATRULLA || estadoActual == EstadoOrco.BLOQUEAR_SALIDA)
                    estadoPrevio = estadoActual;
                
                estadoActual = EstadoOrco.BUSQUEDA;
                temporizadorBusqueda = tiempoBusqueda;
                actuador.IniciarBusqueda();
            }
            return;
        }
    }

    // Invoca los métodos correspondientes del actuador según el estado activo
    void EjecutarEstado()
    {
        switch (estadoActual)
        {
            case EstadoOrco.PATRULLA:
                actuador.EjecutarPatrulla();
                break;

            case EstadoOrco.PERSECUCION:
                actuador.EjecutarPersecucion(frodoVisible);
                break;

            case EstadoOrco.BUSQUEDA:
                actuador.EjecutarBusqueda();
                break;

            case EstadoOrco.COMPROBAR_ANILLO:
                if (actuador.EjecutarComprobarAnillo())
                {
                    alguienComprobando = false;
                    estadoActual = sensorVista.AnilloEnPedestal()
                        ? EstadoOrco.PATRULLA
                        : EstadoOrco.BLOQUEAR_SALIDA;
                }
                break;

            case EstadoOrco.BLOQUEAR_SALIDA:
                actuador.EjecutarBloquearSalida();
                break;
        }
    }
}