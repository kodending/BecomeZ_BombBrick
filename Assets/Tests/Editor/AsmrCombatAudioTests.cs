using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BombBrick.Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AsmrCombatAudioTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    private const string Root = "Audio/Combat/Asmr/";
    private static readonly string[] Keys = {
        "hit_body", "hit_bite", "hit_slash", "hit_light", "hit_energy",
        "attack_swing", "attack_claw", "attack_throw", "attack_magic", "attack_organic", "critical_accent"
    };
    private GameObject host, voiceHost;
    private GameAudio audio;
    private AudioSource[] voices, layers;
    private object previousAudio, previousFrontend;
    private bool previousTestMode;
    private float previousScale;
    private readonly List<AudioClip> generated = new List<AudioClip>();

    [UnitySetUp]
    public IEnumerator Setup()
    {
        previousAudio = Field("instance").GetValue(null);
        previousFrontend = typeof(GameFrontend).GetField("instance", Hidden).GetValue(null);
        previousTestMode = CombatEffectTestConfiguration.Active;
        previousScale = Time.timeScale;
        CombatEffectTestConfiguration.Active = true;
        typeof(GameFrontend).GetField("instance", Hidden).SetValue(null, null);
        Time.timeScale = 1f;
        foreach (string key in Keys)
        {
            AudioClip clip = Resources.Load<AudioClip>(Root + key);
            Assert.That(clip, Is.Not.Null, key);
            clip.LoadAudioData();
            while (clip.loadState == AudioDataLoadState.Loading) yield return null;
            Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded), key);
        }
        host = new GameObject("Inactive ASMR combat audio fixture"); host.SetActive(false);
        voiceHost = new GameObject("ASMR combat audio fixture voices");
        audio = host.AddComponent<GameAudio>();
        Call("InitializeCombatAudio");
        Field("instance").SetValue(null, audio);
        Field("previewEnabled").SetValue(audio, true);
        voices = (AudioSource[])Field("combatVoices").GetValue(audio);
        layers = (AudioSource[])Field("combatLayers").GetValue(audio);
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = voiceHost.AddComponent<AudioSource>(); voices[i].playOnAwake = false;
            layers[i] = voiceHost.AddComponent<AudioSource>(); layers[i].playOnAwake = false;
        }
    }

    [TearDown]
    public void Cleanup()
    {
        if (host != null) Object.DestroyImmediate(host);
        if (voiceHost != null) Object.DestroyImmediate(voiceHost);
        foreach (AudioClip clip in generated) Object.DestroyImmediate(clip);
        generated.Clear();
        Field("instance").SetValue(null, previousAudio);
        typeof(GameFrontend).GetField("instance", Hidden).SetValue(null, previousFrontend);
        CombatEffectTestConfiguration.Active = previousTestMode;
        Time.timeScale = previousScale;
    }

    [TestCase("hit_body")]
    [TestCase("hit_bite")]
    [TestCase("hit_slash")]
    [TestCase("hit_light")]
    [TestCase("hit_energy")]
    [TestCase("attack_swing")]
    [TestCase("attack_claw")]
    [TestCase("attack_throw")]
    [TestCase("attack_magic")]
    [TestCase("attack_organic")]
    [TestCase("critical_accent")]
    public void IncomingClipsAreShortMonoPcmWithHeadroomAndPromptOnsets(string key)
    {
        AudioClip clip = Resources.Load<AudioClip>(Root + key);
        Assert.That(clip.channels, Is.EqualTo(1));
        Assert.That(clip.frequency, Is.EqualTo(48000));
        Assert.That(clip.length, Is.InRange(.02f, .6f));
        var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
        Assert.That(importer.forceToMono, Is.True);
        Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
        Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
        var data = new float[clip.samples];
        Assert.That(clip.GetData(data, 0), Is.True);
        Assert.That(data.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && Mathf.Abs(x) <= .66f), Is.True);
        Assert.That(data.Take(2400).Max(x => Mathf.Abs(x)), Is.GreaterThan(.001f), "The first 50 ms must contain the intended gesture.");
        Assert.That(Mathf.Abs(data[0]), Is.LessThan(.001f));
        Assert.That(Mathf.Abs(data[data.Length - 1]), Is.LessThan(.001f));
    }

    [TestCase(CombatSoundStyle.Heavy, "hit_body", "attack_swing")]
    [TestCase(CombatSoundStyle.Bite, "hit_bite", "attack_swing")]
    [TestCase(CombatSoundStyle.Slash, "hit_slash", "attack_claw")]
    [TestCase(CombatSoundStyle.Projectile, "hit_light", "attack_throw")]
    [TestCase(CombatSoundStyle.Arcane, "hit_energy", "attack_magic")]
    [TestCase(CombatSoundStyle.Spore, "hit_energy", "attack_organic")]
    [TestCase(CombatSoundStyle.Spirit, "hit_energy", "attack_magic")]
    public void InitializedPaletteRoutesEveryStyleToTheSharedContactAndRelease(
        CombatSoundStyle style, string hit, string attack)
    {
        Assert.That(((AudioClip[])Field("asmrHits").GetValue(audio))[(int)style], Is.SameAs(Resources.Load<AudioClip>(Root + hit)));
        Assert.That(((AudioClip[])Field("asmrAttacks").GetValue(audio))[(int)style], Is.SameAs(Resources.Load<AudioClip>(Root + attack)));
    }

    [Test]
    public void PublicBasicReleaseUsesItsQuietMotionInsteadOfTheOldImpact()
    {
        AudioClip release = Resources.Load<AudioClip>(Root + "attack_throw");
        Field("authoredBasicHit").SetValue(audio, release);
        string randomBefore = JsonUtility.ToJson(UnityEngine.Random.state);
        GameAudio.PlayCombat(CombatSoundStyle.Projectile, CombatSoundCue.Attack, 1, Vector3.zero);
        Assert.That(audio.PlayedCombatSounds, Is.EqualTo(1));
        Assert.That(voices[0].clip, Is.SameAs(release));
        Assert.That(((float[])Field("voiceGain").GetValue(audio))[0], Is.EqualTo(.055f).Within(.00001f));
        Assert.That(layers[0].isPlaying, Is.False);
        Assert.That(((int[])Field("voicePriority").GetValue(audio))[0], Is.EqualTo(-1));
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(randomBefore));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CriticalContactKeepsTheFamilyCoreAndAddsOneAccentWithoutAnotherEvent(bool critical)
    {
        AudioClip core = Resources.Load<AudioClip>(Root + "hit_slash");
        Field("authoredBasicHit").SetValue(audio, core);
        GameAudio.PlayCombat(CombatSoundStyle.Slash, CombatSoundCue.Hit, 2, Vector3.zero, critical: critical);
        Assert.That(audio.PlayedCombatSounds, Is.EqualTo(1));
        Assert.That(voices[0].clip, Is.SameAs(core));
        Assert.That(((float[])Field("voiceGain").GetValue(audio))[0], Is.EqualTo(critical ? .24f : .22f).Within(.00001f));
        if (critical)
        {
            Assert.That(layers[0].clip, Is.SameAs(Resources.Load<AudioClip>(Root + "critical_accent")));
            Assert.That(layers[0].volume, Is.EqualTo(voices[0].volume * .38f).Within(.00001f));
            Assert.That(layers[0].loop, Is.False);
        }
        else Assert.That(layers[0].isPlaying, Is.False);
        GameAudio.PlayCombat(CombatSoundStyle.Slash, CombatSoundCue.Hit, 2, Vector3.zero, critical: critical);
        Assert.That(audio.PlayedCombatSounds, Is.EqualTo(1), "One source's same-frame AoE contacts coalesce.");
    }

    [Test]
    public void AuthoredSkillContactKeepsItsOwnClipAndDoesNotAcquireAnUnrelatedBasicAccent()
    {
        AudioClip skill = Resources.Load<AudioClip>("Audio/Combat/Raptor/rank3_impact");
        Assert.That(skill, Is.Not.Null);
        GameAudio.PlayCombat(CombatSoundStyle.Bite, CombatSoundCue.Impact, 3, Vector3.zero, authoredSound: skill);
        Assert.That(audio.PlayedCombatSounds, Is.EqualTo(1));
        Assert.That(voices[0].clip, Is.SameAs(skill));
        Assert.That(layers[0].isPlaying, Is.False);
    }

    [Test]
    public void AGroupOfReleasesCannotFillTheNormalHitBudget()
    {
        for (int i = 0; i < 6; i++)
            GameAudio.PlayCombat((CombatSoundStyle)i, CombatSoundCue.Attack, 100 + i, Vector3.zero);
        int releases = audio.PlayedCombatSounds;
        Assert.That(releases, Is.EqualTo(2), "Release textures have their own small simultaneous budget.");
        for (int i = 0; i < 6; i++)
            GameAudio.PlayCombat(CombatSoundStyle.Heavy, CombatSoundCue.Hit, 200 + i, Vector3.zero);
        Assert.That(audio.PlayedCombatSounds, Is.EqualTo(releases + 6));
        Assert.That(audio.PlayedCount(CombatSoundStyle.Heavy, CombatSoundCue.Hit), Is.EqualTo(6));
    }

    [Test]
    public void AnActualHitCanReplaceAQuietReleaseWhenEveryVoiceIsOccupied()
    {
        AudioClip carrier = Carrier();
        var priorities = (int[])Field("voicePriority").GetValue(audio);
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i].clip = carrier; voices[i].Play();
            Assert.That(voices[i].isPlaying, Is.True);
            priorities[i] = i < 2 ? -1 : 2;
        }
        GameAudio.PlayCombat(CombatSoundStyle.Bite, CombatSoundCue.Hit, 400, Vector3.zero);
        Assert.That(audio.PlayedCombatSounds, Is.EqualTo(1));
        Assert.That(voices.Take(2).Count(v => v.clip == Resources.Load<AudioClip>(Root + "hit_bite")), Is.EqualTo(1));
        Assert.That(voices.Skip(2).All(v => v.clip == carrier), Is.True, "Skill voices keep priority over ordinary contacts.");
    }

    [Test]
    public void AnExplicitFinalImpactCanReplaceAnOrdinaryImpactInAFullPoolWithoutStealingADeathTail()
    {
        AudioClip carrier=Carrier();
        AudioClip finish=Resources.Load<AudioClip>("Audio/Combat/Native/wizard_mouse/meteor_finish")??
            Resources.Load<AudioClip>("Audio/Combat/WizardMouse/crash");
        Assert.That(finish,Is.Not.Null);
        var priorities=(int[])Field("voicePriority").GetValue(audio);
        var owners=(int[])Field("voiceOwner").GetValue(audio);
        var until=(float[])Field("voiceUntil").GetValue(audio);
        bool previousSkillAudio=CombatReadabilityPreview.SkillAudio;CombatReadabilityPreview.SkillAudio=true;
        try
        {
            for(int i=0;i<voices.Length;i++)
            {
                voices[i].clip=carrier;voices[i].Play();Assert.That(voices[i].isPlaying,Is.True);
                priorities[i]=2;owners[i]=600+i;until[i]=Time.unscaledTime+5f+i;
            }
            GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Impact,700,Vector3.zero,
                strength:1.4f,authoredSound:finish);
            Assert.That(audio.PlayedCombatSounds,Is.Zero,"Loudness alone must not increase priority.");
            Assert.That(audio.SuppressedCombatSounds,Is.EqualTo(1));
            GameAudio.PlayCombat(CombatSoundStyle.Heavy,CombatSoundCue.Death,701,Vector3.zero);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(1));
            int death=Array.IndexOf(owners,701);Assert.That(death,Is.GreaterThanOrEqualTo(0));
            AudioClip deathClip=voices[death].clip;Assert.That(priorities[death],Is.EqualTo(3));
            GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Impact,702,Vector3.zero,
                strength:.4f,authoredSound:finish,importantImpact:true);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(2),"The explicit low-volume finale still replaces one normal impact.");
            int final=Array.IndexOf(owners,702);Assert.That(final,Is.GreaterThanOrEqualTo(0).And.Not.EqualTo(death));
            Assert.That(priorities[final],Is.EqualTo(3));Assert.That(voices[final].clip,Is.SameAs(finish));
            Assert.That(voices.Count(v=>v.clip==carrier),Is.EqualTo(voices.Length-2));
            Assert.That(owners[death],Is.EqualTo(701));Assert.That(voices[death].clip,Is.SameAs(deathClip));
            GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Impact,703,Vector3.zero,authoredSound:finish);
            GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Impact,704,Vector3.zero,
                authoredSound:finish,loop:true,importantImpact:true);
            GameAudio.PlayCombat(CombatSoundStyle.Arcane,CombatSoundCue.Cast,705,Vector3.zero,
                authoredSound:finish,importantImpact:true);
            Assert.That(audio.PlayedCombatSounds,Is.EqualTo(2),"Ordinary impacts, loops and cast sounds retain their priorities.");
            Assert.That(owners[death],Is.EqualTo(701));Assert.That(owners[final],Is.EqualTo(702));
        }
        finally{CombatReadabilityPreview.SkillAudio=previousSkillAudio;}
    }

    [Test]
    public void APlayingAccentTailKeepsItsSlotAfterTheMainContactEnds()
    {
        AudioClip carrier = Carrier();
        var priorities = (int[])Field("voicePriority").GetValue(audio);
        for (int i = 0; i < voices.Length; i++)
        {
            priorities[i] = 2;
            if (i == 0) continue;
            voices[i].clip = carrier; voices[i].Play();
            Assert.That(voices[i].isPlaying, Is.True);
        }
        layers[0].clip = carrier; layers[0].Play();
        Assert.That(layers[0].isPlaying, Is.True);
        Assert.That(voices[0].isPlaying, Is.False);
        GameAudio.PlayCombat(CombatSoundStyle.Heavy, CombatSoundCue.Hit, 500, Vector3.zero);
        Assert.That(audio.PlayedCombatSounds, Is.Zero);
        Assert.That(layers[0].clip, Is.SameAs(carrier));
        Assert.That(layers[0].isPlaying, Is.True);
    }

    [UnityTest]
    public IEnumerator LoadingWarmupIncludesAndSeedsEveryNewPaletteClipBeforeCombat()
    {
        var catalog = JsonUtility.FromJson<GameAudio.PreparedCombatSounds>(
            Resources.Load<TextAsset>(GameAudio.PreparedCombatSoundsPath).text);
        foreach (string key in Keys)
            Assert.That(catalog.clips.Count(entry => entry.path == Root + key), Is.EqualTo(1), key);
        var warmup = GameAudio.WarmupCombatSounds();
        try { while (warmup.MoveNext()) yield return warmup.Current; }
        finally { (warmup as IDisposable)?.Dispose(); }
        var levels = (IDictionary)Field("combatLevels").GetValue(audio);
        foreach (string key in Keys)
            Assert.That(levels.Contains(Resources.Load<AudioClip>(Root + key)), Is.True, key);
        Assert.That(Field("combatAudioWarmupComplete").GetValue(audio), Is.EqualTo(true));
    }

    private AudioClip Carrier()
    {
        AudioClip clip = AudioClip.Create("ASMR pool occupancy fixture", 480000, 1, 48000, false);
        generated.Add(clip);
        return clip;
    }

    private static FieldInfo Field(string name) => typeof(GameAudio).GetField(name, Hidden);
    private object Call(string name, params object[] args) => typeof(GameAudio).GetMethod(name, Hidden).Invoke(audio, args);
}
