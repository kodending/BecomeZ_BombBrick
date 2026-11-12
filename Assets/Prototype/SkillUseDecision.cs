public sealed class SkillUseSnapshot
{
    public string behavior;
    public int enemiesInEffect;
    public int alliesInEffect;
    public float targetDistanceTiles;
}

public static class SkillUseDecision
{
    public static bool ShouldUse(SkillUseSnapshot snapshot)
    {
        if(snapshot==null)return false;
        switch(snapshot.behavior??string.Empty)
        {
            case "GuardianTaunt":
            case "ClusterBreath":
            case "GroundSlam":
            case "EarthPulse":
            case "TyrantTremor":
            case "HighHeadSlam":
            case "Skyfall":
            case "SkyFlip":
            case "SkyDive":
            case "AcrobaticBarrage":
            case "CrossScytheWind":
            case "SporeColony":
            case "WyvernBreath":
            case "GiantCaptureWeb":
                return snapshot.enemiesInEffect>0;
            case "JusticeRoar":
                return snapshot.enemiesInEffect>0||snapshot.alliesInEffect>0;
            case "SpiritSanctuary":
                return snapshot.alliesInEffect>0;
            default:
                return true;
        }
    }

    public static bool SupportsBehavior(string behavior)
    {
        if(string.IsNullOrEmpty(behavior))return false;
        switch(behavior)
        {
            case "GuardianTaunt": case "ClusterBreath": case "GroundSlam":
            case "EarthPulse": case "TyrantTremor": case "HighHeadSlam":
            case "Skyfall": case "SkyFlip": case "SkyDive":
            case "AcrobaticBarrage": case "CrossScytheWind": case "SporeColony":
            case "WyvernBreath": case "JusticeRoar": case "FightingSpirit":
            case "GiantCaptureWeb": case "SpiritSanctuary": case "EarthCharge":
            case "AssassinLeap": case "ShadowSlash": case "ShadowHunt":
            case "ThreeSidedHunt": case "DeathRoll": case "FangLock":
            case "TongueHarpoon": case "MagicStarBarrage":
                return true;
            default:
                return false;
        }
    }
}
