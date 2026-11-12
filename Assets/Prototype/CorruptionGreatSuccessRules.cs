using System;
using System.Collections.Generic;

public static class CorruptionGreatSuccessRules
{
    private static readonly HashSet<string> supported=new HashSet<string>(
        new[]
        {
            "CorruptDevourMark","CorruptStormSplit","CorruptSkillEcho",
            "CorruptPersonalRewind","CorruptShellBurst","CorruptSpiritCircuit",
            "CorruptGiantAwakening","CorruptCriticalShards","CorruptBerserkCycle",
            "CorruptBloodBlade","CorruptHuntMark","CorruptStoredRetaliation",
            "CorruptBloodMoon","CorruptPredatorGrowth","CorruptExecutionSentence",
            "CorruptMagicTurbine","CorruptTimeMultiHit","CorruptAdaptiveShell",
            "CorruptPurificationStorm","CorruptVampireSwarm",
            "CorruptCriticalStormZone","CorruptGrandSpellTimer",
            "CorruptRoyalFortress","CorruptStatusEcho","CorruptLivingPlague",
            "CorruptSkillCritFracture","CorruptAllyRewind","CorruptInfiniteCycle",
            "CorruptHealthEqualize","CorruptChosenFate","CorruptDamageLink",
            "CorruptStoredSupernova","CorruptDamageMirror",
            "CorruptTwilightDecoy","CorruptLiberationWave","CorruptBloodStar"
        },StringComparer.Ordinal);

    public static bool Supports(string effectId)
    {
        return !string.IsNullOrEmpty(effectId)&&supported.Contains(effectId);
    }

    public static IReadOnlyCollection<string> SupportedEffectIds=>supported;
}
