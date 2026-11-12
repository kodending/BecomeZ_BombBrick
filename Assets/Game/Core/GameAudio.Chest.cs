using UnityEngine;

namespace BombBrick.Game.Core
{
    public sealed partial class GameAudio
    {
        public enum ChestCue { Arrival, Tension, Hold, Open, Reveal, Confirm, Stat }
        public enum CaptureCue { Beat, Success, Failure }

        private readonly AudioSource[] chestVoices = new AudioSource[8];
        private readonly float[] chestGains = new float[8];
        private readonly float[] chestPeaks = new float[8];
        private AudioSource chestTension;
        private AudioClip chestOpenClip, chestTensionClip, chestRevealClip;
        private AudioClip captureBeatClip, captureFailureClip;
        private AudioClip chestStampClip, chestContactClip, chestStatAccentClip;
        private int nextChestVoice;
        private int chestConfirmationSlot = -1;
        private float chestTensionFade;

        private void InitializeChestAudio()
        {
            chestOpenClip = Resources.Load<AudioClip>("Audio/Rewards/chest_open");
            chestTensionClip = Resources.Load<AudioClip>("Audio/Rewards/chest_tension");
            chestRevealClip = Resources.Load<AudioClip>("Audio/Rewards/chest_reveal");
            captureBeatClip = Resources.Load<AudioClip>("Audio/Rewards/capture_beat");
            captureFailureClip = Resources.Load<AudioClip>("Audio/Rewards/capture_failure");
            chestStampClip = Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_heavy_000");
            chestContactClip = Resources.Load<AudioClip>("Audio/Combat/Samples/impactSoft_medium_000");
            chestStatAccentClip = Resources.Load<AudioClip>("Audio/Combat/Samples/impactGeneric_light_000");
            chestTension = gameObject.AddComponent<AudioSource>();
            chestTension.playOnAwake = false;
            chestTension.spatialBlend = 0f;
            for (int i = 0; i < chestVoices.Length; i++)
            {
                chestVoices[i] = gameObject.AddComponent<AudioSource>();
                chestVoices[i].playOnAwake = false;
                chestVoices[i].spatialBlend = 0f;
            }
        }

        public static void PlayChest(ChestCue cue, bool shiny = false, int revealIndex = 0, int qualityTier = 2)
        {
            if (instance == null || (!GameFrontend.IsProduction && !instance.previewEnabled)) return;
            instance.PlayChestCue(cue, shiny, revealIndex, qualityTier);
        }

        private void PlayChestCue(ChestCue cue, bool shiny, int revealIndex, int qualityTier)
        {
            int quality = Mathf.Clamp(qualityTier, 0, 4);
            if (cue == ChestCue.Stat)
            {
                int stat = Mathf.Clamp(revealIndex, 0, MonsterAcquisitionReveal.StatCount - 1);
                // A short physical tap leaves room for both the faster beat and the individual grade's pitch.
                PlayRewardVoice(chestContactClip ?? authoredBasicHit ?? authoredPlace, .065f + quality * .01f,
                    .85f + stat * .075f + quality * .16f, 0f);
                if (quality >= 3)
                    PlayRewardVoice(chestStatAccentClip, .014f + quality * .004f, 1.8f + (quality - 3) * .3f, 0f);
                return;
            }
            if (cue == ChestCue.Reveal)
            {
                PlayRewardVoice(chestContactClip ?? authoredBasicHit, .07f + quality * .01f, 1f + quality * .025f, 0f);
                PlayRewardVoice(chestStampClip ?? authoredCriticalHit ?? authoredPlace,
                    .09f + quality * .02f, 1.10f - quality * .045f, 0f);
                if (quality >= 3 || shiny)
                    PlayRewardVoice(chestStatAccentClip, .02f + quality * .002f + (shiny ? .015f : 0f),
                        1.2f + quality * .08f, 0f);
                return;
            }
            if (cue == ChestCue.Arrival) StopChestSounds();
            if (cue == ChestCue.Open || cue == ChestCue.Confirm)
                chestTension.Stop();
            if (cue == ChestCue.Hold) { chestTensionFade = .02f; return; }
            if (cue == ChestCue.Tension)
            {
                if (chestTensionClip == null) return;
                chestTension.clip = chestTensionClip;
                chestTensionFade = 0f;
                chestTension.pitch = 1f;
                chestTension.volume = effectsVolume * Mathf.Min(1f, MatchBasicHitLevel(chestTensionClip, .10f));
                chestTension.timeSamples = Mathf.RoundToInt(chestTensionClip.frequency * .10f);
                chestTension.Play();
                return;
            }

            bool opening = cue == ChestCue.Open;
            AudioClip clip = cue == ChestCue.Arrival ? authoredPlace : opening ? chestOpenClip : chestRevealClip;
            float pitch = cue == ChestCue.Confirm ? .94f :
                opening ? 1f + Mathf.Clamp(revealIndex, 0, 3) * .025f : 1f;
            float gain = cue == ChestCue.Arrival ? .16f : opening ? .25f :
                .20f;
            // The supplied chest's main crack is at 179 ms; lead with it, not the quiet pickup.
            float pickup = opening ? 6558f / 44100f : cue == ChestCue.Arrival ? 0f : .07f;
            PlayRewardVoice(clip, gain, pitch, pickup, cue == ChestCue.Confirm);
        }

        public static void PlayCapture(CaptureCue cue, float strength = 1f)
        {
            if (instance == null || (!GameFrontend.IsProduction && !instance.previewEnabled)) return;
            strength = Mathf.Clamp01(strength);
            bool beat = cue == CaptureCue.Beat;
            AudioClip clip = beat ? instance.captureBeatClip : cue == CaptureCue.Success
                ? instance.chestRevealClip : instance.captureFailureClip;
            float gain = beat ? Mathf.Lerp(.12f, .22f, strength) : .28f;
            float pitch = beat ? Mathf.Lerp(.82f, 1.12f, strength) : 1f;
            instance.PlayRewardVoice(clip, gain, pitch, cue == CaptureCue.Success ? .07f : 0f);
        }

        private void PlayRewardVoice(AudioClip clip, float gain, float pitch, float pickup,
            bool keepOnSceneChange = false)
        {
            if (clip == null) return;
            int slot = nextChestVoice++ % chestVoices.Length;
            if (keepOnSceneChange) chestConfirmationSlot = slot;
            else if (slot == chestConfirmationSlot) chestConfirmationSlot = -1;
            AudioSource voice = chestVoices[slot];
            voice.Stop();
            voice.clip = clip;
            voice.pitch = pitch;
            chestGains[slot] = Mathf.Min(1f, MatchBasicHitLevel(clip, gain));
            chestPeaks[slot] = MeasureCombatClip(clip).peak;
            voice.timeSamples = Mathf.RoundToInt(clip.frequency * pickup);
            voice.Play();
            RefreshChestMix();
        }

        private void RefreshChestMix()
        {
            if (!paused && chestTensionFade > 0f && chestTension != null)
            {
                float previous = chestTensionFade;
                chestTensionFade = Mathf.Max(0f, chestTensionFade - Time.unscaledDeltaTime);
                chestTension.volume *= chestTensionFade / previous;
                if (chestTensionFade <= 0f) chestTension.Stop();
            }
            float peak = 0f;
            for (int i = 0; i < chestVoices.Length; i++)
                if (chestVoices[i] != null && chestVoices[i].isPlaying) peak += chestGains[i] * chestPeaks[i];
            float gain = peak > .8f ? .8f / peak : 1f;
            for (int i = 0; i < chestVoices.Length; i++)
                if (chestVoices[i] != null) chestVoices[i].volume = effectsVolume * chestGains[i] * gain;
        }

        private void StopChestSounds(bool keepConfirmation = false)
        {
            if (chestTension != null) chestTension.Stop();
            chestTensionFade = 0f;
            for (int i = 0; i < chestVoices.Length; i++)
                if (chestVoices[i] != null && (!keepConfirmation || i != chestConfirmationSlot))
                    chestVoices[i].Stop();
            if (!keepConfirmation) chestConfirmationSlot = -1;
            nextChestVoice = 0;
        }
    }
}
