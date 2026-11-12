using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI=LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class KingKongLeylineRequest
    {public Unit unit;public int generation,revision;}

    private sealed class KingKongLeylineCinematic
    {
        public Unit unit;
        public int generation,revision;
        public float elapsed,savedSpeed,fieldOfView;
        public Vector2 lensShift;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Matrix4x4 projection;
        public CombatSkillClock clock;
        public bool directorEnabled,environmentEnabled,sourceHidden,auraWasActive;
        public SpriteRenderer sourceRenderer;
        public LayeredBattleEnvironment environment;
        public PixelKingKongLeylineAura aura;
    }

    private static MonsterAutoBattlePrototype kingKongCinematicOwner;
    private readonly List<KingKongLeylineRequest> kingKongCinematicQueue=new List<KingKongLeylineRequest>();
    private KingKongLeylineCinematic kingKongCinematic;
    private KingKongLeylineCinematicStage kingKongCinematicStage;

    private System.Collections.IEnumerator WarmupKingKongLeylineCinematic()
    {
        Unit source=null;var species=new HashSet<string>();
        foreach(Unit unit in units)
            if(IsKingKongFamily(unit)&&HasFamilyTranscendence(unit)&&Rank3Renderer(unit)?.sprite!=null)
            {if(source==null)source=unit;species.Add(unit.definition.monsterId);}
        if(source==null||mainCamera==null)yield break;
        Texture2D background=FrogCatchCurrentBackground();if(background==null)yield break;
        yield return GameResourceCache.WarmupTexture(KingKongLeylineCinematicStage.ResourceRoot+"GroundFissures");
        foreach(string id in species)
        {
            string resource=KingKongLeylineCinematicStage.PoseResource(id);
            if(resource!=null)yield return GameResourceCache.WarmupTexture(resource);
        }
        if(kingKongCinematicStage==null)kingKongCinematicStage=new KingKongLeylineCinematicStage(transform);
        foreach(string id in species){kingKongCinematicStage.Warmup(id);yield return null;}
        SpriteRenderer renderer=Rank3Renderer(source);
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        try
        {
            if(kingKongCinematicStage.Begin(background,FrogCatchActor(source,height,true),FrogCatchSceneryRegion()))
            {kingKongCinematicStage.Tick(1.8f,0f);yield return null;}
        }
        finally{kingKongCinematicStage.Hide();}
    }

    private bool IsKingKongLeylineCinematicPending(Unit source)
    {
        if(kingKongCinematic?.unit==source)return true;
        foreach(var request in kingKongCinematicQueue)if(request.unit==source)return true;
        return false;
    }

    private bool QueueKingKongLeylineCinematic(Unit source)
    {
        if(!IsKingKongFamily(source)||!HasFamilyTranscendence(source)||
           !ShouldPresentCommonSkill(source)||mainCamera==null||!string.IsNullOrEmpty(resultText)||
           FamilyTranscendenceStateFor(source).kingKongCinematicGeneration==combatGeneration)return false;
        if(IsKingKongLeylineCinematicPending(source))return true;
        var request=new KingKongLeylineRequest{unit=source,generation=combatGeneration,revision=source.positionRevision};
        kingKongCinematicQueue.Add(request);
        FamilyTranscendenceStateFor(source).leylineAura?.SetPresentationPaused(true);
        return true;
    }

    private bool KingKongLeylineRequestAlive(Unit unit,int generation,int revision)=>
        generation==combatGeneration&&battleStarted&&!battleEnding&&IsCombatActive(unit)&&unit.hp>0f&&
        HasFamilyTranscendence(unit)&&unit.positionRevision==revision&&
        !unit.combatStatuses.Has(CombatStatusKind.Knockdown)&&!unit.combatStatuses.Has(CombatStatusKind.Fear)&&
        !unit.combatStatuses.Has(CombatStatusKind.Airborne)&&!SpiderSilkBound(unit)&&unit.giraffeStunTimer<=0f;

    private bool BeginKingKongLeylineCinematic(KingKongLeylineRequest request)
    {
        Unit unit=request.unit;
        if(kingKongCinematic!=null||raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||
           frogCatchCinematic!=null||frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
           CombatSkillClock.Active||Time.timeScale<=0f||!KingKongLeylineRequestAlive(unit,request.generation,request.revision)||
           !ShouldPresentCommonSkill(unit)||unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        SpriteRenderer renderer=Rank3Renderer(unit);Texture2D background=FrogCatchCurrentBackground();
        if(renderer==null||renderer.sprite==null||background==null)return false;
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        EndAllyPreparationSequence();EndSkillCameraPresentation();combatCameraDirector?.PrepareForCinematic();
        var state=new KingKongLeylineCinematic{unit=unit,generation=request.generation,revision=request.revision,
            savedSpeed=Time.timeScale,fieldOfView=mainCamera.fieldOfView,lensShift=mainCamera.lensShift,
            projection=mainCamera.projectionMatrix,cameraPosition=mainCamera.transform.position,cameraRotation=mainCamera.transform.rotation,
            directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled,
            environment=layeredBattleEnvironment,environmentEnabled=layeredBattleEnvironment!=null&&layeredBattleEnvironment.enabled,
            sourceRenderer=renderer,sourceHidden=renderer.forceRenderingOff,
            aura=FamilyTranscendenceStateFor(unit).leylineAura,clock=new CombatSkillClock(unit.model.transform)};
        state.auraWasActive=state.aura!=null&&state.aura.gameObject.activeSelf;
        kingKongCinematic=state;kingKongCinematicOwner=this;
        try
        {
            if(kingKongCinematicStage==null)kingKongCinematicStage=new KingKongLeylineCinematicStage(transform);
            if(!kingKongCinematicStage.Begin(background,FrogCatchActor(unit,height,true),FrogCatchSceneryRegion()))
            {FinishKingKongLeylineCinematic(false);return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            if(state.environment!=null)state.environment.enabled=false;
            renderer.forceRenderingOff=true;
            if(state.aura!=null)state.aura.gameObject.SetActive(false);
            state.clock.Hold();Time.timeScale=0f;
            FamilyTranscendenceStateFor(unit).kingKongCinematicGeneration=combatGeneration;
            kingKongCinematicStage.Tick(0f,0f);
            BeginFamilyRank4CinematicAudio(unit,"kingkong");return true;
        }
        catch(System.Exception exception)
        {
            FinishKingKongLeylineCinematic(false);
            Debug.LogWarning("Leyline stage unavailable; continuing the committed cast. "+exception.Message);return false;
        }
    }

    private bool UpdateKingKongLeylineCinematic()
    {
        var state=kingKongCinematic;
        if(state==null)
        {
            if(raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||
               frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
               CombatSkillClock.Active||paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)return false;
            int count=kingKongCinematicQueue.Count;
            while(count-->0)
            {
                var request=kingKongCinematicQueue[0];kingKongCinematicQueue.RemoveAt(0);
                if(!KingKongLeylineRequestAlive(request.unit,request.generation,request.revision))
                {FamilyTranscendenceStateFor(request.unit,false)?.leylineAura?.SetPresentationPaused(false);continue;}
                if(request.unit.combatStatuses.Has(CombatStatusKind.Freeze)){kingKongCinematicQueue.Add(request);continue;}
                if(TryBeginRank4SkillPrelude(request.unit,request))
                {kingKongCinematicQueue.Insert(0,request);return true;}
                if(BeginKingKongLeylineCinematic(request))return true;
                FamilyTranscendenceStateFor(request.unit,false)?.leylineAura?.SetPresentationPaused(false);
            }
            return false;
        }
        if(!KingKongLeylineRequestAlive(state.unit,state.generation,state.revision)||paused||GameFrontend.BlocksGameplay||
           state.unit.combatStatuses.Has(CombatStatusKind.Freeze)||mainCamera==null||state.sourceRenderer==null||Time.timeScale!=0f)
        {FinishKingKongLeylineCinematic(false);return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelKingKongLeylineCinematic();paused=true;return true;}
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime);state.elapsed+=dt;
        TickFamilyRank4CinematicAudio(state.elapsed);
        MatchKingKongLeylineReturnView(state);
        if(state.elapsed>=KingKongLeylineCinematicStage.Duration-.45f)
        {
            state.sourceRenderer.forceRenderingOff=state.sourceHidden;
            if(state.aura!=null)state.aura.gameObject.SetActive(state.auraWasActive);
        }
        kingKongCinematicStage.Tick(state.elapsed,dt);
        if(state.elapsed<KingKongLeylineCinematicStage.Duration)return true;
        FinishKingKongLeylineCinematic(true);return false;
    }

    private void MatchKingKongLeylineReturnView(KingKongLeylineCinematic state)
    {
        var renderer=state.sourceRenderer;if(renderer==null||renderer.sprite==null||mainCamera==null)return;
        Bounds body=PixelMantisReaper.VisibleBody(renderer.sprite);
        Vector3 foot=new Vector3(body.center.x,body.min.y,0f),head=new Vector3(body.center.x,body.max.y,0f);
        if(renderer.flipX){foot.x=-foot.x;head.x=-head.x;}
        Vector3 low=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(foot));
        Vector3 high=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(head));
        kingKongCinematicStage.SetReturnView(low,Mathf.Abs(high.y-low.y));
    }

    private void FinishKingKongLeylineCinematic(bool completed)
    {
        var state=kingKongCinematic;if(state==null)return;
        if(completed)EndFamilyRank4CinematicAudio();else CancelFamilyRank4CinematicAudio();
        kingKongCinematic=null;if(kingKongCinematicOwner==this)kingKongCinematicOwner=null;
        if(state.sourceRenderer!=null)state.sourceRenderer.forceRenderingOff=state.sourceHidden;
        if(state.aura!=null){state.aura.SetPresentationPaused(false);state.aura.gameObject.SetActive(state.auraWasActive);}
        kingKongCinematicStage?.Hide();state.clock.End();
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

    private void CancelKingKongLeylineCinematic()
    {
        foreach(var request in kingKongCinematicQueue)
            FamilyTranscendenceStateFor(request.unit,false)?.leylineAura?.SetPresentationPaused(false);
        kingKongCinematicQueue.Clear();FinishKingKongLeylineCinematic(false);
    }

    private void CancelKingKongLeylineCinematicFor(Unit unit)
    {
        for(int i=kingKongCinematicQueue.Count-1;i>=0;i--)
            if(kingKongCinematicQueue[i].unit==unit)
            {FamilyTranscendenceStateFor(unit,false)?.leylineAura?.SetPresentationPaused(false);kingKongCinematicQueue.RemoveAt(i);}
        if(kingKongCinematic?.unit==unit)FinishKingKongLeylineCinematic(false);
    }

    private void DrawKingKongLeylineCinematic()
    {
        if(Event.current.type!=EventType.Repaint||kingKongCinematic==null||kingKongCinematicStage?.Texture==null)return;
        Color previous=GUI.color;
        float alpha=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(kingKongCinematic.elapsed/.12f))*
            (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(KingKongLeylineCinematicStage.Duration-.45f,KingKongLeylineCinematicStage.Duration,kingKongCinematic.elapsed)));
        try
        {
            GUI.color=new Color(1f,1f,1f,alpha);
            GUI.DrawTexture(new Rect(0f,0f,BombBrickUiLayout.Width,BombBrickUiLayout.Height),kingKongCinematicStage.Texture,ScaleMode.ScaleAndCrop,true);
        }
        finally{GUI.color=previous;}
    }
}
