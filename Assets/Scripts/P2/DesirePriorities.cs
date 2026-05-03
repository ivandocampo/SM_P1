public static class DesirePriorities
{
    public const float Patrol = 10f;

    public const float PursuitBase = 100f;
    public const float TacticalSearchAfterLoss = 98f;
    public const float TacticalFallbackSearch = 82f;

    public const float SearchAssigned = 95f;
    public const float AcceptedRequest = 75f;

    public const float PriorityCheckPedestal = 96f;
    public const float CheckPedestalAfterLocalSearch = 88f;
    public const float PeriodicCheckPedestal = 35f;
    public const float TacticalCheckPedestal = 94f;
    public const float CheckPedestalByDistanceBase = 86f;

    public const float InterceptRingSafeKnown = 92f;
    public const float InterceptRingSafeLost = 74f;
    public const float InterceptRingStolenKnown = 90f;
    public const float InterceptRingStolenLost = 76f;

    public const float BlockExitRingStolenKnown = 98f;
    public const float BlockExitRingStolenLost = 99f;
    public const float FallbackDefense = 30f;
}
