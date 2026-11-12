#if UNITY_EDITOR
public enum CombatReadabilityPreset { Current, Compact, Local }

// Editor-only policy; production scenes never opt into the comparison.
public static class CombatReadabilityPreview
{
    public static bool Enabled;
    public static CombatReadabilityPreset Preset = CombatReadabilityPreset.Local;
    public static int Seed = 20261026;
    public static bool BasicOnly;
    public static bool ContactFeedback = true;
    public static bool SkillAudio = true;
    public static bool Active => Enabled && CombatEffectTestConfiguration.Active;
    public static bool Adjusted => Active && Preset != CombatReadabilityPreset.Current;
    public static bool RefinedSkills => Active && Preset == CombatReadabilityPreset.Local && !CombatSkillClock.Active;
    public static float BodyVisualScale => Active && Preset == CombatReadabilityPreset.Local ? .9f : 1f;
    public static float EffectVisualScale => Active && Preset == CombatReadabilityPreset.Local ? BodyVisualScale * 1.35f : 1f;

    public static float PreparationDuration => !Adjusted ? 1f : Preset == CombatReadabilityPreset.Compact ? .3f : .35f;
    public static float PreparationSpeed => !Adjusted ? .08f : Preset == CombatReadabilityPreset.Compact ? .45f : 1f;
    public static float ShadeAlpha => !Adjusted ? .5f : Preset == CombatReadabilityPreset.Compact ? .15f : 0f;
    public static float SkillBodyScale => !Adjusted ? 1.5f : Preset == CombatReadabilityPreset.Compact ? 1.12f : 1f;
    public static int HologramCount => Adjusted ? 1 : 3;
    public static float HologramOpacity => Adjusted ? .45f : 1f;
    public static float HologramScale(int index) => Adjusted ? 1.14f : 1.42f + index * .14f;
    public static float ContactIntensity => Adjusted ? .55f : 1f;
    public static float ContactSize => Adjusted ? .75f : 1f;
    public static float SkillSoundGain => Adjusted ? .7f : 1f;

    public static UnityEngine.SpriteRenderer ProjectileContactRenderer;
    public static UnityEngine.Vector3 ProjectileContactDirection;

    public static UnityEngine.Vector3 VisibleBodyCenter(UnityEngine.SpriteRenderer renderer)
    {
        var center = PixelMantisReaper.VisibleBody(renderer.sprite).center;
        if (renderer.flipX) center.x = -center.x;
        if (renderer.flipY) center.y = -center.y;
        return renderer.transform.TransformPoint(center);
    }

    public static UnityEngine.Matrix4x4 WorldHudMatrix(UnityEngine.Matrix4x4 current, UnityEngine.Rect body)
    {
        float scale = (Active && Preset == CombatReadabilityPreset.Local ? .75f : 1f) * BodyVisualScale;
        var pivot = new UnityEngine.Vector3(body.center.x, body.yMin - 6f, 0f);
        // Scale the complete HUD around its bottom anchor so it stays clear of the head.
        return current * UnityEngine.Matrix4x4.TRS(pivot * (1f - scale),
            UnityEngine.Quaternion.identity, new UnityEngine.Vector3(scale, scale, 1f));
    }

    public static void Reset()
    {
        Enabled = false;
        Preset = CombatReadabilityPreset.Local;
        Seed = 20261026;
        BasicOnly = false;
        ContactFeedback = SkillAudio = true;
        ProjectileContactRenderer = null;
        ProjectileContactDirection = UnityEngine.Vector3.zero;
    }
}
#endif
