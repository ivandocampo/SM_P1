// =============================================================
// Enumeración de todos los behaviors posibles de un guardia.
// IntentionSelector y BeliefBase usan este tipo para identificar
// el estado activo de cada agente y coordinar roles en equipo
// =============================================================

public enum BehaviorType
{
    Patrol,
    Pursuit,
    Search,
    SearchAssigned,
    Intercept,
    BlockExit,
    CheckPedestal,
    None
}
