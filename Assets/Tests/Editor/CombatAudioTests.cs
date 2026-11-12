using System.Collections;
using System.Linq;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CombatAudioTests
{
    [TestCase(false)] [TestCase(true)]
    public void WyvernDeathCanReleaseItsCurrentLoopWithoutCuttingThePlayingClip(bool finishCurrent)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var singleton=typeof(GameAudio).GetField("instance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var previous=singleton.GetValue(null);
        var host=new GameObject("Wyvern sound release test");host.SetActive(false);
        var voiceHost=new GameObject("Active Wyvern sound release voice");
        var audio=host.AddComponent<GameAudio>();
        try
        {
            var voice=voiceHost.AddComponent<AudioSource>();
            var clip=Resources.Load<AudioClip>("Audio/Combat/Wyvern/breath");
            ((AudioSource[])typeof(GameAudio).GetField("combatVoices",flags).GetValue(audio))[0]=voice;
            ((int[])typeof(GameAudio).GetField("voiceOwner",flags).GetValue(audio))[0]=17;
            ((float[])typeof(GameAudio).GetField("voiceUntil",flags).GetValue(audio))[0]=float.PositiveInfinity;
            singleton.SetValue(null,audio);
            voice.clip=clip;voice.loop=true;voice.Play();voice.timeSamples=128;
            bool playing=voice.isPlaying;int position=voice.timeSamples;
            GameAudio.StopCombatLoop(17,clip,finishCurrent);
            Assert.That(voice.loop,Is.False);
            Assert.That(voice.isPlaying,Is.EqualTo(finishCurrent&&playing));
            if(finishCurrent)
            {
                Assert.That(voice.timeSamples,Is.GreaterThanOrEqualTo(position));
                Assert.That(((float[])typeof(GameAudio).GetField("voiceUntil",flags).GetValue(audio))[0],
                    Is.Not.EqualTo(float.PositiveInfinity));
            }
        }
        finally{singleton.SetValue(null,previous);Object.DestroyImmediate(host);Object.DestroyImmediate(voiceHost);}
    }

    [UnitySetUp]
    public IEnumerator LoadImportedCombatAudioData()
    {
        foreach(var clip in Resources.LoadAll<AudioClip>("Audio/Combat"))
        {
            clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded),clip.name);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void EndingBattlePreservesPlayingSoundsAndFinishesTheCurrentLoop(bool pause)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var singleton=typeof(GameAudio).GetField("instance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        var previous=singleton.GetValue(null);
        var host=new GameObject("Ended combat sound test");host.SetActive(false);
        var voiceHost=new GameObject("Active combat loop test voice");
        var audio=host.AddComponent<GameAudio>();
        try
        {
            typeof(GameAudio).GetMethod("InitializeCombatAudio",flags).Invoke(audio,null);
            typeof(GameAudio).GetField("effects",flags).SetValue(audio,host.AddComponent<AudioSource>());
            singleton.SetValue(null,audio);
            typeof(GameAudio).GetField("previewEnabled",flags).SetValue(audio,true);
            var voices=(AudioSource[])typeof(GameAudio).GetField("combatVoices",flags).GetValue(audio);
            var layers=(AudioSource[])typeof(GameAudio).GetField("combatLayers",flags).GetValue(audio);
            // The resumed-battle check may choose any free slot while the earlier tails still play.
            for(int i=0;i<voices.Length;i++)
            {
                voices[i]=voiceHost.AddComponent<AudioSource>();voices[i].playOnAwake=false;
                layers[i]=voiceHost.AddComponent<AudioSource>();layers[i].playOnAwake=false;
            }
            var clip=Resources.Load<AudioClip>("Audio/Combat/Wyvern/breath");
            foreach(var voice in new[]{voices[0],voices[1],layers[0]})
            {
                voice.clip=clip;voice.volume=.2f;voice.Play();voice.timeSamples=128;
            }
            voices[0].loop=true;
            audio.SetPaused(pause);
            var playing=new[]{voices[0].isPlaying,voices[1].isPlaying,layers[0].isPlaying};
            var positions=new[]{voices[0].timeSamples,voices[1].timeSamples,layers[0].timeSamples};
            GameAudio.SetBattlePhase(false);GameAudio.SetBattlePhase(false);
            var sources=new[]{voices[0],voices[1],layers[0]};
            for(int i=0;i<sources.Length;i++)
            {
                Assert.That(sources[i].loop,Is.False);
                Assert.That(sources[i].clip,Is.SameAs(clip));
                Assert.That(sources[i].isPlaying,Is.EqualTo(playing[i]));
                Assert.That(sources[i].timeSamples,Is.GreaterThanOrEqualTo(positions[i]));
                Assert.That(sources[i].volume,Is.EqualTo(.2f));
            }
            Assert.That(typeof(GameAudio).GetField("paused",flags).GetValue(audio),Is.EqualTo(pause));
            audio.SetPaused(false);
            Assert.That(voices.All(v=>!v.loop),Is.True);
            int played=audio.PlayedCombatSounds;
            GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Death,17,Vector3.zero);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(played),"No new combat sounds after the result.");
            GameAudio.SetBattlePhase(true);
            Assert.That(typeof(GameAudio).GetField("combatEnded",flags).GetValue(audio),Is.False);
            GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Death,17,Vector3.zero);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(played+1),"A new battle must accept combat sounds again.");
            audio.StopCombatSounds();
            Assert.That(voices.All(v=>!v.isPlaying),Is.True,"Explicit resets still stop playback.");
        }
        finally{singleton.SetValue(null,previous);Object.DestroyImmediate(host);Object.DestroyImmediate(voiceHost);}
    }

    [Test]
    public void CombatMixKeepsBasicReferenceBalancesStereoEnergyAndBoundsSparsePeaks()
    {
        var host=new GameObject("Combat mix level test");host.SetActive(false);
        var audio=host.AddComponent<GameAudio>();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var clips=new System.Collections.Generic.List<AudioClip>();
        System.Func<string,float[],int,AudioClip> create=(name,data,channels)=>
        {
            var clip=AudioClip.Create(name,data.Length/channels,channels,1000,false);
            clip.SetData(data,0);clips.Add(clip);return clip;
        };
        try
        {
            var reference=create("Reference",Enumerable.Repeat(.25f,200).ToArray(),1);
            typeof(GameAudio).GetField("authoredBasicHit",flags).SetValue(audio,reference);
            var match=typeof(GameAudio).GetMethod("MatchBasicHitLevel",flags);
            System.Func<AudioClip,float> gain=clip=>(float)match.Invoke(audio,new object[]{clip,.28f});
            Assert.That(gain(reference),Is.EqualTo(.28f));
            var quiet=create("Quiet stereo",Enumerable.Range(0,400).Select(i=>i%2==0?.125f:-.125f).ToArray(),2);
            var loud=create("Loud",Enumerable.Repeat(.5f,200).ToArray(),1);
            Assert.That(gain(quiet)*.125f,Is.EqualTo(.28f*.25f).Within(.00001f));
            Assert.That(gain(loud)*.5f,Is.EqualTo(.28f*.25f).Within(.00001f));
            var sparseData=new float[200];sparseData[90]=1f;
            Assert.That(gain(create("Sparse impulse",sparseData,1)),Is.LessThanOrEqualTo(.28f*.25f*1.35f+.00001f));
            Assert.That(gain(create("Silence",new float[200],1)),Is.EqualTo(.28f));
        }
        finally
        {
            Object.DestroyImmediate(host);
            foreach(var clip in clips)Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void TestProfileIsFreshIsolatedAndSkipsAllFirstTimeTutorials()
    {
        PlayerProfileSave a=CombatEffectTestConfiguration.CreateIsolatedProfile();
        PlayerProfileSave b=CombatEffectTestConfiguration.CreateIsolatedProfile();
        Assert.That(a.profileId,Is.EqualTo("combat_effect_test"));
        Assert.That(a.firstExpeditionGuideStep,Is.EqualTo(8));
        Assert.That(a.firstExpeditionCaptureGuideStep,Is.EqualTo(8));
        Assert.That(a.monsters,Is.Empty);
        Assert.That(a.monsters,Is.Not.SameAs(b.monsters));
    }

    [TestCase("raptor", CombatSoundStyle.Bite)]
    [TestCase("kingkong", CombatSoundStyle.Heavy)]
    [TestCase("mushroom", CombatSoundStyle.Spore)]
    [TestCase("spirit_deer", CombatSoundStyle.Spirit)]
    [TestCase("sabertooth", CombatSoundStyle.Slash)]
    [TestCase("wizard_mouse", CombatSoundStyle.Arcane)]
    [TestCase("monkey", CombatSoundStyle.Projectile)]
    public void FamilyUsesItsSoundIdentity(string family, CombatSoundStyle style)
        => Assert.That(CombatSoundBank.Style(family), Is.EqualTo(style));

    [Test]
    public void OnlyAuthoredFamiliesSuppressPerVictimSkillSounds()
    {
        foreach (string family in new[] { "raptor", "sabertooth", "frog", "crocodile", "kingkong", "mushroom", "spirit_deer", "wizard_mouse", "bear", "wolf", "ninja_cat", "arachne", "tyranno", "monkey", "mantis", "mammoth", "beetle", "griffin" })
            Assert.That(CombatSoundBank.HasAuthoredImpact(family), Is.True);
        Assert.That(CombatSoundBank.HasAuthoredImpact("wyvern"), Is.True);
        Assert.That(CombatSoundBank.HasAuthoredImpact("haetae"), Is.True);
        Assert.That(CombatSoundBank.HasAuthoredImpact("giraffe"), Is.False);
        Assert.That(CombatSoundBank.Style(null, true), Is.EqualTo(CombatSoundStyle.Projectile));
    }

    [TestCase("raptor", CombatSoundStyle.Bite, CombatSoundStyle.Bite)]
    [TestCase("tyranno", CombatSoundStyle.Bite, CombatSoundStyle.Bite)]
    [TestCase("kingkong", CombatSoundStyle.Heavy, CombatSoundStyle.Heavy)]
    [TestCase("bear", CombatSoundStyle.Heavy, CombatSoundStyle.Heavy)]
    [TestCase("mammoth", CombatSoundStyle.Heavy, CombatSoundStyle.Heavy)]
    [TestCase("beetle", CombatSoundStyle.Heavy, CombatSoundStyle.Heavy)]
    [TestCase("crocodile", CombatSoundStyle.Heavy, CombatSoundStyle.Heavy)]
    [TestCase("sabertooth", CombatSoundStyle.Slash, CombatSoundStyle.Slash)]
    [TestCase("wolf", CombatSoundStyle.Slash, CombatSoundStyle.Slash)]
    [TestCase("ninja_cat", CombatSoundStyle.Slash, CombatSoundStyle.Slash)]
    [TestCase("mantis", CombatSoundStyle.Slash, CombatSoundStyle.Slash)]
    [TestCase("griffin", CombatSoundStyle.Slash, CombatSoundStyle.Slash)]
    [TestCase("haetae", CombatSoundStyle.Slash, CombatSoundStyle.Slash)]
    [TestCase("frog", CombatSoundStyle.Projectile, CombatSoundStyle.Spore)]
    [TestCase("monkey", CombatSoundStyle.Projectile, CombatSoundStyle.Projectile)]
    [TestCase("arachne", CombatSoundStyle.Projectile, CombatSoundStyle.Spore)]
    [TestCase("wizard_mouse", CombatSoundStyle.Arcane, CombatSoundStyle.Arcane)]
    [TestCase("wyvern", CombatSoundStyle.Arcane, CombatSoundStyle.Arcane)]
    [TestCase("mushroom", CombatSoundStyle.Spore, CombatSoundStyle.Spore)]
    [TestCase("spirit_deer", CombatSoundStyle.Spirit, CombatSoundStyle.Spirit)]
    public void BasicSoundPaletteFollowsTheActualContactAndReleaseWithoutChangingSkillSounds(
        string family, CombatSoundStyle hit, CombatSoundStyle attack)
    {
        string randomBefore = JsonUtility.ToJson(Random.state);
        foreach(bool ranged in new[] { false, true })
        {
            Assert.That(CombatSoundBank.StyleForCue(family,ranged,CombatSoundCue.Attack), Is.EqualTo(attack));
            Assert.That(CombatSoundBank.StyleForCue(family,ranged,CombatSoundCue.Hit), Is.EqualTo(hit));
            Assert.That(CombatSoundBank.StyleForCue(family,ranged,CombatSoundCue.Death),
                Is.EqualTo(CombatSoundStyle.Heavy));
            foreach(var cue in new[] { CombatSoundCue.Cast, CombatSoundCue.Impact, CombatSoundCue.Periodic })
                Assert.That(CombatSoundBank.StyleForCue(family,ranged,cue),
                    Is.EqualTo(CombatSoundBank.Style(family,ranged)), "Skill and cinematic routing must keep its authored identity.");
        }
        Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(randomBefore));
    }

    [TestCase(false, CombatSoundStyle.Bite)]
    [TestCase(true, CombatSoundStyle.Projectile)]
    public void UnknownBasicFamiliesKeepTheirRangeFallback(bool ranged, CombatSoundStyle expected)
    {
        Assert.That(CombatSoundBank.StyleForCue(null,ranged,CombatSoundCue.Attack), Is.EqualTo(expected));
        Assert.That(CombatSoundBank.StyleForCue("unknown",ranged,CombatSoundCue.Hit), Is.EqualTo(expected));
        Assert.That(CombatSoundBank.Style("arachne"), Is.EqualTo(CombatSoundStyle.Projectile));
        Assert.That(CombatSoundBank.StyleForCue("spider",ranged,CombatSoundCue.Attack), Is.EqualTo(CombatSoundStyle.Spore));
    }

    [Test]
    public void SamplesAreFiniteShortNonSilentAndHaveSilentEdges()
    {
        foreach (CombatSoundStyle style in System.Enum.GetValues(typeof(CombatSoundStyle)))
        foreach (CombatSoundCue cue in System.Enum.GetValues(typeof(CombatSoundCue)))
        for (int variation=0; variation<CombatSoundBank.Variations; variation++)
        {
            float[] samples=CombatSoundBank.Samples(style,cue,variation);
            Assert.That(samples.Length, Is.InRange(1000, CombatSoundBank.SampleRate));
            Assert.That(samples.All(sample=>!float.IsNaN(sample)&&!float.IsInfinity(sample)&&
                Mathf.Abs(sample)<=.63f), Is.True, style+" / "+cue);
            Assert.That(samples.Max(sample=>Mathf.Abs(sample)), Is.GreaterThan(.02f));
            Assert.That(samples[0], Is.Zero);
            Assert.That(samples[samples.Length-1], Is.Zero);
        }
    }

    [Test]
    public void SoundsAndVariationsAreDistinctWithoutChangingGameplayRandom()
    {
        Random.State before=Random.state;
        string state=JsonUtility.ToJson(before);
        var signatures=new System.Collections.Generic.HashSet<string>();
        foreach (CombatSoundStyle style in System.Enum.GetValues(typeof(CombatSoundStyle)))
        foreach (CombatSoundCue cue in new[] {
            CombatSoundCue.Cast,CombatSoundCue.Impact,CombatSoundCue.Periodic })
        for(int v=0;v<CombatSoundBank.Variations;v++)
        {
            float[] data=CombatSoundBank.Samples(style,cue,v);
            string signature=string.Join(",",data.Skip(100).Take(20).Select(x=>x.ToString("R")));
            Assert.That(signatures.Add(signature),Is.True,style+" / "+cue+" / "+v);
        }
        foreach(CombatSoundStyle style in new[] { CombatSoundStyle.Slash,CombatSoundStyle.Projectile })
        for(int v=0;v<CombatSoundBank.Variations;v++)
        {
            float[] data=CombatSoundBank.Samples(style,CombatSoundCue.Attack,v);
            string signature=string.Join(",",data.Skip(100).Take(20).Select(x=>x.ToString("R")));
            Assert.That(signatures.Add(signature),Is.True,style+" / Attack / "+v);
        }
        foreach(CombatSoundCue cue in new[] { CombatSoundCue.Hit,CombatSoundCue.Death })
        for(int v=0;v<CombatSoundBank.Variations;v++)
        {
            float[] data=CombatSoundBank.Samples(CombatSoundStyle.Heavy,cue,v);
            string signature=string.Join(",",data.Skip(100).Take(20).Select(x=>x.ToString("R")));
            Assert.That(signatures.Add(signature),Is.True,cue+" / "+v);
        }
        Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(state));
    }

    [Test]
    public void AoeVictimsCoalesceButSubsequentPulsesAndOtherAttackersDoNot()
    {
        var gate=new CombatSoundGate();
        Assert.That(gate.TryAccept(7,CombatSoundCue.Impact,10,1f),Is.True);
        for(int i=0;i<8;i++)
            Assert.That(gate.TryAccept(7,CombatSoundCue.Impact,10,1f),Is.False);
        Assert.That(gate.TryAccept(8,CombatSoundCue.Impact,10,1f),Is.True);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Impact,11,1.2f),Is.True);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Hit,11,1.2f),Is.True);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Attack,11,1.2f),Is.True);
    }

    [Test]
    public void DifferentAuthoredPhasesCanOverlapButEachClipStillCoalesces()
    {
        var gate=new CombatSoundGate();
        Assert.That(gate.TryAccept(7,CombatSoundCue.Impact,10,1f,101),Is.True);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Impact,10,1f,102),Is.True);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Impact,10,1f,101),Is.False);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Periodic,10,1f,103),Is.True);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Periodic,10,1f,104),Is.True);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Periodic,11,1.2f,103),Is.False);
        Assert.That(gate.TryAccept(7,CombatSoundCue.Periodic,12,2f,103),Is.True);
    }

    [Test]
    public void PeriodicEffectsAreQuietlyRateLimitedAndNewBattleClearsGate()
    {
        var gate=new CombatSoundGate();
        Assert.That(gate.TryAccept(1,CombatSoundCue.Periodic,1,0f),Is.True);
        Assert.That(gate.TryAccept(1,CombatSoundCue.Periodic,2,.2f),Is.False);
        Assert.That(gate.TryAccept(1,CombatSoundCue.Periodic,3,.8f),Is.True);
        gate.Clear();
        Assert.That(gate.TryAccept(1,CombatSoundCue.Periodic,3,.8f),Is.True);
    }

    [Test]
    public void AudioClipIsMonoAndHasExpectedSampleRate()
    {
        AudioClip clip=CombatSoundBank.Create(CombatSoundStyle.Spore,CombatSoundCue.Impact,0);
        try
        {
            Assert.That(clip.channels,Is.EqualTo(1));
            Assert.That(clip.frequency,Is.EqualTo(CombatSoundBank.SampleRate));
            Assert.That(clip.loadState,Is.EqualTo(AudioDataLoadState.Loaded));
        }
        finally { Object.DestroyImmediate(clip); }
    }

    [Test]
    public void ProductionSampleLibraryCoversEveryRoutedCue()
    {
        foreach(CombatSoundStyle style in System.Enum.GetValues(typeof(CombatSoundStyle)))
        foreach(CombatSoundCue cue in System.Enum.GetValues(typeof(CombatSoundCue)))
        for(int variant=0;variant<CombatSoundBank.Variations;variant++)
            Assert.That(CombatSoundBank.Load(style,cue,variant),Is.Not.Null,
                style+" / "+cue+" / "+variant+" / "+
                CombatSoundBank.AssetKey(style,cue,variant,false));
    }

    [Test]
    public void EffectTestCannotActivateProductionFrontend()
    {
        bool before=CombatEffectTestConfiguration.Active;
        var fields=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var collapsed=typeof(CombatEffectTestSceneController).GetField("sidebarCollapsed",fields);
        var popup=typeof(CombatEffectTestSceneController).GetField("activePopupRect",fields);
        object previousCollapsed=collapsed.GetValue(null),previousPopup=popup.GetValue(null);
        try
        {
            collapsed.SetValue(null,false);popup.SetValue(null,new Rect());
            CombatEffectTestConfiguration.Active=true;
            Assert.That(GameFrontend.IsProduction,Is.False);
            Assert.That(GameFrontend.BlocksGameplay,Is.False);
            Rect sidebar=CombatEffectTestSceneController.TestSidebarRect(Screen.width,Screen.height);
            Assert.That(CombatEffectTestSceneController.PointerOverControls(sidebar.center),Is.True);
            Assert.That(CombatEffectTestSceneController.PointerOverControls(new Vector2(Screen.width*.5f,Screen.height*.5f)),Is.False);
        }
        finally
        {
            collapsed.SetValue(null,previousCollapsed);popup.SetValue(null,previousPopup);
            CombatEffectTestConfiguration.Active=before;
        }
    }

    [TestCase("basic_hit", .20f)]
    [TestCase("critical_hit", .27f)]
    [TestCase("death_pop", .34f)]
    public void SharedFeedbackImportsAsShortMonoWithoutClippingOrLeadingSilence(string name,float duration)
    {
        AudioClip clip=Resources.Load<AudioClip>("Audio/Combat/Feedback/"+name);
        Assert.That(clip,Is.Not.Null,name);
        Assert.That(clip.channels,Is.EqualTo(1));
        Assert.That(clip.frequency,Is.EqualTo(48000));
        Assert.That(clip.length,Is.EqualTo(duration).Within(.002f));
        clip.LoadAudioData();
        float[] data=new float[clip.samples];
        Assert.That(clip.GetData(data,0),Is.True);
        Assert.That(data.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Mathf.Abs(x)<.81f),Is.True);
        Assert.That(data.Take(480).Max(x=>Mathf.Abs(x)),Is.GreaterThan(.05f));
        Assert.That(Mathf.Abs(data[0]),Is.LessThan(.001f));
        Assert.That(Mathf.Abs(data[data.Length-1]),Is.LessThan(.001f));
    }

    [TestCase("Raptor")]
    [TestCase("Sabertooth")]
    [TestCase("Frog")]
    [TestCase("Crocodile")]
    [TestCase("KingKong")]
    [TestCase("WizardMouse")]
    [TestCase("Bear")]
    [TestCase("Wolf")]
    [TestCase("NinjaCat")]
    [TestCase("Spider")]
    [TestCase("Tyranno")]
    [TestCase("Monkey")]
    [TestCase("Mantis")]
    [TestCase("Mushroom")]
    [TestCase("Mammoth")]
    [TestCase("Beetle")]
    [TestCase("SpiritDeer")]
    [TestCase("Griffin")]
    [TestCase("Wyvern")]
    [TestCase("Haetae")]
    public void SelectedFamilyClipsLoadWithPromptAttacksAndShortCleanTails(string family)
    {
        string[] keys=family=="Raptor"
            ?new[]{"basic_start","basic_impact","rank3_start","rank3_impact","rank4_start","rank4_impact"}
            :family=="Sabertooth"?new[]{"start","bite","drag","reflux","finish"}
            :family=="Frog"?new[]{"launch","hit","pull","steal","slam","branch"}
            :family=="Crocodile"?new[]{"bite","spin","crush","throw","collision","sweep","jaw","resonance"}
            :family=="KingKong"?new[]{"swing","impact","triplefinish","fissure","charge","overturn","awaken","pulse"}
            :family=="Bear"?new[]{"ignite","swipe","growth","mountainhit","strike","finisher","roarstart","transform","fear"}
            :family=="Wolf"?new[]{"dash","hit","rush","rushhit","formation","spectralhit","companion","alpha","alphahit"}
            :family=="NinjaCat"?new[]{"blink","log","slash","finisher","sheathe","moonburst","plant","rush","blast","scrolls","evade"}
            :family=="Spider"?new[]{"launch","spread","bind","pull","netbreak","compress","cocoonburst","form","pierce","finish","domain","puppet"}
            :family=="Tyranno"?new[]{"prepare","stomp","extinction","spread","step","marchfinish","mawappear","bite","warning","reign"}
            :family=="Monkey"?new[]{"throw","hit","finish","bounce","burst","fall","land","spin","pierce","return","summon","dash"}
            :family=="Mantis"?new[]{"launch","hit","jump","descent","ground","fan","molt","strike","flight","cocoon","birth","hatch"}
            :family=="Mushroom"?new[]{"launch","spread","resonance","attach","grow","bloom","king","breath","transfer","sprout"}
            :family=="Mammoth"?new[]{"prepare","charge","collision","lift","throw","shatter","wall","wallbreak","share","guard","counter","launch","exit"}
            :family=="Beetle"?new[]{"prepare","flip","land","empower","sweep","jump","dive","crush","lift","hover","gravityland","warning","overturn","overturnland"}
            :family=="SpiritDeer"?new[]{"prepare","spread","bless","return","shield","guard","appear","depart","descend","aegis","block"}
            :family=="Griffin"?new[]{"rise","dive","land","typhoonrise","typhoonland","rush","grab","slam","empower","wing","pierce","windstart","wind","deflect"}
            :family=="Wyvern"?new[]{"prepare","breath","hit","compress","pressure","rainlaunch","rainfall","rainhit","overheat","overheathit","shadow","heavenfall","heavenhit"}
            :family=="Haetae"?new[]{"prepare","roar","hit","cleanse","warning","domain","pulse","compress","bellow","appear","absorb","swallow"}
            :new[]{"prepare","launch","hit","split","complete","pulse","descent","crash","constellation","starshot"};
        foreach(string key in keys)
        {
            AudioClip clip=Resources.Load<AudioClip>("Audio/Combat/"+family+"/"+key);
            Assert.That(clip,Is.Not.Null,key);
            Assert.That(clip.channels,Is.EqualTo(2),key);
            Assert.That(clip.frequency,Is.EqualTo(48000),key);
            Assert.That(clip.length,Is.InRange(.1f,family=="SpiritDeer"?2.5f:family=="Wyvern"?1.7f:family=="Tyranno"?1.5f:family=="Haetae"||family=="Mammoth"||family=="Beetle"?1.1f:.6f),key);
            float[] samples=new float[clip.samples*clip.channels];
            Assert.That(clip.GetData(samples,0),Is.True,key);
            float peakLimit=family=="Raptor"?.73f:.79f;
            Assert.That(samples.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Mathf.Abs(x)<peakLimit),Is.True,key);
            Assert.That(samples.Take(960).Max(x=>Mathf.Abs(x)),Is.GreaterThan(key=="drag"?.005f:.01f),key);
            Assert.That(samples.Take(2).Concat(samples.Skip(samples.Length-2)).All(x=>Mathf.Abs(x)<.001f),Is.True,key);
        }
    }
}
