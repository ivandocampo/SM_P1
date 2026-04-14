using System.Collections.Generic;
using UnityEngine;

public class ContractNetManager
{
    private BeliefBase creencias;
    private ComunicacionAgente comunicacion;
    private string agentId;
    private float cooldown;
    private float ultimoContractNet = -100f;

    public ContractNetManager(BeliefBase creencias, ComunicacionAgente comunicacion,
                               string id, float cooldown)
    {
        this.creencias = creencias;
        this.comunicacion = comunicacion;
        this.agentId = id;
        this.cooldown = cooldown;
    }

    public void IniciarDistribucionBusqueda()
    {
        if (Time.time - ultimoContractNet < cooldown) return;
        ultimoContractNet = Time.time;

        List<string> otrosGuardias = AgentRegistry.Instance
            .ObtenerOtrosIdsPorTipo("guard", agentId);

        if (otrosGuardias.Count == 0) return;

        SearchTask tarea = new SearchTask
        {
            TaskId = $"search-{agentId}-{Time.time:F0}",
            TargetArea = new Position(creencias.UltimaPosicionLadron),
            Radius = 15f,
            Urgency = 0.8f
        };

        comunicacion.IniciarContractNet(tarea, otrosGuardias, timeout: 2f);
        Debug.Log($"[{agentId}] Contract Net iniciado");
    }

    public void Gestionar()
    {
        if (comunicacion.ContractNetListoParaEvaluar())
        {
            ACLMessage ganador = comunicacion.EvaluarYAdjudicar();
            if (ganador != null)
                Debug.Log($"[{agentId}] Contract Net resuelto: {ganador.Sender} buscará");
        }
    }
}