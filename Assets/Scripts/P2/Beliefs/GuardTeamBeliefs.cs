using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BeliefBase
{
    /// <summary>Estados reportados por otros guardias.</summary>
    public Dictionary<string, GuardStatus> EstadosOtrosGuardias { get; private set; }
        = new Dictionary<string, GuardStatus>();

    /// <summary>Timestamps de la ultima actualizacion de cada guardia.</summary>
    private Dictionary<string, float> ultimaActualizacionGuardias =
        new Dictionary<string, float>();

    /// <summary>Tiempo maximo sin actualizacion antes de considerar un guardia como stale.</summary>
    private const float TIEMPO_STALE_GUARDIA = 30f;

    public void ActualizarEstadoGuardia(GuardStatus estado)
    {
        if (estado != null && !string.IsNullOrEmpty(estado.GuardId))
        {
            EstadosOtrosGuardias[estado.GuardId] = estado;
            ultimaActualizacionGuardias[estado.GuardId] = Time.time;
        }
    }

    public void ActualizarDisponibilidadGuardia(string guardId, bool disponible, string nuevoEstado = null)
    {
        if (string.IsNullOrEmpty(guardId)) return;
        if (!EstadosOtrosGuardias.TryGetValue(guardId, out GuardStatus estado)) return;

        estado.IsAvailable = disponible;
        if (!string.IsNullOrEmpty(nuevoEstado))
            estado.CurrentState = nuevoEstado;

        ultimaActualizacionGuardias[guardId] = Time.time;
    }

    public void EliminarGuardia(string guardId)
    {
        EstadosOtrosGuardias.Remove(guardId);
        ultimaActualizacionGuardias.Remove(guardId);
    }

    public void LimpiarGuardiasStale()
    {
        List<string> guardiasStale = new List<string>();

        foreach (var kvp in ultimaActualizacionGuardias)
        {
            if (Time.time - kvp.Value > TIEMPO_STALE_GUARDIA)
            {
                guardiasStale.Add(kvp.Key);
            }
        }

        foreach (string guardId in guardiasStale)
        {
            Debug.Log($"[{MiId}] Eliminando guardia stale: {guardId}");
            EliminarGuardia(guardId);
        }
    }

    public int GuardiasEnEstado(BehaviorType estado)
    {
        int count = EstadoActual == estado ? 1 : 0;
        string nombreEstado = estado.ToString();

        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == nombreEstado)
                count++;
        }

        return count;
    }

    public bool AlguienGuardandoPedestal()
    {
        foreach (var par in EstadosOtrosGuardias)
        {
            string estado = par.Value.CurrentState;
            if (estado == BehaviorType.CheckPedestal.ToString())
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Devuelve true si ningun otro guardia conocido esta mas cerca de la posicion dada.
    /// Usado para decidir quien inicia el Contract-Net sin coordinacion centralizada.
    /// </summary>
    public bool SoyElMasCercanoA(Vector3 posicion)
    {
        float miDistancia = Vector3.Distance(MiPosicion, posicion);
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentPosition == null) continue;
            float suDistancia = Vector3.Distance(par.Value.CurrentPosition.ToVector3(), posicion);
            bool estaMasCerca = suDistancia < miDistancia - 0.25f;
            bool empataConMejorId = Mathf.Abs(suDistancia - miDistancia) <= 0.25f &&
                                    string.Compare(par.Key, MiId, StringComparison.Ordinal) < 0;

            if (estaMasCerca || empataConMejorId)
                return false;
        }
        return true;
    }

    public bool SoyEntreMasCercanosA(Vector3 posicion, int maxAgentes)
    {
        float miDistancia = Vector3.Distance(MiPosicion, posicion);
        int mejores = 0;

        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentPosition == null) continue;

            float suDistancia = Vector3.Distance(par.Value.CurrentPosition.ToVector3(), posicion);
            bool estaMasCerca = suDistancia < miDistancia - 0.25f;
            bool empataConMejorId = Mathf.Abs(suDistancia - miDistancia) <= 0.25f &&
                                    string.Compare(par.Key, MiId) < 0;

            if (estaMasCerca || empataConMejorId)
                mejores++;
        }

        return mejores < maxAgentes;
    }

    public List<string> ObtenerIdsMasCercanosA(Vector3 posicion, int maxAgentes, HashSet<string> excluir = null)
    {
        List<KeyValuePair<string, float>> candidatos = new List<KeyValuePair<string, float>>();

        if (excluir == null || !excluir.Contains(MiId))
            candidatos.Add(new KeyValuePair<string, float>(MiId, Vector3.Distance(MiPosicion, posicion)));

        foreach (var par in EstadosOtrosGuardias)
        {
            if (excluir != null && excluir.Contains(par.Key)) continue;
            if (par.Value.CurrentPosition == null) continue;

            float distancia = Vector3.Distance(par.Value.CurrentPosition.ToVector3(), posicion);
            candidatos.Add(new KeyValuePair<string, float>(par.Key, distancia));
        }

        return candidatos
            .OrderBy(c => c.Value)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .Take(maxAgentes)
            .Select(c => c.Key)
            .ToList();
    }

    public HashSet<string> ObtenerIdsBloqueadoresSalidaEstables(int maxAgentes, bool excluirmeSiTengoContactoDirecto = false)
    {
        if (!TienePosicionSalida)
            return new HashSet<string>();

        HashSet<string> excluir = new HashSet<string>();
        if (excluirmeSiTengoContactoDirecto)
            excluir.Add(MiId);

        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.Pursuit.ToString())
                excluir.Add(par.Key);
        }

        if (EstadoActual == BehaviorType.Pursuit)
            excluir.Add(MiId);

        List<string> actuales = new List<string>();
        if (EstadoActual == BehaviorType.BlockExit && !excluir.Contains(MiId))
            actuales.Add(MiId);

        foreach (var par in EstadosOtrosGuardias)
        {
            if (excluir.Contains(par.Key)) continue;
            if (par.Value.CurrentState == BehaviorType.BlockExit.ToString())
                actuales.Add(par.Key);
        }

        actuales = actuales
            .Distinct()
            .OrderBy(id => DistanciaGuardiaASalida(id))
            .ThenBy(id => id, StringComparer.Ordinal)
            .Take(maxAgentes)
            .ToList();

        if (actuales.Count >= maxAgentes)
            return new HashSet<string>(actuales);

        HashSet<string> excluirRelleno = new HashSet<string>(excluir);
        foreach (string id in actuales)
            excluirRelleno.Add(id);

        List<string> relleno = ObtenerIdsMasCercanosA(PosicionSalida, maxAgentes - actuales.Count, excluirRelleno);
        actuales.AddRange(relleno);
        return new HashSet<string>(actuales);
    }

    private float DistanciaGuardiaASalida(string guardId)
    {
        if (guardId == MiId)
            return Vector3.Distance(MiPosicion, PosicionSalida);

        if (EstadosOtrosGuardias.TryGetValue(guardId, out GuardStatus estado) &&
            estado.CurrentPosition != null)
            return Vector3.Distance(estado.CurrentPosition.ToVector3(), PosicionSalida);

        return float.MaxValue;
    }

    public bool SoyEntreMasCercanosParaBloquearSalida(int maxAgentes, bool excluirmeSiTengoContactoDirecto)
    {
        if (!TienePosicionSalida)
            return false;

        HashSet<string> excluir = new HashSet<string>();
        if (excluirmeSiTengoContactoDirecto)
            excluir.Add(MiId);

        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.Pursuit.ToString())
                excluir.Add(par.Key);
        }

        return ObtenerIdsMasCercanosA(PosicionSalida, maxAgentes, excluir).Contains(MiId);
    }
}
