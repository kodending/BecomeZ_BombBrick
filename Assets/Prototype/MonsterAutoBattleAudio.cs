using BombBrick.Game.Core;
using UnityEngine;

public partial class MonsterAutoBattlePrototype
{
    private readonly AudioClip[,] raptorSkillSounds=new AudioClip[3,2];
    private enum SabertoothSound { Start,Bite,Drag,Reflux,Finish,DevourInhale,DevourSnap }
    private readonly AudioClip[] sabertoothSkillSounds=new AudioClip[7];
    private enum FrogSound { Launch,Hit,Pull,Steal,Slam,Branch }
    private readonly AudioClip[] frogSkillSounds=new AudioClip[6];
    private enum CrocodileSound { Bite,Spin,Crush,Throw,Collision,Sweep,Jaw,Resonance,AbyssEnter,AbyssRoll,AbyssSurge,AbyssCrush }
    private readonly AudioClip[] crocodileSkillSounds=new AudioClip[12];
    private enum KingKongSound { Swing,Impact,TripleFinish,Fissure,Charge,Overturn,Awaken,Pulse }
    private readonly AudioClip[] kingKongSkillSounds=new AudioClip[8];
    private enum WizardSound { Prepare,Launch,Hit,Split,Complete,Pulse,Descent,Crash,Constellation,Starshot,MeteorRelease,MeteorHit,MeteorFinish }
    private readonly AudioClip[] wizardSkillSounds=new AudioClip[13];
    private enum BearSound { Ignite,Swipe,Growth,MountainHit,Strike,Finisher,RoarStart,Transform,Fear }
    private readonly AudioClip[] bearSkillSounds=new AudioClip[9];
    private enum WolfSound { Dash,Hit,Rush,RushHit,Formation,SpectralHit,Companion,Alpha,AlphaHit,PackPass,PackHit }
    private readonly AudioClip[] wolfSkillSounds=new AudioClip[11];
    private enum NinjaSound { Blink,Log,Slash,Finisher,Sheathe,MoonBurst,Plant,Rush,Blast,Scrolls,Evade,SpacetimeCharge,SpacetimeCollapse }
    private readonly AudioClip[] ninjaSkillSounds=new AudioClip[13];
    private enum SpiderSound { Launch,Spread,Bind,Pull,NetBreak,Compress,CocoonBurst,Form,Pierce,Finish,Domain,Puppet }
    private readonly AudioClip[] spiderSkillSounds=new AudioClip[12];
    private enum TyrannoSound { Prepare,Stomp,Extinction,Spread,Step,MarchFinish,MawAppear,Bite,Warning,Reign }
    private readonly AudioClip[] tyrannoSkillSounds=new AudioClip[10];
    private enum MonkeySound { Throw,Hit,Finish,Bounce,Burst,Fall,Land,Spin,Pierce,Return,Summon,Dash }
    private readonly AudioClip[] monkeySkillSounds=new AudioClip[12];
    private enum MantisSound { Launch,Hit,Jump,Descent,Ground,Fan,Molt,Strike,Flight,Cocoon,Birth,Hatch }
    private readonly AudioClip[] mantisSkillSounds=new AudioClip[12];
    private enum MushroomSound { Launch,Spread,Resonance,Attach,Grow,Bloom,King,Breath,Transfer,Sprout,DeathBloom,DeathBreath }
    private readonly AudioClip[] mushroomSkillSounds=new AudioClip[12];
    private enum MammothSound { Prepare,Charge,Collision,Lift,Throw,Shatter,Wall,WallBreak,Share,Guard,Counter,Launch,Exit }
    private readonly AudioClip[] mammothSkillSounds=new AudioClip[13];
    private enum BeetleSound { Prepare,Flip,Land,Empower,Sweep,Jump,Dive,Crush,Lift,Hover,GravityLand,Warning,Overturn,OverturnLand }
    private readonly AudioClip[] beetleSkillSounds=new AudioClip[14];

    private enum SpiritDeerSound { Prepare,Spread,Bless,Return,Shield,Guard,Appear,Depart,Descend,Aegis,Block }
    private readonly AudioClip[] spiritDeerSkillSounds=new AudioClip[11];

    private enum GriffinSound { Rise,Dive,Land,TyphoonRise,TyphoonLand,Rush,Grab,Slam,Empower,Wing,Pierce,WindStart,Wind,Deflect,OrbAbsorb,OrbContract,OrbRelease }
    private readonly AudioClip[] griffinSkillSounds=new AudioClip[17];

    private enum WyvernSound { Prepare,Breath,Hit,Compress,Pressure,RainLaunch,RainFall,RainHit,Overheat,OverheatHit,Shadow,HeavenFall,HeavenHit }
    private readonly AudioClip[] wyvernSkillSounds=new AudioClip[13];

    private enum HaetaeSound { Prepare,Roar,Hit,Cleanse,Warning,Domain,Pulse,Compress,Bellow,Appear,Absorb,Swallow }
    private readonly AudioClip[] haetaeSkillSounds=new AudioClip[12];

    private void PlayHaetaeSound(Unit unit,HaetaeSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsHaetaeFamily(unit))return;
        AudioClip clip=haetaeSkillSounds[(int)sound];
        if(clip==null)clip=haetaeSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Haetae/"+sound.ToString().ToLowerInvariant());
        bool repeated=sound==HaetaeSound.Absorb||sound==HaetaeSound.Swallow;
        bool impact=sound==HaetaeSound.Hit||sound==HaetaeSound.Pulse||sound==HaetaeSound.Bellow;
        if(clip!=null)PlayCombatSound(unit,repeated?CombatSoundCue.Periodic:impact?CombatSoundCue.Impact:CombatSoundCue.Cast,
            position,strength,authoredSound:clip);
    }

    private void PlayWyvernSound(Unit unit,WyvernSound sound,Vector3 position,float strength=1f,bool loop=false)
    {
        if(mlEpisodeActive||!IsWyvernFamily(unit))return;
        AudioClip clip=wyvernSkillSounds[(int)sound];
        if(clip==null)clip=wyvernSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Wyvern/"+sound.ToString().ToLowerInvariant());
        bool impact=sound==WyvernSound.Hit||sound==WyvernSound.Pressure||sound==WyvernSound.RainHit||
            sound==WyvernSound.OverheatHit||sound==WyvernSound.HeavenHit;
        if(clip!=null)PlayCombatSound(unit,impact?CombatSoundCue.Impact:CombatSoundCue.Cast,
            position,strength,authoredSound:clip,loop:loop);
    }

    private void SetWyvernBreathSound(WyvernBreathCast cast,bool playing,bool finishCurrent=false)
    {
        if(cast.echo||cast.pressure||cast.rain||cast.sounding==playing)return;
        cast.sounding=playing;
        WyvernSound sound=cast.overheat?WyvernSound.Overheat:WyvernSound.Breath;
        if(playing)PlayWyvernSound(cast.source,sound,cast.origin,.8f,true);
        else GameAudio.StopCombatLoop(cast.source.id,wyvernSkillSounds[(int)sound],finishCurrent);
    }

    private void PlayGriffinSound(Unit unit,GriffinSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsGriffinFamily(unit))return;
        AudioClip clip=griffinSkillSounds[(int)sound];
        if(clip==null)
        {
            string key=sound==GriffinSound.OrbAbsorb?"orb_absorb":
                sound==GriffinSound.OrbContract?"orb_contract":sound==GriffinSound.OrbRelease?"orb_release":null;
            if(key!=null)clip=Resources.Load<AudioClip>("Audio/Combat/Native/griffin/"+key);
            GriffinSound fallback=sound==GriffinSound.OrbAbsorb?GriffinSound.Deflect:
                sound==GriffinSound.OrbContract?GriffinSound.WindStart:sound==GriffinSound.OrbRelease?GriffinSound.Wing:sound;
            if(clip==null)clip=Resources.Load<AudioClip>("Audio/Combat/Griffin/"+fallback.ToString().ToLowerInvariant());
            griffinSkillSounds[(int)sound]=clip;
        }
        bool impact=sound==GriffinSound.Land||sound==GriffinSound.TyphoonLand||
            sound==GriffinSound.Grab||sound==GriffinSound.Slam||sound==GriffinSound.Pierce||sound==GriffinSound.Deflect;
        if(clip!=null)PlayCombatSound(unit,impact?CombatSoundCue.Impact:CombatSoundCue.Cast,
            position,strength,authoredSound:clip,loop:sound==GriffinSound.Wind);
    }

    private void StopGriffinWindSound(Unit unit)=>
        GameAudio.StopCombatLoop(unit.id,griffinSkillSounds[(int)GriffinSound.Wind]);

    private void PlaySpiritDeerSound(Unit unit,SpiritDeerSound sound,Vector3 position,float strength=1f)
    {
        // Return, guard and immunity feedback belong to the protected ally, of any family.
        if(mlEpisodeActive||unit==null)return;
        AudioClip clip=spiritDeerSkillSounds[(int)sound];
        if(clip==null)clip=spiritDeerSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/SpiritDeer/"+sound.ToString().ToLowerInvariant());
        bool repeated=sound==SpiritDeerSound.Bless||sound==SpiritDeerSound.Return||
            sound==SpiritDeerSound.Shield||sound==SpiritDeerSound.Guard||sound==SpiritDeerSound.Block||
            sound==SpiritDeerSound.Appear||sound==SpiritDeerSound.Depart;
        if(clip!=null)PlayCombatSound(unit,repeated?CombatSoundCue.Periodic:CombatSoundCue.Cast,
            position,strength,authoredSound:clip);
    }

    private void PlayBeetleSound(Unit unit,BeetleSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsBeetleFamily(unit))return;
        AudioClip clip=beetleSkillSounds[(int)sound];
        if(clip==null)clip=beetleSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Beetle/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==BeetleSound.Land||sound==BeetleSound.Crush||
            sound==BeetleSound.GravityLand||sound==BeetleSound.OverturnLand?CombatSoundCue.Impact:CombatSoundCue.Cast;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,loop:sound==BeetleSound.Hover);
    }

    private void PlayMammothSound(Unit unit,MammothSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsMammothFamily(unit))return;
        AudioClip clip=mammothSkillSounds[(int)sound];
        if(clip==null)clip=mammothSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Mammoth/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==MammothSound.Collision||sound==MammothSound.Shatter||
            sound==MammothSound.WallBreak||sound==MammothSound.Guard||sound==MammothSound.Counter||
            sound==MammothSound.Launch?CombatSoundCue.Impact:CombatSoundCue.Cast;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip);
    }

    private void PlayMushroomSound(Unit unit,MushroomSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsMushroomFamily(unit))return;
        AudioClip clip=mushroomSkillSounds[(int)sound];
        if(clip==null)
        {
            string key=sound==MushroomSound.DeathBloom?"death_bloom":sound==MushroomSound.DeathBreath?"death_breath":null;
            if(key!=null)clip=Resources.Load<AudioClip>("Audio/Combat/Native/mushroom/"+key);
            MushroomSound fallback=sound==MushroomSound.DeathBloom?MushroomSound.Bloom:
                sound==MushroomSound.DeathBreath?MushroomSound.Breath:sound;
            if(clip==null)clip=Resources.Load<AudioClip>("Audio/Combat/Mushroom/"+fallback.ToString().ToLowerInvariant());
            mushroomSkillSounds[(int)sound]=clip;
        }
        CombatSoundCue cue=sound==MushroomSound.Resonance||sound==MushroomSound.Bloom||
            sound==MushroomSound.King||sound==MushroomSound.Sprout||sound==MushroomSound.DeathBloom?CombatSoundCue.Impact:
            sound==MushroomSound.DeathBreath?CombatSoundCue.Periodic:CombatSoundCue.Cast;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip);
    }

    private void PlayMantisSound(Unit unit,MantisSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsMantisFamily(unit))return;
        if(sound==MantisSound.Hit)
        {
            // A piercing fan and its brood share contact accents, leaving room for each launch.
            Unit owner=unit.mantisBroodOwner??unit;
            if(Time.unscaledTime-owner.mantisHitSoundAt<.055f)return;
            owner.mantisHitSoundAt=Time.unscaledTime;
        }
        AudioClip clip=mantisSkillSounds[(int)sound];
        if(clip==null)clip=mantisSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Mantis/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==MantisSound.Hit||sound==MantisSound.Ground||sound==MantisSound.Hatch
            ?CombatSoundCue.Impact:CombatSoundCue.Cast;
        if(unit.mantisBroodOwner!=null)strength*=.55f;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip);
    }

    private void PlayMonkeySound(Unit unit,MonkeySound sound,Vector3 position,float strength=1f
        ,bool secondary=false
    )
    {
        if(mlEpisodeActive||!IsMonkeyFamily(unit))return;
        AudioClip clip=monkeySkillSounds[(int)sound];
        if(clip==null)clip=monkeySkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Monkey/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==MonkeySound.Hit||sound==MonkeySound.Finish||
            sound==MonkeySound.Bounce||sound==MonkeySound.Burst||sound==MonkeySound.Land||
            sound==MonkeySound.Pierce?CombatSoundCue.Impact:CombatSoundCue.Cast;
        if(secondary&&CombatReadabilityPresentation.RefinedSkills&&(sound==MonkeySound.Throw||sound==MonkeySound.Hit))strength*=.75f;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip);
    }

    private void PlayTyrannoSound(Unit unit,TyrannoSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsTyrannoFamily(unit))return;
        AudioClip clip=tyrannoSkillSounds[(int)sound];
        if(clip==null)clip=tyrannoSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Tyranno/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==TyrannoSound.Prepare||sound==TyrannoSound.MawAppear||
            sound==TyrannoSound.Warning||sound==TyrannoSound.Spread?CombatSoundCue.Cast:CombatSoundCue.Impact;
        bool cinematic=rank4Presentation?.request.source==unit&&rank4Presentation.request.context.family=="tyranno";
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,allowDuringTimeStop:cinematic);
    }

    private void PlaySpiderSound(Unit unit,SpiderSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsSpiderFamily(unit))return;
        AudioClip clip=spiderSkillSounds[(int)sound];
        if(clip==null)clip=spiderSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Spider/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==SpiderSound.Bind||sound==SpiderSound.NetBreak||
            sound==SpiderSound.Compress||sound==SpiderSound.CocoonBurst||
            sound==SpiderSound.Pierce||sound==SpiderSound.Finish?CombatSoundCue.Impact:CombatSoundCue.Cast;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip);
    }

    private void PlayNinjaSound(Unit unit,NinjaSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsNinjaCatFamily(unit))return;
        AudioClip clip=ninjaSkillSounds[(int)sound];
        if(clip==null)clip=ninjaSkillSounds[(int)sound]=sound==NinjaSound.SpacetimeCharge
            ?Resources.Load<AudioClip>("Audio/Combat/Native/ninja_cat/spacetime_charge")??Resources.Load<AudioClip>("Audio/Combat/NinjaCat/scrolls")
            :sound==NinjaSound.SpacetimeCollapse
            ?Resources.Load<AudioClip>("Audio/Combat/Native/ninja_cat/spacetime_collapse")??Resources.Load<AudioClip>("Audio/Combat/NinjaCat/moonburst")
            :Resources.Load<AudioClip>("Audio/Combat/NinjaCat/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==NinjaSound.Slash||sound==NinjaSound.Finisher||
            sound==NinjaSound.MoonBurst||sound==NinjaSound.Blast||sound==NinjaSound.Evade||sound==NinjaSound.SpacetimeCollapse
                ?CombatSoundCue.Impact:CombatSoundCue.Cast;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip);
    }

    private void PlayWolfSound(Unit unit,WolfSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsWolfFamily(unit))return;
        AudioClip clip=wolfSkillSounds[(int)sound];
        if(clip==null)clip=wolfSkillSounds[(int)sound]=sound==WolfSound.PackPass
            ?Resources.Load<AudioClip>("Audio/Combat/Native/wolf/pack_pass")??Resources.Load<AudioClip>("Audio/Combat/Wolf/rush")
            :sound==WolfSound.PackHit
            ?Resources.Load<AudioClip>("Audio/Combat/Native/wolf/pack_hit")??Resources.Load<AudioClip>("Audio/Combat/Wolf/spectralhit")
            :Resources.Load<AudioClip>("Audio/Combat/Wolf/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==WolfSound.Hit||sound==WolfSound.RushHit||
            sound==WolfSound.SpectralHit||sound==WolfSound.AlphaHit||sound==WolfSound.PackHit?CombatSoundCue.Impact:CombatSoundCue.Cast;
        bool cinematic=wolfStampedeCinematic?.request.unit==unit;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,allowDuringTimeStop:cinematic);
    }

    private void PlayBearSound(Unit unit,BearSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsBearFamily(unit))return;
        AudioClip clip=bearSkillSounds[(int)sound];
        if(clip==null)clip=bearSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Bear/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==BearSound.MountainHit||sound==BearSound.Strike||
            sound==BearSound.Finisher||sound==BearSound.RoarStart||sound==BearSound.Fear
                ?CombatSoundCue.Impact:CombatSoundCue.Cast;
        bool cinematic=bearAvatarCinematic?.unit==unit&&sound!=BearSound.Fear;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,allowDuringTimeStop:cinematic);
    }

    private void PlayWizardSound(Unit unit,WizardSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsWizardMouseFamily(unit))return;
        AudioClip clip=wizardSkillSounds[(int)sound];
        if(clip==null)clip=wizardSkillSounds[(int)sound]=sound==WizardSound.MeteorRelease
            ?Resources.Load<AudioClip>("Audio/Combat/Native/wizard_mouse/meteor_release")??Resources.Load<AudioClip>("Audio/Combat/WizardMouse/descent")
            :sound==WizardSound.MeteorHit
            ?Resources.Load<AudioClip>("Audio/Combat/Native/wizard_mouse/meteor_hit")??Resources.Load<AudioClip>("Audio/Combat/WizardMouse/crash")
            :sound==WizardSound.MeteorFinish
            ?Resources.Load<AudioClip>("Audio/Combat/Native/wizard_mouse/meteor_finish")??Resources.Load<AudioClip>("Audio/Combat/WizardMouse/crash")
            :Resources.Load<AudioClip>("Audio/Combat/WizardMouse/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==WizardSound.Hit||sound==WizardSound.Pulse||sound==WizardSound.Crash||
            sound==WizardSound.MeteorHit||sound==WizardSound.MeteorFinish
            ?CombatSoundCue.Impact:CombatSoundCue.Cast;
        bool cinematic=wizardCygnusCinematic?.unit==unit&&
            (sound==WizardSound.Prepare||sound==WizardSound.Constellation||sound==WizardSound.Descent);
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,allowDuringTimeStop:cinematic,
            importantImpact:sound==WizardSound.MeteorFinish);
    }

    private void PlayKingKongSound(Unit unit,KingKongSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsKingKongFamily(unit))return;
        AudioClip clip=kingKongSkillSounds[(int)sound];
        if(clip==null)clip=kingKongSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/KingKong/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==KingKongSound.Pulse?CombatSoundCue.Periodic:
            sound==KingKongSound.Swing||sound==KingKongSound.Charge||sound==KingKongSound.Fissure
                ?CombatSoundCue.Cast:CombatSoundCue.Impact;
        bool cinematic=kingKongCinematic?.unit==unit&&
            (sound==KingKongSound.Charge||sound==KingKongSound.Impact||sound==KingKongSound.Awaken);
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,allowDuringTimeStop:cinematic);
    }

    private void PlayCrocodileSound(Unit unit,CrocodileSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsCrocodileFamily(unit))return;
        AudioClip clip=crocodileSkillSounds[(int)sound];
        if(clip==null)clip=crocodileSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Crocodile/"+CrocodileSoundResource(sound));
        // Resonance overlays the impact without competing for its same-frame gate.
        CombatSoundCue cue=sound==CrocodileSound.Resonance||sound==CrocodileSound.AbyssRoll?CombatSoundCue.Periodic:
            sound==CrocodileSound.Spin||sound==CrocodileSound.Throw||sound==CrocodileSound.AbyssEnter||
            sound==CrocodileSound.AbyssSurge?CombatSoundCue.Cast:CombatSoundCue.Impact;
        bool cinematic=crocodileAbyssCinematic!=null&&crocodileAbyssCinematic.unit==unit&&
            (sound==CrocodileSound.AbyssEnter||sound==CrocodileSound.AbyssRoll||sound==CrocodileSound.AbyssSurge);
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,allowDuringTimeStop:cinematic);
    }

    private static string CrocodileSoundResource(CrocodileSound sound)
    {
        switch(sound)
        {
            case CrocodileSound.AbyssEnter:return "abyss_enter";
            case CrocodileSound.AbyssRoll:return "abyss_roll";
            case CrocodileSound.AbyssSurge:return "abyss_surge";
            case CrocodileSound.AbyssCrush:return "abyss_crush";
            default:return sound.ToString().ToLowerInvariant();
        }
    }

    private void PlayFrogSound(Unit unit,FrogSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||unit==null||unit.definition==null||unit.definition.familyId!="frog")return;
        AudioClip clip=frogSkillSounds[(int)sound];
        if(clip==null)clip=frogSkillSounds[(int)sound]=Resources.Load<AudioClip>(
            "Audio/Combat/Frog/"+sound.ToString().ToLowerInvariant());
        // Separate theft from contact so both can sound on the same frame.
        CombatSoundCue cue=sound==FrogSound.Steal?CombatSoundCue.Periodic:
            sound==FrogSound.Hit||sound==FrogSound.Slam?CombatSoundCue.Impact:CombatSoundCue.Cast;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip);
    }

    private void PlaySabertoothSound(Unit unit,SabertoothSound sound,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsSabertoothFamily(unit))return;
        AudioClip clip=sabertoothSkillSounds[(int)sound];
        if(clip==null)clip=sabertoothSkillSounds[(int)sound]=sound==SabertoothSound.DevourInhale
            ?Resources.Load<AudioClip>("Audio/Combat/Native/sabertooth/devour_inhale")??Resources.Load<AudioClip>("Audio/Combat/Sabertooth/drag")
            :sound==SabertoothSound.DevourSnap
            ?Resources.Load<AudioClip>("Audio/Combat/Native/sabertooth/devour_snap")??Resources.Load<AudioClip>("Audio/Combat/Sabertooth/finish")
            :Resources.Load<AudioClip>("Audio/Combat/Sabertooth/"+sound.ToString().ToLowerInvariant());
        CombatSoundCue cue=sound==SabertoothSound.Reflux?CombatSoundCue.Periodic:
            sound==SabertoothSound.Start||sound==SabertoothSound.Drag||sound==SabertoothSound.DevourInhale?CombatSoundCue.Cast:CombatSoundCue.Impact;
        if(clip!=null)PlayCombatSound(unit,cue,position,strength,authoredSound:clip,loop:sound==SabertoothSound.Drag);
    }

    private void StopSabertoothDragSound(Unit unit)
    {
        if(unit==null)return;
        // Only the original ground drag loops; rank 4's air intake is a finite one-shot.
        GameAudio.StopCombatLoop(unit.id,sabertoothSkillSounds[(int)SabertoothSound.Drag],finishCurrent:true);
        unit.preyDragSoundStarted=false;
    }

    private void PlayRaptorSkillSound(Unit unit,bool impact,Vector3 position,float strength=1f)
    {
        if(mlEpisodeActive||!IsRaptorFamily(unit))return;
        int tier=unit.augmentTranscended?2:
            MonsterTraitRuntime.Has(unit.activeTraitIds,RaptorCombatRules.PiercingHuntTraitId)||
            MonsterTraitRuntime.Has(unit.activeTraitIds,RaptorCombatRules.RelentlessHuntTraitId)||
            MonsterTraitRuntime.Has(unit.activeTraitIds,RaptorCombatRules.ArmorHuntTraitId)?1:0;
        int cue=impact?1:0;
        AudioClip clip=raptorSkillSounds[tier,cue];
        if(clip==null)
        {
            string prefix=tier==2?"rank4":tier==1?"rank3":"basic";
            clip=raptorSkillSounds[tier,cue]=Resources.Load<AudioClip>(
                "Audio/Combat/Raptor/"+prefix+(impact?"_impact":"_start"));
        }
        if(clip!=null)PlayCombatSound(unit,impact?CombatSoundCue.Impact:CombatSoundCue.Cast,
            position,strength,authoredSound:clip);
    }

    public void ReadyCombatEffectTestSkills()
    {
        if (!CombatEffectTestConfiguration.Active || mlEpisodeActive || !battleStarted) return;
        foreach (Unit unit in units)
            if (unit.team == Team.Player && IsCombatActive(unit)) unit.skillTimer = 0f;
    }

    private void PlayCombatSound(Unit source, CombatSoundCue cue, Vector3 position,
        float strength = 1f, bool critical = false, bool leylinePulse = false,bool bearSpirit = false,AudioClip authoredSound = null,bool loop = false,
        bool allowDuringTimeStop=false,bool importantImpact=false)
    {
        // Training/headless collection never allocates or plays sound.
        if (mlEpisodeActive || battleEnding&&!(cue==CombatSoundCue.Death&&waveOutcomeStage==WaveOutcomeStage.Death) ||
            source == null || source.definition == null) return;
        if (CombatEffectTestConfiguration.Active &&
            CombatEffectTestSceneController.MuteEnemySounds && source.team != Team.Player) return;
        if(source.spiritConcertOwner!=null)strength*=.55f;
        GameAudio.PlayCombat(CombatSoundBank.StyleForCue(source.definition.familyId,
            IsRangedUnit(source), cue), cue, source.id, position, strength, critical, leylinePulse,bearSpirit,authoredSound,loop,allowDuringTimeStop,
            source.definition.familyId,importantImpact);
    }

    private void PlayDamageSound(Unit source, Unit target, bool basic, bool skill, float applied)
    {
        if (applied <= 0f || source == null || target == null || target.model == null) return;
        if (basic)
        {
            // These enhanced contacts already play one authored hit for the whole strike.
            if(IsBearFamily(source)&&(MountainColossusActive(source)||RagingHundredStrikesActive(source)))return;
            bool critical = source.lastBasicAttackCritical;
            PlayCombatSound(source, CombatSoundCue.Hit, target.model.transform.position,
                critical ? 1.25f : 1f, critical);
        }
        else if (skill && source.definition != null &&
            !CombatSoundBank.HasAuthoredImpact(source.definition.familyId))
            PlayCombatSound(source, CombatSoundCue.Impact, target.model.transform.position, .65f);
        // Authored families use their actual skill contact/effect events.
        // Poison, reflection and other passive damage do not spam contact sounds.
    }
}
