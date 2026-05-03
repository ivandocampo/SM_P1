// =============================================================
// Parámetros tácticos por fase del sistema multiagente.
// Encapsula los límites de coordinación y las prioridades de cada
// fase táctica en un struct. DesireGenerator llama a For(fase) para
// obtener los valores sin necesidad de switch repetidos en cada método
// =============================================================

public struct TacticalPhasePolicy
{
    public int MaxTacticos;                  // Máximo de perseguidores o interceptores simultáneos
    public int MaxInterceptoresTrasPerdida;  // Interceptores permitidos cuando el ladrón se perdió
    public int MaxBloqueadoresSalida;        // Bloqueadores de salida permitidos
    public float PrioridadIntercept;
    public float PrioridadBloqueoSalida;

    // Devolver la política correspondiente a la fase táctica indicada
    public static TacticalPhasePolicy For(TacticalPhase fase)
    {
        switch (fase)
        {
            case TacticalPhase.RingSafeThiefKnown:
                return new TacticalPhasePolicy
                {
                    MaxTacticos = TacticalConfig.MaxTacticalRingSafe,
                    MaxInterceptoresTrasPerdida = TacticalConfig.MaxInterceptorsAfterLoss,
                    MaxBloqueadoresSalida = 0,
                    PrioridadIntercept = DesirePriorities.InterceptRingSafeKnown,
                    PrioridadBloqueoSalida = 0f
                };
            case TacticalPhase.RingSafeThiefLost:
                return new TacticalPhasePolicy
                {
                    MaxTacticos = TacticalConfig.MaxTacticalRingSafe,
                    MaxInterceptoresTrasPerdida = TacticalConfig.MaxInterceptorsAfterLoss,
                    MaxBloqueadoresSalida = 0,
                    PrioridadIntercept = DesirePriorities.InterceptRingSafeLost,
                    PrioridadBloqueoSalida = 0f
                };
            case TacticalPhase.RingStolenThiefKnown:
                return new TacticalPhasePolicy
                {
                    MaxTacticos = TacticalConfig.MaxTacticalRingStolen,
                    MaxInterceptoresTrasPerdida = TacticalConfig.MaxInterceptorsAfterLoss,
                    MaxBloqueadoresSalida = TacticalConfig.MaxExitBlockers,
                    PrioridadIntercept = DesirePriorities.InterceptRingStolenKnown,
                    PrioridadBloqueoSalida = DesirePriorities.BlockExitRingStolenKnown
                };
            case TacticalPhase.RingStolenThiefLost:
                return new TacticalPhasePolicy
                {
                    MaxTacticos = TacticalConfig.MaxTacticalRingStolen,
                    MaxInterceptoresTrasPerdida = TacticalConfig.MaxInterceptorsAfterLoss,
                    MaxBloqueadoresSalida = TacticalConfig.MaxExitBlockers,
                    PrioridadIntercept = DesirePriorities.InterceptRingStolenLost,
                    PrioridadBloqueoSalida = DesirePriorities.BlockExitRingStolenLost
                };
            default:
                return new TacticalPhasePolicy
                {
                    MaxTacticos = TacticalConfig.MaxTacticalRingSafe,
                    MaxInterceptoresTrasPerdida = 0,
                    MaxBloqueadoresSalida = 0,
                    PrioridadIntercept = 0f,
                    PrioridadBloqueoSalida = 0f
                };
        }
    }
}
