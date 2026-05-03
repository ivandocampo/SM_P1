public static class GameConstants
{
    public static class AgentTypes
    {
        public const string Guard = "guard";
        public const string Spider = "spider";
    }

    public static class ZonePrefixes
    {
        public const string Exit = "Exit_";
        public const string ExitAlt = "Salida_";
        public const string Ring = "Anillo_";
    }

    public static class Protocols
    {
        public const string Inform = "fipa-inform";
        public const string Request = "fipa-request";
        public const string ContractNet = "fipa-contract-net";
    }

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

    public static class PredicateExtras
    {
        public const string SeenCarryingRing = "seen-carrying-ring";
    }
}
