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

    [Header("Temporizadores")]
    public float tiempoGraciaPersecucion = 2f;   // Segundos que sigue persiguiendo sin ver a Frodo
    public float tiempoCooldownOido = 8f;         // Segundos que ignora ruidos tras falsa alarma

    private EstadoOrco estadoActual = EstadoOrco.PATRULLA;
    private EstadoOrco estadoPrevio = EstadoOrco.PATRULLA;
    private ActuadorMovimientoOrco actuador;
    private SensorVistaOrco sensorVista;
    private SensorOidoOrco sensorOido;

    private float temporizadorPersecucion = 0f;
    private float cooldownOido = 0f;

    void Start()
    {
        actuador = GetComponent<ActuadorMovimientoOrco>();
        sensorVista = GetComponent<SensorVistaOrco>();
        sensorOido = GetComponent<SensorOidoOrco>();
    }

    void Update()
    {
        if (!GameManager.Instance.PartidaActiva) return;

        // Restar cooldowns
        cooldownOido -= Time.deltaTime;

        // FIX Bug 1: La captura es por distancia pura, no depende de la vista.
        // Si el orco está tocando a Frodo, lo atrapa aunque esté de espaldas.
        if (Vector3.Distance(transform.position, objetivoFrodo.position) < distanciaAtaque)
        {
            GameManager.Instance.FrodoCapturado();
            return;
        }

        // FIX Bug 5: Comprobar si falta el anillo SIEMPRE, independientemente del oído.
        // Así un ruido en el mismo frame no "tapa" la detección del pedestal vacío.
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

        if (sensorVista.VerFrodo())
        {
            // Ve a Frodo: perseguir y resetear el temporizador de gracia
            actuador.SetUltimaPosicionConocida(objetivoFrodo.position);
            estadoActual = EstadoOrco.PERSECUCION;
            temporizadorPersecucion = tiempoGraciaPersecucion;
        }
        else if (estadoActual == EstadoOrco.PERSECUCION)
        {
            // FIX Bug 2: No rendirse inmediatamente al perder la vista.
            // Sigue persiguiendo hacia la última posición conocida durante unos segundos.
            actuador.SetUltimaPosicionConocida(objetivoFrodo.position);
            temporizadorPersecucion -= Time.deltaTime;
            if (temporizadorPersecucion <= 0)
            {
                // Se acabó el tiempo de gracia: pasa a investigar
                estadoPrevio = EstadoOrco.PATRULLA;
                estadoActual = EstadoOrco.INVESTIGACION;
            }
        }
        // FIX Bug 3: Respetar el cooldown del oído para evitar bucles entre orcos.
        // FIX Bug 3b: Permitir investigar desde COMPROBAR_ANILLO también.
        else if (cooldownOido <= 0 && sensorOido.OirRuido(out origenRuido))
        {
            actuador.SetUltimaPosicionConocida(origenRuido);
            if (estadoActual == EstadoOrco.PATRULLA 
                || estadoActual == EstadoOrco.BLOQUEAR_SALIDA 
                || estadoActual == EstadoOrco.COMPROBAR_ANILLO)
            {
                estadoPrevio = estadoActual;
                estadoActual = EstadoOrco.INVESTIGACION;
            }
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
                // FIX Bug 2b: Si lo ve, persigue su posición real.
                // Si no lo ve (gracia), persigue la última posición conocida.
                actuador.EjecutarPersecucion(sensorVista.VerFrodo());
                break;

            case EstadoOrco.INVESTIGACION:
                if (actuador.EjecutarInvestigacion())
                {
                    if (sensorVista.VerCompañeroCerca())
                    {
                        // Falsa alarma: era un compañero
                        estadoActual = estadoPrevio;
                        // FIX Bug 3: Activar cooldown para no volver a investigar al mismo compañero
                        cooldownOido = tiempoCooldownOido;
                    }
                    else
                    {
                        // No ve a nadie: va a comprobar si el anillo sigue en su sitio
                        estadoActual = EstadoOrco.COMPROBAR_ANILLO;
                    }
                }
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
