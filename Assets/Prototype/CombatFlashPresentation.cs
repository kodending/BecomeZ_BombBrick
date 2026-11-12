using UnityEngine;

// Presentation only: retain effect geometry and combat clocks while reducing bright accents.
public static class CombatFlashPresentation
{
    public static bool Reduced { get; private set; }
    public static float AccentScale => Reduced ? 0.25f : 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() => Apply(BombBrick.Game.Core.GameUserSettings.Load().reduceFlashes);

    public static void Apply(bool reduced)
    {
        Reduced = reduced;
        Shader.SetGlobalFloat("_CombatFlashReduction", reduced ? 1f : 0f);
    }

    public static Color Peak(Color color)
    {
        float peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        if (Reduced && peak > 0.68f)
        {
            float scale = 0.68f / peak;
            color.r *= scale; color.g *= scale; color.b *= scale;
        }
        return color;
    }

    public static Color Flash(Color color)
    {
        color = Peak(color);
        color.a *= AccentScale;
        return color;
    }
}
