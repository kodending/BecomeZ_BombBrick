using System;
using System.Collections.Generic;
using UnityEngine;

public enum CorruptionFeedbackKind
{
    Assault,
    Arcane,
    Guard,
    Sustain,
    Control
}

public readonly struct CorruptionFeedbackStyle
{
    public readonly CorruptionFeedbackKind kind;
    public readonly Color color;
    public readonly float pulseScale;

    public CorruptionFeedbackStyle(
        CorruptionFeedbackKind feedbackKind,Color feedbackColor,float scale)
    {
        kind=feedbackKind;color=feedbackColor;pulseScale=scale;
    }
}

public static class CorruptionGreatSuccessPresentation
{
    private static readonly HashSet<string> guard=new HashSet<string>(
        new[]{"CorruptShellBurst","CorruptGiantAwakening","CorruptStoredRetaliation",
            "CorruptAdaptiveShell","CorruptRoyalFortress","CorruptAllyRewind",
            "CorruptInfiniteCycle","CorruptDamageLink","CorruptStoredSupernova",
            "CorruptDamageMirror","CorruptTwilightDecoy"},StringComparer.Ordinal);
    private static readonly HashSet<string> sustain=new HashSet<string>(
        new[]{"CorruptPersonalRewind","CorruptSpiritCircuit","CorruptBloodBlade",
            "CorruptBloodMoon","CorruptPredatorGrowth","CorruptVampireSwarm",
            "CorruptHealthEqualize","CorruptLiberationWave","CorruptBloodStar"},
        StringComparer.Ordinal);
    private static readonly HashSet<string> arcane=new HashSet<string>(
        new[]{"CorruptSkillEcho","CorruptMagicTurbine","CorruptCriticalStormZone",
            "CorruptGrandSpellTimer","CorruptStatusEcho","CorruptLivingPlague",
            "CorruptSkillCritFracture","CorruptChosenFate"},StringComparer.Ordinal);
    private static readonly HashSet<string> control=new HashSet<string>(
        new[]{"CorruptDevourMark","CorruptHuntMark","CorruptExecutionSentence",
            "CorruptPurificationStorm"},StringComparer.Ordinal);

    public static CorruptionFeedbackStyle Style(string effectId)
    {
        if(guard.Contains(effectId))
            return new CorruptionFeedbackStyle(CorruptionFeedbackKind.Guard,
                new Color(.18f,.82f,1f),.92f);
        if(sustain.Contains(effectId))
            return new CorruptionFeedbackStyle(CorruptionFeedbackKind.Sustain,
                new Color(.25f,1f,.58f),.88f);
        if(arcane.Contains(effectId))
            return new CorruptionFeedbackStyle(CorruptionFeedbackKind.Arcane,
                new Color(.78f,.32f,1f),1.02f);
        if(control.Contains(effectId))
            return new CorruptionFeedbackStyle(CorruptionFeedbackKind.Control,
                new Color(1f,.68f,.16f),.94f);
        return new CorruptionFeedbackStyle(CorruptionFeedbackKind.Assault,
            new Color(1f,.2f,.28f),1f);
    }

    public static string Label(ItemTransformMasterRow row)
    {
        if(row==null)return "";
        string value=string.IsNullOrWhiteSpace(row.corruptionGreatName)
            ?row.displayName:row.corruptionGreatName;
        return "◆ "+GameLocalization.Text(value);
    }
}
