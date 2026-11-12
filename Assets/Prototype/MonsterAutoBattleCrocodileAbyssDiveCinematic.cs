using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI=LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class CrocodileAbyssDiveRequest
    {
        public Unit unit;
        public CrocodileAbyssDiveState cast;
        public int generation;
    }

    private sealed class CrocodileAbyssDiveCinematic
    {
        public Unit unit,target;
        public CrocodileAbyssDiveState cast;
        public int generation;
        public float elapsed,savedSpeed,fieldOfView;
        public Vector2 lensShift;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Matrix4x4 projection;
        public CombatSkillClock clock;
        public bool directorEnabled,environmentEnabled,sourceHidden,targetHidden,handoffPrepared;
        public SpriteRenderer sourceRenderer,targetRenderer;
        public LayeredBattleEnvironment environment;
    }

    private static MonsterAutoBattlePrototype crocodileAbyssCinematicOwner;
    private readonly List<CrocodileAbyssDiveRequest> crocodileAbyssCinematicQueue=new List<CrocodileAbyssDiveRequest>();
    private CrocodileAbyssDiveCinematic crocodileAbyssCinematic;
    private CrocodileAbyssDiveCinematicStage crocodileAbyssCinematicStage;
    private float crocodileAbyssReturnRemaining,crocodileAbyssReturnDuration;
    private bool crocAbyssAdvancingCast;

    private bool CanPresentCrocodileAbyssDive(Unit source)=>ShouldPresentCommonSkill(source)&&
        mainCamera!=null&&string.IsNullOrEmpty(resultText);

    private System.Collections.IEnumerator WarmupCrocodileAbyssDiveCinematic()
    {
        Unit source=null,target=null;var species=new HashSet<string>();
        foreach(Unit unit in units)
            if(UsesCrocodileAbyssDive(unit)&&Rank3Renderer(unit)?.sprite!=null)
            {
                if(source==null)source=unit;
                if(unit.ownedInstance==null||!unit.ownedInstance.isShiny)species.Add(unit.definition.monsterId);
            }
        if(source==null||mainCamera==null)yield break;
        foreach(Unit unit in units)
            if(unit.team!=source.team&&Rank3Renderer(unit)?.sprite!=null){target=unit;break;}
        Texture2D background=FrogCatchCurrentBackground();
        if(target==null||background==null)yield break;
        yield return GameResourceCache.WarmupTexture(CrocodileAbyssDiveCinematicStage.ResourceRoot+"Underwater");
        foreach(string id in species)
        {
            string resource=CrocodileAbyssDiveCinematicStage.PoseResource(id);
            if(resource!=null)yield return GameResourceCache.WarmupTexture(resource);
        }
        if(crocodileAbyssCinematicStage==null)crocodileAbyssCinematicStage=new CrocodileAbyssDiveCinematicStage(transform);
        foreach(string id in species){crocodileAbyssCinematicStage.Warmup(id);yield return null;}
        SpriteRenderer renderer=Rank3Renderer(source);
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        try
        {
            if(crocodileAbyssCinematicStage.Begin(background,FrogCatchActor(source,height,true),FrogCatchActor(target,height,false),null,FrogCatchSceneryRegion()))
            {crocodileAbyssCinematicStage.Tick(2f,0f);yield return null;}
        }
        finally{crocodileAbyssCinematicStage.Hide();}
    }

    private bool IsCrocodileAbyssDiveCinematicPending(Unit source)
    {
        if(crocAbyssAdvancingCast)return false;
        if(crocodileAbyssCinematic?.unit==source)return true;
        foreach(var request in crocodileAbyssCinematicQueue)if(request.unit==source)return true;
        return false;
    }

    private bool QueueCrocodileAbyssDiveCinematic(Unit source,CrocodileAbyssDiveState cast)
    {
        if(source.crocodileAbyssCinematicGeneration==combatGeneration||!CanPresentCrocodileAbyssDive(source)||
           !CrocodileAbyssCaptureValid(source,cast))return false;
        if(IsCrocodileAbyssDiveCinematicPending(source))return true;
        var request=new CrocodileAbyssDiveRequest{unit=source,cast=cast,generation=combatGeneration};
        crocodileAbyssCinematicQueue.Add(request);
        return true;
    }

    private bool CrocodileAbyssRequestAlive(Unit unit,CrocodileAbyssDiveState cast,int generation)=>
        generation==combatGeneration&&battleStarted&&!battleEnding&&IsCombatActive(unit)&&unit.crocodileAbyssDive==cast&&
        IsCombatActive(cast.target)&&unit.positionRevision==cast.revision&&
        !unit.combatStatuses.Has(CombatStatusKind.Knockdown)&&!unit.combatStatuses.Has(CombatStatusKind.Fear)&&
        !unit.combatStatuses.Has(CombatStatusKind.Airborne)&&!SpiderSilkBound(unit)&&unit.giraffeStunTimer<=0f;

    private bool BeginCrocodileAbyssDiveCinematic(CrocodileAbyssDiveRequest request)
    {
        Unit unit=request.unit;var cast=request.cast;
        if(crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||raptorCinematic!=null||
           sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||
           CombatSkillClock.Active||Time.timeScale<=0f||!CrocodileAbyssRequestAlive(unit,cast,request.generation)||
           !CrocodileAbyssCaptureValid(unit,cast)||!CanPresentCrocodileAbyssDive(unit)||unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        SpriteRenderer renderer=Rank3Renderer(unit),targetRenderer=Rank3Renderer(cast.target);
        Texture2D background=FrogCatchCurrentBackground();
        if(renderer==null||renderer.sprite==null||targetRenderer==null||targetRenderer.sprite==null||background==null)return false;
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        var bystanders=new List<FrogCatchCinematicStage.Actor>();
        foreach(Unit other in units)
            if(other!=unit&&other!=cast.target&&IsCombatActive(other)&&Rank3Renderer(other)?.sprite!=null)
                bystanders.Add(FrogCatchActor(other,height,false));
        var state=new CrocodileAbyssDiveCinematic{unit=unit,target=cast.target,cast=cast,generation=request.generation,
            sourceRenderer=renderer,targetRenderer=targetRenderer};
        EndAllyPreparationSequence();EndSkillCameraPresentation();combatCameraDirector?.PrepareForCinematic();
        state.savedSpeed=Time.timeScale;state.fieldOfView=mainCamera.fieldOfView;state.lensShift=mainCamera.lensShift;
        state.projection=mainCamera.projectionMatrix;state.cameraPosition=mainCamera.transform.position;
        state.cameraRotation=mainCamera.transform.rotation;state.directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled;
        state.environment=layeredBattleEnvironment;state.environmentEnabled=state.environment!=null&&state.environment.enabled;
        state.sourceHidden=renderer.forceRenderingOff;state.targetHidden=targetRenderer.forceRenderingOff;
        state.clock=new CombatSkillClock(unit.model.transform);
        crocodileAbyssCinematic=state;crocodileAbyssCinematicOwner=this;
        try
        {
            if(crocodileAbyssCinematicStage==null)crocodileAbyssCinematicStage=new CrocodileAbyssDiveCinematicStage(transform);
            if(!crocodileAbyssCinematicStage.Begin(background,FrogCatchActor(unit,height,true),FrogCatchActor(cast.target,height,false),bystanders,FrogCatchSceneryRegion()))
            {FinishCrocodileAbyssDiveCinematic(false);return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            if(state.environment!=null)state.environment.enabled=false;
            renderer.forceRenderingOff=targetRenderer.forceRenderingOff=true;
            state.clock.Hold();Time.timeScale=0f;
            unit.crocodileAbyssCinematicGeneration=combatGeneration;
            crocodileAbyssCinematicStage.Tick(0f,0f);
            BeginFamilyRank4CinematicAudio(unit,"crocodile");return true;
        }
        catch(System.Exception exception)
        {
            FinishCrocodileAbyssDiveCinematic(false);
            Debug.LogWarning("Crocodile abyss stage unavailable; continuing the committed cast. "+exception.Message);return false;
        }
    }

    private bool UpdateCrocodileAbyssDiveCinematic()
    {
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime);
        crocodileAbyssReturnRemaining=Mathf.Max(0f,crocodileAbyssReturnRemaining-dt);
        var state=crocodileAbyssCinematic;
        if(state==null)
        {
            if(crocodileAbyssReturnRemaining>0f||raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||
               frogCatchCinematic!=null||frogCatchReturnRemaining>0f||CombatSkillClock.Active||paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)return false;
            int count=crocodileAbyssCinematicQueue.Count;
            while(count-->0)
            {
                var request=crocodileAbyssCinematicQueue[0];crocodileAbyssCinematicQueue.RemoveAt(0);
                if(!CrocodileAbyssRequestAlive(request.unit,request.cast,request.generation))continue;
                if(request.unit.combatStatuses.Has(CombatStatusKind.Freeze)){crocodileAbyssCinematicQueue.Add(request);continue;}
                if(TryBeginRank4SkillPrelude(request.unit,request))
                {crocodileAbyssCinematicQueue.Insert(0,request);return true;}
                if(BeginCrocodileAbyssDiveCinematic(request))return true;
            }
            return false;
        }
        if(!CrocodileAbyssRequestAlive(state.unit,state.cast,state.generation)||paused||GameFrontend.BlocksGameplay||
           state.unit.combatStatuses.Has(CombatStatusKind.Freeze)||mainCamera==null||Time.timeScale!=0f||
           !state.handoffPrepared&&!CrocodileAbyssCaptureValid(state.unit,state.cast))
        {FinishCrocodileAbyssDiveCinematic(false);return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelCrocodileAbyssDiveCinematic();paused=true;return true;}
        state.elapsed+=dt;
        TickFamilyRank4CinematicAudio(state.elapsed);
        if(state.elapsed>=CrocodileAbyssDiveCinematicStage.Duration-.45f&&!state.handoffPrepared)PrepareCrocodileAbyssHandoff(state);
        if(state.handoffPrepared)MatchCrocodileAbyssReturnView(state);
        crocodileAbyssCinematicStage.Tick(state.elapsed,dt);
        if(crocodileAbyssCinematic!=state)return false;
        if(state.elapsed<CrocodileAbyssDiveCinematicStage.Duration)return true;
        FinishCrocodileAbyssDiveCinematic(true);return false;
    }

    private bool PrepareCrocodileAbyssHandoff(CrocodileAbyssDiveCinematic state)
    {
        if(state.handoffPrepared)return true;
        if(!CrocodileAbyssRequestAlive(state.unit,state.cast,state.generation)||paused||state.unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        ResumeCrocodileAbyssDiveFromCinematic(state.unit);
        state.handoffPrepared=state.unit.crocodileAbyssDive==state.cast&&!state.cast.hit&&
            Mathf.Abs(state.cast.time-(CrocodileAbyssDiveRules.HitTime-.001f))<.0001f;
        if(state.handoffPrepared)
        {
            if(state.sourceRenderer!=null)state.sourceRenderer.forceRenderingOff=state.sourceHidden;
            if(state.targetRenderer!=null)state.targetRenderer.forceRenderingOff=state.targetHidden;
        }
        return state.handoffPrepared;
    }

    private void MatchCrocodileAbyssReturnView(CrocodileAbyssDiveCinematic state)
    {
        SpriteRenderer renderer=state.sourceRenderer;
        Sprite closed=state.unit.spriteAnimation?.GetSkillAttackFrame(1);
        if(renderer==null||mainCamera==null||closed==null)return;
        Bounds body=PixelMantisReaper.VisibleBody(closed);
        Vector3 foot=new Vector3(body.center.x,body.min.y,0f),head=new Vector3(body.center.x,body.max.y,0f);
        if(renderer.flipX){foot.x=-foot.x;head.x=-head.x;}
        Vector3 low=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(foot));
        Vector3 high=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(head));
        crocodileAbyssCinematicStage.SetReturnView(low,Mathf.Abs(high.y-low.y));
    }

    private void FinishCrocodileAbyssDiveCinematic(bool handoff)
    {
        var state=crocodileAbyssCinematic;if(state==null)return;
        bool matched=handoff&&PrepareCrocodileAbyssHandoff(state);
        if(crocodileAbyssCinematic!=state)return;
        if(handoff)EndFamilyRank4CinematicAudio();else CancelFamilyRank4CinematicAudio();
        crocodileAbyssCinematic=null;
        if(crocodileAbyssCinematicOwner==this)crocodileAbyssCinematicOwner=null;
        if(state.sourceRenderer!=null)state.sourceRenderer.forceRenderingOff=state.sourceHidden;
        if(state.targetRenderer!=null)state.targetRenderer.forceRenderingOff=state.targetHidden;
        crocodileAbyssCinematicStage?.Hide();state.clock.End();
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
            mainCamera.fieldOfView=state.fieldOfView;mainCamera.lensShift=state.lensShift;mainCamera.projectionMatrix=state.projection;
        }
        crocodileAbyssReturnDuration=matched?.085f/Mathf.Max(.25f,state.savedSpeed):0f;
        crocodileAbyssReturnRemaining=crocodileAbyssReturnDuration;
    }

    private void CancelCrocodileAbyssDiveCinematic()
    {crocodileAbyssCinematicQueue.Clear();FinishCrocodileAbyssDiveCinematic(false);crocodileAbyssReturnRemaining=0f;}

    private void CancelCrocodileAbyssDiveCinematicFor(Unit unit)
    {
        for(int i=crocodileAbyssCinematicQueue.Count-1;i>=0;i--)
            if(crocodileAbyssCinematicQueue[i].unit==unit)crocodileAbyssCinematicQueue.RemoveAt(i);
        if(crocodileAbyssCinematic?.unit==unit)FinishCrocodileAbyssDiveCinematic(false);
    }

    private void DrawCrocodileAbyssDiveCinematic()
    {
        if(Event.current.type!=EventType.Repaint||crocodileAbyssCinematic==null||crocodileAbyssCinematicStage?.Texture==null)return;
        Color previous=GUI.color;
        float alpha=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(crocodileAbyssCinematic.elapsed/.12f))*
            (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(3.55f,4f,crocodileAbyssCinematic.elapsed)));
        try
        {
            GUI.color=new Color(1f,1f,1f,alpha);
            GUI.DrawTexture(new Rect(0f,0f,BombBrickUiLayout.Width,BombBrickUiLayout.Height),crocodileAbyssCinematicStage.Texture,ScaleMode.ScaleAndCrop,true);
        }
        finally{GUI.color=previous;}
    }
}
