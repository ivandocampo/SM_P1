using System.Collections.Generic;
using UnityEngine;


public class AgentRegistry : MonoBehaviour
{
    public static AgentRegistry Instance { get; private set; }

    // Almacena las referencias a los componentes de comunicación de cada agente
    private Dictionary<string, ComunicacionAgente> agentesRegistrados =
        new Dictionary<string, ComunicacionAgente>();

    // Almacena el tipo de cada agente para permitir filtrado
    private Dictionary<string, string> tiposAgente =
        new Dictionary<string, string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    
    public void Registrar(string agentId, string tipo, ComunicacionAgente comunicacion)
    {
        agentesRegistrados[agentId] = comunicacion;
        tiposAgente[agentId] = tipo;
        Debug.Log($"[Registry] Agente registrado: {agentId} (tipo: {tipo})");
    }

    
    public void Desregistrar(string agentId)
    {
        agentesRegistrados.Remove(agentId);
        tiposAgente.Remove(agentId);
    }

    
    public ComunicacionAgente ObtenerAgente(string agentId)
    {
        agentesRegistrados.TryGetValue(agentId, out ComunicacionAgente agente);
        return agente;
    }

    
    public List<string> ObtenerTodosLosIds()
    {
        return new List<string>(agentesRegistrados.Keys);
    }

    
    public List<string> ObtenerOtrosIds(string excluirId)
    {
        List<string> otros = new List<string>();
        foreach (string id in agentesRegistrados.Keys)
        {
            if (id != excluirId)
                otros.Add(id);
        }
        return otros;
    }

    
    public List<string> ObtenerIdsPorTipo(string tipo)
    {
        List<string> resultado = new List<string>();
        foreach (var par in tiposAgente)
        {
            if (par.Value == tipo)
                resultado.Add(par.Key);
        }
        return resultado;
    }

    
    public List<string> ObtenerOtrosIdsPorTipo(string tipo, string excluirId)
    {
        List<string> resultado = new List<string>();
        foreach (var par in tiposAgente)
        {
            if (par.Value == tipo && par.Key != excluirId)
                resultado.Add(par.Key);
        }
        return resultado;
    }

    
    public string ObtenerTipo(string agentId)
    {
        tiposAgente.TryGetValue(agentId, out string tipo);
        return tipo;
    }

    
    public int NumeroDeAgentes => agentesRegistrados.Count;
}