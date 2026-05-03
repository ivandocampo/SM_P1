// =============================================================
// Fichero parcial de BeliefBase: creencias sobre el equipo de guardias.
// Mantiene el estado reportado por cada compañero (posición, behavior,
// disponibilidad, zona). Ofrece consultas de proximidad y candidatos
// para roles tácticos (perseguidor, interceptor, bloqueador de salida)
// =============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BeliefBase
{
    /// Estados reportados por otros guardias
    public Dictionary<string, GuardStatus> EstadosOtrosGuardias { get; private set; }
        = new Dictionary<string, GuardStatus>();

    /// Timestamps de la ultima actualizacion de cada guardia
    private Dictionary<string, float> ultimaActualizacionGuardias =
        new Dictionary<string, float>();

    /// Tiempo maximo sin actualizacion antes de considerar un guardia como stale
    private const float TIEMPO_STALE_GUARDIA = 30f;

    // Registrar o actualizar el estado de un guardia conocido
    public void ActualizarEstadoGuardia(GuardStatus estado)
    {
        if (estado != null && !string.IsNullOrEmpty(estado.GuardId))
        {
            EstadosOtrosGuardias[estado.GuardId] = estado;
            ultimaActualizacionGuardias[estado.GuardId] = Time.time;
        }
    }

    // Actualizar la disponibilidad y opcionalmente el estado de un guardia por su ID
    public void ActualizarDisponibilidadGuardia(string guardId, bool disponible, string nuevoEstado = null)
    {
        if (string.IsNullOrEmpty(guardId)) return;
        if (!EstadosOtrosGuardias.TryGetValue(guardId, out GuardStatus estado)) return;

        estado.IsAvailable = disponible;
        if (!string.IsNullOrEmpty(nuevoEstado))
            estado.CurrentState = nuevoEstado;

        ultimaActualizacionGuardias[guardId] = Time.time;
    }

    // Eliminar un guardia del equipo conocido (desconexión o destrucción)
    public void EliminarGuardia(string guardId)
    {
        EstadosOtrosGuardias.Remove(guardId);
        ultimaActualizacionGuardias.Remove(guardId);
    }

    // Eliminar guardias cuya última actualización supera TIEMPO_STALE_GUARDIA segundos
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

    // Contar cuántos guardias (incluido el propio) están en el estado indicado
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

    // Comprobar si algún compañero está vigilando el pedestal actualmente
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

    /// Devuelve true si ningun otro guardia conocido esta mas cerca de la posicion dada
    public bool SoyElMasCercanoA(Vector3 posicion)
    {
        float miDistancia = Vector3.Distance(MiPosicion, posicion);
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentPosition == null) continue;
            float suDistancia = Vector3.Distance(par.Value.CurrentPosition.ToVector3(), posicion);
            bool estaMasCerca = suDistancia < miDistancia - 0.25f;
            // Desempate lexicográfico: el ID menor gana si la diferencia de distancia es pequeña
            bool empataConMejorId = Mathf.Abs(suDistancia - miDistancia) <= 0.25f &&
                                    string.Compare(par.Key, MiId, StringComparison.Ordinal) < 0;

            if (estaMasCerca || empataConMejorId)
                return false;
        }
        return true;
    }

    // Comprobar si este guardia está entre los maxAgentes más cercanos a la posición dada
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

    // Devolver los IDs de los maxAgentes guardias más cercanos, excluyendo los IDs indicados
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

    // Devolver los IDs estables de bloqueadores de salida: prioriza los ya bloqueando, rellena con los más cercanos
    public HashSet<string> ObtenerIdsBloqueadoresSalidaEstables(int maxAgentes, bool excluirmeSiTengoContactoDirecto = false)
    {
        if (!TienePosicionSalida)
            return new HashSet<string>();

        HashSet<string> excluir = new HashSet<string>();
        if (excluirmeSiTengoContactoDirecto)
            excluir.Add(MiId);

        // Excluir a los guardias en persecución activa, que no pueden bloquear
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

        // Ordenar por cercanía a la salida para escoger los mejores bloqueadores
        actuales = actuales
            .Distinct()
            .OrderBy(id => DistanciaGuardiaASalida(id))
            .ThenBy(id => id, StringComparer.Ordinal)
            .Take(maxAgentes)
            .ToList();

        if (actuales.Count >= maxAgentes)
            return new HashSet<string>(actuales);

        // Completar con los más cercanos a la salida que no estén ya incluidos
        HashSet<string> excluirRelleno = new HashSet<string>(excluir);
        foreach (string id in actuales)
            excluirRelleno.Add(id);

        List<string> relleno = ObtenerIdsMasCercanosA(PosicionSalida, maxAgentes - actuales.Count, excluirRelleno);
        actuales.AddRange(relleno);
        return new HashSet<string>(actuales);
    }

    // Obtener la distancia de un guardia a la posición de la salida
    private float DistanciaGuardiaASalida(string guardId)
    {
        if (guardId == MiId)
            return Vector3.Distance(MiPosicion, PosicionSalida);

        if (EstadosOtrosGuardias.TryGetValue(guardId, out GuardStatus estado) &&
            estado.CurrentPosition != null)
            return Vector3.Distance(estado.CurrentPosition.ToVector3(), PosicionSalida);

        return float.MaxValue;
    }

    // Comprobar si este guardia puede ser candidato a bloquear la salida
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
