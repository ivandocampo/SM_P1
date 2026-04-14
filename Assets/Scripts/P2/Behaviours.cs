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

        // Generar puntos de búsqueda alrededor de la última posición del ladrón
        Vector3 centro = creencias.UltimaPosicionLadron;
        puntosBusqueda = new Vector3[maxPuntos];
        for (int i = 0; i < maxPuntos; i++)
        {
            puntosBusqueda[i] = actuador.GenerarPuntoAleatorio(centro, radioBusqueda);
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

// INVESTIGAR — Ir a una posición reportada y escanear

[System.Serializable]
public class InvestigateBehavior : IBehavior
{
    private Vector3 posicionObjetivo;
    private bool haLlegado = false;
    private float tiempoEscaneo = 3f;
    private float temporizadorEscaneo = 0f;

    public InvestigateBehavior(float tiempoEscaneo = 3f)
    {
        this.tiempoEscaneo = tiempoEscaneo;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        posicionObjetivo = creencias.UltimaPosicionLadron;
        haLlegado = false;
        temporizadorEscaneo = 0f;
        actuador.SetDestino(posicionObjetivo, TipoVelocidad.Alerta);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (!haLlegado)
        {
            if (actuador.HaLlegado(2f))
            {
                haLlegado = true;
                actuador.Detener();
            }
            return false;
        }

        // Fase de escaneo — simplemente esperar (los sensores harán su trabajo)
        temporizadorEscaneo += Time.deltaTime;
        // Rotar lentamente para escanear el área
        actuador.transform.Rotate(0, 90f * Time.deltaTime, 0);

        return temporizadorEscaneo >= tiempoEscaneo;
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
    private float tiempoInicio;
    private float duracionMaxima = 15f;

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        tiempoInicio = Time.time;
        indicePunto = 0;

        SearchTask tarea = creencias.TareaAsignada;
        if (tarea == null)
        {
            puntosBusqueda = new Vector3[0];
            return;
        }

        Vector3 centro = tarea.TargetArea.ToVector3();
        float radio = tarea.Radius;
        int numPuntos = 5;

        puntosBusqueda = new Vector3[numPuntos];
        for (int i = 0; i < numPuntos; i++)
        {
            puntosBusqueda[i] = actuador.GenerarPuntoAleatorio(centro, radio);
        }

        if (puntosBusqueda.Length > 0)
            actuador.SetDestino(puntosBusqueda[0], TipoVelocidad.Alerta);
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (puntosBusqueda == null || puntosBusqueda.Length == 0) return true;

        if (Time.time - tiempoInicio > duracionMaxima)
            return true;

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
    BlockExit,
    Investigate,
    CheckPedestal,
    None
}