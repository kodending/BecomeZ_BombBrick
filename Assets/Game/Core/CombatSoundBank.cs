using UnityEngine;

namespace BombBrick.Game.Core
{
    public enum CombatSoundStyle { Bite, Heavy, Slash, Projectile, Arcane, Spore, Spirit }
    public enum CombatSoundCue { Attack, Hit, Cast, Impact, Periodic, Death }

    // Sample-based combat palette. The procedural path is only a missing-asset fallback.
    public static class CombatSoundBank
    {
        public const int Variations = 3;
        public const int SampleRate = 22050;
        private const string SampleRoot = "Audio/Combat/Samples/";

        private static readonly string[] MeleeAttack = { "swish_07", "swish_08", "swish_09" };
        private static readonly string[] RangedAttack = { "swish_11", "swish_12", "swish_13" };
        private static readonly string[] CommonHit = {
            "impactPunch_medium_000", "impactPunch_medium_001", "impactPunch_medium_002" };
        private static readonly string[] CommonDeath = {
            "explosionCrunch_000", "explosionCrunch_001", "explosionCrunch_002" };
        private static readonly string[] SoftHit = {
            "impactSoft_medium_000", "impactSoft_medium_001", "impactSoft_medium_002" };
        private static readonly string[] HeavyHit = {
            "impactPunch_heavy_000", "impactPunch_heavy_001", "impactPunch_heavy_002" };
        private static readonly string[] OrganicHit = {
            "impactSoft_heavy_000", "impactSoft_heavy_001", "impactSoft_heavy_002" };
        private static readonly string[] LightHit = {
            "impactGeneric_light_000", "impactGeneric_light_001", "impactGeneric_light_002" };
        private static readonly string[] LowBoom = {
            "lowFrequency_explosion_000", "lowFrequency_explosion_001" };
        private static readonly string[] Slice = { "knifeSlice_0", "knifeSlice_1", "chop" };
        private static readonly string[] BiteCast = { "swish_05", "swish_06", "swish_10" };
        private static readonly string[] HeavyCast = { "swish_07", "swish_08", "swish_09" };
        private static readonly string[] SlashCast = { "swish_01", "swish_02", "swish_03" };
        private static readonly string[] ProjectileCast = { "swish_11", "swish_12", "swish_13" };
        private static readonly string[] Laser = { "laserSmall_000", "laserSmall_001", "laserSmall_002" };
        private static readonly string[] Slime = { "slime_000", "slime_001" };
        private static readonly string[] ArcaneCast = { "magical_1", "magical_2", "magical_3" };
        private static readonly string[] ArcaneImpact = { "magical_4", "magical_5", "magical_6" };
        private static readonly string[] ArcanePulse = { "magical_7", "magical_4", "magical_5" };
        private static readonly string[] SpiritCast = { "cure_1", "cure_2", "cure_3" };
        private static readonly string[] SpiritImpact = { "cure_4", "cure_5", "cure_6" };
        private static readonly string[] SpiritPulse = { "cure_7", "cure_8", "cure_7" };

        public static CombatSoundStyle Style(string family, bool ranged = false)
        {
            switch (family)
            {
                case "raptor": case "wolf": case "tyranno": case "haetae":
                    return CombatSoundStyle.Bite;
                case "kingkong": case "bear": case "mammoth": case "beetle":
                case "crocodile": return CombatSoundStyle.Heavy;
                case "sabertooth": case "ninja_cat": case "mantis": case "griffin":
                    return CombatSoundStyle.Slash;
                case "monkey": case "spider": case "arachne": case "frog": case "giraffe":
                    return CombatSoundStyle.Projectile;
                case "wizard_mouse": case "wyvern": return CombatSoundStyle.Arcane;
                case "mushroom": return CombatSoundStyle.Spore;
                case "spirit_deer": return CombatSoundStyle.Spirit;
                default: return ranged ? CombatSoundStyle.Projectile : CombatSoundStyle.Bite;
            }
        }

        public static CombatSoundStyle StyleForCue(string family, bool ranged,
            CombatSoundCue cue)
        {
            if (cue == CombatSoundCue.Death)
                return CombatSoundStyle.Heavy;
            if (cue == CombatSoundCue.Attack || cue == CombatSoundCue.Hit)
            {
                if (family == "wolf" || family == "haetae") return CombatSoundStyle.Slash;
                if (cue == CombatSoundCue.Attack &&
                    (family == "frog" || family == "arachne" || family == "spider"))
                    return CombatSoundStyle.Spore;
            }
            return Style(family, ranged);
        }

        public static bool HasAuthoredImpact(string family) =>
            family == "raptor" || family == "sabertooth" || family == "frog" || family == "kingkong" ||
            family == "mushroom" || family == "spirit_deer" || family == "crocodile" || family == "wizard_mouse" || family == "bear" || family == "wolf" || family == "ninja_cat" || family == "arachne" || family == "tyranno" || family == "monkey" || family == "mantis" || family == "mammoth" || family == "beetle" || family == "griffin" || family == "wyvern" || family == "haetae";

        public static AudioClip Load(CombatSoundStyle style, CombatSoundCue cue, int variant)
            => Resources.Load<AudioClip>(SampleRoot + AssetKey(style, cue, variant, false));

        public static AudioClip LoadLayer(CombatSoundStyle style, CombatSoundCue cue, int variant)
        {
            string key = AssetKey(style, cue, variant, true);
            return string.IsNullOrEmpty(key) ? null : Resources.Load<AudioClip>(SampleRoot + key);
        }

        public static string AssetKey(CombatSoundStyle style, CombatSoundCue cue,
            int variant, bool layer)
        {
            if (cue == CombatSoundCue.Attack)
                return layer ? null : Pick(
                    style == CombatSoundStyle.Projectile ? RangedAttack : MeleeAttack, variant);
            if (cue == CombatSoundCue.Hit)
                return Pick(layer ? SoftHit : CommonHit, variant);
            if (cue == CombatSoundCue.Death)
                return Pick(layer ? LowBoom : CommonDeath, variant);
            if (layer)
            {
                if (cue == CombatSoundCue.Impact && style == CombatSoundStyle.Bite)
                    return Pick(CommonHit, variant);
                if (cue == CombatSoundCue.Impact && style == CombatSoundStyle.Heavy)
                    return Pick(LowBoom, variant);
                if (cue == CombatSoundCue.Impact && style == CombatSoundStyle.Spore)
                    return Pick(Slime, variant);
                return null;
            }
            switch (style)
            {
                case CombatSoundStyle.Bite:
                    return Pick(cue == CombatSoundCue.Cast ? BiteCast :
                        cue == CombatSoundCue.Impact ? Slice : SoftHit, variant);
                case CombatSoundStyle.Heavy:
                    return Pick(cue == CombatSoundCue.Cast ? HeavyCast :
                        cue == CombatSoundCue.Impact ? HeavyHit : OrganicHit, variant);
                case CombatSoundStyle.Slash:
                    return Pick(cue == CombatSoundCue.Cast ? SlashCast :
                        cue == CombatSoundCue.Impact ? Slice : LightHit, variant);
                case CombatSoundStyle.Projectile:
                    return Pick(cue == CombatSoundCue.Cast ? ProjectileCast :
                        cue == CombatSoundCue.Impact ? LightHit : Laser, variant);
                case CombatSoundStyle.Arcane:
                    return Pick(cue == CombatSoundCue.Cast ? ArcaneCast :
                        cue == CombatSoundCue.Impact ? ArcaneImpact : ArcanePulse, variant);
                case CombatSoundStyle.Spore:
                    return Pick(cue == CombatSoundCue.Cast ? Slime :
                        cue == CombatSoundCue.Impact ? OrganicHit : SoftHit, variant);
                case CombatSoundStyle.Spirit:
                    return Pick(cue == CombatSoundCue.Cast ? SpiritCast :
                        cue == CombatSoundCue.Impact ? SpiritImpact : SpiritPulse, variant);
                default: return Pick(SoftHit, variant);
            }
        }

        private static string Pick(string[] keys, int variant)
            => keys[Mathf.Abs(variant) % keys.Length];

        public static AudioClip Create(CombatSoundStyle style, CombatSoundCue cue, int variant)
        {
            float[] samples = Samples(style, cue, variant);
            AudioClip clip = AudioClip.Create("Combat " + style + " " + cue + " " + variant,
                samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] Samples(CombatSoundStyle style, CombatSoundCue cue, int variant)
        {
            if (cue == CombatSoundCue.Attack)
                return BasicAttackSamples(style == CombatSoundStyle.Projectile, variant);
            if (cue == CombatSoundCue.Hit) return HitSamples(variant);
            if (cue == CombatSoundCue.Death) return DeathSamples(variant);

            bool motion = cue == CombatSoundCue.Attack || cue == CombatSoundCue.Cast;
            bool large = cue == CombatSoundCue.Impact;
            bool magic = style == CombatSoundStyle.Spirit || style == CombatSoundStyle.Arcane;
            float duration = large ? .34f : cue == CombatSoundCue.Cast ? .24f : .115f;
            if (style == CombatSoundStyle.Spirit)
                duration = large ? .55f : cue == CombatSoundCue.Cast ? .46f : .19f;
            else if (style == CombatSoundStyle.Spore)
                duration = large ? .4f : cue == CombatSoundCue.Cast ? .3f : .14f;
            else if (magic) duration *= 1.25f;
            if (cue == CombatSoundCue.Periodic) duration = .12f;
            float frequency, noiseMix, fall;
            switch (style)
            {
                case CombatSoundStyle.Heavy: frequency = 165f; noiseMix = .24f; fall = 3.2f; break;
                case CombatSoundStyle.Slash: frequency = 330f; noiseMix = .84f; fall = 2.5f; break;
                case CombatSoundStyle.Projectile: frequency = 610f; noiseMix = .35f; fall = 4f; break;
                case CombatSoundStyle.Arcane: frequency = 440f; noiseMix = .15f; fall = 2f; break;
                case CombatSoundStyle.Spore: frequency = 340f; noiseMix = .28f; fall = 2.7f; break;
                case CombatSoundStyle.Spirit: frequency = 410f; noiseMix = .14f; fall = 1.45f; break;
                default: frequency = 250f; noiseMix = .58f; fall = 3.2f; break;
            }
            frequency *= 1f + (variant % Variations - 1) * .055f;
            if (large) frequency *= .72f;
            if (motion) { frequency *= 1.6f; noiseMix = magic ? .16f : .8f; }
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            var random = new System.Random(7301 + (int)style * 137 + (int)cue * 19 + variant);
            float phase = 0f, filteredNoise = 0f, heldNoise = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(count - 1);
                // Sample-and-hold noise gives coarse grain without harsh full-scale clicks.
                if (i % 3 == 0) heldNoise = (float)random.NextDouble() * 2f - 1f;
                filteredNoise = Mathf.Lerp(filteredNoise, heldNoise,
                    style == CombatSoundStyle.Heavy ? .10f : .48f);
                float sweep = motion ? Mathf.Lerp(.6f, 1.7f, progress) :
                    .45f + 1.7f * Mathf.Exp(-t * (style == CombatSoundStyle.Spore ? 35f : 55f));
                phase += frequency * sweep / SampleRate;
                float tone = Mathf.Sin(phase * Mathf.PI * 2f);
                if (style == CombatSoundStyle.Spirit)
                    tone = (tone + .48f * Mathf.Sin((phase * 1.498f + .13f) * Mathf.PI * 2f) +
                        .24f * Mathf.Sin((phase * 2.01f + .31f) * Mathf.PI * 2f)) / 1.72f;
                else if (magic)
                    tone = (tone + .45f * Mathf.Sin(phase * Mathf.PI * 3f) +
                        .22f * Mathf.Sin(phase * Mathf.PI * 5f)) / 1.67f;
                else if (style == CombatSoundStyle.Spore)
                    tone = .72f * tone + .28f * Mathf.Sin(
                        (phase * .51f + Mathf.Sin(t * 38f) * .08f) * Mathf.PI * 2f);
                else if (style == CombatSoundStyle.Bite)
                    tone = Mathf.Clamp(tone * 1.8f, -.8f, .8f);
                float grain = filteredNoise;
                if (style == CombatSoundStyle.Bite || style == CombatSoundStyle.Slash)
                    grain *= .65f + .35f * Mathf.Sin(t * 430f);
                float attack = style == CombatSoundStyle.Spirit ? .035f : motion ? .018f : .003f;
                float envelope = Mathf.Min(1f, t / attack) *
                    Mathf.Pow(1f - progress, fall);
                float wave = Mathf.Lerp(tone, grain, noiseMix);
                if (style == CombatSoundStyle.Heavy && large)
                    wave += grain * Mathf.Exp(-t * 32f) * .32f;
                if (style == CombatSoundStyle.Spirit)
                    wave *= .82f + .18f * Mathf.Sin(t * 31f);
                // Quantize the texture, not its envelope: tail remains smooth and silent.
                data[i] = Mathf.Round(wave * 48f) / 48f * envelope * .62f;
            }
            return data;
        }

        private static float[] BasicAttackSamples(bool ranged, int variant)
        {
            float duration = ranged ? .13f : .16f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            var random = new System.Random(9101 + (ranged ? 101 : 0) + variant * 17);
            float phase = 0f, smooth = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float p = i / (float)(count - 1);
                float raw = (float)random.NextDouble() * 2f - 1f;
                smooth = Mathf.Lerp(smooth, raw, ranged ? .32f : .18f);
                float air = raw - smooth * .72f;
                float frequency = ranged ? Mathf.Lerp(920f, 390f, p) : Mathf.Lerp(260f, 720f, p);
                phase += frequency / SampleRate;
                float tone = Mathf.Sin(phase * Mathf.PI * 2f);
                float envelope = Mathf.Sin(Mathf.PI * p) * (ranged
                    ? Mathf.Exp(-p * 2.2f) : Mathf.Pow(p, .35f));
                float wave = ranged ? tone * .55f + air * .45f : air * .82f + tone * .18f;
                data[i] = Mathf.Clamp(wave * envelope * .54f, -.62f, .62f);
            }
            data[0] = data[count - 1] = 0f;
            return data;
        }

        private static float[] HitSamples(int variant)
        {
            int count = Mathf.CeilToInt(.12f * SampleRate);
            float[] data = new float[count];
            var random = new System.Random(10103 + variant * 23);
            float phase = 0f, smooth = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float p = i / (float)(count - 1);
                float raw = (float)random.NextDouble() * 2f - 1f;
                smooth = Mathf.Lerp(smooth, raw, .24f);
                float crack = raw - smooth * .68f;
                phase += Mathf.Lerp(310f, 180f, p) / SampleRate;
                float body = Mathf.Sin(phase * Mathf.PI * 2f) +
                    .22f * Mathf.Sin(phase * Mathf.PI * 5.4f);
                float envelope = Mathf.Min(1f, t / .0025f) * Mathf.Pow(1f - p, 4.2f);
                float wave = body * .58f + crack * (.42f * Mathf.Exp(-t * 28f));
                data[i] = Mathf.Clamp(wave * envelope * .6f, -.62f, .62f);
            }
            data[0] = data[count - 1] = 0f;
            return data;
        }

        private static float[] DeathSamples(int variant)
        {
            int count = Mathf.CeilToInt(.34f * SampleRate);
            float[] data = new float[count];
            var random = new System.Random(11213 + variant * 29);
            float phase = 0f, smooth = 0f;
            float[] pops = { .045f, .09f, .145f, .21f };
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float p = i / (float)(count - 1);
                float raw = (float)random.NextDouble() * 2f - 1f;
                smooth = Mathf.Lerp(smooth, raw, .16f);
                float burst = raw - smooth * .62f;
                phase += Mathf.Lerp(210f, 72f, p) / SampleRate;
                float body = Mathf.Sin(phase * Mathf.PI * 2f);
                float scatter = 0f;
                for (int n = 0; n < pops.Length; n++)
                {
                    float d = Mathf.Abs(t - pops[n] - variant * .0015f);
                    if (d < .012f) scatter += (1f - d / .012f) * (n % 2 == 0 ? 1f : -.8f);
                }
                float envelope = Mathf.Min(1f, t / .003f) * Mathf.Pow(1f - p, 2.15f);
                float wave = burst * .62f + body * .34f + scatter * .22f;
                data[i] = Mathf.Clamp(wave * envelope * .62f, -.62f, .62f);
            }
            data[0] = data[count - 1] = 0f;
            return data;
        }
    }
}
