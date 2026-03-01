using UnityEngine;
using UnityEngine.AI;

public class ActuadorMovimientoOrco : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadPatrulla = 3.5f;
    public float velocidadAlerta = 6.0f;
    public float velocidadPersecucion = 8f;

    [Header("Patrulla")]
    public Transform[] puntosPatrulla;

    [Header("Patrulla Bloqueo Salida")]
    public Transform[] puntosBloqueoSalida;

    [Header("Puntos Clave")]
    public Transform pedestalAnillo;
    public Transform puntoSalida;
    public Transform objetivoFrodo;

    [Header("Configuración")]
    public float tiempoEsperaInvestigacion = 2f;

    private NavMeshAgent agent;
    private int indicePatrulla = 0;
    private int indiceBloqueo = 0;
    private float temporizadorInvestigacion = 0f;
    private Vector3 ultimaPosicionConocida;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void SetUltimaPosicionConocida(Vector3 posicion)
    {
        ultimaPosicionConocida = posicion;
    }

    public void EjecutarPatrulla()
    {
        agent.speed = velocidadPatrulla;
        if (puntosPatrulla.Length == 0) return;

        agent.destination = puntosPatrulla[indicePatrulla].position;
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            indicePatrulla = (indicePatrulla + 1) % puntosPatrulla.Length;
    }

    // FIX Bug 2b: Recibe si está viendo a Frodo o no.
    // Si lo ve, va a su posición real. Si no (período de gracia), va a la última conocida.
    public void EjecutarPersecucion(bool viendoAFrodo)
    {
        agent.speed = velocidadPersecucion;
        if (viendoAFrodo && objetivoFrodo != null)
            agent.destination = objetivoFrodo.position;
        else
            agent.destination = ultimaPosicionConocida;
    }

    public bool EjecutarInvestigacion()
    {
        agent.speed = velocidadAlerta;
        agent.destination = ultimaPosicionConocida;

        if (!agent.pathPending && agent.remainingDistance < 1.0f)
        {
            temporizadorInvestigacion += Time.deltaTime;
            if (temporizadorInvestigacion >= tiempoEsperaInvestigacion)
            {
                temporizadorInvestigacion = 0f;
                return true;
            }
        }
        else
        {
            // Resetear temporizador si aún no ha llegado
            temporizadorInvestigacion = 0f;
        }
        return false;
    }

    public bool EjecutarComprobarAnillo()
    {
        agent.speed = velocidadPatrulla;
        if (pedestalAnillo != null)
            agent.destination = pedestalAnillo.position;
        return !agent.pathPending && agent.remainingDistance < 2.0f;
    }

    public void EjecutarBloquearSalida()
    {
        agent.speed = velocidadAlerta;

        if (puntosBloqueoSalida != null && puntosBloqueoSalida.Length > 0)
        {
            agent.destination = puntosBloqueoSalida[indiceBloqueo].position;
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
                indiceBloqueo = (indiceBloqueo + 1) % puntosBloqueoSalida.Length;
        }
        else if (puntoSalida != null)
        {
            agent.destination = puntoSalida.position;
        }
    }
}
