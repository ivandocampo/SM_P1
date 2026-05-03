using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BeliefBase
{
    private Dictionary<string, Vector3[]> zonasBusquedaRegistradas =
        new Dictionary<string, Vector3[]>();

    /// <summary>Timestamp de la ultima vez que el propio agente termino de buscar en cada zona.</summary>
    private Dictionary<string, float> ultimaBusquedaPorZona = new Dictionary<string, float>();

    public void RegistrarZonaBusqueda(string zoneId, Vector3[] puntos)
    {
        if (string.IsNullOrEmpty(zoneId) || puntos == null || puntos.Length == 0) return;
        zoneId = zoneId.Trim();
        zonasBusquedaRegistradas[zoneId] = puntos;
    }

    public Vector3[] ObtenerPuntosZona(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return null;
        return zonasBusquedaRegistradas.TryGetValue(zoneId, out Vector3[] puntos) ? puntos : null;
    }

    public List<string> ObtenerIdsZonasBusqueda()
    {
        return new List<string>(zonasBusquedaRegistradas.Keys);
    }

    public Vector3 ObtenerCentroZona(string zoneId)
    {
        Vector3[] puntos = ObtenerPuntosZona(zoneId);
        if (puntos == null || puntos.Length == 0) return Vector3.zero;

        Vector3 centro = Vector3.zero;
        foreach (Vector3 punto in puntos)
            centro += punto;

        return centro / puntos.Length;
    }

    /// <summary>Marca la zona como recien barrida por este agente.</summary>
    public void RegistrarBusquedaCompletada(string zoneId)
    {
        if (!string.IsNullOrEmpty(zoneId))
            ultimaBusquedaPorZona[zoneId] = Time.time;
    }

    /// <summary>
    /// Devuelve el timestamp de la ultima busqueda completa de la zona, o un valor
    /// muy bajo si nunca se busco. Permite priorizar zonas no rastreadas recientemente.
    /// </summary>
    public float ObtenerTiempoUltimaBusqueda(string zoneId)
    {
        return ultimaBusquedaPorZona.TryGetValue(zoneId, out float t) ? t : -100f;
    }

    /// <summary>Conjunto de zonas que otros guardias declaran cubrir actualmente.</summary>
    public HashSet<string> ObtenerZonasCubiertasPorOtros()
    {
        HashSet<string> cubiertas = new HashSet<string>();
        foreach (var par in EstadosOtrosGuardias)
        {
            string zona = par.Value.CurrentZone;
            if (!string.IsNullOrEmpty(zona))
                cubiertas.Add(zona);
        }
        return cubiertas;
    }

    /// <summary>
    /// Devuelve una zona que ningun otro guardia este cubriendo, priorizando
    /// las que llevan mas tiempo sin barrer y, en caso de empate, las mas cercanas.
    /// Si soloExit=true filtra a zonas Exit_*; si no hay disponibles cae a todas.
    /// </summary>
    public string ObtenerZonaSinCubrir(bool soloExit)
    {
        HashSet<string> cubiertas = ObtenerZonasCubiertasPorOtros();

        IEnumerable<string> candidatas = ObtenerIdsZonasBusqueda()
            .Where(z => !cubiertas.Contains(z));

        if (soloExit)
        {
            var exit = candidatas
                .Where(z => z.StartsWith(GameConstants.ZonePrefixes.Exit, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exit.Count > 0) candidatas = exit;
        }

        return candidatas
            .OrderBy(z => ObtenerTiempoUltimaBusqueda(z))
            .ThenBy(z => Vector3.Distance(ObtenerCentroZona(z), MiPosicion))
            .FirstOrDefault();
    }

    public string ObtenerSiguienteZonaExitSecuencial(string zonaActual)
    {
        List<string> zonasExit = ObtenerZonasExitOrdenadas();
        if (zonasExit.Count == 0)
            return "";

        if (!string.IsNullOrEmpty(zonaActual))
        {
            int indiceActual = zonasExit.FindIndex(z =>
                string.Equals(z, zonaActual, StringComparison.OrdinalIgnoreCase));

            if (indiceActual >= 0)
                return zonasExit[(indiceActual + 1) % zonasExit.Count];
        }

        return ObtenerZonaExitPorRol();
    }

    public string ObtenerSiguienteZonaExitSecuencialSinCubrir(string zonaActual)
    {
        List<string> zonasExit = ObtenerZonasExitOrdenadas();
        if (zonasExit.Count == 0)
            return "";

        if (string.IsNullOrEmpty(zonaActual))
            return ObtenerZonaExitPorRol();

        HashSet<string> cubiertas = ObtenerZonasCubiertasPorOtros();
        string siguiente = ObtenerSiguienteZonaDesdeLista(zonasExit, zonaActual, cubiertas);
        if (!string.IsNullOrEmpty(siguiente))
            return siguiente;

        return ObtenerSiguienteZonaExitSecuencial(zonaActual);
    }

    private string ObtenerSiguienteZonaDesdeLista(List<string> zonasExit, string zonaActual, HashSet<string> excluir)
    {
        int inicio = 0;
        if (!string.IsNullOrEmpty(zonaActual))
        {
            int indiceActual = zonasExit.FindIndex(z =>
                string.Equals(z, zonaActual, StringComparison.OrdinalIgnoreCase));
            if (indiceActual >= 0)
                inicio = (indiceActual + 1) % zonasExit.Count;
        }

        for (int offset = 0; offset < zonasExit.Count; offset++)
        {
            string candidata = zonasExit[(inicio + offset) % zonasExit.Count];
            if (!excluir.Contains(candidata))
                return candidata;
        }

        return "";
    }

    private string ObtenerZonaExitPorRol()
    {
        List<string> zonasExit = ObtenerZonasExitOrdenadas();
        if (zonasExit.Count == 0)
            return "";

        HashSet<string> bloqueadores = ObtenerIdsBloqueadoresSalidaEstables(2);
        List<string> buscadores = EstadosOtrosGuardias.Keys
            .Append(MiId)
            .Where(id => !bloqueadores.Contains(id))
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        int indice = buscadores.IndexOf(MiId);
        if (indice < 0)
            indice = 0;

        return zonasExit[indice % zonasExit.Count];
    }

    private List<string> ObtenerZonasExitOrdenadas()
    {
        return ObtenerIdsZonasBusqueda()
            .Where(z => !string.IsNullOrEmpty(z) &&
                        z.StartsWith(GameConstants.ZonePrefixes.Exit, StringComparison.OrdinalIgnoreCase))
            .OrderBy(ExtraerNumeroZona)
            .ThenBy(z => z, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int ExtraerNumeroZona(string zoneId)
    {
        int numero = 0;
        bool tieneDigitos = false;

        foreach (char c in zoneId)
        {
            if (!char.IsDigit(c)) continue;
            tieneDigitos = true;
            numero = numero * 10 + (c - '0');
        }

        return tieneDigitos ? numero : int.MaxValue;
    }
}
