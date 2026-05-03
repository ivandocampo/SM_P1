public struct TacticalPhasePolicy
{
    public int MaxTacticos;
    public int MaxInterceptoresTrasPerdida;
    public int MaxBloqueadoresSalida;
    public float PrioridadIntercept;
    public float PrioridadBloqueoSalida;

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
