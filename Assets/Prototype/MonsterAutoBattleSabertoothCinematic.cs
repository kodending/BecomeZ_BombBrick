using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI = LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private const float SabertoothCinematicDuration=4f;
    private static MonsterAutoBattlePrototype sabertoothCinematicOwner;
    private static Texture2D sabertoothPredatorDomain;
    private readonly List<SabertoothCinematicRequest> sabertoothCinematicQueue=new List<SabertoothCinematicRequest>();
    private SabertoothCinematic sabertoothCinematic;
    private SabertoothDevourCinematicStage sabertoothCinematicStage;
    private Unit sabertoothCinematicHandoffUnit;
    private float sabertoothCinematicHandoffWidth;
    private SabertoothCinematicReturn sabertoothCinematicReturn;

    private sealed class SabertoothCinematicRequest
    {
        public PendingSkillFocus cast;
        public int generation;
        public bool presented;
        public float jawBodyWidthRatio;
    }

    private sealed class SabertoothCinematic
    {
        public SabertoothCinematicRequest request;
        public Unit unit;
        public SpriteRenderer renderer;
        public Sprite pose;
        public Rect entryBody;
        public float elapsed,savedSpeed,fieldOfView;
        public Vector2 lensShift;
        public Matrix4x4 projection;
        public CombatSkillClock clock;
        public bool directorEnabled,environmentEnabled,rendererForceRenderingOff;
        public LayeredBattleEnvironment environment;
    }

    private sealed class SabertoothCinematicReturn
    {
        public Unit unit;
        public SpriteRenderer renderer;
        public Texture texture;
        public Rect actorViewport;
        public Vector2 mouthViewport;
        public float elapsed,jawBodyWidthRatio;
        public int generation;
    }

    private static void WarmupSabertoothDevourCinematic()
    {
        if(sabertoothPredatorDomain==null)
            sabertoothPredatorDomain=Resources.Load<Texture2D>("CombatCinematics/SabertoothDevour/PredatorDomain");
        SabertoothDevourCinematicStage.Warmup();
    }

    private bool CanPresentSabertoothCinematic(Unit unit)=>UsesSabertoothDevour(unit)&&
        ShouldPresentCommonSkill(unit)&&string.IsNullOrEmpty(resultText)&&mainCamera!=null;

    private bool IsSabertoothCinematicPending(Unit unit)
    {
        if(sabertoothCinematic?.unit==unit)return true;
        foreach(var request in sabertoothCinematicQueue)if(request.cast.unit==unit)return true;
        return false;
    }

    private bool TryQueueSabertoothCinematic(Unit unit,bool combinedCastTrigger)
    {
        if(!CanPresentSabertoothCinematic(unit)||IsCommittingFocusedSkill(unit)||
           unit.sabertoothDevourCinematicGeneration==combatGeneration)return false;
        if(IsSabertoothCinematicPending(unit))return true;
        WarmupSabertoothDevourCinematic();
        if(sabertoothPredatorDomain==null||!SabertoothDevourCinematicStage.HasAssets(unit.definition.monsterId))return false;
        var cast=new PendingSkillFocus{unit=unit,combinedCastTrigger=combinedCastTrigger};
        if(!CanContinueSkillFocus(cast,true))return false;
        CaptureFocusedCastPlan(cast);
        // An approved cast is held here; cooldowns, traits, audio and damage commit only after the stage.
        sabertoothCinematicQueue.Add(new SabertoothCinematicRequest
            {cast=cast,generation=combatGeneration});
        unit.currentAction="sabertooth_cinematic_wait";
        return true;
    }

    private bool AttachSabertoothCinematicCombinedSkill(Unit unit,QueuedCombinedSkillGroup group)
    {
        foreach(var request in sabertoothCinematicQueue)
            if(request.cast.unit==unit){request.cast.combinedGroup=group;return true;}
        return false;
    }

    private bool SabertoothCinematicRequestAlive(SabertoothCinematicRequest request)=>request!=null&&
        request.generation==combatGeneration&&
        CanContinueSkillFocus(request.cast,false);

    private bool BeginSabertoothCinematic(SabertoothCinematicRequest request)
    {
        Unit unit=request.cast.unit;
        if(sabertoothCinematic!=null||sabertoothCinematicReturn!=null||raptorCinematic!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||CombatSkillClock.Active||Time.timeScale<=0f||
           !SabertoothCinematicRequestAlive(request)||!PreparedSkillReleaseReady(request.cast)||
           !CanPresentSabertoothCinematic(unit))return false;
        var renderer=unit.spriteAnimation.GetComponent<SpriteRenderer>();
        if(renderer==null||!renderer.enabled||renderer.sprite==null)return false;
        WarmupSabertoothDevourCinematic();
        if(sabertoothPredatorDomain==null||!SabertoothDevourCinematicStage.HasAssets(unit.definition.monsterId))return false;
        EndAllyPreparationSequence();EndSkillCameraPresentation();
        combatCameraDirector?.PrepareForCinematic();
        var state=new SabertoothCinematic {request=request,unit=unit,renderer=renderer,pose=renderer.sprite,
            savedSpeed=Time.timeScale,fieldOfView=mainCamera.fieldOfView,lensShift=mainCamera.lensShift,
            projection=mainCamera.projectionMatrix,directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled,
            rendererForceRenderingOff=renderer.forceRenderingOff,
            clock=new CombatSkillClock(unit.model.transform),environment=FindObjectOfType<LayeredBattleEnvironment>()};
        if(!TryGetSpriteBodyGuiRect(mainCamera,renderer,out state.entryBody))return false;
        if(state.environment!=null)state.environmentEnabled=state.environment.enabled;
        sabertoothCinematic=state;sabertoothCinematicOwner=this;
        try
        {
            if(sabertoothCinematicStage==null)sabertoothCinematicStage=new SabertoothDevourCinematicStage(transform);
            if(!sabertoothCinematicStage.Begin(unit.definition.monsterId,renderer.flipX,
                unit.ownedInstance!=null&&unit.ownedInstance.isShiny))
            {FinishSabertoothCinematic(false);return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            mainCamera.fieldOfView=state.fieldOfView;mainCamera.lensShift=state.lensShift;
            mainCamera.projectionMatrix=state.projection;
            if(state.environment!=null)state.environment.enabled=false;
            renderer.forceRenderingOff=true;
            state.clock.Hold();Time.timeScale=0f;
            request.presented=true;
            unit.sabertoothDevourCinematicGeneration=combatGeneration;
            BeginFamilyRank4CinematicAudio(unit,"sabertooth");
            return true;
        }
        catch(System.Exception exception)
        {
            FinishSabertoothCinematic(false);
            Debug.LogWarning("Sabertooth cinematic unavailable; using the normal skill. "+exception.Message);
            return false;
        }
    }

    private bool UpdateSabertoothCinematic()
    {
        AdvanceSabertoothCinematicReturn(Mathf.Max(0f,Time.unscaledDeltaTime));
        if(sabertoothCinematic==null)
        {
            // Keep queued actors intact while another cinematic owns the global freeze.
            if(sabertoothCinematicReturn!=null||raptorCinematic!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||CombatSkillClock.Active)return false;
            if(paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)return false;
            int waiting=sabertoothCinematicQueue.Count;
            while(waiting-->0)
            {
                var request=sabertoothCinematicQueue[0];sabertoothCinematicQueue.RemoveAt(0);
                if(!SabertoothCinematicRequestAlive(request))continue;
                if(!PreparedSkillReleaseReady(request.cast)||Time.timeAsDouble<request.cast.nextAttemptAt)
                {sabertoothCinematicQueue.Add(request);continue;}
                if(!request.presented&&TryBeginRank4SkillPrelude(request.cast.unit,request.cast))
                {sabertoothCinematicQueue.Insert(0,request);return true;}
                if(!request.presented&&BeginSabertoothCinematic(request))return true;
                ReleaseOrDeferSabertoothCinematicCast(request,false);
            }
            return false;
        }
        var state=sabertoothCinematic;
        if(!SabertoothCinematicRequestAlive(state.request)||!CanPresentSabertoothCinematic(state.unit)||
           state.renderer==null||Time.timeScale!=0f)
        {FinishSabertoothCinematic(false);return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelSabertoothCinematic();paused=true;return true;}
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime);
        state.elapsed+=dt;TickFamilyRank4CinematicAudio(state.elapsed);
        sabertoothCinematicStage?.Tick(state.elapsed,dt);
        if(state.elapsed<SabertoothCinematicDuration)return true;
        FinishSabertoothCinematic(true);
        return false;
    }

    private bool ReleaseSabertoothCinematicCast(SabertoothCinematicRequest request,bool matchedReturn)
    {
        if(!SabertoothCinematicRequestAlive(request)||!PreparedSkillReleaseReady(request.cast))return false;
        sabertoothCinematicHandoffUnit=matchedReturn?request.cast.unit:null;
        Unit unit=request.cast.unit;
        bool emphasize=unit.team==Team.Player,used=false;
        if(emphasize)unit.spriteAnimation?.SetSkillFocus(1f);
        if(matchedReturn&&unit.spriteAnimation!=null)
        {
            var renderer=unit.spriteAnimation.GetComponent<SpriteRenderer>();
            if(renderer!=null&&renderer.sprite!=null)
                sabertoothCinematicHandoffWidth=request.jawBodyWidthRatio*
                    PixelMantisReaper.VisibleBody(renderer.sprite).size.x*Mathf.Abs(renderer.transform.lossyScale.x);
        }
        try
        {
            used=ExecuteFocusedSkill(request.cast);
            var presentation=SkillPresentationFor(unit);
            if(used&&emphasize&&presentation!=null)
            {presentation.allySkillEmphasis=true;RefreshAllyPreparationEmphasis();}
            if(used&&matchedReturn&&unit.sabertoothDevour?.effect!=null)
            {
                var renderer=unit.spriteAnimation.GetComponent<SpriteRenderer>();
                float width=request.jawBodyWidthRatio*PixelMantisReaper.VisibleBody(renderer.sprite).size.x*
                    Mathf.Abs(renderer.transform.lossyScale.x);
                var live=unit.sabertoothDevour;
                live.effect.BeginCinematicHandoff(width);
                // Apply the actual attack-pose scale before the first live frame, without advancing this cast.
                live.effect.Advance(live.effect.Age,SabertoothDevourMouth(unit,live),0f);
            }
            return used;
        }
        finally
        {
            sabertoothCinematicHandoffUnit=null;
            sabertoothCinematicHandoffWidth=0f;
            if(!used&&emphasize)unit.spriteAnimation?.SetSkillFocus(0f);
        }
    }

    private bool ReleaseOrDeferSabertoothCinematicCast(SabertoothCinematicRequest request,bool matchedReturn)
    {
        if(!SabertoothCinematicRequestAlive(request))return false;
        if(ReleaseSabertoothCinematicCast(request,matchedReturn))return true;
        request.presented=true;
        request.cast.nextAttemptAt=Time.timeAsDouble+.1d;
        sabertoothCinematicQueue.Add(request);
        return false;
    }

    private void CancelSabertoothCinematic()
    {
        sabertoothCinematicQueue.Clear();FinishSabertoothCinematic(false);ClearSabertoothCinematicReturn();
    }

    private void FinishSabertoothCinematic(bool releaseCast)
    {
        var state=sabertoothCinematic;
        if(state==null)return;
        if(releaseCast)EndFamilyRank4CinematicAudio();else CancelFamilyRank4CinematicAudio();
        sabertoothCinematic=null;
        if(sabertoothCinematicOwner==this)sabertoothCinematicOwner=null;
        var tail=releaseCast&&sabertoothCinematicStage!=null?new SabertoothCinematicReturn
        {
            unit=state.unit,renderer=state.renderer,texture=sabertoothCinematicStage.Texture,
            actorViewport=sabertoothCinematicStage.ActorViewportRect,mouthViewport=sabertoothCinematicStage.MouthViewport,
            generation=state.request.generation,jawBodyWidthRatio=sabertoothCinematicStage.JawBodyWidthRatio
        }:null;
        if(releaseCast&&sabertoothCinematicStage!=null)
            state.request.jawBodyWidthRatio=sabertoothCinematicStage.JawBodyWidthRatio;
        if(state.renderer!=null)state.renderer.forceRenderingOff=state.rendererForceRenderingOff;
        sabertoothCinematicStage?.Hide();state.clock.End();
        if(Time.timeScale==0f)Time.timeScale=state.savedSpeed;
        if(state.environment!=null)state.environment.enabled=state.environmentEnabled;
        if(combatCameraDirector!=null)
        {
            combatCameraDirector.enabled=state.directorEnabled;
            if(state.directorEnabled&&battleStarted&&!battleEnding)combatCameraDirector.BeginBattle();
        }
        if(mainCamera!=null)
        {
            mainCamera.fieldOfView=state.fieldOfView;mainCamera.lensShift=state.lensShift;
            mainCamera.projectionMatrix=state.projection;
        }
        if(releaseCast&&ReleaseOrDeferSabertoothCinematicCast(state.request,true))sabertoothCinematicReturn=tail;
    }

    private void AdvanceSabertoothCinematicReturn(float dt)
    {
        var tail=sabertoothCinematicReturn;
        if(tail==null)return;
        tail.elapsed+=Mathf.Max(0f,dt);
        if(tail.elapsed>=.18f||tail.generation!=combatGeneration||!IsCombatActive(tail.unit)||
           battleEnding||paused||GameFrontend.BlocksGameplay)
            ClearSabertoothCinematicReturn();
    }

    private void ClearSabertoothCinematicReturn()=>sabertoothCinematicReturn=null;

    private static Rect SabertoothMatchBodyRect(Rect viewport,Rect body,Vector2 textureSize)
    {
        float scale=Mathf.Min(body.width/Mathf.Max(.001f,viewport.width*textureSize.x),
            body.height/Mathf.Max(.001f,viewport.height*textureSize.y));
        Vector2 size=textureSize*scale;
        Vector2 center=body.center-new Vector2(viewport.center.x-.5f,.5f-viewport.center.y)*size;
        return new Rect(center-size*.5f,size);
    }

    private static Rect SabertoothMatchMouthRect(Rect viewport,Vector2 mouthViewport,Rect body,
        Vector2 mouth,Vector2 textureSize)
    {
        // The live jaw uses this same body-width ratio, so its silhouette cannot pop at release.
        Vector2 size=textureSize*(body.width/Mathf.Max(.001f,viewport.width*textureSize.x));
        return new Rect(mouth-new Vector2(mouthViewport.x,1f-mouthViewport.y)*size,size);
    }

    private bool SabertoothReturnGeometry(Unit unit,SpriteRenderer renderer,bool anticipateFocus,out Rect body,out Vector2 mouth)
    {
        body=Rect.zero;mouth=Vector2.zero;
        if(renderer==null)return false;
        Sprite original=renderer.sprite;Vector3 originalScale=renderer.transform.localScale;
        bool preview=anticipateFocus&&unit.spriteAnimation!=null;
        try
        {
            if(preview)
            {
                renderer.sprite=unit.spriteAnimation.GetSkillAttackFrame(0)??original;
                renderer.transform.localScale=Vector3.one*unit.spriteAnimation.SkillAttackAuthoredScale*
                    (unit.team==Team.Player?1.5f:1f);
            }
            if(!TryGetSpriteBodyGuiRect(mainCamera,renderer,out body))return false;
            Vector3 point;
            if(unit.sabertoothDevour!=null)point=SabertoothDevourMouth(unit,unit.sabertoothDevour);
            else
            {
                Unit target=IsCombatTargetable(unit.target)?unit.target:null;
                Vector3 forward=target==null?DeploymentFacing(unit.team):
                    Flat(target.model.transform.position-unit.model.transform.position);
                if(forward.sqrMagnitude<.0001f)forward=DeploymentFacing(unit.team);
                point=renderer.bounds.center+forward.normalized*Mathf.Max(.2f,renderer.bounds.size.x*.32f);
            }
            Vector3 screen=mainCamera.WorldToScreenPoint(point);
            mouth=BombBrickUiLayout.ScreenToGuiPoint(new Vector2(screen.x,screen.y));
            return screen.z>0f;
        }
        finally
        {
            if(preview){renderer.sprite=original;renderer.transform.localScale=originalScale;}
        }
    }

    private void DrawSabertoothCinematicReturn()
    {
        var tail=sabertoothCinematicReturn;
        if(tail==null||Event.current.type!=EventType.Repaint||tail.texture==null||
           !SabertoothReturnGeometry(tail.unit,tail.renderer,false,out Rect body,out Vector2 mouth))return;
        Rect destination=SabertoothMatchMouthRect(tail.actorViewport,tail.mouthViewport,body,mouth,
            new Vector2(BombBrickUiLayout.Width,BombBrickUiLayout.Height));
        Color saved=GUI.color;
        try
        {
            GUI.color=new Color(1f,1f,1f,1f-RaptorEase(0f,.18f,tail.elapsed));
            GUI.DrawTexture(destination,tail.texture,ScaleMode.StretchToFill,true);
        }
        finally{GUI.color=saved;}
    }

    private void DrawSabertoothCinematic()
    {
        if(Event.current.type!=EventType.Repaint)return;
        var state=sabertoothCinematic;
        if(state==null||state.renderer==null||sabertoothCinematicStage==null)return;
        float t=state.elapsed,w=BombBrickUiLayout.Width,h=BombBrickUiLayout.Height;
        Rect screen=new Rect(0f,0f,w,h);
        float enter=RaptorEase(0f,.5f,t),leave=RaptorEase(3.25f,4f,t);
        Color saved=GUI.color;
        try
        {
            float backdrop=RaptorEase(.08f,.55f,t)*(1f-RaptorEase(3.25f,3.7f,t));
            DrawRaptorFill(screen,new Color(.008f,.006f,.011f,.93f*backdrop));
            GUI.color=new Color(1f,1f,1f,backdrop);
            GUI.DrawTexture(screen,sabertoothCinematicStage.BackgroundTexture??sabertoothPredatorDomain,ScaleMode.ScaleAndCrop,true);
            Rect destination=screen;
            Rect actor=sabertoothCinematicStage.ActorViewportRect;
            if(actor.width>.001f&&actor.height>.001f)
            {
                if(t<.5f)
                {
                    Rect match=SabertoothMatchBodyRect(actor,state.entryBody,screen.size);
                    destination=new Rect(Vector2.Lerp(match.position,screen.position,enter),Vector2.Lerp(match.size,screen.size,enter));
                }
                else if(leave>0f&&SabertoothReturnGeometry(state.unit,state.renderer,true,out Rect body,out Vector2 mouth))
                {
                    Rect match=SabertoothMatchMouthRect(actor,sabertoothCinematicStage.MouthViewport,body,mouth,screen.size);
                    destination=new Rect(Vector2.Lerp(screen.position,match.position,leave),Vector2.Lerp(screen.size,match.size,leave));
                }
            }
            float actorAlpha=RaptorEase(.08f,.42f,t);
            GUI.color=new Color(1f,1f,1f,actorAlpha);
            GUI.DrawTexture(destination,sabertoothCinematicStage.Texture,ScaleMode.StretchToFill,true);
            if(t<.42f)
            {
                Rect mapped=new Rect(destination.position+new Vector2(actor.x,1f-actor.yMax)*destination.size,
                    actor.size*destination.size);
                Rect rawBody=new Rect(Vector2.Lerp(state.entryBody.position,mapped.position,enter),
                    Vector2.Lerp(state.entryBody.size,mapped.size,enter));
                DrawRaptorBody(state.pose,rawBody,state.renderer.flipX,state.renderer.flipY,1f-actorAlpha);
            }
            float bars=h*.055f*RaptorEase(.05f,.3f,t)*(1f-leave);
            DrawRaptorFill(new Rect(0f,0f,w,bars),Color.black);
            DrawRaptorFill(new Rect(0f,h-bars,w,bars),Color.black);
        }
        finally{GUI.color=saved;}
    }

}
