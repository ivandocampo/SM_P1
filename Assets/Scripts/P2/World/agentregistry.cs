// =============================================================
// Registro global de agentes del mundo.
// Mantiene las referencias a los componentes ComunicacionAgente y
// permite localizar destinatarios por identificador o por tipo.
// Es la pieza que usa la comunicacion para enviar mensajes directos,
// broadcast general y broadcast filtrado
// =============================================================

using System.Collections.Generic;
using UnityEngine;


public class AgentRegistry : MonoBehaviour
{
    // Instancia global accesible desde los componentes de comunicacion.
    public static AgentRegistry Instance { get; private set; }

    // Relaciona cada identificador de agente con su buzon de comunicacion.
    private Dictionary<string, ComunicacionAgente> agentesRegistrados =
        new Dictionary<string, ComunicacionAgente>();

    // Relaciona cada identificador con su tipo: guard, spider, etc.
    private Dictionary<string, string> tiposAgente =
        new Dictionary<string, string>();

    void Awake()
    {
        // Singleton simple: si ya existe otro registro, se elimina el duplicado.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    
    public void Registrar(string agentId, string tipo, ComunicacionAgente comunicacion)
    {
        // Evita que dos objetos distintos usen el mismo identificador de agente.
        if (agentesRegistrados.TryGetValue(agentId, out ComunicacionAgente existente) &&
            existente != null &&
            existente != comunicacion)
        {
            Debug.LogWarning($"[Registry] ID duplicado '{agentId}'. Se mantiene el agente ya registrado.");
            return;
        }

        // Guarda la referencia de comunicacion y el tipo para busquedas posteriores.
        agentesRegistrados[agentId] = comunicacion;
        tiposAgente[agentId] = tipo;
        Debug.Log($"[Registry] Agente registrado: {agentId} (tipo: {tipo})");
    }

    
    public void Desregistrar(string agentId)
    {
        // Elimina tanto el buzon como el tipo asociado al agente.
        agentesRegistrados.Remove(agentId);
        tiposAgente.Remove(agentId);
    }

    
    public ComunicacionAgente ObtenerAgente(string agentId)
    {
        // Devuelve el componente de comunicacion asociado a un ID concreto.
        agentesRegistrados.TryGetValue(agentId, out ComunicacionAgente agente);
        // Si Unity destruyo el objeto pero la clave sigue guardada, se limpia el registro.
        if (agente == null && agentesRegistrados.ContainsKey(agentId))
            Desregistrar(agentId);

        return agente;
    }

    
    public List<string> ObtenerTodosLosIds()
    {
        // Copia la lista de IDs para que quien la recibe no modifique el diccionario interno.
        return new List<string>(agentesRegistrados.Keys);
    }

    
    public List<string> ObtenerOtrosIds(string excluirId)
    {
        // Devuelve todos los agentes excepto el indicado, util para broadcast sin enviarse a si mismo.
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
        // Filtra agentes por tipo, por ejemplo todos los guardias o todas las aranas.
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
        // Igual que ObtenerIdsPorTipo, pero excluyendo al agente emisor.
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
        // Consulta rapida del tipo asociado a un agente concreto.
        tiposAgente.TryGetValue(agentId, out string tipo);
        return tipo;
    }

    // Numero total de agentes actualmente registrados.
    public int NumeroDeAgentes => agentesRegistrados.Count;
}
