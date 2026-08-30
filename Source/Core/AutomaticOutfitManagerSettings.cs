using Verse;

namespace AutomaticOutfitManager.Core
{
    public enum AomLoggingLevel
    {
        Quiet,
        Basic,
        Detailed
    }

    public sealed class AutomaticOutfitManagerSettings : ModSettings
    {
        public AomLoggingLevel LoggingLevel = AomLoggingLevel.Basic;

        public override void ExposeData()
        {
            Scribe_Values.Look(
                ref LoggingLevel,
                "loggingLevel",
                AomLoggingLevel.Basic);
            base.ExposeData();
        }
    }
}
