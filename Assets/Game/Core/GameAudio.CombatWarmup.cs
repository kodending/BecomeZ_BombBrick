using System;
using System.Collections;
using UnityEngine;

namespace BombBrick.Game.Core
{
    public sealed partial class GameAudio
    {
        public const string PreparedCombatSoundsPath = "Audio/Combat/PreparedCombatSounds";

        [Serializable]
        public sealed class PreparedCombatSound
        {
            public string path;
            public int samples, channels, frequency;
            public float peak, rms;
        }

        [Serializable]
        public sealed class PreparedCombatSounds
        {
            public PreparedCombatSound[] clips;
        }

        private bool combatAudioWarmupRunning, combatAudioWarmupComplete;
        private void SeedPreparedEffectSoundLevels(PreparedEffectSoundLevels effectLevels)
        {
            if(effectLevels==null||effectLevels.clips==null)return;
            foreach(var entry in effectLevels.clips)
            {
                var clip=entry.clip;
                if(clip!=null&&clip.samples==entry.samples&&clip.channels==entry.channels&&
                   clip.frequency==entry.frequency&&entry.peak>0f&&entry.rms>=0f)
                    combatLevels[clip]=new CombatLevel{peak=entry.peak,rms=entry.rms};
            }
        }

        private static bool CombatAudioSliceExpired(long slice)=>
            (System.Diagnostics.Stopwatch.GetTimestamp()-slice)*1000d/
                System.Diagnostics.Stopwatch.Frequency>=2d;

        private static void RequireCombatAudioData(AudioClip clip)
        {
            if(clip!=null&&clip.loadState!=AudioDataLoadState.Loaded)
                throw new InvalidOperationException("Combat audio data failed to load: "+clip.name);
        }

        public static IEnumerator WarmupCombatSounds()
        {
            GameAudio audio=instance;
            if(audio==null||audio.combatAudioWarmupComplete)yield break;
            if(audio.combatAudioWarmupRunning)
            {
                while(audio!=null&&audio.combatAudioWarmupRunning)yield return null;
                if(audio==null||audio.combatAudioWarmupComplete)yield break;
            }
            var asset=Resources.Load<TextAsset>(PreparedCombatSoundsPath);
            if(asset==null)yield break;
            var prepared=JsonUtility.FromJson<PreparedCombatSounds>(asset.text);
            if(prepared?.clips==null)yield break;
            audio.combatAudioWarmupRunning=true;
            try
            {
                // shortcut: rebuild the prepared catalog after editing audio; metadata cannot detect same-length replacements.
                const int batchSize=8;
                var requests=new ResourceRequest[batchSize];
                long slice=System.Diagnostics.Stopwatch.GetTimestamp();
                for(int offset=0;offset<prepared.clips.Length;offset+=batchSize)
                {
                    int count=Math.Min(batchSize,prepared.clips.Length-offset);
                    for(int i=0;i<count;i++)
                    {
                        requests[i]=Resources.LoadAsync<AudioClip>(prepared.clips[offset+i].path);
                        if(CombatAudioSliceExpired(slice))
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                    // Start data loading for every clip in this batch before waiting on any decoder.
                    for(int i=0;i<count;i++)
                    {
                        while(!requests[i].isDone)
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                        if(audio==null)yield break;
                        var clip=requests[i].asset as AudioClip;
                        if(clip!=null)clip.LoadAudioData();
                        if(CombatAudioSliceExpired(slice))
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                    for(int i=0;i<count;i++)
                    {
                        var clip=requests[i].asset as AudioClip;
                        while(clip!=null&&clip.loadState==AudioDataLoadState.Loading)
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                        if(audio==null)yield break;
                        RequireCombatAudioData(clip);
                        var entry=prepared.clips[offset+i];
                        if(clip!=null&&clip.samples==entry.samples&&clip.channels==entry.channels&&
                           clip.frequency==entry.frequency&&entry.peak>0f&&entry.rms>=0f)
                            audio.combatLevels[clip]=new CombatLevel{peak=entry.peak,rms=entry.rms};
                        else if(clip!=null)audio.MeasureCombatClip(clip);
                        if(CombatAudioSliceExpired(slice))
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                }
                var effectRequest=Resources.LoadAsync<PreparedEffectSoundLevels>("Audio/PreparedEffectSoundLevels");
                while(!effectRequest.isDone)
                {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                var effectLevels=effectRequest.asset as PreparedEffectSoundLevels;
                if(audio==null)yield break;
                if(effectLevels?.clips!=null)
                {
                    foreach(var entry in effectLevels.clips)
                    {
                        if(entry.clip!=null)entry.clip.LoadAudioData();
                        if(CombatAudioSliceExpired(slice))
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                    foreach(var entry in effectLevels.clips)
                    {
                        while(entry.clip!=null&&entry.clip.loadState==AudioDataLoadState.Loading)
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                        if(audio==null)yield break;
                        RequireCombatAudioData(entry.clip);
                        if(CombatAudioSliceExpired(slice))
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                }
                audio.SeedPreparedEffectSoundLevels(effectLevels);
                if(effectLevels?.clips!=null)
                    foreach(var entry in effectLevels.clips)
                    {
                        if(entry.clip!=null&&!audio.combatLevels.ContainsKey(entry.clip))audio.MeasureCombatClip(entry.clip);
                        if(CombatAudioSliceExpired(slice))
                        {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                audio.combatAudioWarmupComplete=true;
            }
            finally{if(audio!=null)audio.combatAudioWarmupRunning=false;}
        }
    }
}
