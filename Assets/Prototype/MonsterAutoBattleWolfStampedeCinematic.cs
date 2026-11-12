using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI=LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class WolfStampedeRequest
    {public Unit unit;public WolfStampedeState cast;public FrogCatchCinematicStage.Actor actor;}
    private sealed class WolfStampedeCinematic
    {
        public WolfStampedeRequest request;
        public float elapsed,savedSpeed,fieldOfView;
        public Vector2 lensShift;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Matrix4x4 projection;
        public CombatSkillClock clock;
        public bool directorEnabled,environmentEnabled,sourceHidden;
        public SpriteRenderer sourceRenderer;
        public LayeredBattleEnvironment environment;
    }
    private static MonsterAutoBattlePrototype wolfStampedeCinematicOwner;
    private readonly List<WolfStampedeRequest> wolfStampedeCinematicQueue=new List<WolfStampedeRequest>();
    private WolfStampedeCinematic wolfStampedeCinematic;
    private WolfStampedeCinematicStage wolfStampedeCinematicStage;

    private System.Collections.IEnumerator WarmupWolfStampedeCinematic()
    {
        Unit source=null;
        foreach(Unit unit in units)if(UsesWolfStampede(unit)&&Rank3Renderer(unit)?.sprite!=null){source=unit;break;}
        if(source==null||mainCamera==null)yield break;
        yield return GameResourceCache.WarmupTexture(PixelWolfStampede.AtlasResource);
        foreach(string name in new[]{"MoonHuntPanorama","MoonHuntGround","HuntEffectsAction"})
            yield return GameResourceCache.WarmupTexture(WolfStampedeCinematicStage.ResourceRoot+name);
        PixelWolfStampede.Warmup();
        PixelWolfStampede.WarmupContacts();
        if(wolfStampedeCinematicStage==null)wolfStampedeCinematicStage=new WolfStampedeCinematicStage(transform);
        wolfStampedeCinematicStage.Warmup();
        var renderer=Rank3Renderer(source);
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        try
        {
            if(wolfStampedeCinematicStage.Begin(FrogCatchActor(source,height,true),FrogCatchCurrentBackground()))
                foreach(float time in new[]{.2f,.8f,1.6f,2.4f}){wolfStampedeCinematicStage.Tick(time,0f);yield return null;}
        }
        finally{wolfStampedeCinematicStage.Hide();}
    }

    private bool IsWolfStampedeCinematicPending(Unit unit)
    {
        if(wolfStampedeCinematic?.request.unit==unit)return true;
        foreach(var request in wolfStampedeCinematicQueue)if(request.unit==unit)return true;
        return false;
    }
    private bool QueueWolfStampedeCinematic(Unit unit,WolfStampedeState cast)
    {
        if(!ShouldPresentCommonSkill(unit)||mainCamera==null||!string.IsNullOrEmpty(resultText)||
           FamilyTranscendenceStateFor(unit).wolfCinematicGeneration==combatGeneration)return false;
        if(IsWolfStampedeCinematicPending(unit))return true;
        SpriteRenderer renderer=Rank3Renderer(unit);if(renderer?.sprite==null)return false;
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        var request=new WolfStampedeRequest{unit=unit,cast=cast,actor=FrogCatchActor(unit,height,true)};
        wolfStampedeCinematicQueue.Add(request);
        return true;
    }
    private bool WolfStampedeRequestAlive(WolfStampedeRequest request)=>request!=null&&
        request.cast.generation==combatGeneration&&battleStarted&&!battleEnding&&!request.cast.cancelled&&
        (request.cast.released||WolfStampedePreparationValid(request.cast));

    private bool BeginWolfStampedeCinematic(WolfStampedeRequest request)
    {
        Unit unit=request.unit;
        if(wolfStampedeCinematic!=null||bearAvatarCinematic!=null||wizardCygnusCinematic!=null||kingKongCinematic!=null||
           raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||
           frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
           CombatSkillClock.Active||Time.timeScale<=0f||!WolfStampedeRequestAlive(request)||!ShouldPresentCommonSkill(unit)||
           unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        SpriteRenderer renderer=Rank3Renderer(unit);if(renderer?.sprite==null)return false;
        EndAllyPreparationSequence();EndSkillCameraPresentation();combatCameraDirector?.PrepareForCinematic();
        var state=new WolfStampedeCinematic{request=request,savedSpeed=Time.timeScale,
            fieldOfView=mainCamera.fieldOfView,lensShift=mainCamera.lensShift,projection=mainCamera.projectionMatrix,
            cameraPosition=mainCamera.transform.position,cameraRotation=mainCamera.transform.rotation,
            directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled,
            environment=layeredBattleEnvironment,environmentEnabled=layeredBattleEnvironment!=null&&layeredBattleEnvironment.enabled,
            sourceRenderer=renderer,sourceHidden=renderer.forceRenderingOff,clock=new CombatSkillClock(unit.model.transform)};
        wolfStampedeCinematic=state;wolfStampedeCinematicOwner=this;
        try
        {
            if(wolfStampedeCinematicStage==null)wolfStampedeCinematicStage=new WolfStampedeCinematicStage(transform);
            if(!wolfStampedeCinematicStage.Begin(request.actor,FrogCatchCurrentBackground()))
            {CancelFamilyRank4CinematicAudio();FinishWolfStampedeCinematic();return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            if(state.environment!=null)state.environment.enabled=false;
            renderer.forceRenderingOff=true;state.clock.Hold();Time.timeScale=0f;
            FamilyTranscendenceStateFor(unit).wolfCinematicGeneration=combatGeneration;
            wolfStampedeCinematicStage.Tick(0f,0f);
            BeginFamilyRank4CinematicAudio(unit,"wolf");return true;
        }
        catch(System.Exception exception)
        {
            CancelFamilyRank4CinematicAudio();
            FinishWolfStampedeCinematic();
            Debug.LogWarning("Wolf stampede stage unavailable; continuing the committed hunt. "+exception.Message);return false;
        }
    }

    private bool UpdateWolfStampedeCinematic()
    {
        var state=wolfStampedeCinematic;
        if(state==null)
        {
            if(CombatSkillClock.Active||paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f||
               bearAvatarCinematic!=null||wizardCygnusCinematic!=null||kingKongCinematic!=null||raptorCinematic!=null||
               sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||
               crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f)return false;
            int count=wolfStampedeCinematicQueue.Count;
            while(count-->0)
            {
                var request=wolfStampedeCinematicQueue[0];wolfStampedeCinematicQueue.RemoveAt(0);
                if(!WolfStampedeRequestAlive(request))continue;
                if(request.unit.combatStatuses.Has(CombatStatusKind.Freeze)){wolfStampedeCinematicQueue.Add(request);continue;}
                if(TryBeginRank4SkillPrelude(request.unit,request))
                {wolfStampedeCinematicQueue.Insert(0,request);return true;}
                if(BeginWolfStampedeCinematic(request))return true;
            }
            return false;
        }
        var cast=state.request.cast;
        if(!WolfStampedeRequestAlive(state.request)||paused||GameFrontend.BlocksGameplay||mainCamera==null||
           state.sourceRenderer==null||Time.timeScale!=0f||!cast.released&&state.request.unit.combatStatuses.Has(CombatStatusKind.Freeze))
        {CancelFamilyRank4CinematicAudio();FinishWolfStampedeCinematic();return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelWolfStampedeCinematic();paused=true;return true;}
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime),previous=state.elapsed;state.elapsed+=dt;
        TickFamilyRank4CinematicAudio(state.elapsed);
        float releaseTime=WolfStampedeCinematicStage.Duration-.25f;
        if(state.elapsed>=releaseTime)
        {
            state.sourceRenderer.forceRenderingOff=state.sourceHidden;
            if(!cast.released)ReleaseWolfStampede(state.request.unit);
            if(cast.released)AdvanceWolfStampede(cast,Mathf.Max(0f,state.elapsed-Mathf.Max(previous,releaseTime)));
        }
        if(wolfStampedeCinematic!=state)return false;
        wolfStampedeCinematicStage.Tick(state.elapsed,dt);
        if(state.elapsed<WolfStampedeCinematicStage.Duration)return true;
        FinishWolfStampedeCinematic();return false;
    }

    private void FinishWolfStampedeCinematic()
    {
        var state=wolfStampedeCinematic;if(state==null)return;
        EndFamilyRank4CinematicAudio();
        wolfStampedeCinematic=null;if(wolfStampedeCinematicOwner==this)wolfStampedeCinematicOwner=null;
        if(state.sourceRenderer!=null)state.sourceRenderer.forceRenderingOff=state.sourceHidden;
        wolfStampedeCinematicStage?.Hide();state.clock.End();
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
    }
    private void CancelWolfStampedeCinematic()
    {
        wolfStampedeCinematicQueue.Clear();
        if(wolfStampedeCinematic!=null)CancelFamilyRank4CinematicAudio();
        FinishWolfStampedeCinematic();
    }
    private void CancelWolfStampedeCinematicFor(Unit unit)
    {
        for(int i=wolfStampedeCinematicQueue.Count-1;i>=0;i--)
            if(wolfStampedeCinematicQueue[i].unit==unit)wolfStampedeCinematicQueue.RemoveAt(i);
        if(wolfStampedeCinematic?.request.unit==unit)
        {CancelFamilyRank4CinematicAudio();FinishWolfStampedeCinematic();}
    }
    private void DrawWolfStampedeCinematic()
    {
        if(Event.current.type!=EventType.Repaint||wolfStampedeCinematic==null||wolfStampedeCinematicStage?.Texture==null)return;
        Color previous=GUI.color;
        float alpha=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(wolfStampedeCinematic.elapsed/.1f))*
            (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(WolfStampedeCinematicStage.Duration-.25f,WolfStampedeCinematicStage.Duration,wolfStampedeCinematic.elapsed)));
        try
        {
            GUI.color=new Color(1f,1f,1f,alpha);
            GUI.DrawTexture(new Rect(0f,0f,BombBrickUiLayout.Width,BombBrickUiLayout.Height),wolfStampedeCinematicStage.Texture,ScaleMode.ScaleAndCrop,true);
        }
        finally{GUI.color=previous;}
    }
}
