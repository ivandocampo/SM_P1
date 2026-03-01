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
    public Transform[] puntosBloqueoSalida;   // Asignar 2-3 waypoints cerca de la salida por orco

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

    // Llamado por CerebroOrco al entrar en INVESTIGACION para resetear el temporizador
    public void ResetearInvestigacion()
    {
        temporizadorInvestigacion = 0f;
    }

    public void EjecutarPatrulla()
    {
        agent.speed = velocidadPatrulla;
        if (puntosPatrulla.Length == 0) return;

        agent.destination = puntosPatrulla[indicePatrulla].position;
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            indicePatrulla = (indicePatrulla + 1) % puntosPatrulla.Length;
    }

    public void EjecutarPersecucion()
    {
        agent.speed = velocidadPersecucion;
        agent.destination = objetivoFrodo.position;
    }

    // Devuelve true cuando termina de investigar (llegó + esperó 2s)
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
        return false;
    }

    // Devuelve true cuando llega al pedestal
    public bool EjecutarComprobarAnillo()
    {
        agent.speed = velocidadAlerta;
        agent.destination = pedestalAnillo.position;
        return !agent.pathPending && agent.remainingDistance < 2.0f;
    }

    public void EjecutarBloquearSalida()
    {
        agent.speed = velocidadAlerta;
        
        if (puntosBloqueoSalida.Length > 0)
        {
            agent.destination = puntosBloqueoSalida[indiceBloqueo].position;
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
                indiceBloqueo = (indiceBloqueo + 1) % puntosBloqueoSalida.Length;
        }
        else
        {
            agent.destination = puntoSalida.position;
        }
    }
}