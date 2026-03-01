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
    public float tiempoBusqueda = 10f;

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA;
    private EstadoOrco estadoPrevio = EstadoOrco.PATRULLA;
    private ActuadorMovimientoOrco actuador;
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;

    private float temporizadorPersecucion = 0f;
    private float temporizadorBusqueda = 0f;

    void Start()
    {
        actuador = GetComponent<ActuadorMovimientoOrco>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

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
        // =============================================
        if (sensorVista.VerFrodo())
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

            // Si oye algo durante la gracia, actualiza destino y resetea
            if (sensorOido.OirRuido(out origenRuido))
            {
                actuador.SetUltimaPosicionConocida(origenRuido);
                temporizadorPersecucion = tiempoGraciaPersecucion;
            }

            // Gracia acabada O ya llegó al último sitio → búsqueda inmediata
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
        // =============================================
        if (estadoActual == EstadoOrco.BUSQUEDA)
        {
            temporizadorBusqueda -= Time.deltaTime;

            // Si ve a un compañero durante la búsqueda, falsa alarma
            if (sensorVista.VerCompañeroCerca())
            {
                estadoActual = estadoPrevio;
                return;
            }

            // Oye cualquier ruido → actualiza posición conocida (nuevos puntos se generarán al llegar al actual)
            Vector3 origenRuidoBusqueda;
            if (sensorOido.OirRuido(out origenRuidoBusqueda))
            {
                actuador.SetUltimaPosicionConocida(origenRuidoBusqueda);

                // Solo ruido fuerte (correr) resetea el timer
                Vector3 dummy;
                if (sensorOido.OirRuidoFuerte(out dummy))
                {
                    temporizadorBusqueda = tiempoBusqueda;
                }
            }

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
        // PRIORIDAD 4: OÍDO — reactivo desde otros estados
        // =============================================
        if (sensorOido.OirRuido(out origenRuido))
        {
            actuador.SetUltimaPosicionConocida(origenRuido);

            if (estadoActual == EstadoOrco.PATRULLA
                || estadoActual == EstadoOrco.BLOQUEAR_SALIDA
                || estadoActual == EstadoOrco.COMPROBAR_ANILLO)
            {
                // Solo guardar estadoPrevio si es un estado "base" (no COMPROBAR_ANILLO)
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
                actuador.EjecutarPersecucion(sensorVista.VerFrodo());
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