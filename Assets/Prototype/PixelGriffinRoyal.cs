using UnityEngine;

// Wing frames and ground particles use the battle clock, including pause and hit-frame timing.
public sealed class PixelGriffinRoyal : MonoBehaviour
{
    private SpriteRenderer source, rim, wings;
    private Material rimMaterial;
    private ParticleSystem gust, featherPixels;
    private readonly ParticleSystem.Particle[] pixels = new ParticleSystem.Particle[15];
    private AnimationClip wingClip;
    private Vector3 origin, forward, side, wingAnchor;
    private Color tint;
    private float length, halfWidth, prepare = -10f, windup = GriffinRank3Rules.RoyalWingLead, strike = -10f, depart = -1f;
    private float wingScale = 1f;
    private bool wave, preparing;
    public int WingFrame
    {
        get
        {
            if (wings == null || !wings.enabled || wings.sprite == null) return -1;
            string name = wings.sprite.name;
            return int.TryParse(name.Substring(name.LastIndexOf('_') + 1), out int frame) ? frame : -1;
        }
    }

    public static PixelGriffinRoyal Ready(SpriteRenderer source, Color tint)
    {
        var fx = new GameObject("Griffin Royal Wing Glow").AddComponent<PixelGriffinRoyal>();
        fx.source = source; fx.tint = tint;
        fx.transform.SetParent(source.transform, false);
        fx.rim = new GameObject("Lit Feather Edges").AddComponent<SpriteRenderer>();
        fx.rim.transform.SetParent(fx.transform, false);
        fx.rimMaterial = new Material(Resources.Load<Shader>("CombatEffects/Griffin/RoyalWingGlow"));
        fx.rim.sharedMaterial = fx.rimMaterial;
        fx.wings = new GameObject("Great Wings Animated Sprite").AddComponent<SpriteRenderer>();
        fx.wings.transform.SetParent(fx.transform, false);
        fx.wings.enabled = false;
        // Unity needs an Animator binding to sample sprite reference curves; the battle clock drives it.
        var animator = fx.wings.gameObject.AddComponent<Animator>();
        animator.enabled = false;
        fx.wingClip = Resources.Load<AnimationClip>("CombatEffects/Griffin/RoyalWingbeat");
        fx.featherPixels = PixelMushroomGas.CreateParticles("Gathering Feather Pixels", fx.transform, 10,
            PixelMushroomGas.Material(true), source, 3, true);
        return fx;
    }

    public static PixelGriffinRoyal Wave(SpriteRenderer source, Vector3 origin, Vector3 forward,
        float length, float halfWidth, Color tint)
    {
        var fx = new GameObject("Griffin Ground Gust").AddComponent<PixelGriffinRoyal>();
        fx.source = source; fx.origin = origin; fx.forward = forward;
        fx.side = Vector3.Cross(Vector3.up, forward);
        fx.length = length; fx.halfWidth = halfWidth; fx.tint = tint; fx.wave = true;
        fx.gust = PixelMushroomGas.CreateParticles("Ground Skimming Wind", fx.transform, fx.pixels.Length,
            PixelMushroomGas.Material(true), source, 1, true);
        fx.gust.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.World;
        var main = fx.gust.main; main.startRotation3D = true;
        fx.Pose(0f, false);
        return fx;
    }

    public void Prepare(float age, float duration)
    {
        // Keep the normal basic-hit timing; play the brief wing action immediately around it.
        windup = Mathf.Clamp(duration, .001f, GriffinRank3Rules.RoyalWingLead);
        prepare = age + Mathf.Max(0f, duration - windup); strike = -10f; preparing = true;
        wingScale = Mathf.Clamp(source.bounds.size.x * 2.8f, 4.8f, 5.8f) / 5f;
        // Attack sprites have different bounds; keep the shoulder attached to the same body point.
        Bounds body = PixelMantisReaper.VisibleBody(source.sprite);
        wingAnchor = body.center + new Vector3(-body.size.x * .12f, body.size.y * .12f, 0f);
    }
    public void Strike(float age) { strike = age; preparing = false; Pose(age, true); }
    public void CancelPreparation() { preparing = false; }

    public bool Pose(float age, bool ready)
    {
        if (this == null || source == null) return true;
        if (wave) { DrawGroundGust(age); return false; }
        if (!ready && depart < 0f) depart = age;
        if (ready) depart = -1f;
        float fade = depart < 0f ? 1f : Mathf.Clamp01(1f - (age - depart) / .28f);
        float after = age - strike;
        bool stroking = strike >= 0f && after < GriffinRank3Rules.RoyalWingRelease;
        rim.sprite = source.sprite; rim.flipX = source.flipX; rim.flipY = source.flipY;
        rim.sortingLayerID = source.sortingLayerID; rim.sortingOrder = source.sortingOrder + 1;
        rim.color = new Color(tint.r, tint.g, tint.b, fade * .55f);
        if (source.sprite != null)
        {
            Bounds body = PixelMantisReaper.VisibleBody(source.sprite);
            Vector2 pivot = source.sprite.pivot; float ppu = source.sprite.pixelsPerUnit;
            Rect rect = source.sprite.textureRect;
            rimMaterial.SetVector("_BodyUV", new Vector4(
                (rect.x + pivot.x + body.min.x * ppu) / source.sprite.texture.width,
                (rect.y + pivot.y + body.min.y * ppu) / source.sprite.texture.height,
                body.size.x * ppu / source.sprite.texture.width, body.size.y * ppu / source.sprite.texture.height));
        }
        wings.enabled = wingClip != null && ((preparing && age >= prepare) || stroking);
        if (wings.enabled)
        {
            wings.flipX = source.flipX;
            wings.sortingLayerID = source.sortingLayerID;
            wings.sortingOrder = source.sortingOrder - 1;
            wings.transform.localPosition = new Vector3(source.flipX ? -wingAnchor.x : wingAnchor.x, wingAnchor.y, wingAnchor.z);
            Vector3 bodyScale = source.transform.lossyScale;
            wings.transform.localScale = new Vector3(wingScale / bodyScale.x, wingScale / bodyScale.y, wingScale / bodyScale.z);
            wings.color = Color.Lerp(tint, Color.white, .58f);
            // The native clip contains only sprite and opacity curves. All wing motion is drawn.
            // Hold the anticipation until the actual basic-hit callback starts the forward stroke.
            float clipTime = stroking ? GriffinRank3Rules.RoyalWingLead + after :
                Mathf.Min(GriffinRank3Rules.RoyalWingLead - .001f,
                    (age - prepare) / windup * GriffinRank3Rules.RoyalWingLead);
            wingClip.SampleAnimation(wings.gameObject, clipTime);
            DrawFeatherPixels(clipTime);
        }
        else featherPixels.SetParticles(pixels, 0);
        return depart >= 0f && age - depart > .28f && !wings.enabled;
    }

    private void DrawFeatherPixels(float clipTime)
    {
        bool forming = clipTime < .06f, dissolving = clipTime > .22f;
        if (!forming && !dissolving) { featherPixels.SetParticles(pixels, 0); return; }
        float t = Mathf.Clamp01(forming ? clipTime / .06f : (clipTime - .22f) / .1f);
        Bounds pose = wings.sprite.bounds;
        var renderer = featherPixels.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = wings.sortingLayerID; renderer.sortingOrder = wings.sortingOrder + 1;
        for (int i = 0; i < 10; i++)
        {
            float s = (i + 1f) / 10f;
            Vector3 point = pose.center * (s * 1.65f);
            point.y += Mathf.Sin(i * 2.4f) * pose.extents.y * .22f;
            Vector3 drift = new Vector3(Mathf.Sin(i * 2.4f), .45f + Mathf.Cos(i * 1.3f) * .4f, 0);
            point += drift * (forming ? (1f - t) * .65f : t * .7f);
            if (wings.flipX) point.x = -point.x;
            Color color = Color.Lerp(tint, Color.white, .7f);
            color.a = .65f * Mathf.Sin(t * Mathf.PI);
            pixels[i] = new ParticleSystem.Particle {
                position = wings.transform.TransformPoint(point), startColor = color,
                startSize3D = Vector3.one * (.045f + i % 3 * .015f),
                startLifetime = 10f, remainingLifetime = 10f, randomSeed = (uint)(i + 1)
            };
        }
        featherPixels.SetParticles(pixels, 10);
    }

    private void DrawGroundGust(float age)
    {
        float t = GriffinRank3Rules.RoyalWindProgress(age), front = length * t;
        float fade = Mathf.Clamp01(age / .025f) *
            Mathf.Clamp01((GriffinRank3Rules.RoyalWindDuration + .14f - age) / .14f);
        float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        int count = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            float seed = Mathf.Repeat(Mathf.Sin(i * 12.9898f + 1.17f) * 43758.5453f, 1f);
            // Five lanes with three short, fading flecks each keep the full width readable.
            int trail = i / 5;
            float lateral = (i % 5 / 4f * 2f - 1f) * halfWidth;
            float along = front - trail * .44f - seed * .05f;
            if (along < 0f) continue;
            Color color = Color.Lerp(tint, Color.white, .7f + seed * .25f);
            color.a = fade * (1f - trail * .3f) * (.6f + seed * .15f);
            pixels[count++] = new ParticleSystem.Particle {
                position = origin + forward * along + side * lateral + Vector3.up * (.04f + seed * .035f),
                startSize3D = new Vector3(.055f + seed * .015f, (.55f + seed * .15f) * (1f - trail * .2f), .1f),
                startColor = color, rotation3D = new Vector3(90f, angle, 0f),
                startLifetime = 10f, remainingLifetime = 10f,
                randomSeed = (uint)(i + 1)
            };
        }
        gust.SetParticles(pixels, count);
    }

    private void OnDestroy()
    {
        if (rimMaterial != null) { if (Application.isPlaying) Destroy(rimMaterial); else DestroyImmediate(rimMaterial); }
    }
    public void Cancel()
    {
        if (this == null) return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
    }
}
