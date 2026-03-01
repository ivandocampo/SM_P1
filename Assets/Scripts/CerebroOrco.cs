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
    public float tiempoEntreComprobaciones = 30f; // Cada cuánto va a comprobar el anillo

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA;
    private EstadoOrco estadoPrevio = EstadoOrco.PATRULLA;
    private ActuadorMovimientoOrco actuador;
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;

    private float temporizadorPersecucion = 0f;
    private float temporizadorBusqueda = 0f;
    private float temporizadorComprobacion = 0f;

    private bool frodoVisible;

    // Solo 1 orco comprueba el anillo a la vez
    private static bool alguienComprobando = false;

    void Start()
    {
        actuador = GetComponent<ActuadorMovimientoOrco>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();

        // Offset aleatorio para que no vayan todos a comprobar a la vez
        temporizadorComprobacion = Random.Range(10f, tiempoEntreComprobaciones);
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        frodoVisible = sensorVista.VerFrodo();

        if (Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            GameManager.Instance.FrodoCapturado();
            return;
        }

        if (sensorVista.NoAnillo() && estadoActual == EstadoOrco.PATRULLA)
        {
            estadoActual = EstadoOrco.BLOQUEAR_SALIDA;
        }

        // Comprobación periódica del anillo durante patrulla (solo 1 orco a la vez)
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
                // Otro ya está comprobando, esperar un poco más
                temporizadorComprobacion = Random.Range(5f, 15f);
            }
        }

        DecidirEstado();
        EjecutarEstado();
    }

    void DecidirEstado()
    {
        Vector3 origenRuido;

        // PRIORIDAD 1: VISTA — si ve a Frodo, perseguir SIEMPRE
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

        // PRIORIDAD 2: PERSECUCIÓN — gracia, solo oye a Frodo
        if (estadoActual == EstadoOrco.PERSECUCION)
        {
            temporizadorPersecucion -= Time.deltaTime;

            if (sensorOido.OirFrodo(out origenRuido))
            {
                actuador.SetUltimaPosicionConocida(origenRuido);
                temporizadorPersecucion = tiempoGraciaPersecucion;
            }

            if (temporizadorPersecucion <= 0 || actuador.HaLlegado(1.5f))
            {
                estadoActual = EstadoOrco.BUSQUEDA;
                temporizadorBusqueda = tiempoBusqueda;
                actuador.IniciarBusqueda();
            }
            return;
        }

        // PRIORIDAD 3: BÚSQUEDA — 7 segundos, 100% sorda
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

        // PRIORIDAD 4: OÍDO — solo Frodo activa búsqueda
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