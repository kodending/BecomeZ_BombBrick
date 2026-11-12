using System;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using BombBrick.Game.Core;
using UnityEngine;
using GUI=LocalizedGUI;

public static class Rank4CinematicCatalog
{
    public static readonly string[] Families={"spider","ninja_cat","tyranno","monkey","mantis","mushroom",
        "mammoth","beetle","spirit_deer","griffin","wyvern","haetae"};
    public static string Canonical(string family)=>family=="arachne"?"spider":family;
    public static bool Supports(string family)=>Array.IndexOf(Families,Canonical(family))>=0;
    public static Rank4CinematicStage Create(string family,Transform parent)
    {
        switch(Canonical(family))
        {
            case "spider":return new SpiderQueenRank4Stage(parent);
            case "ninja_cat":return new NinjaSubstitutionRank4Stage(parent);
            case "tyranno":return new TyrannoReignRank4Stage(parent);
            case "monkey":return new MonkeyCircusRank4Stage(parent);
            case "mantis":return new MantisDeathMoltRank4Stage(parent);
            case "mushroom":return new MushroomColonyRank4Stage(parent);
            case "mammoth":return new MammothExpulsionRank4Stage(parent);
            case "beetle":return new BeetleOverturnRank4Stage(parent);
            case "spirit_deer":return new SpiritGuardianRank4Stage(parent);
            case "griffin":return new GriffinCrosswindRank4Stage(parent);
            case "wyvern":return new WyvernHeavenRank4Stage(parent);
            case "haetae":return new HaetaeMandateRank4Stage(parent);
            default:return null;
        }
    }
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class Rank4PresentationRequest
    {
        public Unit source;
        public int generation,revision;
        public Rank4StageContext context;
    }
    private sealed class Rank4Presentation
    {
        public Rank4PresentationRequest request;
        public Rank4CinematicStage stage;
        public CombatSkillClock clock;
        public float elapsed,savedSpeed;
        public bool handoffStarted;
        public SpriteRenderer renderer;
        public SpriteRenderer[] sourceRenderers;
        public bool[] sourceRendererHidden;
        public bool sourceHidden,directorEnabled,environmentEnabled;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public float fieldOfView;
        public Vector2 lensShift;
        public Matrix4x4 projection;
        public Texture2D wyvernBattlefieldFrame;
        public Sprite wyvernBattlefieldSprite;
        public TyrannoGodFootstepsVisuals tyrannoFootsteps;
        public TyrannoGodImpactVisuals tyrannoImpact;
        public int tyrannoStepSounds;
        public GameObject tyrannoFoot;
        public bool tyrannoFootWasActive;
        public ParticleSystem[] tyrannoStepDust;
        public Rank4CinematicVisibility mantisVisibility,mushroomVisibility,mushroomBattleVisibility;
        public Rank4CinematicVisibility spiritGuardianVisibility,spiritOppositeGuardianVisibility,spiritBattleVisibility;
        public Rank4CinematicVisibility griffinBattleVisibility;
    }
    private readonly List<Rank4PresentationRequest> rank4PresentationQueue=new List<Rank4PresentationRequest>();
    private readonly HashSet<Unit> rank4PresentedUnits=new HashSet<Unit>();
    private readonly Dictionary<string,Rank4CinematicStage> rank4PresentationStages=new Dictionary<string,Rank4CinematicStage>();
    private readonly HashSet<string> rank4PresentationFailures=new HashSet<string>();
    private Rank4Presentation rank4Presentation;

    private bool IsNinjaSpacetimeCinematicPending(Unit source)=>
        rank4Presentation?.request.source==source&&rank4Presentation.request.context.family=="ninja_cat"||
        rank4PresentationQueue.Any(r=>r.source==source&&r.context.family=="ninja_cat"&&Rank4PresentationValid(r));

    private bool QueueRank4Cinematic(Unit source,IEnumerable<Unit> targets=null)
    {
        if(mlEpisodeActive||!battleStarted||battleEnding||source?.model==null||mainCamera==null||
           !HasFamilyTranscendence(source)||!Rank4CinematicCatalog.Supports(source.definition.familyId)||
           rank4PresentedUnits.Contains(source)||rank4PresentationQueue.Any(r=>r.source==source)||
           rank4Presentation?.request.source==source)return false;
        string family=Rank4CinematicCatalog.Canonical(source.definition.familyId);
        if(family=="mushroom"&&!IsMushroomDeathBloomActive(source))return false;
        if(rank4PresentationFailures.Contains(family))return false;
        SpriteRenderer renderer=Rank3Renderer(source);if(renderer?.sprite==null)return false;
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        var context=new Rank4StageContext{family=family,source=FrogCatchActor(source,height,true),enemy=source.team==Team.Enemy,
            battlefieldBackdrop=family=="ninja_cat"||family=="mushroom"||family=="beetle"?FrogCatchCurrentBackground():null,
            sceneryRegion=family=="haetae"||family=="beetle"||family=="griffin"?FrogCatchSceneryRegion():null};
        CaptureRank4ReturnBody(context,renderer);
        if(family=="griffin")CaptureGriffinStormOrbReturn(context,source);
        if(family=="mushroom"||family=="haetae"||family=="beetle"||family=="griffin")
        {context.entryPosition=context.returnPosition;context.entryHeight=context.returnHeight;}
        var selectedTargets=(targets??units.Where(u=>u.team!=source.team&&IsCombatTargetable(u)))
            .Where(u=>u?.model!=null&&Rank3Renderer(u)?.sprite!=null).Take(8).ToArray();
        context.targets=selectedTargets.Select(u=>FrogCatchActor(u,height,false)).ToArray();
        if(family=="beetle")context.beetleLiftAllowed=selectedTargets.Select(u=>!PreyDisplacementImmune(u)&&u.beetleOverturnedBy==null).ToArray();
        if(family=="mushroom"&&source.spriteAnimation?.DeathFrames?.Length>0)
        {
            var fallen=context.source;fallen.frames=(Sprite[])source.spriteAnimation.DeathFrames.Clone();context.source=fallen;
        }
        context.allies=units.Where(u=>u!=source&&u.team==source.team&&IsCombatActive(u)&&Rank3Renderer(u)?.sprite!=null)
            .Take(7).Select(u=>FrogCatchActor(u,height,false)).ToArray();
        var request=new Rank4PresentationRequest{source=source,generation=combatGeneration,
            revision=source.positionRevision,context=context};
        rank4PresentationQueue.Add(request);return true;
    }

    private void CaptureRank4ReturnBody(Rank4StageContext context,SpriteRenderer renderer)
    {
        Bounds body=PixelMantisReaper.VisibleBody(renderer.sprite);
        Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        // The billboard's local up differs from world up on the tilted battlefield camera.
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 corner=new Vector3(x==0?body.min.x:body.max.x,y==0?body.min.y:body.max.y,0f);
            if(renderer.flipX)corner.x=-corner.x;if(renderer.flipY)corner.y=-corner.y;
            Vector3 point=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(corner));
            min=Vector2.Min(min,point);max=Vector2.Max(max,point);
        }
        context.returnPosition=new Vector2(((min.x+max.x)*.5f-.5f)*16f,(min.y-.5f)*9f);
        context.returnHeight=(max.y-min.y)*9f;
    }

    private void CaptureGriffinStormOrbReturn(Rank4StageContext context,Unit source)
    {
        context.griffinOrbViewport=new Vector2(source.team==Team.Player?.55f:.76f,.85f);
        context.griffinOrbHeight=.11f;
        var effect=GriffinStormOrbEffect(source);
        if(effect==null||mainCamera==null)return;
        Vector3 center=effect.AnchorPosition,up=mainCamera.transform.up*(effect.WorldHeight*.5f);
        Vector3 projected=mainCamera.WorldToViewportPoint(center);
        context.griffinOrbViewport=new Vector2(projected.x,projected.y);
        context.griffinOrbHeight=Mathf.Abs(mainCamera.WorldToViewportPoint(center+up).y-mainCamera.WorldToViewportPoint(center-up).y);
    }

    private void UpdateGriffinStormOrbCinematicVisibility(Rank4Presentation presentation)
    {
        if(presentation.request.context.family!="griffin")return;
        if(presentation.elapsed<GriffinCrosswindRank4Stage.PrivateStageTime||presentation.elapsed>=GriffinCrosswindRank4Stage.FieldRevealTime)
        {presentation.griffinBattleVisibility?.Restore();return;}
        if(presentation.griffinBattleVisibility==null)presentation.griffinBattleVisibility=new Rank4CinematicVisibility();
        foreach(Unit unit in units)
        {
            // The source body's separate owner restores its prior renderer flags after the match return.
            if(unit!=presentation.request.source)presentation.griffinBattleVisibility.Track(unit.model);
            presentation.griffinBattleVisibility.Track(unit.ring);
        }
        presentation.griffinBattleVisibility.Hide();
    }

    private bool Rank4PresentationValid(Rank4PresentationRequest request)
    {
        if(request==null||request.generation!=combatGeneration||!battleStarted||battleEnding||
           request.source?.model==null||!HasFamilyTranscendence(request.source))return false;
        Unit source=request.source;
        // The death flower and released heaven breath outlive their original caster.
        if(request.context.family=="wyvern")return rank4Presentation?.request==request||
            wyvernHeavenBreaths.Any(c=>c.source==source&&c.generation==combatGeneration);
        if(request.context.family=="mushroom")return IsMushroomDeathBloomActive(source);
        if(request.context.family=="griffin")return IsGriffinStormOrbActive(source);
        if(request.context.family=="ninja_cat"&&rank4Presentation?.request==request&&rank4Presentation.handoffStarted)return true;
        if(!IsCombatActive(source)||source.hp<=0f)return false;
        bool active=rank4Presentation?.request==request;
        switch(request.context.family)
        {
            case "ninja_cat":return active||NinjaSpacetimeActive(source);
            case "monkey":return active||IsMonkeyStormActive(source);
            case "mammoth":
            {
                if(active&&rank4Presentation.handoffStarted)return true;
                var cast=source.mammothExpulsion;
                return cast!=null&&MammothExpulsionValid(source,cast)&&
                    units.Any(target=>MammothExpulsionTarget(source,target));
            }
            case "mantis":return source.mantisMolt!=null&&source.mantisMolt.generation==combatGeneration;
            case "beetle":return active&&rank4Presentation.handoffStarted||source.beetleOverturn!=null&&
                BeetleImperialHornValid(source.beetleOverturn)&&source.beetleOverturn.time<BeetleOverturnRules.End;
            case "tyranno":return active||FamilyTranscendenceStateFor(source,false)?.tyrannoStompTime>=0f;
            default:return source.positionRevision==request.revision;
        }
    }

    private bool LegacyRank4PresentationActive()=>wolfStampedeCinematic!=null||bearAvatarCinematic!=null||
        wizardCygnusCinematic!=null||kingKongCinematic!=null||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
        frogCatchCinematic!=null||frogCatchReturnRemaining>0f||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||raptorCinematic!=null;

    private bool BeginRank4Cinematic(Rank4PresentationRequest request)
    {
        if(rank4Presentation!=null||mainCamera==null||CombatSkillClock.Active||LegacyRank4PresentationActive()||Time.timeScale<=0f||
           paused||GameFrontend.BlocksGameplay||!Rank4PresentationValid(request))return false;
        Unit source=request.source;var renderer=Rank3Renderer(source);if(renderer?.sprite==null)return false;
        string family=request.context.family;
        Rank4Presentation prepared=null;
        try
        {
            if(!rank4PresentationStages.TryGetValue(family,out Rank4CinematicStage stage))
            {
                stage=Rank4CinematicCatalog.Create(family,transform);
                if(stage==null)return false;
                rank4PresentationStages[family]=stage;
            }
            EndAllyPreparationSequence();EndSkillCameraPresentation();combatCameraDirector?.PrepareForCinematic();
            // Match the settled battlefield, after common preparation scaling and camera motion end.
            CaptureRank4ReturnBody(request.context,renderer);
            if(family=="mushroom"||family=="wyvern"||family=="haetae"||family=="beetle"||family=="griffin")
            {
                request.context.entryPosition=request.context.returnPosition;request.context.entryHeight=request.context.returnHeight;
                var flower=family=="mushroom"?MushroomDeathBloomBodyRenderer(source):null;
                if(flower!=null)CaptureRank4ReturnBody(request.context,flower);
            }
            if(family=="spirit_deer")CaptureSpiritGuardianReturn(request.context,source);
            if(family=="haetae")CaptureHaetaeGuardianReturn(request.context,source);
            if(family=="griffin")CaptureGriffinStormOrbReturn(request.context,source);
            request.context.battlefieldBounds=combatBounds;
            if(family=="tyranno")
            {
                var nativeFoot=FamilyTranscendenceStateFor(source,false)?.tyrannoStomp;
                request.context.hasTyrannoFootprint=nativeFoot!=null;
                if(nativeFoot!=null)request.context.tyrannoFootprintViewport=nativeFoot.ContactViewportRect(mainCamera);
            }
            request.context.returnSprite=renderer.sprite;
            request.context.returnBodyVisible=!renderer.forceRenderingOff&&renderer.enabled&&IsCombatActive(source)&&source.hp>0f;
            var state=new Rank4Presentation{request=request,stage=stage,clock=new CombatSkillClock(source.model.transform),
                savedSpeed=Time.timeScale,renderer=renderer,sourceHidden=renderer.forceRenderingOff,
                directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled,
                environmentEnabled=layeredBattleEnvironment!=null&&layeredBattleEnvironment.enabled,
                cameraPosition=mainCamera.transform.position,cameraRotation=mainCamera.transform.rotation,
                fieldOfView=mainCamera.fieldOfView,lensShift=mainCamera.lensShift,projection=mainCamera.projectionMatrix};
            state.sourceRenderers=source.model.GetComponentsInChildren<SpriteRenderer>(true);
            state.sourceRendererHidden=state.sourceRenderers.Select(r=>r.forceRenderingOff).ToArray();
            prepared=state;
            if(family=="wyvern")CaptureWyvernCinematicBattlefield(state);
            if(!stage.Begin(request.context)){ReleaseWyvernCinematicBattlefield(state);return false;}
            rank4Presentation=state;rank4PresentedUnits.Add(source);
            if(family=="tyranno")
            {
                state.tyrannoStepDust=new ParticleSystem[TyrannoGodFootstepsRules.StepCount];
                state.tyrannoFootsteps=new TyrannoGodFootstepsVisuals(units
                    .Where(u=>u.team!=source.team&&IsCombatTargetable(u)&&Rank3Renderer(u)!=null&&
                        Rank3Renderer(u).transform!=u.model.transform)
                    .Select(u=>Rank3Renderer(u).transform).Distinct().ToArray());
                var stomp=FamilyTranscendenceStateFor(source,false)?.tyrannoStomp;
                state.tyrannoFoot=stomp==null?null:stomp.gameObject;
                if(state.tyrannoFoot!=null)
                {state.tyrannoFootWasActive=state.tyrannoFoot.activeSelf;state.tyrannoFoot.SetActive(false);}
            }
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            if(layeredBattleEnvironment!=null)layeredBattleEnvironment.enabled=false;
            SetRank4SourceHidden(state,family!="tyranno"&&family!="monkey");
            UpdateMantisCinematicVisibility(state);
            UpdateMushroomCinematicVisibility(state);
            UpdateSpiritGuardianCinematicVisibility(state);
            UpdateGriffinStormOrbCinematicVisibility(state);
            state.clock.Hold();Time.timeScale=0f;
            BeginFamilyRank4CinematicAudio(source,family);return true;
        }
        catch(Exception exception)
        {
            CancelFamilyRank4CinematicAudio();FinishRank4Cinematic();rank4PresentationFailures.Add(family);
            ReleaseWyvernCinematicBattlefield(prepared);
            if(rank4PresentationStages.TryGetValue(family,out Rank4CinematicStage failedStage))failedStage.Hide();
            Debug.LogWarning("Rank-4 presentation unavailable for "+family+"; existing skill continues. "+exception.Message);
            return false;
        }
    }

    private bool UpdateRank4Cinematic()
    {
        UpdateHaetaeCinematicPresentation();
        var state=rank4Presentation;
        if(state==null)
        {
            if(paused||GameFrontend.BlocksGameplay||CombatSkillClock.Active||LegacyRank4PresentationActive()||Time.timeScale<=0f)return false;
            int count=rank4PresentationQueue.Count;
            while(count-->0)
            {
                var request=rank4PresentationQueue[0];rank4PresentationQueue.RemoveAt(0);
                if(!Rank4PresentationValid(request))
                {if(request.context.family=="griffin")RevealGriffinStormOrb(request.source);continue;}
                if(TryBeginRank4SkillPrelude(request.source,request))
                {rank4PresentationQueue.Insert(0,request);return true;}
                if(BeginRank4Cinematic(request))return true;
                if(request.context.family=="griffin")RevealGriffinStormOrb(request.source);
            }
            return false;
        }
        if(!Rank4PresentationValid(state.request)||paused||GameFrontend.BlocksGameplay||mainCamera==null||Time.timeScale!=0f)
        {CancelFamilyRank4CinematicAudio();FinishRank4Cinematic();return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelFamilyRank4CinematicAudio();FinishRank4Cinematic();paused=true;return true;}
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime);state.elapsed+=dt;
        try
        {
            TickFamilyRank4CinematicAudio(state.elapsed);
            UpdateTyrannoGodFootsteps(state);
            if(state.request.context.family=="monkey")
                SetRank4SourceHidden(state,state.elapsed>=MonkeyCircusRank4Stage.PreludeEnd&&state.elapsed<MonkeyCircusRank4Stage.FieldReturnTime);
            state.request.context.returnSprite=state.renderer==null?null:state.renderer.sprite;
            state.request.context.returnBodyVisible=state.renderer!=null&&!state.sourceHidden&&state.renderer.enabled&&
                IsCombatActive(state.request.source)&&state.request.source.hp>0f;
            UpdateMantisCinematicVisibility(state);
            UpdateMushroomCinematicVisibility(state);
            UpdateSpiritGuardianCinematicVisibility(state);
            UpdateGriffinStormOrbCinematicVisibility(state);
            UpdateHaetaeCinematicPresentation();
            bool ninjaReturn=state.request.context.family=="ninja_cat"&&state.elapsed>=NinjaSubstitutionRank4Stage.FieldReturnTime;
            bool tyrannoReturn=state.request.context.family=="tyranno"&&state.elapsed>=TyrannoReignRank4Stage.FieldReturnTime;
            bool monkeyReturn=state.request.context.family=="monkey"&&state.elapsed>=MonkeyCircusRank4Stage.FieldReturnTime;
            bool mantisReturn=state.request.context.family=="mantis"&&state.elapsed>=MantisDeathMoltRank4Stage.FieldReturnTime;
            bool mushroomReturn=state.request.context.family=="mushroom"&&state.elapsed>=MushroomColonyRank4Stage.FieldReturnTime;
            bool spiritReturn=state.request.context.family=="spirit_deer"&&state.elapsed>=SpiritGuardianRank4Stage.FieldRevealTime;
            bool mammothReturn=state.request.context.family=="mammoth"&&state.elapsed>=MammothExpulsionRank4Stage.FieldReturnTime;
            bool haetaeReturn=state.request.context.family=="haetae"&&state.elapsed>=HaetaeMandateRank4Stage.FieldReturnTime;
            bool beetleReturn=state.request.context.family=="beetle"&&state.elapsed>=BeetleOverturnRank4Stage.FieldReturnTime;
            bool griffinReturn=state.request.context.family=="griffin"&&state.elapsed>=GriffinCrosswindRank4Stage.FieldReturnTime;
            if(ninjaReturn||tyrannoReturn||monkeyReturn||spiritReturn||haetaeReturn||beetleReturn||griffinReturn||state.request.context.family=="mushroom")
                state.request.context.returnBodyVisible=false;
            state.stage.Tick(state.elapsed,dt);
            if(ninjaReturn||tyrannoReturn||monkeyReturn||mantisReturn||mushroomReturn||spiritReturn||haetaeReturn||beetleReturn||griffinReturn)
            {
                SetRank4SourceHidden(state,false);
                if(mushroomReturn&&state.request.source.hp<=0f)state.request.source.model.SetActive(false);
                if(combatCameraDirector!=null&&!combatCameraDirector.enabled&&state.directorEnabled&&
                   (ninjaReturn||monkeyReturn||mantisReturn||mushroomReturn||state.elapsed>=TyrannoReignRank4Stage.ImpactTime))
                {combatCameraDirector.enabled=true;if(battleStarted&&!battleEnding)combatCameraDirector.BeginBattle();}
            }
            if(state.request.context.family!="mantis"&&state.request.context.family!="mammoth"&&state.request.context.family!="mushroom"&&state.elapsed>state.stage.Duration*.5f&&state.stage.Opacity<.999f&&state.renderer!=null)
                SetRank4SourceHidden(state,false);
            AdvanceRank4CinematicHandoff(state);
            if(rank4Presentation!=state)return false;
            if(griffinReturn){FinishRank4Cinematic();return false;}
            if(mammothReturn){FinishRank4Cinematic();return false;}
            if(state.request.context.family=="tyranno"&&state.elapsed>=TyrannoReignRank4Stage.FilmImpactTime)
            {
                if(state.tyrannoImpact==null)
                    state.tyrannoImpact=new TyrannoGodImpactVisuals(units
                        .Where(u=>u.team!=state.request.source.team&&!u.inStorage&&u.model!=null&&u.model.activeInHierarchy&&Rank3Renderer(u)!=null&&
                            Rank3Renderer(u).transform!=u.model.transform)
                        .Select(u=>Rank3Renderer(u).transform).Distinct().ToArray());
                state.tyrannoImpact.Pose(state.elapsed-TyrannoReignRank4Stage.FilmImpactTime);
            }
        }
        catch(Exception exception)
        {
            CancelFamilyRank4CinematicAudio();FinishRank4Cinematic();rank4PresentationFailures.Add(state.request.context.family);
            Debug.LogWarning("Rank-4 presentation stopped; existing skill continues. "+exception.Message);return false;
        }
        if(state.elapsed<state.stage.Duration)return true;
        FinishRank4Cinematic();return false;
    }

    private void CaptureWyvernCinematicBattlefield(Rank4Presentation state)
    {
        var source=state.request.source;
        Vector2 min=Vector2.one,max=Vector2.zero;
        for(int x=0;x<2;x++)for(int z=0;z<2;z++)
        {
            Vector3 point=mainCamera.WorldToViewportPoint(new Vector3(x==0?combatBounds.min.x:combatBounds.max.x,
                combatBounds.center.y,z==0?combatBounds.min.z:combatBounds.max.z));
            min=Vector2.Min(min,point);max=Vector2.Max(max,point);
        }
        state.request.context.wyvernBattlefieldViewportRect=Rect.MinMaxRect(Mathf.Clamp01(min.x-.025f),
            Mathf.Clamp01(min.y-.025f),Mathf.Clamp01(max.x+.025f),Mathf.Clamp01(max.y+.025f));
        Renderer[] hidden=source.model.GetComponentsInChildren<Renderer>(true)
            .Concat(localSkillPresentations.Where(p=>p.active&&p.unit==source&&p.mirror!=null)
                .SelectMany(p=>p.mirror.GetComponentsInChildren<Renderer>(true)))
            .Concat(wyvernHeavenBreaths.Where(c=>c.source==source&&c.effect!=null)
                .SelectMany(c=>c.effect.GetComponentsInChildren<Renderer>(true))).Distinct().ToArray();
        bool[] previousHidden=hidden.Select(r=>r.forceRenderingOff).ToArray();
        RenderTexture previousTarget=mainCamera.targetTexture,previousActive=RenderTexture.active;
        RenderTexture target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);
        try
        {
            foreach(Renderer renderer in hidden)renderer.forceRenderingOff=true;
            mainCamera.targetTexture=target;mainCamera.Render();RenderTexture.active=target;
            var frame=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false)
                {name="Wyvern actual battlefield",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            state.wyvernBattlefieldFrame=frame;
            frame.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);frame.Apply(false,false);
            state.wyvernBattlefieldSprite=Sprite.Create(frame,new Rect(0,0,frame.width,frame.height),
                new Vector2(.5f,.5f),64f,0,SpriteMeshType.FullRect);
            state.wyvernBattlefieldSprite.name="Wyvern captured map and combatants";
            state.request.context.wyvernBattlefieldSprite=state.wyvernBattlefieldSprite;
        }
        finally
        {
            mainCamera.targetTexture=previousTarget;RenderTexture.active=previousActive;
            for(int i=0;i<hidden.Length;i++)if(hidden[i]!=null)hidden[i].forceRenderingOff=previousHidden[i];
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void ReleaseWyvernCinematicBattlefield(Rank4Presentation state)
    {
        if(state==null)return;
        if(state.request.context.wyvernBattlefieldSprite==state.wyvernBattlefieldSprite)
            state.request.context.wyvernBattlefieldSprite=null;
        if(state.wyvernBattlefieldSprite!=null)
        {if(Application.isPlaying)Destroy(state.wyvernBattlefieldSprite);else DestroyImmediate(state.wyvernBattlefieldSprite);}
        if(state.wyvernBattlefieldFrame!=null)
        {if(Application.isPlaying)Destroy(state.wyvernBattlefieldFrame);else DestroyImmediate(state.wyvernBattlefieldFrame);}
        state.wyvernBattlefieldSprite=null;state.wyvernBattlefieldFrame=null;
    }

    private void FinishRank4Cinematic()
    {
        var state=rank4Presentation;if(state==null)return;
        rank4Presentation=null;
        bool closed=false;
        try{state.stage.Hide();closed=true;}
        finally
        {
            if(closed)EndFamilyRank4CinematicAudio();else CancelFamilyRank4CinematicAudio();
            ReleaseWyvernCinematicBattlefield(state);
            state.tyrannoFootsteps?.Dispose();state.tyrannoImpact?.Dispose();state.clock.End();
            state.mantisVisibility?.Restore();
            state.mushroomVisibility?.Restore();
            state.mushroomBattleVisibility?.Restore();
            state.spiritGuardianVisibility?.Restore();
            state.spiritOppositeGuardianVisibility?.Restore();
            state.spiritBattleVisibility?.Restore();
            state.griffinBattleVisibility?.Restore();
            ClearTyrannoFootstepDust(state);
            if(state.tyrannoFoot!=null)state.tyrannoFoot.SetActive(state.tyrannoFootWasActive);
            SetRank4SourceHidden(state,false);
            if(state.request.context.family=="mushroom"&&state.request.source.hp<=0f&&state.request.source.model!=null)
                state.request.source.model.SetActive(false);
            if(Time.timeScale==0f)Time.timeScale=state.savedSpeed;
            if(layeredBattleEnvironment!=null)layeredBattleEnvironment.enabled=state.environmentEnabled;
            if(mainCamera!=null)
            {
                mainCamera.transform.SetPositionAndRotation(state.cameraPosition,state.cameraRotation);
                mainCamera.fieldOfView=state.fieldOfView;mainCamera.lensShift=state.lensShift;mainCamera.projectionMatrix=state.projection;
            }
            if(combatCameraDirector!=null)
            {combatCameraDirector.enabled=state.directorEnabled;if(state.directorEnabled&&battleStarted&&!battleEnding)combatCameraDirector.BeginBattle();}
            if(state.request.context.family=="monkey"&&IsMonkeyStormPending(state.request.source))ReleaseMonkeyStorm(state.request.source);
            if(state.request.context.family=="mushroom"&&IsMushroomDeathBloomPending(state.request.source))ReleaseMushroomDeathBloom(state.request.source);
            if(state.request.context.family=="griffin"&&!state.handoffStarted)RevealGriffinStormOrb(state.request.source);
            UpdateHaetaeCinematicPresentation();
        }
    }

    private void UpdateTyrannoGodFootsteps(Rank4Presentation state)
    {
        if(state.tyrannoFootsteps==null)return;
        state.tyrannoFootsteps.Pose(state.elapsed);
        if(state.tyrannoFoot!=null)
            state.tyrannoFoot.SetActive(state.tyrannoFootWasActive&&state.elapsed>=TyrannoReignRank4Stage.FieldReturnTime);
        bool prelude=state.elapsed<TyrannoGodFootstepsRules.FieldPreludeEndTime;
        if(!prelude)ClearTyrannoFootstepDust(state);
        if(state.elapsed<TyrannoReignRank4Stage.FieldReturnTime)SetRank4SourceHidden(state,!prelude);
        if(state.elapsed<TyrannoReignRank4Stage.ImpactTime)
        {
            Vector2 jolt=prelude?TyrannoGodFootstepsRules.CameraJoltAt(state.elapsed):Vector2.zero;
            if(CombatFlashPresentation.Reduced)jolt*=.45f;
            mainCamera.transform.position=state.cameraPosition+state.cameraRotation*new Vector3(jolt.x,jolt.y,0f);
        }
        int count=TyrannoGodFootstepsRules.CompletedStepsAt(state.elapsed);
        while(state.tyrannoStepSounds<count)
        {
            int step=state.tyrannoStepSounds++;
            if(prelude)PlayTyrannoFootstepDust(state,step);
        }
    }

    private void PlayTyrannoFootstepDust(Rank4Presentation state,int step)
    {
        float inward=state.request.source.team==Team.Player?1f:-1f;
        Vector3 edge=new Vector3(inward>0f?combatBounds.min.x+.24f:combatBounds.max.x-.24f,.08f,combatBounds.center.z);
        var dust=PixelCombatEffect.PlayTyrannoTremorImpact(edge,.16f,state.renderer,false);
        state.tyrannoStepDust[step]=dust;dust.name="Approaching tyrant footstep dust "+step;
        dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=dust.main;main.useUnscaledTime=true;main.loop=false;main.maxParticles=24;main.startSpeed=0f;
        main.startLifetime=new ParticleSystem.MinMaxCurve(.28f,.46f);main.startSize=new ParticleSystem.MinMaxCurve(.07f,.13f+step*.02f);
        main.startColor=new ParticleSystem.MinMaxGradient(MeadowAmbientParticles.TerrainColor(new Color(.45f,.36f,.23f,.65f)),
            MeadowAmbientParticles.TerrainColor(new Color(.77f,.69f,.52f,.55f)));
        var emission=dust.emission;emission.enabled=false;var shape=dust.shape;shape.enabled=false;
        var velocity=dust.velocityOverLifetime;velocity.enabled=false;
        var fade=dust.colorOverLifetime;fade.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
            new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(0f,1f)});fade.color=new ParticleSystem.MinMaxGradient(gradient);
        dust.Play();int particles=16+step*4;
        for(int i=0;i<particles;i++)
        {
            float across=(i+.5f)/particles;
            dust.Emit(new ParticleSystem.EmitParams{position=edge+Vector3.forward*((across-.5f)*combatBounds.size.z*.45f),
                velocity=new Vector3(inward*(1.3f+(i%3)*.3f+step*.25f),.6f+i%4*.12f,(i%3-1)*.25f),rotation=i*43f},1);
        }
    }

    private static void ClearTyrannoFootstepDust(Rank4Presentation state)
    {
        if(state.tyrannoStepDust==null)return;
        for(int i=0;i<state.tyrannoStepDust.Length;i++)
        {
            var dust=state.tyrannoStepDust[i];if(dust==null)continue;
            dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);dust.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(dust.gameObject);else DestroyImmediate(dust.gameObject);
            state.tyrannoStepDust[i]=null;
        }
    }

    private static void SetRank4SourceHidden(Rank4Presentation state,bool hidden)
    {
        // The transcendence outline is also a body sprite; leaving it behind makes ascent look duplicated.
        for(int i=0;i<state.sourceRenderers.Length;i++)
            if(state.sourceRenderers[i]!=null)
                state.sourceRenderers[i].forceRenderingOff=hidden||state.sourceRendererHidden[i];
    }

    private void ResetRank4Cinematics()
    {
        CancelFamilyRank4CinematicAudio();
        ResetRank4SkillPreludes();
        foreach(var request in rank4PresentationQueue)if(request.context.family=="griffin")RevealGriffinStormOrb(request.source);
        rank4PresentationQueue.Clear();FinishRank4Cinematic();rank4PresentedUnits.Clear();rank4PresentationFailures.Clear();
    }
    private void DisposeRank4Cinematics()
    {ResetRank4Cinematics();DisposeRank4SkillPrelude();foreach(var stage in rank4PresentationStages.Values)stage.Dispose();rank4PresentationStages.Clear();}

    private void DrawRank4Cinematic()
    {
        var state=rank4Presentation;
        if(Event.current.type!=EventType.Repaint||state==null||state.stage.Texture==null)return;
        if(state.request.context.family=="griffin"&&
            (state.elapsed<GriffinCrosswindRank4Stage.PrivateStageTime||state.elapsed>=GriffinCrosswindRank4Stage.FieldRevealTime))DrawBattleGui();
        Color previous=GUI.color;
        float alpha=Mathf.Clamp01(state.stage.Opacity);
        try{GUI.color=new Color(1f,1f,1f,alpha);GUI.DrawTexture(new Rect(0,0,BombBrickUiLayout.Width,BombBrickUiLayout.Height),
            state.stage.Texture,ScaleMode.ScaleAndCrop,true);}
        finally{GUI.color=previous;}
    }

    private void AdvanceRank4CinematicHandoff(Rank4Presentation presentation)
    {
        Unit source=presentation.request.source;float time=presentation.stage.ActionTime;
        if(presentation.request.context.family=="spider"&&presentation.elapsed>=SpiderQueenRank4Stage.FieldReturnTime)
        {
            RevealSilkQueenDomain(source,FamilyTranscendenceStateFor(source,false),presentation.elapsed-SpiderQueenRank4Stage.DomainRevealTime);
            if(!presentation.handoffStarted)
            {presentation.handoffStarted=true;ReleaseSilkQueenCocoons(source);}
        }
        if(presentation.request.context.family=="griffin")
        {
            if(!presentation.handoffStarted&&presentation.elapsed>=GriffinCrosswindRank4Stage.FieldReturnTime)
            {presentation.handoffStarted=true;RevealGriffinStormOrb(source);}
            return;
        }
        if(time<0f)return;
        using(CombatSkillClock.Enter(presentation.clock))
        switch(presentation.request.context.family)
        {
            case "monkey":
            {
                if(!presentation.handoffStarted)
                {presentation.handoffStarted=true;ReleaseMonkeyStorm(source);}
                AdvanceMonkeyStormCinematic(source,time);break;
            }
            case "ninja_cat":
            {
                if(time>=NinjaSpacetimeRules.Windup)presentation.handoffStarted=true;
                AdvanceNinjaSpacetime(source,time);break;
            }
            case "tyranno":
            {
                var state=FamilyTranscendenceStateFor(source,false);
                if(state==null||state.tyrannoStompTime<0f)break;
                float desired=Mathf.Min(FamilyTranscendenceRules.TyrannoStompEnd,time);
                TickTyrannoReign(source,state,Mathf.Max(0f,desired-state.tyrannoStompTime));break;
            }
            case "mammoth":
            {
                if(time<MammothExpulsionRules.Contact)break;
                if(!presentation.handoffStarted)
                {
                    presentation.handoffStarted=true;var cast=source.mammothExpulsion;
                    if(cast!=null)
                    {
                        AdvanceMammothExpulsion(source,cast,MammothExpulsionRules.Contact+.001f);
                        if(rank4Presentation!=presentation)break;
                        source.spriteAnimation?.SyncMammothChargeTime(MammothExpulsionRules.Contact+.001f);
                    }
                }
                break;
            }
            case "beetle":
            {
                var cast=source.beetleOverturn;if(cast==null)break;
                float desired=Mathf.Min(time,BeetleOverturnRules.End);
                AdvanceBeetleOverturn(cast,Mathf.Max(0f,desired-cast.time)/CombatStatFormula.CombatAnimationRate);
                if(cast.landed)presentation.handoffStarted=true;break;
            }
            case "mantis":
            {
                var cast=source.mantisMolt;
                if(cast==null)break;
                float desired=Mathf.Min(.57f,time);
                if(cast.age<desired)TickMantisDeathMolt(source,desired-cast.age);
                cast.cocoon?.RefreshPose();
                if(time>=.9f)foreach(var child in cast.children)
                    if(child.mantisBroodRise>0f)TickMantisBroodling(child,child.mantisBroodRise);
                UpdateMantisCinematicVisibility(presentation);
                break;
            }
            case "mushroom":
            {
                if(presentation.elapsed<MushroomColonyRank4Stage.FieldReturnTime)break;
                if(!presentation.handoffStarted)
                {presentation.handoffStarted=true;ReleaseMushroomDeathBloom(source);}
                AdvanceMushroomDeathBloomCinematic(source,time);break;
            }
        }
    }

    private System.Collections.IEnumerator WarmupRemainingRank4Cinematics()
    {
        foreach(string path in units.Where(u=>HasFamilyTranscendence(u)&&IsTyrannoFamily(u))
                    .Select(u=>PixelTyrannoReignStomp.MotionPath(u.definition.monsterId,u.ownedInstance!=null&&u.ownedInstance.isShiny)).Distinct())
        {Resources.Load<Texture2D>(path);yield return null;}
        foreach(Unit source in units.Where(u=>HasFamilyTranscendence(u)&&Rank4CinematicCatalog.Supports(u.definition.familyId))
                    .GroupBy(u=>Rank4CinematicCatalog.Canonical(u.definition.familyId)).Select(g=>g.First()).ToArray())
        {
            string family=Rank4CinematicCatalog.Canonical(source.definition.familyId);
            if(rank4PresentationStages.ContainsKey(family))continue;
            var renderer=Rank3Renderer(source);if(renderer?.sprite==null)continue;
            Rank4CinematicStage stage=null;
            float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
            var context=new Rank4StageContext{family=family,source=FrogCatchActor(source,height,true),returnHeight=1f,
                battlefieldBackdrop=family=="ninja_cat"||family=="mushroom"||family=="beetle"?FrogCatchCurrentBackground():null,
                sceneryRegion=family=="haetae"||family=="beetle"||family=="griffin"?FrogCatchSceneryRegion():null,
                entryHeight=family=="haetae"||family=="beetle"||family=="griffin"?1f:0f};
            bool ready=false;
            try
            {
                stage=Rank4CinematicCatalog.Create(family,transform);
                if(stage!=null)rank4PresentationStages[family]=stage;
            }
            catch(Exception exception)
            {
                stage?.Hide();rank4PresentationFailures.Add(family);
                Debug.LogWarning("Rank-4 warmup unavailable for "+family+"; existing skill continues. "+exception.Message);
            }
            if(stage==null)continue;
            yield return PixelMantisReaper.WarmupVisibleBodies(stage.ArtworkFrames);
            try{ready=stage.Begin(context);}
            catch(Exception exception)
            {
                stage.Hide();rank4PresentationFailures.Add(family);
                Debug.LogWarning("Rank-4 warmup unavailable for "+family+"; existing skill continues. "+exception.Message);
            }
            if(!ready)continue;
            try
            {
                for(int shot=0;shot<4;shot++)
                {stage.Tick(stage.Duration*(shot+.5f)/4f,0f);yield return null;}
            }
            finally{stage.Hide();}
        }
    }

}
