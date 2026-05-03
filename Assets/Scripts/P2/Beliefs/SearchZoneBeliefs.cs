// =============================================================
// Fichero parcial de BeliefBase: gestión de zonas de búsqueda.
// Registra los waypoints de cada zona, rastrea cuándo se barrió
// por última vez y ofrece lógica de selección de zona libre.
// Las reservas globales (diccionario estático) evitan que dos
// guardias reclamen la misma zona simultáneamente sin coordinador central
// =============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BeliefBase
{
    // Mapa de zona: array de puntos registrados para esa zona
    private Dictionary<string, Vector3[]> zonasBusquedaRegistradas =
        new Dictionary<string, Vector3[]>();

    /// Timestamp de la ultima vez que el propio agente termino de buscar en cada zona
    private Dictionary<string, float> ultimaBusquedaPorZona = new Dictionary<string, float>();

    // Reservas globales compartidas entre todas las instancias para evitar solapamiento de zonas
    private static Dictionary<string, string> reservasZonaGlobales =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Registrar los puntos de una zona de búsqueda para uso posterior por SearchAssignedBehavior
    public void RegistrarZonaBusqueda(string zoneId, Vector3[] puntos)
    {
        if (string.IsNullOrEmpty(zoneId) || puntos == null || puntos.Length == 0) return;
        zoneId = zoneId.Trim();
        zonasBusquedaRegistradas[zoneId] = puntos;
    }

    // Devolver los puntos registrados de una zona, o null si no existe
    public Vector3[] ObtenerPuntosZona(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return null;
        return zonasBusquedaRegistradas.TryGetValue(zoneId, out Vector3[] puntos) ? puntos : null;
    }

    // Devolver todos los IDs de zonas registradas
    public List<string> ObtenerIdsZonasBusqueda()
    {
        return new List<string>(zonasBusquedaRegistradas.Keys);
    }

    // Calcular el centroide de los puntos de una zona para usarlo como referencia de distancia
    public Vector3 ObtenerCentroZona(string zoneId)
    {
        Vector3[] puntos = ObtenerPuntosZona(zoneId);
        if (puntos == null || puntos.Length == 0) return Vector3.zero;

        Vector3 centro = Vector3.zero;
        foreach (Vector3 punto in puntos)
            centro += punto;

        return centro / puntos.Length;
    }

    /// Marca la zona como recien barrida por este agente
    public void RegistrarBusquedaCompletada(string zoneId)
    {
        if (!string.IsNullOrEmpty(zoneId))
            ultimaBusquedaPorZona[zoneId] = Time.time;
    }

    /// Devuelve el timestamp de la ultima busqueda completa de la zona
    public float ObtenerTiempoUltimaBusqueda(string zoneId)
    {
        return ultimaBusquedaPorZona.TryGetValue(zoneId, out float t) ? t : -100f;
    }

    /// Conjunto de zonas que otros guardias declaran cubrir actualmente
    public HashSet<string> ObtenerZonasCubiertasPorOtros()
    {
        HashSet<string> cubiertas = new HashSet<string>();
        foreach (var par in EstadosOtrosGuardias)
        {
            string zona = par.Value.CurrentZone;
            if (!string.IsNullOrEmpty(zona))
                cubiertas.Add(zona);
        }

        // Incluir también las reservas estáticas de otros guardias
        foreach (var reserva in reservasZonaGlobales)
        {
            if (reserva.Value != MiId)
                cubiertas.Add(reserva.Key);
        }

        return cubiertas;
    }

    // Comprobar si otro guardia ya ha reclamado la zona mediante reserva global
    public bool ZonaReservadaPorOtro(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            return false;

        return reservasZonaGlobales.TryGetValue(zoneId.Trim(), out string ownerId) &&
               ownerId != MiId;
    }

    // Registrar la zona como propia en el diccionario estático global
    private void ReservarZonaGlobal(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            return;

        reservasZonaGlobales[zoneId.Trim()] = MiId;
    }

    // Liberar la reserva de la zona solo si este agente era su dueño
    private void LiberarReservaZonaGlobal(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            return;

        string normalizada = zoneId.Trim();
        if (reservasZonaGlobales.TryGetValue(normalizada, out string ownerId) &&
            ownerId == MiId)
        {
            reservasZonaGlobales.Remove(normalizada);
        }
    }

    /// Devuelve una zona que ningun otro guardia este cubriendo
    /// Prioriza las que llevan mas tiempo sin barrer y, en caso de empate, las mas cercanas
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

    // Devolver la siguiente zona Exit en orden circular tras la zona actual
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

    // Devolver la siguiente zona Exit no cubierta; caer a la secuencial si todas están cubiertas
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

    // Buscar la siguiente zona de la lista que no esté en el conjunto excluir
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

    // Asignar zona Exit a este guardia según su posición ordinal entre los no-bloqueadores
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

    // Devolver las zonas Exit_ ordenadas numéricamente para recorrido secuencial consistente
    private List<string> ObtenerZonasExitOrdenadas()
    {
        return ObtenerIdsZonasBusqueda()
            .Where(z => !string.IsNullOrEmpty(z) &&
                        z.StartsWith(GameConstants.ZonePrefixes.Exit, StringComparison.OrdinalIgnoreCase))
            .OrderBy(ExtraerNumeroZona)
            .ThenBy(z => z, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Extraer el número al final del ID de zona para ordenación numérica correcta
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
