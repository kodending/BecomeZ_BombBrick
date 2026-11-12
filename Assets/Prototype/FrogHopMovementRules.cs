using UnityEngine;

public static class FrogHopMovementRules
{
    public const float HopDuration=0.36f;
    public const float RestDuration=0.44f;
    public const float HopHeight=0.32f;
    public const float HopDistanceSpeedSeconds=0.25f;
    public const float MinimumHopDistanceTiles=0.42f;
    public const float MaximumHopDistanceTiles=0.82f;

    public static bool IsFrogFamily(string familyId)
    {
        return string.Equals(familyId,"frog",System.StringComparison.OrdinalIgnoreCase);
    }

    public static float HopDistanceTiles(float speedTilesPerSecond)
    {
        return Mathf.Clamp(
            Mathf.Max(0f,speedTilesPerSecond)*HopDistanceSpeedSeconds,
            MinimumHopDistanceTiles,
            MaximumHopDistanceTiles);
    }

    public static float ArcHeight(float normalizedProgress)
    {
        return Mathf.Sin(Mathf.Clamp01(normalizedProgress)*Mathf.PI)*HopHeight;
    }
}
