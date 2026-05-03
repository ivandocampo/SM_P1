using UnityEngine;

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
    private Vector3 centroBusquedaActual;
    private const float UMBRAL_RECENTRAR_BUSQUEDA_SQR = 16f;

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
        GenerarPuntosBusqueda(creencias, actuador);
    }

    private void GenerarPuntosBusqueda(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        Vector3 centro = creencias.UltimaPosicionLadron;
        float radioActual = radioBusqueda;

        if (creencias.TieneDireccionLadron)
        {
            centro += creencias.UltimaDireccionLadron * (radioBusqueda * 0.5f);
        }

        centroBusquedaActual = centro;
        puntosBusqueda = new Vector3[maxPuntos];
        indicePunto = 0;
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

        Vector3 centroDeseado = creencias.UltimaPosicionLadron;
        if (creencias.TieneDireccionLadron)
            centroDeseado += creencias.UltimaDireccionLadron * (radioBusqueda * 0.5f);

        if ((centroDeseado - centroBusquedaActual).sqrMagnitude > UMBRAL_RECENTRAR_BUSQUEDA_SQR)
        {
            GenerarPuntosBusqueda(creencias, actuador);
            posicionAnterior = actuador.transform.position;
            tiempoAtascado = 0f;
        }

        if (Time.time - tiempoInicio > duracionMaxima)
            return true;

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

        if (actuador.HaLlegado(1.5f))
        {
            indicePunto++;
            tiempoAtascado = 0f;
        }

        if (indicePunto >= puntosBusqueda.Length)
            return true;

        actuador.SetDestino(puntosBusqueda[indicePunto], TipoVelocidad.Alerta);
        return false;
    }

    public void Detener(ActuadorMovimiento actuador) { }
}
