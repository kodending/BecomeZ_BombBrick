using System.Collections.Generic;
using UnityEngine;

namespace BombBrick.Game.Core
{
    public sealed partial class GameAudio
    {
        private struct CombatLevel { public float peak, rms; }
        private readonly Dictionary<AudioClip,CombatLevel> combatLevels=new Dictionary<AudioClip,CombatLevel>();
        private readonly float[] voicePeak=new float[CombatVoiceLimit];
        private float combatMixGain=1f;
        private readonly bool[] ordinaryHitVoice = new bool[CombatVoiceLimit];
        private float lastHeavyImpactAt = -100f;

        private static float ImpactDuckGain(bool ordinaryHit, float elapsed)
        {
            if (!ordinaryHit || elapsed < 0f || elapsed >= .09f) return 1f;
            return Mathf.Lerp(.55f, 1f, Mathf.SmoothStep(0f, 1f, (elapsed - .025f) / .065f));
        }

        private float CurrentCombatVoiceGain(int slot) => voiceGain[slot] *
            ImpactDuckGain(ordinaryHitVoice[slot], Time.unscaledTime - lastHeavyImpactAt);

        private CombatLevel MeasureCombatClip(AudioClip clip)
        {
            if(clip==null)return default;
            if(combatLevels.TryGetValue(clip,out var level))return level;
            // Read each imported clip once, never scan samples during subsequent attacks.
            clip.LoadAudioData();
            var samples=new float[clip.samples*clip.channels];
            if(!clip.GetData(samples,0))return combatLevels[clip]=new CombatLevel {peak=1f};
            int window=Mathf.Clamp(Mathf.RoundToInt(clip.frequency*.05f),1,clip.samples);
            double energy=0d,maximum=0d;
            for(int frame=0;frame<clip.samples;frame++)
            {
                for(int channel=0;channel<clip.channels;channel++)
                {
                    float sample=samples[frame*clip.channels+channel];
                    level.peak=Mathf.Max(level.peak,Mathf.Abs(sample));
                    energy+=(double)sample*sample;
                    if(frame<window)continue;
                    float previous=samples[(frame-window)*clip.channels+channel];
                    energy-=(double)previous*previous;
                }
                if(frame>=window-1)maximum=System.Math.Max(maximum,energy);
            }
            level.rms=(float)System.Math.Sqrt(maximum/(window*clip.channels));
            combatLevels[clip]=level;return level;
        }

        private float MatchBasicHitLevel(AudioClip clip,float gain)
        {
            if(clip==null||authoredBasicHit==null||clip==authoredBasicHit)return gain;
            CombatLevel reference=MeasureCombatClip(authoredBasicHit),level=MeasureCombatClip(clip);
            if(reference.rms<=.00001f||level.rms<=.00001f)return gain;
            // Match the strongest 50 ms, with a peak ceiling for very sparse transients.
            // This is an SFX reference level, not an integrated LUFS measurement.
            float correction=Mathf.Min(reference.rms/level.rms,reference.peak*1.35f/level.peak);
            return gain*correction;
        }

        private void RefreshCombatMix(bool release=false)
        {
            if(paused)return;
            float peakBudget=0f;
            for(int i=0;i<CombatVoiceLimit;i++)
                if(combatVoices[i]!=null&&(combatVoices[i].isPlaying||combatLayers[i]!=null&&combatLayers[i].isPlaying))
                    peakBudget+=CurrentCombatVoiceGain(i)*voicePeak[i];
            // Leave a lone hit unchanged; reserve headroom when many attacks coincide.
            float target=peakBudget>.85f?.85f/peakBudget:1f;
            combatMixGain=release?Mathf.MoveTowards(combatMixGain,target,Time.unscaledDeltaTime*3f):Mathf.Min(combatMixGain,target);
            combatMixGain=Mathf.Min(combatMixGain,target);
            for(int i=0;i<CombatVoiceLimit;i++)
            {
                float gain=CurrentCombatVoiceGain(i);
                if(combatVoices[i]!=null)combatVoices[i].volume=effectsVolume*gain*combatMixGain;
                if(combatLayers[i]!=null)combatLayers[i].volume=effectsVolume*gain*combatMixGain*.38f;
            }
        }
    }
}
