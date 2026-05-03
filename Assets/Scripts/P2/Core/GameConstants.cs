// =============================================================
// Constantes compartidas por los sistemas de la practica.
// Agrupa cadenas usadas por agentes, zonas, protocolos, motivos de
// rechazo, predicados y nombres de comportamientos. Sirve para evitar
// repetir literales en distintas partes del codigo
// =============================================================

public static class GameConstants
{
    // Tipos con los que se registran los agentes en AgentRegistry
    public static class AgentTypes
    {
        public const string Guard = "guard";
        public const string Spider = "spider";
    }

    // Prefijos usados para reconocer tipos de zonas por su nombre
    public static class ZonePrefixes
    {
        public const string Exit = "Exit_";
        public const string ExitAlt = "Salida_";
        public const string Ring = "Anillo_";
    }

    // Nombres de protocolos FIPA guardados dentro de ACLMessage.Protocol
    public static class Protocols
    {
        public const string Inform = "fipa-inform";
        public const string Request = "fipa-request";
        public const string ContractNet = "fipa-contract-net";
    }

    // Motivos estandarizados para rechazar solicitudes o propuestas
    public static class RefusalReasons
    {
        public const string Busy = "busy";
        public const string CheckingPedestal = "checking-pedestal";
        public const string InPursuit = "in-pursuit";
        public const string InvalidTask = "invalid-task";
        public const string InvalidAction = "invalid-action";
        public const string InvalidPredicate = "invalid-predicate";
        public const string UnknownConversation = "unknown-conversation";
        public const string ContentNotUnderstood = "content-not-understood";
        public const string Unavailable = "unavailable";
    }

    // Informacion adicional que acompana a algunos predicados factuales
    public static class PredicateExtras
    {
        public const string SeenCarryingRing = "seen-carrying-ring";
    }
}
