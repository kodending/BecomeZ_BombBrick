using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI=LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class FrogCatchRequest
    {
        public Unit unit;
        public FrogGrandSlamState state;
        public int generation;
    }

    private sealed class FrogCatchCinematic
    {
        public Unit unit;
        public FrogGrandSlamState cast;
        public int generation;
        public float elapsed,savedSpeed,fieldOfView;
        public Vector2 lensShift;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Matrix4x4 projection;
        public CombatSkillClock clock;
        public bool directorEnabled,environmentEnabled,sourceHidden,handoffPrepared;
        public SpriteRenderer renderer;
        public LayeredBattleEnvironment environment;
        public readonly List<FrogGrandSlamVictim> victims=new List<FrogGrandSlamVictim>();
    }

    private static MonsterAutoBattlePrototype frogCatchCinematicOwner;
    private readonly List<FrogCatchRequest> frogCatchCinematicQueue=new List<FrogCatchRequest>();
    private FrogCatchCinematic frogCatchCinematic;
    private FrogCatchCinematicStage frogCatchCinematicStage;
    private float frogCatchReturnRemaining,frogCatchReturnDuration;
    private bool frogCatchAdvancingCast;

    private bool FrogCatchPresentationAvailable(Unit source)=>ShouldPresentCommonSkill(source)&&
        mainCamera!=null&&string.IsNullOrEmpty(resultText);

    private System.Collections.IEnumerator WarmupFrogCatchCinematic()
    {
        Unit source=null;var poseSpecies=new HashSet<string>();
        foreach(Unit unit in units)
            if(unit.definition?.familyId=="frog"&&HasFamilyTranscendence(unit)&&Rank3Renderer(unit)?.sprite!=null)
            {
                if(source==null)source=unit;
                if(unit.ownedInstance==null||!unit.ownedInstance.isShiny)poseSpecies.Add(unit.definition.monsterId);
            }
        if(source==null||mainCamera==null)yield break;
        foreach(string species in poseSpecies)
        {
            string resource=FrogCatchCinematicStage.PoseResource(species);
            if(resource!=null)yield return GameResourceCache.WarmupTexture(resource);
        }
        Texture2D background=FrogCatchCurrentBackground();if(background==null)yield break;
        SpriteRenderer renderer=Rank3Renderer(source);
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        var enemies=new List<FrogCatchCinematicStage.Actor>();
        foreach(Unit unit in units)
            if(unit.team!=source.team&&Rank3Renderer(unit)?.sprite!=null)enemies.Add(FrogCatchActor(unit,height,false));
        if(enemies.Count==0)yield break;
        if(frogCatchCinematicStage==null)frogCatchCinematicStage=new FrogCatchCinematicStage(transform);
        foreach(string species in poseSpecies)
        {frogCatchCinematicStage.Warmup(species);yield return null;}
        try
        {
            if(frogCatchCinematicStage.Begin(background,FrogCatchActor(source,height,true),enemies,null,FrogCatchSceneryRegion()))
            {
                frogCatchCinematicStage.Tick(2.5f,0f);
                yield return null;
            }
        }
        finally{frogCatchCinematicStage?.Hide();}
    }

    private bool IsFrogCatchCinematicPending(Unit source)
    {
        if(frogCatchAdvancingCast)return false;
        if(frogCatchCinematic?.unit==source)return true;
        foreach(var request in frogCatchCinematicQueue)if(request.unit==source)return true;
        return false;
    }

    private bool QueueFrogCatchCinematic(Unit source,FrogGrandSlamState cast)
    {
        if(source.frogCatchCinematicGeneration==combatGeneration||!FrogCatchPresentationAvailable(source))return false;
        if(IsFrogCatchCinematicPending(source))return true;
        bool captured=false;
        foreach(var victim in cast.victims)
            if(victim.captured&&FrogGrandSlamControlValid(source,victim)&&Rank3Renderer(victim.unit)?.sprite!=null)
            {captured=true;break;}
        if(!captured)return false;
        var request=new FrogCatchRequest{unit=source,state=cast,generation=combatGeneration};
        frogCatchCinematicQueue.Add(request);
        return true;
    }

    private bool FrogCatchRequestAlive(Unit unit,FrogGrandSlamState cast,int generation)=>
        generation==combatGeneration&&battleStarted&&!battleEnding&&IsCombatActive(unit)&&unit.frogGrandSlam==cast&&
        unit.positionRevision==cast.revision&&!unit.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !unit.combatStatuses.Has(CombatStatusKind.Fear)&&!unit.combatStatuses.Has(CombatStatusKind.Airborne)&&
        !SpiderSilkBound(unit)&&unit.giraffeStunTimer<=0f;

    private Texture2D FrogCatchCurrentBackground()
    {
        string region=string.IsNullOrWhiteSpace(environmentPreviewRegionId)?expeditionRun?.regionId:environmentPreviewRegionId;
        return Resources.Load<Texture2D>(EnvironmentResourceRoot(region)+"/BattleBackground");
    }

    private string FrogCatchSceneryRegion()
    {
        string region=string.IsNullOrWhiteSpace(environmentPreviewRegionId)?expeditionRun?.regionId:environmentPreviewRegionId;
        return EnvironmentResourceRoot(region);
    }

    private FrogCatchCinematicStage.Actor FrogCatchActor(Unit unit,float sourceHeight,bool source)
    {
        SpriteRenderer renderer=Rank3Renderer(unit);
        SpriteMonsterAnimationDriver driver=unit.spriteAnimation;
        Sprite[] frames;
        if(source&&driver!=null&&driver.SkillAttackFrameCount>0)
        {
            frames=new Sprite[driver.SkillAttackFrameCount];
            for(int i=0;i<frames.Length;i++)frames[i]=driver.GetSkillAttackFrame(i);
        }
        else frames=driver!=null&&driver.DragFrames!=null&&driver.DragFrames.Length>0?driver.DragFrames:driver?.IdleFrames;
        if(!source&&driver!=null&&renderer?.sprite!=null)
        {
            bool authored=false;
            foreach(Sprite frame in driver.CombatFrames())if(frame==renderer.sprite){authored=true;break;}
            // A transformed creature must not turn back into its normal drag animation in the cutscene.
            if(!authored)frames=new[]{renderer.sprite};
        }
        Vector3 position=Flat(unit.model.transform.position);
        Bounds bounds=combatBounds;
        float height=renderer==null||renderer.sprite==null?sourceHeight:
            PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        return new FrogCatchCinematicStage.Actor
        {
            species=unit.definition.monsterId,sprite=renderer?.sprite,frames=frames,
            flipX=renderer!=null&&renderer.flipX,color=renderer==null?Color.white:renderer.color,
            shiny=unit.ownedInstance!=null&&unit.ownedInstance.isShiny,
            ground=new Vector2(Mathf.Clamp((position.x-bounds.center.x)/Mathf.Max(1f,bounds.extents.x),-1f,1f),
                Mathf.Clamp((position.z-bounds.center.z)/Mathf.Max(1f,bounds.extents.z),-1f,1f)),
            size=Mathf.Clamp(height/Mathf.Max(.01f,sourceHeight),.2f,4f)
        };
    }

    private bool BeginFrogCatchCinematic(FrogCatchRequest request)
    {
        Unit unit=request.unit;
        if(frogCatchCinematic!=null||frogCatchReturnRemaining>0f||raptorCinematic!=null||sabertoothCinematic!=null||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
           sabertoothCinematicReturn!=null||CombatSkillClock.Active||Time.timeScale<=0f||
           !FrogCatchRequestAlive(unit,request.state,request.generation)||!FrogCatchPresentationAvailable(unit)||
           unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        SpriteRenderer renderer=Rank3Renderer(unit);
        Texture2D background=FrogCatchCurrentBackground();
        if(renderer==null||renderer.sprite==null||background==null)return false;
        var state=new FrogCatchCinematic{unit=unit,cast=request.state,generation=request.generation,renderer=renderer};
        var captures=new List<FrogCatchCinematicStage.Actor>();
        var bystanders=new List<FrogCatchCinematicStage.Actor>();
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        foreach(var victim in request.state.victims)
            if(victim.captured&&FrogGrandSlamControlValid(unit,victim)&&Rank3Renderer(victim.unit)?.sprite!=null)
            {state.victims.Add(victim);captures.Add(FrogCatchActor(victim.unit,height,false));}
        if(captures.Count==0)return false;
        foreach(Unit other in units)
        {
            if(other==unit||!IsCombatActive(other)||Rank3Renderer(other)?.sprite==null)continue;
            bool captured=false;
            foreach(var victim in state.victims)if(victim.unit==other){captured=true;break;}
            if(!captured)bystanders.Add(FrogCatchActor(other,height,false));
        }
        EndAllyPreparationSequence();EndSkillCameraPresentation();combatCameraDirector?.PrepareForCinematic();
        state.savedSpeed=Time.timeScale;state.fieldOfView=mainCamera.fieldOfView;state.lensShift=mainCamera.lensShift;
        state.projection=mainCamera.projectionMatrix;state.cameraPosition=mainCamera.transform.position;
        state.cameraRotation=mainCamera.transform.rotation;state.directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled;
        state.environment=layeredBattleEnvironment;state.environmentEnabled=state.environment!=null&&state.environment.enabled;
        state.sourceHidden=renderer.forceRenderingOff;state.clock=new CombatSkillClock(unit.model.transform);
        frogCatchCinematic=state;frogCatchCinematicOwner=this;
        try
        {
            if(frogCatchCinematicStage==null)frogCatchCinematicStage=new FrogCatchCinematicStage(transform);
            if(!frogCatchCinematicStage.Begin(background,FrogCatchActor(unit,height,true),captures,bystanders,FrogCatchSceneryRegion()))
            {FinishFrogCatchCinematic(false);return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            if(state.environment!=null)state.environment.enabled=false;
            renderer.forceRenderingOff=true;state.clock.Hold();Time.timeScale=0f;
            unit.frogCatchCinematicGeneration=combatGeneration;
            frogCatchCinematicStage.Tick(0f,0f);
            BeginFamilyRank4CinematicAudio(unit,"frog");
            return true;
        }
        catch(System.Exception exception)
        {
            FinishFrogCatchCinematic(false);
            Debug.LogWarning("Frog catch cinematic unavailable; continuing the same cast. "+exception.Message);
            return false;
        }
    }

    private bool UpdateFrogCatchCinematic()
    {
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime);
        frogCatchReturnRemaining=Mathf.Max(0f,frogCatchReturnRemaining-dt);
        var state=frogCatchCinematic;
        if(state==null)
        {
            if(frogCatchReturnRemaining>0f||raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
               CombatSkillClock.Active||paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)return false;
            int count=frogCatchCinematicQueue.Count;
            while(count-->0)
            {
                var request=frogCatchCinematicQueue[0];frogCatchCinematicQueue.RemoveAt(0);
                if(!FrogCatchRequestAlive(request.unit,request.state,request.generation))continue;
                if(request.unit.combatStatuses.Has(CombatStatusKind.Freeze))
                {frogCatchCinematicQueue.Add(request);continue;}
                if(TryBeginRank4SkillPrelude(request.unit,request))
                {frogCatchCinematicQueue.Insert(0,request);return true;}
                if(BeginFrogCatchCinematic(request))return true;
            }
            return false;
        }
        if(!FrogCatchRequestAlive(state.unit,state.cast,state.generation)||paused||GameFrontend.BlocksGameplay||
           state.unit.combatStatuses.Has(CombatStatusKind.Freeze)||mainCamera==null||Time.timeScale!=0f)
        {FinishFrogCatchCinematic(false);return false;}
        foreach(var victim in state.victims)
            if(!victim.captured||!FrogGrandSlamControlValid(state.unit,victim))
            {FinishFrogCatchCinematic(false);return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelFrogCatchCinematic();paused=true;return true;}
        state.elapsed+=dt;
        TickFamilyRank4CinematicAudio(state.elapsed);
        frogCatchCinematicStage.Tick(state.elapsed,dt);
        if(state.elapsed>=FrogCatchCinematicStage.Duration-.16f&&!state.handoffPrepared)
            PrepareFrogCatchHandoff(state);
        if(frogCatchCinematic!=state)return false;
        if(state.elapsed<FrogCatchCinematicStage.Duration)return true;
        FinishFrogCatchCinematic(true);return false;
    }

    private bool PrepareFrogCatchHandoff(FrogCatchCinematic state)
    {
        if(state.handoffPrepared)return true;
        if(!FrogCatchRequestAlive(state.unit,state.cast,state.generation)||paused||
           state.unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        // The contact already happened: advance that exact cast, never execute a second skill.
        frogCatchAdvancingCast=true;
        try{AdvanceFrogGrandSlam(state.unit,Mathf.Max(0f,FrogGrandSlamRules.SlamStart-state.cast.time));}
        finally{frogCatchAdvancingCast=false;}
        state.handoffPrepared=state.unit.frogGrandSlam==state.cast&&
            Mathf.Abs(state.cast.time-FrogGrandSlamRules.SlamStart)<.001f&&!state.cast.landed;
        return state.handoffPrepared;
    }

    private void FinishFrogCatchCinematic(bool handoff)
    {
        var state=frogCatchCinematic;if(state==null)return;
        bool matched=handoff&&PrepareFrogCatchHandoff(state);
        if(frogCatchCinematic!=state)return;
        if(handoff)EndFamilyRank4CinematicAudio();else CancelFamilyRank4CinematicAudio();
        frogCatchCinematic=null;
        if(frogCatchCinematicOwner==this)frogCatchCinematicOwner=null;
        if(state.renderer!=null)state.renderer.forceRenderingOff=state.sourceHidden;
        frogCatchCinematicStage?.Hide();state.clock.End();
        if(Time.timeScale==0f)Time.timeScale=state.savedSpeed;
        if(state.environment!=null)state.environment.enabled=state.environmentEnabled;
        if(combatCameraDirector!=null)
        {
            combatCameraDirector.enabled=state.directorEnabled;
            if(state.directorEnabled&&battleStarted&&!battleEnding)combatCameraDirector.BeginBattle();
        }
        if(mainCamera!=null)
        {
            mainCamera.transform.SetPositionAndRotation(state.cameraPosition,state.cameraRotation);
            mainCamera.fieldOfView=state.fieldOfView;mainCamera.lensShift=state.lensShift;
            mainCamera.projectionMatrix=state.projection;
        }
        frogCatchReturnDuration=matched ? .085f/Mathf.Max(.25f,state.savedSpeed) : 0f;
        frogCatchReturnRemaining=frogCatchReturnDuration;
    }

    private void CancelFrogCatchCinematic()
    {
        frogCatchCinematicQueue.Clear();FinishFrogCatchCinematic(false);frogCatchReturnRemaining=0f;
    }

    private void CancelFrogCatchCinematicFor(Unit unit)
    {
        for(int i=frogCatchCinematicQueue.Count-1;i>=0;i--)
            if(frogCatchCinematicQueue[i].unit==unit)frogCatchCinematicQueue.RemoveAt(i);
        if(frogCatchCinematic?.unit==unit)FinishFrogCatchCinematic(false);
    }

    private void DrawFrogCatchCinematic()
    {
        if(Event.current.type!=EventType.Repaint||frogCatchCinematicStage?.Texture==null)return;
        Color saved=GUI.color;
        float alpha=frogCatchCinematic!=null?Mathf.SmoothStep(0f,1f,Mathf.Clamp01(frogCatchCinematic.elapsed/.12f)):
            Mathf.SmoothStep(0f,1f,frogCatchReturnRemaining/Mathf.Max(.001f,frogCatchReturnDuration));
        try
        {
            GUI.color=new Color(1f,1f,1f,alpha);
            GUI.DrawTexture(new Rect(0f,0f,BombBrickUiLayout.Width,BombBrickUiLayout.Height),
                frogCatchCinematicStage.Texture,ScaleMode.ScaleAndCrop,true);
        }
        finally{GUI.color=saved;}
    }
}
