// =============================================================
// Behavior de intercepción: corta la ruta del ladrón hacia su objetivo.
// Recalcula el punto de corte cada intervaloRecalculo segundos o al llegar.
// Si la info sobre el ladrón caduca o se supera el tiempo de gracia sin
// visual, activa BuscarLocalAntesDeCoordinar y cede el turno al ciclo BDI
// =============================================================

using UnityEngine;

[System.Serializable]
public class InterceptBehavior : IBehavior
{
    private float distanciaAdelante;
    private float distanciaLateral;
    private float intervaloRecalculo;
    private float ultimoRecalculo = -100f;
    private Vector3 destinoActual = Vector3.zero;

    public InterceptBehavior(float adelante = 6f, float lateral = 4f, float intervalo = 0.5f)
    {
        distanciaAdelante = adelante;
        distanciaLateral = lateral;
        intervaloRecalculo = intervalo;
    }

    // Forzar recálculo inmediato al iniciar para obtener el punto de corte más fresco
    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        ultimoRecalculo = -100f;
        ActualizarDestino(creencias, actuador);
    }

    // Terminar si la info es demasiado antigua o si se supera el tiempo de gracia sin visual
    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        float ventanaInfo = creencias.AnilloRobado ? 25f : 20f;
        if (!creencias.TieneInfoReciente(ventanaInfo))
            return true;

        if (!creencias.LadronVisible &&
            creencias.AntiguedadInfoLadron >= BeliefBase.TIEMPO_GRACIA_PERDIDA_LADRON)
        {
            creencias.BuscarLocalAntesDeCoordinar = true;
            return true;
        }

        bool haLlegado = actuador.HaLlegado(1.2f);
        if (Time.time - ultimoRecalculo >= intervaloRecalculo || haLlegado)
            ActualizarDestino(creencias, actuador);

        return false;
    }

    // Recalcular el punto de corte; si no es alcanzable por NavMesh, buscar uno alternativo cercano
    private void ActualizarDestino(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        ultimoRecalculo = Time.time;
        destinoActual = creencias.CalcularPuntoInterceptacion(distanciaAdelante, distanciaLateral);

        if (!actuador.PuntoAlcanzable(destinoActual))
            destinoActual = actuador.GenerarPuntoAleatorio(destinoActual, 3f);

        actuador.SetDestino(destinoActual, TipoVelocidad.Alerta);
    }

    public void Detener(ActuadorMovimiento actuador) { }
}
