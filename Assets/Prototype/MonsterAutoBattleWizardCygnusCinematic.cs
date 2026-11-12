using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI=LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class WizardCygnusRequest
    {public Unit unit;public WizardCygnusShowerState cast;public int generation;}

    private sealed class WizardCygnusCinematic
    {
        public Unit unit;
        public WizardCygnusShowerState cast;
        public int generation;
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

    private static MonsterAutoBattlePrototype wizardCygnusCinematicOwner;
    private readonly List<WizardCygnusRequest> wizardCygnusCinematicQueue=new List<WizardCygnusRequest>();
    private WizardCygnusCinematic wizardCygnusCinematic;
    private WizardCygnusCinematicStage wizardCygnusCinematicStage;

    private System.Collections.IEnumerator WarmupWizardCygnusCinematic()
    {
        Unit source=null;var species=new HashSet<string>();
        foreach(Unit unit in units)
            if(UsesWizardCygnusShower(unit)&&Rank3Renderer(unit)?.sprite!=null)
            {if(source==null)source=unit;species.Add(unit.definition.monsterId);}
        if(source==null||mainCamera==null)yield break;
        yield return GameResourceCache.WarmupTexture(WizardCygnusCinematicStage.ResourceRoot+"CygnusMeteorAction");
        PixelWizardCygnusMeteorShower.Warmup();
        Texture2D background=FrogCatchCurrentBackground();if(background==null)yield break;
        yield return GameResourceCache.WarmupTexture(WizardCygnusCinematicStage.ResourceRoot+"CygnusSkyAction");
        foreach(string id in species)
        {
            string resource=WizardCygnusCinematicStage.PoseResource(id);
            if(resource!=null)yield return GameResourceCache.WarmupTexture(resource);
        }
        if(wizardCygnusCinematicStage==null)wizardCygnusCinematicStage=new WizardCygnusCinematicStage(transform);
        foreach(string id in species){wizardCygnusCinematicStage.Warmup(id);yield return null;}
        SpriteRenderer renderer=Rank3Renderer(source);
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        try
        {
            if(wizardCygnusCinematicStage.Begin(background,FrogCatchActor(source,height,true),FrogCatchSceneryRegion()))
            {wizardCygnusCinematicStage.Tick(1.65f,0f);yield return null;}
        }
        finally{wizardCygnusCinematicStage.Hide();}
    }

    private bool IsWizardCygnusCinematicPending(Unit source)
    {
        if(wizardCygnusCinematic?.unit==source)return true;
        foreach(var request in wizardCygnusCinematicQueue)if(request.unit==source)return true;
        return false;
    }

    private bool QueueWizardCygnusCinematic(Unit source,WizardCygnusShowerState cast)
    {
        if(!UsesWizardCygnusShower(source)||!ShouldPresentCommonSkill(source)||mainCamera==null||
           !string.IsNullOrEmpty(resultText)||source.wizardCygnusCinematicGeneration==combatGeneration)return false;
        if(IsWizardCygnusCinematicPending(source))return true;
        var request=new WizardCygnusRequest{unit=source,cast=cast,generation=combatGeneration};
        wizardCygnusCinematicQueue.Add(request);return true;
    }

    private bool WizardCygnusRequestAlive(Unit unit,WizardCygnusShowerState cast,int generation)=>
        cast!=null&&generation==combatGeneration&&battleStarted&&!battleEnding&&IsCombatActive(unit)&&unit.hp>0f&&
        UsesWizardCygnusShower(unit)&&unit.wizardCygnusShower==cast&&!cast.released&&unit.positionRevision==cast.revision&&
        !unit.combatStatuses.Has(CombatStatusKind.Knockdown)&&!unit.combatStatuses.Has(CombatStatusKind.Fear)&&
        !unit.combatStatuses.Has(CombatStatusKind.Airborne)&&!SpiderSilkBound(unit)&&unit.giraffeStunTimer<=0f;

    private bool BeginWizardCygnusCinematic(WizardCygnusRequest request)
    {
        Unit unit=request.unit;
        if(wizardCygnusCinematic!=null||kingKongCinematic!=null||raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||
           frogCatchCinematic!=null||frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
           CombatSkillClock.Active||Time.timeScale<=0f||!WizardCygnusRequestAlive(unit,request.cast,request.generation)||
           !ShouldPresentCommonSkill(unit)||unit.combatStatuses.Has(CombatStatusKind.Freeze))return false;
        SpriteRenderer renderer=Rank3Renderer(unit);Texture2D background=FrogCatchCurrentBackground();
        if(renderer==null||renderer.sprite==null||background==null)return false;
        float height=PixelMantisReaper.VisibleBody(renderer.sprite).size.y*Mathf.Abs(renderer.transform.lossyScale.y);
        EndAllyPreparationSequence();EndSkillCameraPresentation();combatCameraDirector?.PrepareForCinematic();
        var state=new WizardCygnusCinematic{unit=unit,cast=request.cast,generation=request.generation,
            savedSpeed=Time.timeScale,fieldOfView=mainCamera.fieldOfView,lensShift=mainCamera.lensShift,
            projection=mainCamera.projectionMatrix,cameraPosition=mainCamera.transform.position,cameraRotation=mainCamera.transform.rotation,
            directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled,
            environment=layeredBattleEnvironment,environmentEnabled=layeredBattleEnvironment!=null&&layeredBattleEnvironment.enabled,
            sourceRenderer=renderer,sourceHidden=renderer.forceRenderingOff,clock=new CombatSkillClock(unit.model.transform)};
        wizardCygnusCinematic=state;wizardCygnusCinematicOwner=this;
        try
        {
            if(wizardCygnusCinematicStage==null)wizardCygnusCinematicStage=new WizardCygnusCinematicStage(transform);
            if(!wizardCygnusCinematicStage.Begin(background,FrogCatchActor(unit,height,true),FrogCatchSceneryRegion()))
            {FinishWizardCygnusCinematic(false);return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            if(state.environment!=null)state.environment.enabled=false;
            renderer.forceRenderingOff=true;state.clock.Hold();Time.timeScale=0f;
            unit.wizardCygnusCinematicGeneration=combatGeneration;
            wizardCygnusCinematicStage.Tick(0f,0f);
            BeginFamilyRank4CinematicAudio(unit,"wizard_mouse");return true;
        }
        catch(System.Exception exception)
        {
            FinishWizardCygnusCinematic(false);
            Debug.LogWarning("Cygnus stage unavailable; continuing the committed shower. "+exception.Message);return false;
        }
    }

    private bool UpdateWizardCygnusCinematic()
    {
        var state=wizardCygnusCinematic;
        if(state==null)
        {
            if(kingKongCinematic!=null||raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||
               frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||
               CombatSkillClock.Active||paused||GameFrontend.BlocksGameplay||Time.timeScale<=0f)return false;
            int count=wizardCygnusCinematicQueue.Count;
            while(count-->0)
            {
                var request=wizardCygnusCinematicQueue[0];wizardCygnusCinematicQueue.RemoveAt(0);
                if(!WizardCygnusRequestAlive(request.unit,request.cast,request.generation))continue;
                if(request.unit.combatStatuses.Has(CombatStatusKind.Freeze)){wizardCygnusCinematicQueue.Add(request);continue;}
                if(TryBeginRank4SkillPrelude(request.unit,request))
                {wizardCygnusCinematicQueue.Insert(0,request);return true;}
                if(BeginWizardCygnusCinematic(request))return true;
            }
            return false;
        }
        if(!WizardCygnusRequestAlive(state.unit,state.cast,state.generation)||paused||GameFrontend.BlocksGameplay||
           state.unit.combatStatuses.Has(CombatStatusKind.Freeze)||mainCamera==null||state.sourceRenderer==null||Time.timeScale!=0f)
        {FinishWizardCygnusCinematic(false);return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelWizardCygnusCinematic();paused=true;return true;}
        float dt=Mathf.Max(0f,Time.unscaledDeltaTime);state.elapsed+=dt;
        TickFamilyRank4CinematicAudio(state.elapsed);
        MatchWizardCygnusReturnView(state);
        if(state.elapsed>=WizardCygnusCinematicStage.Duration-.45f)state.sourceRenderer.forceRenderingOff=state.sourceHidden;
        wizardCygnusCinematicStage.Tick(state.elapsed,dt);
        if(state.elapsed<WizardCygnusCinematicStage.Duration)return true;
        FinishWizardCygnusCinematic(true);return false;
    }

    private void MatchWizardCygnusReturnView(WizardCygnusCinematic state)
    {
        var renderer=state.sourceRenderer;if(renderer==null||renderer.sprite==null||mainCamera==null)return;
        Bounds body=PixelMantisReaper.VisibleBody(renderer.sprite);
        Vector3 foot=new Vector3(body.center.x,body.min.y,0f),head=new Vector3(body.center.x,body.max.y,0f);
        if(renderer.flipX){foot.x=-foot.x;head.x=-head.x;}
        Vector3 low=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(foot));
        Vector3 high=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(head));
        wizardCygnusCinematicStage.SetReturnView(low,Mathf.Abs(high.y-low.y));
        Vector3 targetCenter=Vector3.zero;int count=0;
        foreach(Unit target in state.cast.targets)
            if(IsCombatActive(target)){targetCenter+=target.model.transform.position;count++;}
        if(count>0)wizardCygnusCinematicStage.SetMeteorReturnView(mainCamera.WorldToViewportPoint(targetCenter/count));
    }

    private void FinishWizardCygnusCinematic(bool completed)
    {
        var state=wizardCygnusCinematic;if(state==null)return;
        if(completed)EndFamilyRank4CinematicAudio();else CancelFamilyRank4CinematicAudio();
        wizardCygnusCinematic=null;if(wizardCygnusCinematicOwner==this)wizardCygnusCinematicOwner=null;
        if(state.sourceRenderer!=null)state.sourceRenderer.forceRenderingOff=state.sourceHidden;
        wizardCygnusCinematicStage?.Hide();state.clock.End();
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
        if(completed&&state.unit.wizardCygnusShower==state.cast)ReleaseWizardCygnusShower(state.unit);
    }

    private void CancelWizardCygnusCinematic()
    {wizardCygnusCinematicQueue.Clear();FinishWizardCygnusCinematic(false);}

    private void CancelWizardCygnusCinematicFor(Unit unit)
    {
        for(int i=wizardCygnusCinematicQueue.Count-1;i>=0;i--)
            if(wizardCygnusCinematicQueue[i].unit==unit)wizardCygnusCinematicQueue.RemoveAt(i);
        if(wizardCygnusCinematic?.unit==unit)FinishWizardCygnusCinematic(false);
    }

    private void DrawWizardCygnusCinematic()
    {
        if(Event.current.type!=EventType.Repaint||wizardCygnusCinematic==null||wizardCygnusCinematicStage?.Texture==null)return;
        Color previous=GUI.color;
        float alpha=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(wizardCygnusCinematic.elapsed/.12f))*
            (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(WizardCygnusCinematicStage.Duration-.45f,WizardCygnusCinematicStage.Duration,wizardCygnusCinematic.elapsed)));
        try
        {
            GUI.color=new Color(1f,1f,1f,alpha);
            GUI.DrawTexture(new Rect(0f,0f,BombBrickUiLayout.Width,BombBrickUiLayout.Height),wizardCygnusCinematicStage.Texture,ScaleMode.ScaleAndCrop,true);
        }
        finally{GUI.color=previous;}
    }
}
