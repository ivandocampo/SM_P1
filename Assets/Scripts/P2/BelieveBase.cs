using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TacticalPhase
{
    NormalPatrol,
    RingSafeThiefKnown,
    RingSafeThiefLost,
    RingStolenThiefKnown,
    RingStolenThiefLost
}

[System.Serializable]
public class BeliefBase
{
    public const float TIEMPO_INFO_TACTICA_LADRON = 8f;
    public const float TIEMPO_INVESTIGACION_LADRON = 25f;
    public const float TIEMPO_GRACIA_PERDIDA_LADRON = 1.5f;

    // CREENCIAS SOBRE EL LADRÓN

    /// <summary>Si el ladrón es visible en este momento (por sensores propios).</summary>
    public bool LadronVisible { get; private set; } = false;

    /// <summary>Última posición conocida del ladrón (vista u oída, propia o reportada).</summary>
    public Vector3 UltimaPosicionLadron { get; private set; } = Vector3.zero;

    /// <summary>Timestamp de la última detección del ladrón.</summary>
    public float TiempoUltimaDeteccion { get; private set; } = -100f;

    /// <summary>Si la última detección fue por visión directa (true) u oído/reporte (false).</summary>
    public bool UltimaDeteccionDirecta { get; private set; } = false;

    /// <summary>ID del agente que reportó la última posición (puede ser el propio).</summary>
    public string FuenteUltimaDeteccion { get; private set; } = "";

    /// <summary>Si tenemos información reciente sobre el ladrón (menos de maxAge segundos).</summary>
    public bool TieneInfoReciente(float maxAge = 10f)
    {
        return (Time.time - TiempoUltimaDeteccion) < maxAge;
    }

    /// <summary>Antigüedad en segundos de la última información sobre el ladrón.</summary>
    public float AntiguedadInfoLadron => Time.time - TiempoUltimaDeteccion;

    /// <summary>Si la última detección reciente procede de un sensor propio, no de un mensaje.</summary>
    public bool TieneDeteccionPropiaReciente(float maxAge = 8f)
    {
        return FuenteUltimaDeteccion == MiId && TieneInfoReciente(maxAge);
    }

    /// <summary>Dirección más reciente conocida del ladrón.</summary>
    public Vector3 UltimaDireccionLadron { get; private set; } = Vector3.zero;

    public bool TieneDireccionLadron { get; private set; } = false;

    // CREENCIAS SOBRE EL ANILLO

    /// <summary>Si se sabe que el anillo ha sido robado.</summary>
    public bool AnilloRobado { get; private set; } = false;

    /// <summary>Timestamp de cuando se supo que el anillo fue robado.</summary>
    public float TiempoAnilloRobado { get; private set; } = -100f;

    /// <summary>Si algún guardia ha visto directamente al ladrón llevando el anillo.</summary>
    public bool LadronVistoConAnillo { get; private set; } = false;

    // CREENCIAS SOBRE EL PROPIO AGENTE

    /// <summary>ID del propio agente.</summary>
    public string MiId { get; private set; }

    /// <summary>Posición actual del agente (actualizada cada frame).</summary>
    public Vector3 MiPosicion { get; set; }

    /// <summary>Posición del pedestal, conocida por el propio agente.</summary>
    public Vector3 PosicionPedestal { get; set; } = Vector3.zero;

    public bool TienePosicionPedestal { get; set; } = false;

    /// <summary>Posición de la salida del mapa, conocida por el propio agente.</summary>
    public Vector3 PosicionSalida { get; set; } = Vector3.zero;

    public bool TienePosicionSalida { get; set; } = false;

    /// <summary>Nombre de la intención/estado actual.</summary>
    public BehaviorType EstadoActual { get; set; } = BehaviorType.None;

    /// <summary>Si el agente está disponible para aceptar tareas.</summary>
    public bool Disponible => EstadoActual == BehaviorType.Patrol || EstadoActual == BehaviorType.None;

    public bool DebeComprobarPedestal { get; set; } = false;
    public bool DebeComprobarPedestalPrioritario { get; set; } = false;
    public bool DebeBuscarAlrededorPedestal { get; set; } = false;
    public bool BuscarLocalAntesDeCoordinar { get; set; } = false;
    public bool ComprobarPedestalTrasBusquedaLocal { get; set; } = false;
    public float UltimoChequeoPedestal { get; private set; } = -100f;

    // EVENTOS DE COMUNICACIÓN PENDIENTES
    // La capa de percepción (sensores) los activa al detectar cambios relevantes.
    // La capa de comunicación (GestionarComunicacionReactiva en GuardAgent) los
    // consume cada frame, desacoplando ambas capas: añadir un nuevo sensor solo
    // requiere actualizar creencias y activar el flag correspondiente.

    /// <summary>El ladrón acaba de perderse de vista — comunicar THIEF_LOST e iniciar búsqueda coordinada.</summary>
    public bool PendienteComunicarLadronPerdido { get; set; } = false;

    /// <summary>El pedestal del anillo está vacío — comunicar RING_STOLEN al equipo.</summary>
    public bool PendienteComunicarAnilloDesaparecido { get; set; } = false;

    /// <summary>Se ha visto al ladrón portando el anillo — comunicar RING_STOLEN con contexto adicional.</summary>
    public bool PendienteComunicarLadronConAnillo { get; set; } = false;

    /// <summary>Primera detección del ladrón en este ciclo — forzar avistamiento inmediato sin throttle.</summary>
    public bool PrimerAvistamiento { get; set; } = false;

    /// <summary>
    /// Se ha producido un cambio de creencias relevante que puede alterar la intención óptima
    /// (tarea asignada vía Contract-Net, anillo robado recibido por mensaje...).
    /// GuardAgent lo consume para forzar una ronda de deliberación inmediata.
    /// </summary>
    public bool NecesitaDeliberar { get; set; } = false;

    // CREENCIAS SOBRE OTROS AGENTES

    /// <summary>Estados reportados por otros guardias.</summary>
    public Dictionary<string, GuardStatus> EstadosOtrosGuardias { get; private set; }
        = new Dictionary<string, GuardStatus>();

    private Dictionary<string, Vector3[]> zonasBusquedaRegistradas =
        new Dictionary<string, Vector3[]>();

    /// <summary>Timestamp de la última vez que el propio agente terminó de buscar en cada zona.</summary>
    private Dictionary<string, float> ultimaBusquedaPorZona = new Dictionary<string, float>();

    /// <summary>Timestamps de la última actualización de cada guardia.</summary>
    private Dictionary<string, float> ultimaActualizacionGuardias = 
        new Dictionary<string, float>();

    /// <summary>Tiempo máximo sin actualización antes de considerar un guardia como stale.</summary>
    private const float TIEMPO_STALE_GUARDIA = 30f;

    // TAREAS ASIGNADAS

    /// <summary>Tarea de búsqueda asignada vía Contract Net.</summary>
    public SearchTask TareaAsignada { get; private set; } = null;

    /// <summary>Si hay una tarea asignada pendiente de ejecutar.</summary>
    public bool TieneTareaAsignada => TareaAsignada != null;

    /// <summary>ID de la conversación de la tarea asignada (para INFORM_DONE).</summary>
    public string ConversacionTareaAsignada { get; private set; } = "";

    /// <summary>ID del agente que nos asignó la tarea.</summary>
    public string AsignadorTarea { get; private set; } = "";

    public int VersionTareaAsignada { get; private set; } = 0;

    // REQUEST PENDIENTES

    /// <summary>Solicitud de acción recibida vía REQUEST que aceptamos.</summary>
    public ActionRequest RequestAceptado { get; private set; } = null;

    /// <summary>Si hay un REQUEST aceptado pendiente.</summary>
    public bool TieneRequestPendiente => RequestAceptado != null;

    public string ConversacionRequest { get; private set; } = "";
    public string SolicitanteRequest { get; private set; } = "";

    // CONSTRUCTOR

    public BeliefBase(string agentId)
    {
        MiId = agentId;
    }

    // ACTUALIZACIONES — Llamadas desde el GuardAgent

    
    public void ActualizarPosicionLadron(Vector3 posicion, float timestamp,
        bool visionDirecta, string fuente, Vector3 direccion = default(Vector3), bool tieneDireccion = false)
    {
        // Solo aceptar información más reciente
        if (timestamp <= TiempoUltimaDeteccion) return;

        UltimaPosicionLadron = posicion;
        TiempoUltimaDeteccion = timestamp;
        UltimaDeteccionDirecta = visionDirecta;
        FuenteUltimaDeteccion = fuente;

        if (tieneDireccion && direccion.sqrMagnitude > 0.01f)
        {
            UltimaDireccionLadron = direccion.normalized;
            TieneDireccionLadron = true;
        }

        // Solo marcar como visible si es visión directa propia
        if (visionDirecta && fuente == MiId)
            LadronVisible = true;
    }

    
    public void MarcarLadronPerdido()
    {
        LadronVisible = false;
        NecesitaDeliberar = true;
    }

    
    public void MarcarAnilloRobado()
    {
        if (!AnilloRobado)
        {
            AnilloRobado = true;
            TiempoAnilloRobado = Time.time;
            // El robo cambia el escenario radicalmente: las zonas exit pasan a ser
            // las únicas relevantes, así que el tracking previo de zonas generales
            // ya no aporta y conviene partir de cero.
            ultimaBusquedaPorZona.Clear();
            NecesitaDeliberar = true; // BlockExit(95) entra en juego
        }
    }


    public void RegistrarChequeoPedestal()
    {
        UltimoChequeoPedestal = Time.time;
        DebeComprobarPedestal = false;
        DebeComprobarPedestalPrioritario = false;
    }


    public void MarcarLadronConAnillo()
    {
        LadronVistoConAnillo = true;
        MarcarAnilloRobado();
    }

    
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

    public void RegistrarZonaBusqueda(string zoneId, Vector3[] puntos)
    {
        if (string.IsNullOrEmpty(zoneId) || puntos == null || puntos.Length == 0) return;
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

    /// <summary>Marca la zona como recién barrida por este agente.</summary>
    public void RegistrarBusquedaCompletada(string zoneId)
    {
        if (!string.IsNullOrEmpty(zoneId))
            ultimaBusquedaPorZona[zoneId] = Time.time;
    }

    /// <summary>
    /// Devuelve el timestamp de la última búsqueda completa de la zona, o un valor
    /// muy bajo si nunca se buscó. Permite priorizar zonas no rastreadas recientemente.
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
    /// Devuelve una zona que ningún otro guardia esté cubriendo, priorizando
    /// las que llevan más tiempo sin barrer y, en caso de empate, las más cercanas.
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
                .Where(z => z.StartsWith("Exit_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exit.Count > 0) candidatas = exit;
        }

        return candidatas
            .OrderBy(z => ObtenerTiempoUltimaBusqueda(z))
            .ThenBy(z => Vector3.Distance(ObtenerCentroZona(z), MiPosicion))
            .FirstOrDefault();
    }

    public string ObtenerSiguienteZonaExitPorRotacion(int maxPorZona = 2)
    {
        Dictionary<string, int> ocupacion = new Dictionary<string, int>();
        foreach (var par in EstadosOtrosGuardias)
        {
            string zona = par.Value.CurrentZone;
            if (string.IsNullOrEmpty(zona)) continue;
            ocupacion.TryGetValue(zona, out int n);
            ocupacion[zona] = n + 1;
        }

        var candidatas = ObtenerIdsZonasBusqueda()
            .Where(z => !string.IsNullOrEmpty(z) &&
                        z.StartsWith("Exit_", StringComparison.OrdinalIgnoreCase))
            .Where(z => (ocupacion.TryGetValue(z, out int n) ? n : 0) < maxPorZona)
            .ToList();

        // Si no quedan zonas con hueco (caso degenerado, e.g. estados aun no
        // propagados), relajamos el filtro para no devolver vacio.
        if (candidatas.Count == 0)
        {
            candidatas = ObtenerIdsZonasBusqueda()
                .Where(z => !string.IsNullOrEmpty(z) &&
                            z.StartsWith("Exit_", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return candidatas
            .OrderBy(z => ObtenerTiempoUltimaBusqueda(z))
            .ThenBy(ExtraerNumeroZona)
            .ThenBy(z => z, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
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

    public TacticalPhase FaseActual()
    {
        if (AnilloRobado)
            return TieneInfoReciente(TIEMPO_INFO_TACTICA_LADRON)
                ? TacticalPhase.RingStolenThiefKnown
                : TacticalPhase.RingStolenThiefLost;

        if (TieneInfoReciente(TIEMPO_INFO_TACTICA_LADRON))
            return TacticalPhase.RingSafeThiefKnown;

        if (TieneInfoReciente(TIEMPO_INVESTIGACION_LADRON))
            return TacticalPhase.RingSafeThiefLost;

        return TacticalPhase.NormalPatrol;
    }

    public void DescartarHipotesisLadronCaducada()
    {
        LadronVisible = false;
        UltimaDeteccionDirecta = false;
        FuenteUltimaDeteccion = "";
        TiempoUltimaDeteccion = -100f;
        UltimaDireccionLadron = Vector3.zero;
        TieneDireccionLadron = false;
        BuscarLocalAntesDeCoordinar = false;
        ComprobarPedestalTrasBusquedaLocal = false;
        DebeBuscarAlrededorPedestal = false;
        DebeComprobarPedestalPrioritario = false;
        LimpiarTarea();
        LimpiarRequest();
        NecesitaDeliberar = true;
    }

    public Vector3 ObjetivoCriticoActual()
    {
        if (AnilloRobado && TienePosicionSalida)
            return PosicionSalida;

        if (!AnilloRobado && TienePosicionPedestal)
            return PosicionPedestal;

        return UltimaPosicionLadron;
    }

    public bool TieneObjetivoCriticoActual()
    {
        return AnilloRobado ? TienePosicionSalida : TienePosicionPedestal;
    }

    public int CarrilInterceptacion()
    {
        List<string> guardiasActivos = EstadosOtrosGuardias
            .Where(par => par.Value.CurrentState == BehaviorType.Intercept.ToString() ||
                          par.Value.CurrentState == BehaviorType.Pursuit.ToString())
            .Select(par => par.Key)
            .ToList();

        guardiasActivos.Add(MiId);
        guardiasActivos = guardiasActivos.Distinct().ToList();
        guardiasActivos.Sort(StringComparer.Ordinal);

        int modulo = guardiasActivos.IndexOf(MiId) % 3;
        if (modulo == 0) return -1;
        if (modulo == 1) return 0;
        return 1;
    }

    public Vector3 CalcularPuntoInterceptacion(float distanciaAdelante = 6f, float distanciaLateral = 4f)
    {
        return CalcularPuntoInterceptacion(CarrilInterceptacion(), distanciaAdelante, distanciaLateral);
    }

    public Vector3 CalcularPuntoInterceptacion(int carril, float distanciaAdelante = 6f, float distanciaLateral = 4f)
    {
        if (FaseActual() == TacticalPhase.RingSafeThiefKnown)
            return CalcularPuntoCierreSobreLadron(carril, distanciaAdelante, distanciaLateral);

        return CalcularPuntoCorteRuta(carril, distanciaAdelante, distanciaLateral);
    }

    private Vector3 CalcularPuntoCierreSobreLadron(int carril, float distanciaAdelante, float distanciaLateral)
    {
        Vector3 direccionCierre = TieneDireccionLadron
            ? UltimaDireccionLadron
            : (UltimaPosicionLadron - MiPosicion);
        direccionCierre.y = 0f;

        if (direccionCierre.sqrMagnitude < 0.01f)
            return UltimaPosicionLadron;

        direccionCierre.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, direccionCierre).normalized;

        return UltimaPosicionLadron
            + direccionCierre * distanciaAdelante
            + lateral * carril * distanciaLateral;
    }

    private Vector3 CalcularPuntoCorteRuta(int carril, float distanciaAdelante, float distanciaLateral)
    {
        Vector3 objetivo = ObjetivoCriticoActual();
        Vector3 direccionObjetivo = objetivo - UltimaPosicionLadron;
        direccionObjetivo.y = 0f;

        if (direccionObjetivo.sqrMagnitude < 0.01f)
        {
            direccionObjetivo = TieneDireccionLadron
                ? UltimaDireccionLadron
                : (UltimaPosicionLadron - MiPosicion);
            direccionObjetivo.y = 0f;
        }

        if (direccionObjetivo.sqrMagnitude < 0.01f)
            return UltimaPosicionLadron;

        direccionObjetivo.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, direccionObjetivo).normalized;

        return UltimaPosicionLadron
            + direccionObjetivo * distanciaAdelante
            + lateral * carril * distanciaLateral;
    }

    /// <summary>
    /// Elimina un guardia del registro de estados (cuando se desconecta o destruye).
    /// </summary>
    public void EliminarGuardia(string guardId)
    {
        EstadosOtrosGuardias.Remove(guardId);
        ultimaActualizacionGuardias.Remove(guardId);
    }

    /// <summary>
    /// Limpia guardias que no han enviado actualizaciones en mucho tiempo.
    /// Debe llamarse periódicamente desde el Update del agente.
    /// </summary>
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


    public void AsignarTarea(SearchTask tarea, string conversacionId, string asignador)
    {
        TareaAsignada = tarea;
        ConversacionTareaAsignada = conversacionId;
        AsignadorTarea = asignador;
        VersionTareaAsignada++;
        NecesitaDeliberar = true; // SearchAssigned(85) entra en juego
    }


    public void LimpiarTarea()
    {
        TareaAsignada = null;
        ConversacionTareaAsignada = "";
        AsignadorTarea = "";
    }


    public void AceptarRequest(ActionRequest request, string conversacionId, string solicitante)
    {
        RequestAceptado = request;
        ConversacionRequest = conversacionId;
        SolicitanteRequest = solicitante;
    }

    public void LimpiarRequest()
    {
        RequestAceptado = null;
        ConversacionRequest = "";
        SolicitanteRequest = "";
    }

    // CONSULTAS DE ALTO NIVEL

    
    public float DistanciaEstimadaAlLadron()
    {
        return Vector3.Distance(MiPosicion, UltimaPosicionLadron);
    }


    public bool AlguienPersiguiendo()
    {
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.Pursuit.ToString())
                return true;
        }
        return false;
    }

    public int GuardiasPersiguiendo()
    {
        int count = 0;
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.Pursuit.ToString())
                count++;
        }
        return count;
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


    public bool AlguienBloqueandoSalida()
    {
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.BlockExit.ToString())
                return true;
        }
        return false;
    }

    public int GuardiasBloqueando()
    {
        int count = 0;
        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.BlockExit.ToString())
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


    public int GuardiasBuscando()
    {
        int count = 0;
        foreach (var par in EstadosOtrosGuardias)
        {
            string estado = par.Value.CurrentState;
            if (estado == BehaviorType.Search.ToString() ||
                estado == BehaviorType.SearchAssigned.ToString())
                count++;
        }
        return count;
    }

    /// <summary>
    /// Devuelve true si ningún otro guardia conocido está más cerca de la posición dada.
    /// Usado para decidir quién inicia el Contract-Net sin coordinación centralizada.
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

    public HashSet<string> ObtenerIdsBloqueadoresSalida(int maxAgentes)
    {
        if (!TienePosicionSalida)
            return new HashSet<string>();

        HashSet<string> excluir = new HashSet<string>();
        if (EstadoActual == BehaviorType.Pursuit)
            excluir.Add(MiId);

        foreach (var par in EstadosOtrosGuardias)
        {
            if (par.Value.CurrentState == BehaviorType.Pursuit.ToString())
                excluir.Add(par.Key);
        }

        return new HashSet<string>(ObtenerIdsMasCercanosA(PosicionSalida, maxAgentes, excluir));
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
