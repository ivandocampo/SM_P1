// =============================================================
// Constantes de prioridad para todos los tipos de deseo BDI.
// Mayor valor = más urgente. IntentionSelector usa estos valores
// para ordenar los deseos y aplicar el umbral de histéresis (15 puntos)
// que evita oscilaciones entre behaviors de prioridad similar.
// Rango orientativo: patrulla = 10, persecución = 100
// =============================================================

public static class DesirePriorities
{
    public const float Patrol = 10f;

    // Persecución y búsqueda táctica tras pérdida visual
    public const float PursuitBase = 100f;
    public const float TacticalSearchAfterLoss = 98f;
    public const float TacticalFallbackSearch = 82f;

    // Tareas coordinadas (Contract-Net y REQUEST)
    public const float SearchAssigned = 95f;
    public const float AcceptedRequest = 75f;

    // Comprobación del pedestal según contexto
    public const float PriorityCheckPedestal = 96f;
    public const float CheckPedestalAfterLocalSearch = 88f;
    public const float PeriodicCheckPedestal = 35f;
    public const float TacticalCheckPedestal = 94f;
    public const float CheckPedestalByDistanceBase = 86f;

    // Intercepción según fase táctica
    public const float InterceptRingSafeKnown = 92f;
    public const float InterceptRingSafeLost = 74f;
    public const float InterceptRingStolenKnown = 90f;
    public const float InterceptRingStolenLost = 76f;

    // Bloqueo de salida y fallback defensivo
    public const float BlockExitRingStolenKnown = 98f;
    public const float BlockExitRingStolenLost = 99f;
    public const float FallbackDefense = 30f;
}
