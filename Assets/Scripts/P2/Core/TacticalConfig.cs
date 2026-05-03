// =============================================================
// Configuracion tactica global de los agentes guardia.
// Centraliza tiempos y limites usados por la deliberacion BDI:
// vigencia de informacion sobre Frodo, duracion de investigacion y
// numero maximo de agentes en roles como persecucion, bloqueo o
// interceptacion
// =============================================================

public static class TacticalConfig
{
    public const float ThiefInfoRecentSeconds = 8f;
    public const float ThiefInvestigationSeconds = 25f;
    public const float LostGraceSeconds = 1.5f;

    public const int MaxTacticalRingSafe = 4;
    public const int MaxTacticalRingStolen = 3;
    public const int MaxExitBlockers = 2;
    public const int MaxInterceptorsAfterLoss = 2;
}
