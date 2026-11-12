using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI=LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class BearAvatarRequest
    {public Unit unit;public int generation,revision;public FrogCatchCinematicStage.Actor actor;}

    private sealed class BearAvatarCinematic
    {
        public Unit unit;
        public int generation,revision;
        public float elapsed,savedSpeed,fieldOfView;
        public Vector2 lensShift;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Matrix4x4 projection;
        public CombatSkillClock clock;
        public bool directorEnabled,environmentEnabled,sourceHidden,roarPrepared;
        public SpriteRenderer sourceRenderer;
        public LayeredBattleEnvironment environment;
    }

    private static MonsterAutoBattlePrototype bearAvatarCinematicOwner;
    private readonly List<BearAvatarRequest> bearAvatarCinematicQueue=new List<BearAvatarRequest>();
    private BearAvatarCinematic bearAvatarCinematic;
    private BearAvatarCinematicStage bearAvatarCinematicStage;

    private System.Collections.IEnumerator WarmupBearAvatarCinematic()
    {
        Unit source=null;
        foreach(Unit unit in units)
            if(IsBearFamily(unit)&&HasFamilyTranscendence(unit)&&Rank3Renderer(unit)?.sprite!=null)
            {source=unit;break;}
        if(source==null||mainCamera==null)yield break;
        yield return GameResourceCache.WarmupTexture(BearAvatarCinematicStage.ResourceRoot+"PartsAction");
        yield return GameResourceCache.WarmupTexture(BearAvatarCinematicStage.ResourceRoot+"HeadRoarAction");
        if(bearAvatarCinematicStage==null)bearAvatarCinematicStage=new BearAvatarCinematicStage(transform);
        bearAvatarCinematicStage.Warmup(source.definition.monsterId);
        SpriteRenderer renderer=Rank3Renderer(source);
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        try
        {
            if(bearAvatarCinematicStage.Begin(FrogCatchActor(source,height,true)))
            {
                foreach(float time in new[]{.2f,.55f,.85f,1.25f,1.7f,2.1f})
                {bearAvatarCinematicStage.Tick(time,0f);yield return null;}
            }
        }
        finally{bearAvatarCinematicStage.Hide();}
    }

    private bool IsBearAvatarCinematicPending(Unit source)
    {
        if(bearAvatarCinematic?.unit==source)return true;
        foreach(var request in bearAvatarCinematicQueue)if(request.unit==source)return true;
        return false;
    }

    private bool QueueBearAvatarCinematic(Unit source)
    {
        if(!IsBearFamily(source)||!HasFamilyTranscendence(source)||!ShouldPresentCommonSkill(source)||
           mainCamera==null||!string.IsNullOrEmpty(resultText)||
           FamilyTranscendenceStateFor(source).bearCinematicGeneration==combatGeneration)return false;
        if(IsBearAvatarCinematicPending(source))return true;
        SpriteRenderer renderer=Rank3Renderer(source);if(renderer==null||renderer.sprite==null)return false;
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        var request=new BearAvatarRequest{unit=source,generation=combatGeneration,
            revision=source.positionRevision,actor=FrogCatchActor(source,height,true)};
        bearAvatarCinematicQueue.Add(request);
        return true;
    }

    private bool BearAvatarRequestAlive(Unit unit,int generation,int revision)=>
        generation==combatGeneration&&battleStarted&&!battleEnding&&IsCombatActive(unit)&&unit.hp>0f&&
        HasFamilyTranscendence(unit)&&BearAvatarActive(unit)&&unit.bearAvatarRoarPending&&unit.positionRevision==revision&&
        !unit.combatStatuses.Has(CombatStatusKind.Knockdown)&&!unit.combatStatuses.Has(CombatStatusKind.Fear)&&
        !unit.combatStatuses.Has(CombatStatusKind.Airborne)&&!SpiderSilkBound(unit)&&unit.giraffeStunTimer<=0f;

    private bool BeginBearAvatarCinematic(BearAvatarRequest request)
    {
        Unit unit=request.unit;
        if(bearAvatarCinematic!=null||wizardCygnusCinematic!=null||kingKongCinematic!=null||raptorCinematic!=null||
           sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||
           crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||CombatSkillClock.Active||Time.timeScale<=0f||
           !BearAvatarRequestAlive(unit,request.generation,request.revision)||!ShouldPresentCommonSkill(unit)||
           unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        SpriteRenderer renderer=Rank3Renderer(unit);if(renderer==null||renderer.sprite==null)return false;
        EndAllyPreparationSequence();EndSkillCameraPresentation();combatCameraDirector?.PrepareForCinematic();
        var state=new BearAvatarCinematic{unit=unit,generation=request.generation,revision=request.revision,
            savedSpeed=Time.timeScale,fieldOfView=mainCamera.fieldOfView,lensShift=mainCamera.lensShift,
            projection=mainCamera.projectionMatrix,cameraPosition=mainCamera.transform.position,cameraRotation=mainCamera.transform.rotation,
            directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled,
            environment=layeredBattleEnvironment,environmentEnabled=layeredBattleEnvironment!=null&&layeredBattleEnvironment.enabled,
            sourceRenderer=renderer,sourceHidden=renderer.forceRenderingOff,clock=new CombatSkillClock(unit.model.transform)};
        bearAvatarCinematic=state;bearAvatarCinematicOwner=this;
        try
        {
            if(bearAvatarCinematicStage==null)bearAvatarCinematicStage=new BearAvatarCinematicStage(transform);
            if(!bearAvatarCinematicStage.Begin(request.actor)){FinishBearAvatarCinematic(false);return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            if(state.environment!=null)state.environment.enabled=false;
            renderer.forceRenderingOff=true;state.clock.Hold();Time.timeScale=0f;
            FamilyTranscendenceStateFor(unit).bearCinematicGeneration=combatGeneration;
            bearAvatarCinematicStage.Tick(0f,0f);
            BeginFamilyRank4CinematicAudio(unit,"bear");return true;
        }
        catch(System.Exception exception)
        {
            FinishBearAvatarCinematic(false);
            Debug.LogWarning("Bear transformation stage unavailable; continuing the committed roar. "+exception.Message);return false;
        }
    }

    private bool UpdateBearAvatarCinematic()
    {
        var state=bearAvatarCinematic;
        if(state==null)
        {
            if(wizardCygnusCinematic!=null||kingKongCinematic!=null||raptorCinematic!=null||sabertoothCinematic!=null||
               sabertoothCinematicReturn!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||
               crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||CombatSkillClock.Active||
               paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)return false;
            int count=bearAvatarCinematicQueue.Count;
            while(count-->0)
            {
                var request=bearAvatarCinematicQueue[0];bearAvatarCinematicQueue.RemoveAt(0);
                if(!BearAvatarRequestAlive(request.unit,request.generation,request.revision))continue;
                if(request.unit.combatStatuses.Has(CombatStatusKind.Freeze)){bearAvatarCinematicQueue.Add(request);continue;}
                if(TryBeginRank4SkillPrelude(request.unit,request))
                {bearAvatarCinematicQueue.Insert(0,request);return true;}
                if(BeginBearAvatarCinematic(request))return true;
            }
            return false;
        }
        if(!BearAvatarRequestAlive(state.unit,state.generation,state.revision)||paused||GameFrontend.BlocksGameplay||
           state.unit.combatStatuses.Has(CombatStatusKind.Freeze)||mainCamera==null||state.sourceRenderer==null||Time.timeScale!=0f)
        {FinishBearAvatarCinematic(false);return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelBearAvatarCinematic();paused=true;return true;}
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime);state.elapsed+=dt;
        TickFamilyRank4CinematicAudio(state.elapsed);
        if(state.elapsed>=BearAvatarCinematicStage.Duration-.35f)
        {
            PrepareBearAvatarCinematicRoar(state);
            state.sourceRenderer.forceRenderingOff=state.sourceHidden;
        }
        MatchBearAvatarReturnView(state);
        bearAvatarCinematicStage.Tick(state.elapsed,dt);
        if(state.elapsed<BearAvatarCinematicStage.Duration)return true;
        FinishBearAvatarCinematic(true);return false;
    }

    private void PrepareBearAvatarCinematicRoar(BearAvatarCinematic state)
    {
        if(state.roarPrepared)return;state.roarPrepared=true;
        Unit unit=state.unit;
        unit.spriteAnimation?.PlayBearAvatarRoar(()=>
        {
            if(!BearAvatarRequestAlive(unit,state.generation,state.revision))return;
            unit.spriteAnimation.HoldSkillAttackHitFrame(BearCombatRules.AvatarRoarImpactHoldDuration);
            ResolveBearAvatarRoar(unit);unit.bearAvatarRoarPending=false;
        },prepared:true);
    }

    private void MatchBearAvatarReturnView(BearAvatarCinematic state)
    {
        SpriteRenderer renderer=state.sourceRenderer;if(renderer==null||renderer.sprite==null||mainCamera==null)return;
        Bounds body=PixelMantisReaper.VisibleBody(renderer.sprite);
        Vector3 foot=new Vector3(body.center.x,body.min.y,0f),head=new Vector3(body.center.x,body.max.y,0f);
        if(renderer.flipX){foot.x=-foot.x;head.x=-head.x;}
        Vector3 low=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(foot));
        Vector3 high=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(head));
        bearAvatarCinematicStage.SetReturnView(low,Mathf.Abs(high.y-low.y));
        if(state.roarPrepared)bearAvatarCinematicStage.SetReturnSprite(renderer.sprite);
    }

    private void FinishBearAvatarCinematic(bool completed)
    {
        var state=bearAvatarCinematic;if(state==null)return;
        if(completed)EndFamilyRank4CinematicAudio();else CancelFamilyRank4CinematicAudio();
        bool release=completed&&BearAvatarRequestAlive(state.unit,state.generation,state.revision);
        if(release)PrepareBearAvatarCinematicRoar(state);
        bearAvatarCinematic=null;if(bearAvatarCinematicOwner==this)bearAvatarCinematicOwner=null;
        if(state.sourceRenderer!=null)state.sourceRenderer.forceRenderingOff=state.sourceHidden;
        bearAvatarCinematicStage?.Hide();state.clock.End();
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
        if(release)
        {
            if(state.unit.spriteAnimation!=null)state.unit.spriteAnimation.NotifySkillAttackHitFrameFromAnimation();
            else {ResolveBearAvatarRoar(state.unit);state.unit.bearAvatarRoarPending=false;}
        }
        else if(state.roarPrepared)state.unit.spriteAnimation?.InterruptCurrentAction(true);
    }

    private void CancelBearAvatarCinematic()
    {bearAvatarCinematicQueue.Clear();FinishBearAvatarCinematic(false);}

    private void CancelBearAvatarCinematicFor(Unit unit)
    {
        for(int i=bearAvatarCinematicQueue.Count-1;i>=0;i--)
            if(bearAvatarCinematicQueue[i].unit==unit)bearAvatarCinematicQueue.RemoveAt(i);
        if(bearAvatarCinematic?.unit==unit)FinishBearAvatarCinematic(false);
    }

    private void DrawBearAvatarCinematic()
    {
        if(Event.current.type!=EventType.Repaint||bearAvatarCinematic==null||bearAvatarCinematicStage?.Texture==null)return;
        Color previous=GUI.color;
        float alpha=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(bearAvatarCinematic.elapsed/.1f))*
            (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(BearAvatarCinematicStage.Duration-.35f,BearAvatarCinematicStage.Duration,bearAvatarCinematic.elapsed)));
        try
        {
            GUI.color=new Color(1f,1f,1f,alpha);
            GUI.DrawTexture(new Rect(0f,0f,BombBrickUiLayout.Width,BombBrickUiLayout.Height),bearAvatarCinematicStage.Texture,ScaleMode.ScaleAndCrop,true);
        }
        finally{GUI.color=previous;}
    }
}

