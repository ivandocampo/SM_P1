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

    [Header("Cooldown tras falsa alarma")]
    public float cooldownFalsaAlarma = 5f;

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA;
    private EstadoOrco estadoPrevio = EstadoOrco.PATRULLA;
    private ActuadorMovimientoOrco actuador;
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;
    private float temporizadorCooldown = 0f;

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

        if (temporizadorCooldown > 0f)
            temporizadorCooldown -= Time.deltaTime;

        DecidirEstado();
        EjecutarEstado();
    }

    void CambiarEstado(EstadoOrco nuevoEstado)
    {
        if (nuevoEstado == EstadoOrco.INVESTIGACION)
            actuador.ResetearInvestigacion();

        estadoActual = nuevoEstado;
    }

    void DecidirEstado()
    {
        Vector3 origenRuido;

        if (sensorVista.VerFrodo())
        {
            actuador.SetUltimaPosicionConocida(objetivoFrodo.position);
            CambiarEstado(EstadoOrco.PERSECUCION);
        }
        else if (estadoActual == EstadoOrco.PERSECUCION)
        {
            estadoPrevio = EstadoOrco.PATRULLA;
            CambiarEstado(EstadoOrco.INVESTIGACION);
        }
        else if (temporizadorCooldown <= 0f && sensorOido.OirRuido(out origenRuido))
        {
            if (estadoActual == EstadoOrco.PATRULLA || estadoActual == EstadoOrco.BLOQUEAR_SALIDA)
            {
                actuador.SetUltimaPosicionConocida(origenRuido);
                estadoPrevio = estadoActual;
                CambiarEstado(EstadoOrco.INVESTIGACION);
            }
        }
        else if (sensorVista.NoAnillo() && estadoActual == EstadoOrco.PATRULLA)
        {
            CambiarEstado(EstadoOrco.BLOQUEAR_SALIDA);
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
                actuador.EjecutarPersecucion();
                break;

            case EstadoOrco.INVESTIGACION:
                if (actuador.EjecutarInvestigacion())
                {
                    if (sensorVista.VerCompañeroCerca())
                    {
                        temporizadorCooldown = cooldownFalsaAlarma;
                        CambiarEstado(estadoPrevio);
                    }
                    else
                    {
                        CambiarEstado(EstadoOrco.COMPROBAR_ANILLO);
                    }
                }
                break;

            case EstadoOrco.COMPROBAR_ANILLO:
                if (actuador.EjecutarComprobarAnillo())
                {
                    if (sensorVista.AnilloEnPedestal())
                        CambiarEstado(EstadoOrco.PATRULLA);
                    else
                        CambiarEstado(EstadoOrco.BLOQUEAR_SALIDA);
                }
                break;

            case EstadoOrco.BLOQUEAR_SALIDA:
                actuador.EjecutarBloquearSalida();
                break;
        }
    }
}