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

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA;
    private EstadoOrco estadoPrevio = EstadoOrco.PATRULLA;
    private ActuadorMovimientoOrco actuador;
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;

    private float temporizadorPersecucion = 0f;
    private float temporizadorBusqueda = 0f;

    // Cache para no llamar VerFrodo() varias veces por frame
    private bool frodoVisible;

    void Start()
    {
        actuador = GetComponent<ActuadorMovimientoOrco>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();
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

        DecidirEstado();
        EjecutarEstado();
    }

    void DecidirEstado()
    {
        Vector3 origenRuido;

        // =============================================
        // PRIORIDAD 1: VISTA — si ve a Frodo, perseguir SIEMPRE
        // (Aplica desde CUALQUIER estado, incluida BUSQUEDA)
        // =============================================
        if (frodoVisible)
        {
            actuador.SetUltimaPosicionConocida(objetivoFrodo.position);
            if (estadoActual != EstadoOrco.PERSECUCION)
                estadoPrevio = estadoActual;
            estadoActual = EstadoOrco.PERSECUCION;
            temporizadorPersecucion = tiempoGraciaPersecucion;
            return;
        }

        // =============================================
        // PRIORIDAD 2: PERSECUCIÓN — gracia corriendo al último sitio
        // =============================================
        if (estadoActual == EstadoOrco.PERSECUCION)
        {
            temporizadorPersecucion -= Time.deltaTime;

            // Solo escuchar a Frodo durante la gracia (ignorar otros orcos)
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

        // =============================================
        // PRIORIDAD 3: BÚSQUEDA — explorando la zona
        // 100% SORDA. 7 segundos y sale. Sin excepciones.
        // Solo sale por: ver a Frodo (prioridad 1) o timer agotado.
        // =============================================
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

        // =============================================
        // PRIORIDAD 4: OÍDO — solo reacciona a Frodo
        // (Otros orcos NO activan búsqueda: elimina bucles)
        // =============================================
        if (sensorOido.OirFrodo(out origenRuido))
        {
            actuador.SetUltimaPosicionConocida(origenRuido);

            if (estadoActual == EstadoOrco.PATRULLA
                || estadoActual == EstadoOrco.BLOQUEAR_SALIDA
                || estadoActual == EstadoOrco.COMPROBAR_ANILLO)
            {
                if (estadoActual != EstadoOrco.COMPROBAR_ANILLO)
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
                    estadoActual = sensorVista.AnilloEnPedestal()
                        ? EstadoOrco.PATRULLA
                        : EstadoOrco.BLOQUEAR_SALIDA;
                break;

            case EstadoOrco.BLOQUEAR_SALIDA:
                actuador.EjecutarBloquearSalida();
                break;
        }
    }
}