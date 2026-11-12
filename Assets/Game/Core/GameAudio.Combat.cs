using System.Collections.Generic;
using UnityEngine;

namespace BombBrick.Game.Core
{
    public sealed partial class GameAudio
    {
        public const int CombatVoiceLimit = 12;
        private const int BasicVoiceLimit = 6;
        private const int AttackVoiceLimit = 2;
        private readonly AudioSource[] combatVoices = new AudioSource[CombatVoiceLimit];
        private readonly AudioSource[] combatLayers = new AudioSource[CombatVoiceLimit];
        private readonly float[] voiceUntil = new float[CombatVoiceLimit];
        private readonly int[] voicePriority = new int[CombatVoiceLimit];
        private readonly int[] voiceOwner = new int[CombatVoiceLimit];
        private AudioClip[,,] combatClips;
        private AudioClip[,,] combatLayerClips;
        private AudioClip authoredBasicHit;
        private AudioClip authoredCriticalHit;
        private AudioClip authoredDeath;
        private AudioClip authoredLeylinePulse;
        private AudioClip authoredBearSpirit;
        private readonly AudioClip[] asmrHits = new AudioClip[7];
        private readonly AudioClip[] asmrAttacks = new AudioClip[7];
        private readonly Dictionary<string,AudioClip[]> basicFamilyClips=new Dictionary<string,AudioClip[]>();
        private AudioClip asmrCriticalAccent;
        private readonly CombatSoundGate soundGate = new CombatSoundGate();
        private readonly float[,] lastStyleAt = new float[7, 6];
        private readonly int[,] variation = new int[7, 6];
        private readonly int[,] playedByCue = new int[7, 6];
        private readonly float[] voiceGain = new float[CombatVoiceLimit];
        private float effectsVolume = .65f;
        private bool previewEnabled;
        private bool paused;
        private bool combatEnded;
        private int mutedCues;
        public string LastCombatSound { get; private set; } = "-";
        public int PlayedCombatSounds { get; private set; }
        public int SuppressedCombatSounds { get; private set; }
        public int PlayedCount(CombatSoundStyle style, CombatSoundCue cue) => playedByCue[(int)style, (int)cue];

        private void InitializeCombatAudio()
        {
            authoredBasicHit = Resources.Load<AudioClip>(
                "Audio/Combat/Feedback/basic_hit");
            authoredCriticalHit = Resources.Load<AudioClip>(
                "Audio/Combat/Feedback/critical_hit");
            authoredDeath = Resources.Load<AudioClip>("Audio/Combat/Feedback/death_burst");
            authoredLeylinePulse = Resources.Load<AudioClip>(
                "Audio/Combat/Samples/lowFrequency_explosion_000");
            authoredBearSpirit = Resources.Load<AudioClip>("Sounds/Bear Fighting Spirit Growl");
            const string asmrRoot = "Audio/Combat/Asmr/";
            string[] hitNames = { "hit_bite", "hit_body", "hit_slash", "hit_light", "hit_energy", "hit_energy", "hit_energy" };
            string[] attackNames = { "attack_swing", "attack_swing", "attack_claw", "attack_throw", "attack_magic", "attack_organic", "attack_magic" };
            for (int style = 0; style < asmrHits.Length; style++)
            {
                asmrHits[style] = Resources.Load<AudioClip>(asmrRoot + hitNames[style]);
                asmrAttacks[style] = Resources.Load<AudioClip>(asmrRoot + attackNames[style]);
            }
            asmrCriticalAccent = Resources.Load<AudioClip>(asmrRoot + "critical_accent");
            string[,] familyPaths={
                {"wyvern","BasicFamilies/Wyvern/attack","BasicFamilies/Wyvern/hit"},
                {"monkey","BasicFamilies/Monkey/attack","BasicFamilies/Monkey/hit"},
                {"wizard_mouse","WizardMouse/launch","WizardMouse/hit"},
                {"arachne","Spider/launch","Spider/pierce"},
                {"frog","Frog/launch","Frog/hit"},
                {"mushroom","Mushroom/launch","Mushroom/attach"},
                {"spirit_deer","SpiritDeer/spread","SpiritDeer/guard"}
            };
            for(int i=0;i<familyPaths.GetLength(0);i++)
                basicFamilyClips[familyPaths[i,0]]=new[]{
                    Resources.Load<AudioClip>("Audio/Combat/"+familyPaths[i,1]),
                    Resources.Load<AudioClip>("Audio/Combat/"+familyPaths[i,2])};
            basicFamilyClips["spider"]=basicFamilyClips["arachne"];
            combatClips = new AudioClip[7, 6, CombatSoundBank.Variations];
            combatLayerClips = new AudioClip[7, 6, CombatSoundBank.Variations];
            for (int style = 0; style < 7; style++)
            for (int cue = 0; cue < 6; cue++)
            {
                lastStyleAt[style, cue] = -100f;
                for (int variant = 0; variant < CombatSoundBank.Variations; variant++)
                {
                    CombatSoundStyle soundStyle = (CombatSoundStyle)style;
                    CombatSoundCue soundCue = (CombatSoundCue)cue;
                    combatClips[style, cue, variant] =
                        CombatSoundBank.Load(soundStyle, soundCue, variant) ??
                        CombatSoundBank.Create(soundStyle, soundCue, variant);
                    combatLayerClips[style, cue, variant] =
                        CombatSoundBank.LoadLayer(soundStyle, soundCue, variant);
                }
            }
            for (int i = 0; i < combatVoices.Length; i++)
            {
                combatVoices[i] = gameObject.AddComponent<AudioSource>();
                combatVoices[i].playOnAwake = false;
                combatVoices[i].spatialBlend = 0f;
                combatLayers[i] = gameObject.AddComponent<AudioSource>();
                combatLayers[i].playOnAwake = false;
                combatLayers[i].spatialBlend = 0f;
            }
        }

        public static GameAudio EnableTestAudio(GameObject owner)
        {
            if (instance == null) owner.AddComponent<GameAudio>();
            instance.previewEnabled = true;
            instance.combatEnded = false;
            instance.Apply(GameUserSettings.Load());
            instance.music.Stop();
            return instance;
        }

        private AudioClip ResolveBasicClip(CombatSoundStyle style,CombatSoundCue cue,string family)
        {
            if(cue!=CombatSoundCue.Attack&&cue!=CombatSoundCue.Hit)return null;
            AudioClip[] clips;
            AudioClip dedicated=family!=null&&basicFamilyClips.TryGetValue(family,out clips)
                ?clips[cue==CombatSoundCue.Attack?0:1]:null;
            return dedicated!=null?dedicated:cue==CombatSoundCue.Attack?asmrAttacks[(int)style]:asmrHits[(int)style];
        }

        public void EndTestAudio()
        {
            previewEnabled = false;
            mutedCues = 0;
            StopCombatSounds();
        }

        public bool CueEnabled(CombatSoundCue cue) => (mutedCues & (1 << (int)cue)) == 0;
        public void ToggleCue(CombatSoundCue cue) => mutedCues ^= 1 << (int)cue;
        public void Preview(CombatSoundStyle style, CombatSoundCue cue)
        {
            if (previewEnabled && CueEnabled(cue)) PlayCombatVoice(style, cue, 0f, 1f, true);
        }

        public static void PlayCombat(CombatSoundStyle style, CombatSoundCue cue,
            int sourceId, Vector3 position, float strength = 1f,
            bool critical = false, bool leylinePulse = false,bool bearSpirit = false,AudioClip authoredSound = null,bool loop = false,
            bool allowDuringTimeStop = false,string basicFamily = null,bool importantImpact = false)
        {
            if(CombatReadabilityPresentation.Active&&!CombatReadabilityPresentation.SkillAudio&&
               (cue==CombatSoundCue.Cast||cue==CombatSoundCue.Impact||cue==CombatSoundCue.Periodic))return;
            // Only explicitly authored sounds opt into the otherwise-muted skill channels.
            leylinePulse &= style == CombatSoundStyle.Heavy && cue == CombatSoundCue.Periodic;
            bearSpirit &= style == CombatSoundStyle.Heavy && cue == CombatSoundCue.Impact;
            if(cue!=CombatSoundCue.Impact&&cue!=CombatSoundCue.Cast&&cue!=CombatSoundCue.Periodic)authoredSound=null;
            loop &= authoredSound!=null;
            allowDuringTimeStop &= authoredSound!=null&&!loop;
            bool attack = cue == CombatSoundCue.Attack && instance != null && instance.ResolveBasicClip(style,cue,basicFamily) != null;
            if (cue != CombatSoundCue.Hit && cue != CombatSoundCue.Death && !attack && !leylinePulse&&!bearSpirit&&authoredSound==null) return;
            if (instance == null || instance.combatEnded || instance.paused || Time.timeScale <= 0f&&!allowDuringTimeStop ||
                (!GameFrontend.IsProduction && !instance.previewEnabled) ||
                GameFrontend.BlocksGameplay || !instance.CueEnabled(cue)) return;
            // Coalesce AoE victims while allowing different authored phases on the same frame.
            if (!instance.soundGate.TryAccept(sourceId, cue, Time.frameCount, allowDuringTimeStop?Time.unscaledTime:Time.time,
                authoredSound==null?0:authoredSound.GetInstanceID(),allowDuringTimeStop))
            {
                instance.SuppressedCombatSounds++;
                return;
            }
            Camera camera = Camera.main;
            float pan = camera == null ? 0f :
                Mathf.Clamp((camera.WorldToViewportPoint(position).x - .5f) * .8f, -.4f, .4f);
            instance.PlayCombatVoice(style, cue, pan, strength, false, critical, leylinePulse,bearSpirit,authoredSound,sourceId,loop
                ,allowDuringTimeStop,basicFamily,importantImpact
            );
        }

        private void PlayCombatVoice(CombatSoundStyle style, CombatSoundCue cue,
            float pan, float strength, bool preview, bool critical = false, bool leylinePulse = false,bool bearSpirit = false,AudioClip authoredSound = null,int sourceId = 0,bool loop = false
            ,bool preservePresentationGain = false,string basicFamily = null,bool importantImpact = false
        )
        {
            int s = (int)style, c = (int)cue;
            float now = Time.unscaledTime;
            bool criticalHit = cue == CombatSoundCue.Hit && critical;
            bool importantContact = importantImpact && authoredSound != null && !loop && cue == CombatSoundCue.Impact;
            int priority = cue == CombatSoundCue.Attack ? -1 : cue == CombatSoundCue.Death ? 3 :
                cue == CombatSoundCue.Impact ? (importantContact ? 3 : 2) : criticalHit || cue == CombatSoundCue.Cast || authoredSound!=null ? 1 : 0;
            // Different attackers' hits may overlap. The source gate already coalesces AoE victims.
            if (!preview && authoredSound == null && cue != CombatSoundCue.Hit && now - lastStyleAt[s, c] < .035f)
            { SuppressedCombatSounds++; return; }
            if (priority <= 0)
            {
                int activeBasics = 0;
                for (int i = 0; i < CombatVoiceLimit; i++)
                    if ((combatVoices[i].isPlaying || combatLayers[i].isPlaying) && voicePriority[i] == priority) activeBasics++;
                if (activeBasics >= (priority < 0 ? AttackVoiceLimit : BasicVoiceLimit))
                { SuppressedCombatSounds++; return; }
            }
            int slot = -1;
            // Skill/death tails in early slots must not hide free slots from basic attacks.
            for (int i = 0; i < CombatVoiceLimit; i++)
                if (!combatVoices[i].isPlaying && !combatLayers[i].isPlaying) { slot = i; break; }
            if (slot < 0 && priority >= 0)
                for (int i = 0; i < CombatVoiceLimit; i++)
                    if (voicePriority[i] < priority &&
                        (slot < 0 || voiceUntil[i] < voiceUntil[slot])) slot = i;
            if (slot < 0) { SuppressedCombatSounds++; return; }
            lastStyleAt[s, c] = now;
            int v = variation[s, c]++ % CombatSoundBank.Variations;
            AudioSource voice = combatVoices[slot];
            AudioSource layer = combatLayers[slot];
            voice.Stop();
            layer.Stop();
            voice.loop = loop;
            voiceOwner[slot] = sourceId;
            AudioClip basicClip = ResolveBasicClip(style,cue,basicFamily);
            AudioClip authoredClip = authoredSound!=null?authoredSound:bearSpirit ? authoredBearSpirit : leylinePulse ? authoredLeylinePulse : cue == CombatSoundCue.Death
                ? authoredDeath
                : basicClip != null ? basicClip : cue == CombatSoundCue.Hit ? (criticalHit ? authoredCriticalHit : authoredBasicHit) : null;
            voice.clip = authoredClip != null ? authoredClip : combatClips[s, c, v];
            // Small deterministic variations keep repeated hits natural without using combat RNG.
            voice.pitch = cue == CombatSoundCue.Hit
                ? .98f + v * .02f : 1f;
            voice.panStereo = pan;
            float gain = basicClip != null ? (cue == CombatSoundCue.Attack ? .055f : criticalHit ? .24f : .22f) : loop?.11f:cue==CombatSoundCue.Periodic?.12f:
                cue==CombatSoundCue.Cast?.20f:cue==CombatSoundCue.Hit&&!criticalHit?.28f:.30f;
            voiceGain[slot] = Mathf.Min(1f,MatchBasicHitLevel(voice.clip,gain) * Mathf.Clamp(strength, .3f, 1.4f));
            if(authoredSound!=null&&!preservePresentationGain&&!CombatSkillClock.Active)
                voiceGain[slot]*=CombatReadabilityPresentation.SkillSoundGain;
            voice.volume = effectsVolume * voiceGain[slot] * combatMixGain;
            voice.priority = 160 - priority * 40;
            // User death_burst: the first impact is at 47 ms. Skip its quiet pickup
            // at a near-zero sample (2027 / 48000 s) so the burst event leads by <5 ms.
            float startTime = cue == CombatSoundCue.Death && authoredClip != null && authoredClip == authoredDeath
                ? 2027f / 48000f : 0f;
            voice.timeSamples = Mathf.RoundToInt(startTime * voice.clip.frequency);
            voice.Play();
            AudioClip layerClip = criticalHit && basicClip != null ? asmrCriticalAccent : authoredClip == null ? combatLayerClips[s, c, v] : null;
            if (layerClip != null)
            {
                layer.clip = layerClip;
                layer.loop = false;
                layer.pitch = voice.pitch;
                layer.panStereo = pan;
                layer.volume = voice.volume * .38f;
                layer.priority = voice.priority + 1;
                layer.Play();
            }
            voicePeak[slot]=MeasureCombatClip(voice.clip).peak+MeasureCombatClip(layerClip).peak*.38f;
            voicePriority[slot] = priority;
            ordinaryHitVoice[slot] = cue == CombatSoundCue.Hit && !critical;
            // Only an accepted, authored heavy impact briefly clears space around its transient.
            if (!preview && !loop && style == CombatSoundStyle.Heavy && cue == CombatSoundCue.Impact &&
                authoredClip != null && now - lastHeavyImpactAt >= .14f)
                lastHeavyImpactAt = now;
            RefreshCombatMix();
            voiceUntil[slot] = loop ? float.PositiveInfinity : now + Mathf.Max(voice.clip.length - startTime,
                layerClip == null ? 0f : layerClip.length) / voice.pitch;
            LastCombatSound = style + " / " + cue;
            PlayedCombatSounds++;
            playedByCue[s, c]++;
        }

        public static void StopCombatLoop(int sourceId,AudioClip clip,bool finishCurrent=false)
        {
            if(instance==null||clip==null)return;
            for(int i=0;i<CombatVoiceLimit;i++)
            {
                AudioSource voice=instance.combatVoices[i];
                if(instance.voiceOwner[i]!=sourceId||voice.clip!=clip||!voice.loop)continue;
                voice.loop=false;
                if(!finishCurrent)voice.Stop();
                else instance.voiceUntil[i]=Time.unscaledTime+
                    Mathf.Max(0f,clip.length-voice.time)/Mathf.Max(.01f,Mathf.Abs(voice.pitch));
            }
        }

        public void StopCombatSounds()
        {
            for (int i = 0; i < combatVoices.Length; i++)
            {
                if (combatVoices[i] != null) { combatVoices[i].Stop(); combatVoices[i].loop=false; }
                if (combatLayers[i] != null) combatLayers[i].Stop();
            }
            soundGate.Clear();
            combatMixGain=1f;
            lastHeavyImpactAt = -100f;
            LastCombatSound = "-";
            PlayedCombatSounds = SuppressedCombatSounds = 0;
            System.Array.Clear(playedByCue, 0, playedByCue.Length);
            for (int s = 0; s < 7; s++)
            for (int c = 0; c < 6; c++) lastStyleAt[s, c] = -100f;
        }

        public static void ResetCombatSounds()
        {
            if (instance != null) instance.StopCombatSounds();
        }

        private void DestroyCombatAudio()
        {
            if (combatClips != null)
                foreach (AudioClip clip in combatClips)
                    if (clip != null && clip.name.StartsWith("Combat ")) Destroy(clip);
        }
    }

    public sealed class CombatSoundGate
    {
        private struct Stamp { public int frame; public float time; }
        private readonly Dictionary<(int,CombatSoundCue,int,bool),Stamp> stamps = new Dictionary<(int,CombatSoundCue,int,bool),Stamp>();
        public bool TryAccept(int sourceId, CombatSoundCue cue, int frame, float time,int soundId=0,bool unscaledClock=false)
        {
            // A cinematic's unscaled timestamp must not suppress a later, resumed-world sound.
            var key=(sourceId,cue,soundId,unscaledClock);
            if (stamps.TryGetValue(key, out Stamp previous) &&
                (frame == previous.frame || cue == CombatSoundCue.Periodic && time - previous.time < .6f))
                return false;
            stamps[key] = new Stamp { frame = frame, time = time };
            return true;
        }
        public void Clear() => stamps.Clear();
    }
}
