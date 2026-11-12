using System;
using System.Collections;
using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class Rank4CinematicAudio:IDisposable
{
    private const string ResourceRoot="Audio/Combat/Cinematics/";
    private const float CrossfadeSeconds=.10f;
    [Serializable] private sealed class Catalog {public Bank[] families;}
    [Serializable] private sealed class Bank
    {
        public string family;
        public float duration;
        public Cue prelude,timeline;
    }
    [Serializable] private sealed class Cue
    {
        public string key;
        public float duration,gain=1f;
        [NonSerialized]public AudioClip clip;
    }
    private static Dictionary<string,Bank> banks;
    private static Rank4CinematicAudio current;
    private static readonly HashSet<Rank4CinematicAudio> instances=new HashSet<Rank4CinematicAudio>();
    private readonly GameObject root;
    private readonly AudioSource[] mainVoices=new AudioSource[2];
    private readonly AudioSource preludeSource;
    private AudioSource source;
    private Bank bank;
    private int nextVoice;
    private bool playAudio,focusHeld,disposed,preludeActive;
    private float preludeFade=-1f,mainFade=-1f,preludeGain,mainGain;

    public Rank4CinematicAudio(Transform parent)
    {
        root=new GameObject("Rank 4 continuous cinematic audio");root.transform.SetParent(parent,false);
        preludeSource=CreateVoice("Prelude");
        for(int i=0;i<mainVoices.Length;i++)mainVoices[i]=CreateVoice("Timeline and natural tail "+i);
        source=mainVoices[0];
        root.AddComponent<Rank4CinematicAudioDriver>().Initialize(this);
        instances.Add(this);
    }

    private AudioSource CreateVoice(string name)
    {
        var host=new GameObject(name);host.transform.SetParent(root.transform,false);
        var voice=host.AddComponent<AudioSource>();
        voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0f;voice.pitch=1f;
        return voice;
    }

    private static string Canonical(string family)
    {
        string value=(family??string.Empty).Trim().ToLowerInvariant();
        return value=="arachne"?"spider":value;
    }

    private static void ReadCatalog()
    {
        if(banks!=null)return;
        var asset=Resources.Load<TextAsset>(ResourceRoot+"catalog");
        if(asset==null)return;
        Catalog catalog;
        try{catalog=JsonUtility.FromJson<Catalog>(asset.text);}
        catch(ArgumentException){return;}
        banks=new Dictionary<string,Bank>(StringComparer.Ordinal);
        if(catalog?.families==null)return;
        foreach(var entry in catalog.families)
        {
            if(entry==null||!(entry.duration>0f)||float.IsInfinity(entry.duration))continue;
            string family=Canonical(entry.family);if(family.Length==0)continue;
            entry.family=family;
            if(entry.timeline==null)entry.timeline=new Cue{key="timeline",duration=entry.duration+.65f,gain=1f};
            banks[family]=entry;
        }
    }

    private static Bank Find(string family)
    {
        string key=Canonical(family);ReadCatalog();Bank result;
        return banks!=null&&banks.TryGetValue(key,out result)?result:null;
    }
    public static bool Supports(string family)=>Find(family)!=null;

    private static IEnumerable<Cue> Clips(Bank entry)
    {
        if(entry.prelude!=null)yield return entry.prelude;
        if(entry.timeline!=null)yield return entry.timeline;
    }
    private static void LoadClip(Bank entry,Cue cue)
    {
        if(string.IsNullOrEmpty(cue.key))return;
        if(cue.clip==null)cue.clip=Resources.Load<AudioClip>(ResourceRoot+entry.family+"/"+cue.key);
        if(cue.clip!=null&&cue.clip.loadState==AudioDataLoadState.Unloaded)cue.clip.LoadAudioData();
    }
    private static void LoadBank(Bank entry)
    {foreach(var cue in Clips(entry))LoadClip(entry,cue);}
    public static void Warmup()
    {
        ReadCatalog();if(banks==null)return;
        foreach(var entry in banks.Values)LoadBank(entry);
    }
    public static IEnumerator WarmupClips()
    {
        ReadCatalog();if(banks==null)yield break;
        long slice=System.Diagnostics.Stopwatch.GetTimestamp();
        foreach(var entry in banks.Values)foreach(var cue in Clips(entry))
        {
            if(string.IsNullOrEmpty(cue.key))continue;
            if(cue.clip==null)
            {
                var request=Resources.LoadAsync<AudioClip>(ResourceRoot+entry.family+"/"+cue.key);
                while(!request.isDone){yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
                cue.clip=request.asset as AudioClip;
            }
            if(cue.clip!=null&&cue.clip.loadState==AudioDataLoadState.Unloaded)cue.clip.LoadAudioData();
            while(cue.clip!=null&&cue.clip.loadState==AudioDataLoadState.Loading)
            {yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();}
            if((System.Diagnostics.Stopwatch.GetTimestamp()-slice)*1000d/System.Diagnostics.Stopwatch.Frequency<3d)continue;
            yield return null;slice=System.Diagnostics.Stopwatch.GetTimestamp();
        }
    }

    public void PlayPrelude(string family,bool enabled)
    {
        if(disposed)return;
        StopActiveTimeline();
        StopVoice(preludeSource);preludeFade=mainFade=-1f;
        var selected=Find(family);playAudio=enabled;
        preludeActive=false;
        if(selected==null)return;
        if(enabled&&Application.isPlaying)LoadBank(selected);
        if(StartVoice(preludeSource,selected.prelude))
        {preludeGain=preludeSource.volume;preludeActive=true;}
    }

    public void Begin(string family,bool enabled)
    {
        if(disposed)return;
        StopActiveTimeline();bank=Find(family);playAudio=enabled;
        preludeActive=false;
        if(bank==null)return;
        if(enabled&&Application.isPlaying)LoadBank(bank);
        source=mainVoices[nextVoice];nextVoice=(nextVoice+1)%mainVoices.Length;
        StopVoice(source);mainFade=-1f;
        bool blend=preludeSource.clip!=null;
        if(StartVoice(source,bank.timeline,blend))
        {
            if(blend)mainFade=CrossfadeSeconds;
        }
        if(blend)preludeFade=CrossfadeSeconds;
    }

    public void Tick(float elapsed)
    {
        if(bank==null||float.IsNaN(elapsed))return;
        // The continuous file already advances on the audio clock, including through stalled frames.
        if(elapsed>=bank.duration)Finish();
    }

    public void Finish()
    {
        bank=null;playAudio=false;preludeActive=false;
        // Both main voices keep their recorded endings while the battlefield resumes.
    }

    public void UpdateAudio(float unscaledDeltaTime)
    {
        if(disposed)return;
        float dt=Mathf.Max(0f,unscaledDeltaTime);
        if(preludeFade>=0f)
        {
            preludeFade=Mathf.Max(0f,preludeFade-dt);
            if(preludeSource!=null)preludeSource.volume=preludeGain*preludeFade/CrossfadeSeconds;
            if(preludeFade<=0f){StopVoice(preludeSource);preludeFade=-1f;}
        }
        if(mainFade>=0f)
        {
            mainFade=Mathf.Max(0f,mainFade-dt);
            if(source!=null)source.volume=mainGain*(1f-mainFade/CrossfadeSeconds);
            if(mainFade<=0f)mainFade=-1f;
        }
        if(focusHeld&&!AnyVoicePlaying())ReleaseFocus();
    }

    private bool StartVoice(AudioSource voice,Cue cue,bool fadeIn=false)
    {
        if(!playAudio||voice==null||cue?.clip==null||!(cue.gain>0f))return false;
        float gain=GameUserSettings.Load().effectsVolume*.65f*cue.gain;
        if(float.IsNaN(gain)||float.IsInfinity(gain)||gain<0f)return false;
        voice.clip=cue.clip;voice.pitch=1f;voice.volume=fadeIn?0f:gain;voice.timeSamples=0;
        if(fadeIn)mainGain=gain;
        if(!Application.isPlaying||!voice.isActiveAndEnabled||gain<=0f)return true;
        ClaimFocus();voice.Play();return true;
    }

    private void ClaimFocus()
    {
        if(current!=this)
        {
            // A former owner's finite tail may continue, but it must not later unduck the new film.
            if(current!=null)current.focusHeld=false;
            current=this;
        }
        if(!focusHeld){GameAudio.SetCinematicAudioFocus(true);focusHeld=true;}
    }

    private bool AnyVoicePlaying()
    {
        if(preludeSource!=null&&preludeSource.isPlaying)return true;
        foreach(var voice in mainVoices)if(voice!=null&&voice.isPlaying)return true;
        return false;
    }
    private void StopActiveTimeline()
    {
        if(bank!=null)StopVoice(source);
        bank=null;
    }
    private static void StopVoice(AudioSource voice)
    {if(voice!=null){voice.Stop();voice.clip=null;}}
    private void ReleaseFocus()
    {
        if(focusHeld&&current==this)GameAudio.SetCinematicAudioFocus(false);
        focusHeld=false;if(current==this)current=null;
    }
    public void Stop()
    {
        StopVoice(preludeSource);foreach(var voice in mainVoices)StopVoice(voice);
        bank=null;playAudio=false;preludeActive=false;preludeFade=mainFade=-1f;ReleaseFocus();
    }
    public void CancelPresentation()
    {
        StopActiveTimeline();
        if(preludeActive||preludeFade>=0f)StopVoice(preludeSource);
        playAudio=false;preludeActive=false;preludeFade=mainFade=-1f;
        if(!AnyVoicePlaying())ReleaseFocus();
    }
    public static void StopAll()
    {foreach(var audio in instances)audio.Stop();}
    public void Dispose()
    {
        if(disposed)return;
        Stop();disposed=true;instances.Remove(this);
        if(root==null)return;
        if(Application.isPlaying)Object.Destroy(root);else Object.DestroyImmediate(root);
    }
}
