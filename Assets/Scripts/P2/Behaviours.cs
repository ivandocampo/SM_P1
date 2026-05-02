using UnityEngine;

public interface IBehavior
{
    void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador);

    bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador);

    void Detener(ActuadorMovimiento actuador);
}

// PATRULLA — Recorrido cíclico por waypoints


[System.Serializable]
public class PatrolBehavior : IBehavior
{
    private Transform[] puntosPatrulla;
    private int indiceActual = 0;

    public PatrolBehavior(Transform[] puntos)
    {
        puntosPatrulla = puntos;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosPatrulla == null || puntosPatrulla.Length == 0) return;
        actuador.SetDestino(puntosPatrulla[indiceActual].position, TipoVelocidad.Patrulla);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosPatrulla == null || puntosPatrulla.Length == 0) return false;

        if (actuador.HaLlegado(0.5f))
        {
            indiceActual = (indiceActual + 1) % puntosPatrulla.Length;
            actuador.SetDestino(puntosPatrulla[indiceActual].position, TipoVelocidad.Patrulla);
        }

        return false; // Nunca termina por sí solo
    }

    public void Detener(ActuadorMovimiento actuador)
    {
        // No detenemos al agente — simplemente dejará de ir al siguiente waypoint
    }
}

// PERSECUCIÓN — Ir hacia el ladrón a máxima velocidad


[System.Serializable]
public class PursuitBehavior : IBehavior
{
    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        actuador.SetDestino(creencias.UltimaPosicionLadron, TipoVelocidad.Persecucion);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        // Actualizar destino continuamente con la última posición conocida
        actuador.SetDestino(creencias.UltimaPosicionLadron, TipoVelocidad.Persecucion);

        // Terminamos si llegamos al destino y no lo vemos
        if (!creencias.LadronVisible && actuador.HaLlegado(1.5f))
        {
            return true; // Persecución terminada — pasar a búsqueda
        }

        return false;
    }

    public void Detener(ActuadorMovimiento actuador)
    {
        // Al dejar persecución, podemos reducir velocidad
        actuador.CambiarVelocidad(TipoVelocidad.Alerta);
    }
}

// BÚSQUEDA — Explorar puntos aleatorios cerca de la última posición

[System.Serializable]
public class SearchBehavior : IBehavior
{
    private float radioBusqueda;
    private int maxPuntos;
    private float duracionMaxima;

    private Vector3[] puntosBusqueda;
    private int indicePunto = 0;
    private float tiempoInicio;
    private float tiempoAtascado = 0f;
    private Vector3 posicionAnterior;

    public SearchBehavior(float radio = 12f, int puntos = 4, float duracion = 10f)
    {
        radioBusqueda = radio;
        maxPuntos = puntos;
        duracionMaxima = duracion;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        tiempoInicio = Time.time;
        indicePunto = 0;
        tiempoAtascado = 0f;
        posicionAnterior = actuador.transform.position;

        // Sesgar la búsqueda hacia delante si conocemos la dirección de huida.
        Vector3 centro = creencias.UltimaPosicionLadron;
        float radioActual = radioBusqueda;

        if (creencias.DebeBuscarAlrededorPedestal && creencias.TienePosicionPedestal)
        {
            centro = creencias.PosicionPedestal;
            radioActual = radioBusqueda * 0.5f;
            creencias.DebeBuscarAlrededorPedestal = false;
        }
        else if (creencias.TieneDireccionLadron)
        {
            centro += creencias.UltimaDireccionLadron * (radioBusqueda * 0.5f);
        }

        puntosBusqueda = new Vector3[maxPuntos];
        for (int i = 0; i < maxPuntos; i++)
        {
            puntosBusqueda[i] = actuador.GenerarPuntoAleatorio(centro, radioActual);
        }

        if (puntosBusqueda.Length > 0)
            actuador.SetDestino(puntosBusqueda[0], TipoVelocidad.Alerta);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosBusqueda == null || puntosBusqueda.Length == 0) return true;

        // Tiempo máximo alcanzado
        if (Time.time - tiempoInicio > duracionMaxima)
            return true;

        // Detectar si el agente está atascado
        if (Vector3.Distance(actuador.transform.position, posicionAnterior) < 0.3f)
        {
            tiempoAtascado += Time.deltaTime;
            if (tiempoAtascado > 2f)
            {
                indicePunto++;
                tiempoAtascado = 0f;
            }
        }
        else
        {
            tiempoAtascado = 0f;
            posicionAnterior = actuador.transform.position;
        }

        // Llegamos al punto actual
        if (actuador.HaLlegado(1.5f))
        {
            indicePunto++;
            tiempoAtascado = 0f;
        }

        // Todos los puntos visitados
        if (indicePunto >= puntosBusqueda.Length)
            return true;

        actuador.SetDestino(puntosBusqueda[indicePunto], TipoVelocidad.Alerta);
        return false;
    }

    public void Detener(ActuadorMovimiento actuador) { }
}

// BLOQUEAR SALIDA — Patrullar la zona de escape

// INTERCEPTAR - Cortar la ruta hacia el objetivo critico actual

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

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        ultimoRecalculo = -100f;
        ActualizarDestino(creencias, actuador);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        float ventanaInfo = creencias.AnilloRobado ? 25f : 20f;
        if (!creencias.TieneInfoReciente(ventanaInfo))
            return true;

        if (Time.time - ultimoRecalculo >= intervaloRecalculo || actuador.HaLlegado(1.2f))
            ActualizarDestino(creencias, actuador);

        return false;
    }

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

// BLOQUEAR SALIDA - Patrullar la zona de escape

[System.Serializable]
public class BlockExitBehavior : IBehavior
{
    private Transform puntoSalida;
    private Transform[] puntosBloqueo;
    private int indiceBloqueo = 0;

    public BlockExitBehavior(Transform salida, Transform[] puntosBloqueo)
    {
        this.puntoSalida = salida;
        this.puntosBloqueo = puntosBloqueo;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosBloqueo != null && puntosBloqueo.Length > 0)
        {
            actuador.SetDestino(puntosBloqueo[indiceBloqueo].position, TipoVelocidad.Alerta);
        }
        else if (puntoSalida != null)
        {
            actuador.SetDestino(puntoSalida.position, TipoVelocidad.Alerta);
        }
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosBloqueo != null && puntosBloqueo.Length > 0)
        {
            if (actuador.HaLlegado(0.5f))
            {
                indiceBloqueo = (indiceBloqueo + 1) % puntosBloqueo.Length;
                actuador.SetDestino(puntosBloqueo[indiceBloqueo].position, TipoVelocidad.Alerta);
            }
        }
        else if (puntoSalida != null)
        {
            actuador.SetDestino(puntoSalida.position, TipoVelocidad.Alerta);
        }

        return false; // Nunca termina por sí solo
    }

    public void Detener(ActuadorMovimiento actuador) { }
}

// COMPROBAR PEDESTAL — Ir al pedestal y verificar el anillo

[System.Serializable]
public class CheckPedestalBehavior : IBehavior
{
    private Transform pedestal;

    public CheckPedestalBehavior(Transform pedestal)
    {
        this.pedestal = pedestal;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (pedestal != null)
            actuador.SetDestino(pedestal.position, TipoVelocidad.Alerta);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        return actuador.HaLlegado(2f);
    }

    public void Detener(ActuadorMovimiento actuador) { }
}

// BÚSQUEDA ASIGNADA — Ejecutar tarea del Contract Net

[System.Serializable]
public class SearchAssignedBehavior : IBehavior
{
    private Vector3[] puntosBusqueda;
    private int indicePunto = 0;

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        indicePunto = 0;

        SearchTask tarea = creencias.TareaAsignada;
        if (tarea == null)
        {
            puntosBusqueda = new Vector3[0];
            return;
        }

        Vector3[] puntosZona = creencias.ObtenerPuntosZona(tarea.ZoneId);
        if (puntosZona != null && puntosZona.Length > 0)
        {
            puntosBusqueda = puntosZona;
        }
        else
        {
            puntosBusqueda = new Vector3[0];
            Debug.LogWarning($"[SearchAssigned] Zona no valida o sin puntos: '{tarea.ZoneId}'");
            return;
        }

        if (puntosBusqueda.Length > 0)
            actuador.SetDestino(puntosBusqueda[0], TipoVelocidad.Alerta);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosBusqueda == null || puntosBusqueda.Length == 0) return true;

        if (actuador.HaLlegado(1.5f))
        {
            indicePunto++;
        }

        if (indicePunto >= puntosBusqueda.Length)
            return true;

        actuador.SetDestino(puntosBusqueda[indicePunto], TipoVelocidad.Alerta);
        return false;
    }

    public void Detener(ActuadorMovimiento actuador) { }
}


public enum BehaviorType
{
    Patrol,
    Pursuit,
    Search,
    SearchAssigned,
    Intercept,
    BlockExit,
    CheckPedestal,
    None
}
