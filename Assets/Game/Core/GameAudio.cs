using UnityEngine;

namespace BombBrick.Game.Core
{
    // Shared UI and generated fallback sounds.
    public sealed partial class GameAudio : MonoBehaviour
    {
        public enum Cue
        {
            Click, Start, Hit, Skill, Reward, Victory, Defeat, Place,
            SlideOpen, SlideClose, Logo
        }
        private static GameAudio instance;
        private AudioSource music;
        private AudioSource effects;
        private AudioClip[] clips;
        private AudioClip authoredClick;
        private AudioClip authoredPlace;
        private AudioClip authoredSlideOpen;
        private AudioClip authoredSlideClose;
        private AudioClip authoredBattleVictory;
        private AudioClip authoredBattleDefeat;
        private AudioClip authoredTownBgm;
        private AudioClip authoredExpeditionBgm;
        private AudioClip authoredLogo;
        private AudioClip authoredBattleStart;
        private AudioClip calmLoop;
        private float configuredMusicVolume = 1f;
        private float musicTargetVolume;
        private float musicFadeElapsed;
        private float musicFadeDuration;
        private bool cinematicAudioFocus;
        private float cinematicMusicGain=1f;
        private float nextHitAt;
        private float nextSkillAt;

        private void Awake()
        {
            instance = this;
            InitializeCombatAudio();
            music = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = effects.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = effects.spatialBlend = 0f;
            authoredClick = Resources.Load<AudioClip>(
                "Sounds/Menu Pop Up Open 1");
            authoredPlace = Resources.Load<AudioClip>(
                "Sounds/45_Landing_01");
            authoredSlideOpen = Resources.Load<AudioClip>(
                "Sounds/Organice Ice Next Page Slide Transition 1");
            authoredSlideClose = Resources.Load<AudioClip>(
                "Sounds/Organice Ice Next Page Slide Transition 2");
            authoredBattleVictory = Resources.Load<AudioClip>("Sounds/BattleVictory");
            authoredBattleDefeat = Resources.Load<AudioClip>("Sounds/BattleDefeat");
            authoredTownBgm = Resources.Load<AudioClip>("Sounds/TownAfternoonBGM");
            authoredLogo = Resources.Load<AudioClip>("Sounds/Special 4-6");
            authoredBattleStart = Resources.Load<AudioClip>("Sounds/BattleEngage");
            SeedPreparedEffectSoundLevels(Resources.Load<PreparedEffectSoundLevels>("Audio/PreparedEffectSoundLevels"));
            InitializeChestAudio();
            InitializeEvolutionAudio();
            clips = new[]
            {
                authoredClick != null
                    ? authoredClick
                    : Tone("UI click", new[] { 660f, 880f }, 0.035f),
                authoredBattleStart != null
                    ? authoredBattleStart
                    : Tone("Battle engage", new[] { 110f, 73f }, 0.14f, true),
                Tone("Hit", new[] { 150f, 70f }, 0.028f),
                Tone("Skill", new[] { 220f, 440f, 660f }, 0.065f),
                Tone("Reward", new[] { 660f, 880f, 1100f }, 0.075f),
                authoredBattleVictory != null
                    ? authoredBattleVictory
                    : Tone("Victory", new[] { 523f, 659f, 784f, 1047f }, 0.18f),
                authoredBattleDefeat != null
                    ? authoredBattleDefeat
                    : Tone("Defeat", new[] { 392f, 330f, 262f }, 0.24f),
                authoredPlace != null
                    ? authoredPlace
                    : Tone("Monster place", new[] { 180f, 110f }, 0.08f),
                authoredSlideOpen != null
                    ? authoredSlideOpen
                    : Tone("Slide open", new[] { 440f, 660f }, 0.10f),
                authoredSlideClose != null
                    ? authoredSlideClose
                    : Tone("Slide close", new[] { 660f, 440f }, 0.10f),
                authoredLogo != null
                    ? authoredLogo
                    : Tone("Logo", new[] { 330f, 660f, 990f }, 0.12f)
            };
            // Sparse original pentatonic motifs; samples are created only once.
            calmLoop = Tone("Camp theme", new[] { 262f, 0f, 330f, 392f, 0f, 330f, 294f, 0f,
                220f, 0f, 262f, 330f, 0f, 294f, 262f, 0f }, 0.48f, true);
        }

        public static void Play(Cue cue)
        {
            if (instance == null || (!GameFrontend.IsProduction && !instance.previewEnabled) ||
                GameFrontend.BlocksGameplay && cue == Cue.Hit) return;
            int index = (int)cue;
            if (instance.effects == null || instance.clips == null || index < 0 ||
                index >= instance.clips.Length || instance.clips[index] == null) return;
            float now = Time.unscaledTime;
            if (cue == Cue.Hit)
            {
                if (now < instance.nextHitAt) return;
                instance.nextHitAt = now + 0.09f;
            }
            if (cue == Cue.Skill)
            {
                if (now < instance.nextSkillAt) return;
                instance.nextSkillAt = now + 0.16f;
            }
            instance.effects.PlayOneShot(
                instance.clips[index],
                cue == Cue.Hit ? 0.28f : cue == Cue.Click ? 0.22f :
                cue == Cue.Start ? 0.52f : 0.65f);
        }

        public void Apply(GameUserSettings settings)
        {
            effectsVolume = settings.effectsVolume;
            for (int i = 0; i < combatVoices.Length; i++)
            {
                float gain = CurrentCombatVoiceGain(i);
                combatVoices[i].volume = effectsVolume * gain * combatMixGain;
                combatLayers[i].volume = effectsVolume * gain * combatMixGain * .38f;
            }
            RefreshChestMix();
            if (chestTension != null && chestTensionClip != null)
                chestTension.volume = effectsVolume * Mathf.Min(1f, MatchBasicHitLevel(chestTensionClip, .10f));
            configuredMusicVolume = settings.musicVolume;
            musicTargetVolume = configuredMusicVolume * MusicGain(music.clip);
            ApplyMusicVolume();
            effects.volume = settings.effectsVolume;
        }

        public void SetScene(string scene)
        {
            StopCombatSounds();
            StopChestSounds(true);
            bool expedition = scene == GameSceneCatalog.ExpeditionBattle;
            if (expedition && authoredExpeditionBgm == null)
                authoredExpeditionBgm = Resources.Load<AudioClip>("Sounds/ExpeditionPredatorGround");
            AudioClip next = scene == GameSceneCatalog.Boot ? null :
                expedition ? authoredExpeditionBgm :
                scene == GameSceneCatalog.Town && authoredTownBgm != null
                    ? authoredTownBgm : calmLoop;
            SwitchMusic(next, expedition ? .8f : 0f);
        }

        public void StartMainMenuMusic() => SwitchMusic(calmLoop, 3.4f);

        public static void SetCinematicAudioFocus(bool active)
        {
            if(instance!=null)instance.cinematicAudioFocus=active;
        }

        public static void SetBattlePhase(bool active)
        {
            if (instance == null) return;
            instance.combatEnded = !active;
            if (active) return;
            FinishCombatLoops();
        }

        public static void FinishCombatLoops()
        {
            if(instance==null)return;
            // Stop repeating without cutting off the current sound or its layer.
            foreach (var voice in instance.combatVoices)
                if (voice != null) voice.loop = false;
            foreach (var layer in instance.combatLayers)
                if (layer != null) layer.loop = false;
        }

        private void SwitchMusic(AudioClip next, float fadeInDuration)
        {
            if (music.clip == next) return;
            music.Stop();
            music.clip = next;
            if (next == null)
            {
                musicTargetVolume = musicFadeDuration = music.volume = 0f;
                return;
            }
            music.pitch = 1f;
            musicTargetVolume = configuredMusicVolume * MusicGain(next);
            music.Play();
            musicFadeElapsed = 0f;
            musicFadeDuration = fadeInDuration;
            ApplyMusicVolume();
        }

        private void Update()
        {
            RefreshCombatMix(true);
            RefreshChestMix();
            AdvanceMusic(Time.unscaledDeltaTime);
        }

        private void AdvanceMusic(float unscaledDeltaTime)
        {
            float dt=Mathf.Max(0f,unscaledDeltaTime);
            cinematicMusicGain=Mathf.MoveTowards(cinematicMusicGain,cinematicAudioFocus?.35f:1f,
                dt*.65f/(cinematicAudioFocus?.10f:.22f));
            if(musicFadeDuration>0f)
            {
                musicFadeElapsed+=dt;
                if(musicFadeElapsed>=musicFadeDuration)musicFadeDuration=0f;
            }
            ApplyMusicVolume();
        }

        private void ApplyMusicVolume()
        {
            if(music==null)return;
            float reveal=musicFadeDuration>0f?
                Mathf.SmoothStep(0f,1f,musicFadeElapsed/musicFadeDuration):1f;
            music.volume=musicTargetVolume*reveal*cinematicMusicGain;
        }

        public void SetPaused(bool paused)
        {
            if (this.paused == paused) return;
            this.paused = paused;
            foreach (AudioSource voice in combatVoices)
                if (paused) voice.Pause(); else voice.UnPause();
            foreach (AudioSource layer in combatLayers)
                if (paused) layer.Pause(); else layer.UnPause();
            foreach (AudioSource voice in chestVoices)
                if (voice != null) { if (paused) voice.Pause(); else voice.UnPause(); }
            if (chestTension != null) { if (paused) chestTension.Pause(); else chestTension.UnPause(); }
            if (paused) effects.Pause();
            else effects.UnPause();
        }

        private float MusicGain(AudioClip clip) =>
            clip != null && clip == authoredExpeditionBgm ? .06f :
            clip == authoredTownBgm ? 0.16f : 0.32f;

        public static AudioClip Tone(string name, float[] notes, float seconds, bool soft = false)
        {
            const int rate = 22050;
            int length = Mathf.Max(1, Mathf.RoundToInt(seconds * rate));
            float[] samples = new float[length * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            for (int i = 0; i < length; i++)
            {
                if (notes[n] <= 0f) continue;
                float t = i / (float)rate;
                float envelope = Mathf.Min(1f, t / 0.008f) * Mathf.Pow(1f - i / (float)length, 2f);
                float phase = Mathf.Repeat(t * notes[n], 1f);
                float wave = soft ? 1f - 4f * Mathf.Abs(phase - 0.5f) : (phase < 0.25f ? 0.65f : -0.22f);
                samples[n * length + i] = wave * envelope * 0.32f;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            DestroyCombatAudio();
            if (instance == this) instance = null;
            if (clips != null)
                foreach (AudioClip clip in clips)
                    if (clip != null && clip != authoredClick &&
                        clip != authoredPlace && clip != authoredSlideOpen &&
                        clip != authoredSlideClose && clip != authoredBattleVictory &&
                        clip != authoredBattleDefeat &&
                        clip != authoredLogo && clip != authoredBattleStart)
                        Destroy(clip);
            if (calmLoop != null) Destroy(calmLoop);
        }
    }
}
