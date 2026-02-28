using UnityEngine;

public class CerebroOrco : MonoBehaviour
{
    public enum EstadoOrco
    {
        PATRULLA,
        INVESTIGACION,
        PERSECUCION,
        COMPROBAR_ANILLO,
        BLOQUEAR_SALIDA
    }

    [Header("Referencias")]
    public Transform objetivoFrodo;
    public float distanciaAtaque = 1.2f;

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA;
    private EstadoOrco estadoPrevio = EstadoOrco.PATRULLA;  // Para volver si el ruido era un compañero
    private ActuadorMovimientoOrco actuador;
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;

    void Start()
    {
        actuador = GetComponent<ActuadorMovimientoOrco>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;
        if (sensorVista.VerFrodo() && Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            GameManager.Instance.FrodoCapturado();
            return;
        }

        DecidirEstado();
        EjecutarEstado();
    }

    // Consulta los sensores y actualiza el estado del agente
    void DecidirEstado()
    {
        Vector3 origenRuido;

        if (sensorVista.VerFrodo())
        {
            actuador.SetUltimaPosicionConocida(objetivoFrodo.position);
            estadoActual = EstadoOrco.PERSECUCION;
        }
        else if (estadoActual == EstadoOrco.PERSECUCION)
        {
            estadoPrevio = EstadoOrco.PATRULLA; // Tras persecución fallida, si era falsa alarma vuelve a patrullar
            estadoActual = EstadoOrco.INVESTIGACION;
        }
        // Si oye ruido desde patrulla o bloqueo, va a investigar
        else if (sensorOido.OirRuido(out origenRuido))
        {
            actuador.SetUltimaPosicionConocida(origenRuido);
            if (estadoActual == EstadoOrco.PATRULLA || estadoActual == EstadoOrco.BLOQUEAR_SALIDA)
            {
                estadoPrevio = estadoActual; // Recuerda si estaba patrullando o bloqueando
                estadoActual = EstadoOrco.INVESTIGACION;
            }
        }
        else if (sensorVista.NoAnillo() && estadoActual == EstadoOrco.PATRULLA)
        {
            estadoActual = EstadoOrco.BLOQUEAR_SALIDA;
        }
    }

    // Delega la ejecución al actuador según el estado actual
    void EjecutarEstado()
    {
        switch (estadoActual)
        {
            case EstadoOrco.PATRULLA:
                actuador.EjecutarPatrulla();
                break;
            case EstadoOrco.PERSECUCION:
                actuador.EjecutarPersecucion();
                break;
            case EstadoOrco.INVESTIGACION:
                if (actuador.EjecutarInvestigacion())
                {
                    // Al llegar al origen del ruido, mira a su alrededor
                    if (sensorVista.VerCompañeroCerca())
                    {
                        // Solo ve a un compañero orco: falsa alarma, vuelve a lo que estaba haciendo
                        estadoActual = estadoPrevio;
                    }
                    else
                    {
                        // No ve a nadie o ve algo sospechoso: va a comprobar el anillo
                        estadoActual = EstadoOrco.COMPROBAR_ANILLO;
                    }
                }
                break;
            case EstadoOrco.COMPROBAR_ANILLO:
                if (actuador.EjecutarComprobarAnillo())
                    // El orco mira con sus propios ojos si el anillo sigue en el pedestal
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