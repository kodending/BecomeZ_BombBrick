using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;
using GUI = LocalizedGUI;

public sealed partial class MonsterAutoBattlePrototype
{
    private const float RaptorCinematicDuration=6f;
    private static MonsterAutoBattlePrototype raptorCinematicOwner;
    private static Texture2D raptorApexStage;
    private readonly List<Unit> raptorCinematicQueue=new List<Unit>();
    private RaptorApexCinematic raptorCinematic;
    private RaptorApexCinematicStage raptorCinematicStage;

    private sealed class RaptorApexCinematic
    {
        public Unit unit;
        public SpriteRenderer renderer;
        public Sprite pose;
        public float elapsed,savedSpeed,fieldOfView;
        public Vector2 lensShift,headNdc;
        public Matrix4x4 projection;
        public CombatSkillClock clock;
        public bool directorEnabled,environmentEnabled;
        public LayeredBattleEnvironment environment;
        public int phase;
    }

    private static void WarmupRaptorApexCinematic()
    {
        if(raptorApexStage==null)
            raptorApexStage=Resources.Load<Texture2D>("CombatCinematics/RaptorApex/ApexPrimordialStage");
        PixelRaptorApexWings.Warmup();
        RaptorApexCinematicStage.Warmup();
        WarmupSabertoothDevourCinematic();
    }

    private bool CanPresentRaptorApex(Unit unit)=>!mlEpisodeActive&&battleStarted&&!battleEnding&&
        !paused&&string.IsNullOrEmpty(resultText)&&!GameFrontend.BlocksGameplay&&
        RaptorApexPredatorActive(unit)&&IsCombatActive(unit)&&unit.spriteAnimation!=null&&
        unit.spiritConcertOwner==null&&!unit.augmentShadowClone&&unit.mantisBroodOwner==null;

    private void BeginRaptorPreludeAudio(Unit unit)
    {
        if(raptorCinematicStage==null)raptorCinematicStage=new RaptorApexCinematicStage(transform);
        raptorCinematicStage.PlayPrelude(!CombatEffectTestConfiguration.Active||
            !CombatEffectTestSceneController.MuteEnemySounds||unit.team==Team.Player);
    }

    private void QueueRaptorApexCinematic(Unit unit)
    {
        if(!CanPresentRaptorApex(unit)||unit.raptorApexCinematicGeneration==combatGeneration||
           raptorCinematic?.unit==unit||raptorCinematicQueue.Contains(unit))return;
        // Acquire the clock at the next Update, after every actor has finished this frame's simulation.
        raptorCinematicQueue.Add(unit);
    }

    private bool BeginRaptorApexCinematic(Unit unit)
    {
        if(raptorCinematic!=null||sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||CombatSkillClock.Active||!CanPresentRaptorApex(unit)||
           unit.raptorApexCinematicGeneration==combatGeneration||mainCamera==null||Time.timeScale<=0f)return false;
        SpriteRenderer renderer=unit.spriteAnimation.GetComponent<SpriteRenderer>();
        if(renderer==null||!renderer.enabled||renderer.sprite==null)return false;
        WarmupRaptorApexCinematic();
        if(raptorApexStage==null||RaptorApexCinematicStage.Frames(unit.definition.monsterId)==null)return false;
        if(raptorCinematicOwner!=null&&raptorCinematicOwner!=this)
            raptorCinematicOwner.CancelRaptorApexCinematic();
        EndAllyPreparationSequence();
        EndSkillCameraPresentation();
        combatCameraDirector?.PrepareForCinematic();
        var state=new RaptorApexCinematic
        {
            unit=unit,renderer=renderer,pose=renderer.sprite,savedSpeed=Time.timeScale,
            fieldOfView=mainCamera.fieldOfView,lensShift=mainCamera.lensShift,
            projection=mainCamera.projectionMatrix,directorEnabled=combatCameraDirector!=null&&combatCameraDirector.enabled,
            clock=new CombatSkillClock(unit.model.transform),
            environment=FindObjectOfType<LayeredBattleEnvironment>()
        };
        if(state.environment!=null)state.environmentEnabled=state.environment.enabled;
        raptorCinematic=state;raptorCinematicOwner=this;
        try
        {
            if(raptorCinematicStage==null)raptorCinematicStage=new RaptorApexCinematicStage(transform);
            if(!raptorCinematicStage.Begin(unit.definition.monsterId,renderer.flipX,
                unit.ownedInstance!=null&&unit.ownedInstance.isShiny,
                !CombatEffectTestConfiguration.Active||!CombatEffectTestSceneController.MuteEnemySounds||unit.team==Team.Player))
            {CancelRaptorApexCinematic();return false;}
            if(combatCameraDirector!=null)combatCameraDirector.enabled=false;
            // Director.OnDisable removes an applied shake/pan before the cinematic owns projection.
            mainCamera.fieldOfView=state.fieldOfView;mainCamera.lensShift=state.lensShift;
            mainCamera.projectionMatrix=state.projection;
            Bounds body=PixelMantisReaper.VisibleBody(state.pose);
            Vector3 head=body.center+new Vector3(body.size.x*.23f,body.size.y*.24f,0f);
            if(renderer.flipX)head.x=-head.x;
            if(renderer.flipY)head.y=-head.y;
            Vector3 viewport=mainCamera.WorldToViewportPoint(renderer.transform.TransformPoint(head));
            state.headNdc=new Vector2(viewport.x*2f-1f,viewport.y*2f-1f);
            if(state.environment!=null)state.environment.enabled=false;
            unit.raptorApexWings=PixelRaptorApexWings.Attach(renderer);
            unit.raptorApexWings.SetAwakened(true);unit.raptorApexWings.SetReveal(0f);
            state.clock.Hold();Time.timeScale=0f;
            unit.raptorApexCinematicGeneration=combatGeneration;
            return true;
        }
        catch
        {
            CancelRaptorApexCinematic();
            throw;
        }
    }

    private bool UpdateRaptorCinematic()
    {
        if(raptorCinematic==null)
        {
            if(sabertoothCinematic!=null||sabertoothCinematicReturn!=null||frogCatchCinematic!=null||frogCatchReturnRemaining>0f||crocodileAbyssCinematic!=null||crocodileAbyssReturnRemaining>0f||CombatSkillClock.Active)return false;
            while(raptorCinematicQueue.Count>0)
            {
                Unit next=raptorCinematicQueue[0];raptorCinematicQueue.RemoveAt(0);
                if(!CanPresentRaptorApex(next)||next.raptorApexCinematicGeneration==combatGeneration)continue;
                if(TryBeginRank4SkillPrelude(next,next))
                {raptorCinematicQueue.Insert(0,next);return true;}
                if(BeginRaptorApexCinematic(next))return true;
            }
            return false;
        }
        RaptorApexCinematic state=raptorCinematic;
        if(!CanPresentRaptorApex(state.unit)||mainCamera==null||state.renderer==null||Time.timeScale!=0f)
        {CancelRaptorApexCinematic();return false;}
        if(Input.GetKeyDown(KeyCode.Space))
        {Rank4CinematicAudio.StopAll();CancelRaptorApexCinematic();paused=true;return true;}
        state.elapsed+=Mathf.Max(0f,Time.unscaledDeltaTime);
        state.phase=state.elapsed<.65f?0:state.elapsed<1.35f?1:state.elapsed<1.95f?2:
            state.elapsed<2.45f?3:state.elapsed<4.5f?4:state.elapsed<5.25f?5:6;
        float enter=RaptorEase(.65f,1.35f,state.elapsed);
        // Return the battlefield lens while the stage still covers it.
        float leave=RaptorEase(5.08f,5.28f,state.elapsed);
        float amount=enter*(1f-leave),zoom=Mathf.Lerp(1f,7f,amount);
        Vector2 pan=state.headNdc*((1f-amount)-zoom);
        mainCamera.projectionMatrix=Matrix4x4.TRS(new Vector3(pan.x,pan.y,0f),
            Quaternion.identity,new Vector3(zoom,zoom,1f))*state.projection;
        raptorCinematicStage?.Tick(state.elapsed,Time.unscaledDeltaTime);
        state.unit.raptorApexWings?.SetReveal(RaptorEase(2.4f,3.45f,state.elapsed));
        if(state.elapsed<RaptorCinematicDuration)return true;
        FinishRaptorApexCinematic();
        return false;
    }

    public static void PrepareForRaptorCinematicGameplayPause()
    {
        Rank4CinematicAudio.StopAll();
        if(rank4SkillPreludeOwner!=null)rank4SkillPreludeOwner.CancelRank4SkillPrelude();
        if(wolfStampedeCinematicOwner!=null)wolfStampedeCinematicOwner.CancelWolfStampedeCinematic();
        if(bearAvatarCinematicOwner!=null)bearAvatarCinematicOwner.CancelBearAvatarCinematic();
        if(wizardCygnusCinematicOwner!=null)wizardCygnusCinematicOwner.CancelWizardCygnusCinematic();
        if(kingKongCinematicOwner!=null)kingKongCinematicOwner.CancelKingKongLeylineCinematic();
        if(raptorCinematicOwner!=null)raptorCinematicOwner.CancelRaptorApexCinematic();
        if(sabertoothCinematicOwner!=null)sabertoothCinematicOwner.CancelSabertoothCinematic();
        if(frogCatchCinematicOwner!=null)frogCatchCinematicOwner.CancelFrogCatchCinematic();
        if(crocodileAbyssCinematicOwner!=null)crocodileAbyssCinematicOwner.CancelCrocodileAbyssDiveCinematic();
    }

    private void CancelRaptorApexCinematic()
    {
        raptorCinematicStage?.CancelPresentation();
        CancelWolfStampedeCinematic();
        CancelBearAvatarCinematic();
        CancelWizardCygnusCinematic();
        CancelKingKongLeylineCinematic();
        CancelCrocodileAbyssDiveCinematic();
        CancelFrogCatchCinematic();
        CancelSabertoothCinematic();
        raptorCinematicQueue.Clear();
        FinishRaptorApexCinematic();
    }

    private void FinishRaptorApexCinematic()
    {
        RaptorApexCinematic state=raptorCinematic;
        if(state==null)return;
        raptorCinematic=null;
        if(raptorCinematicOwner==this)raptorCinematicOwner=null;
        raptorCinematicStage?.CompletePresentation();
        state.clock.End();
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
        if(state.unit.raptorApexWings!=null)
        {
            if(IsCombatActive(state.unit)&&RaptorApexPredatorActive(state.unit))state.unit.raptorApexWings.SetReveal(1f);
            else state.unit.raptorApexWings.Clear();
        }
    }

    private static float RaptorEase(float start,float end,float elapsed)=>
        Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(start,end,elapsed));

    private static float RaptorCinematicShadeAlpha(float elapsed,bool local)=>local
        ?.82f*RaptorEase(1.05f,1.6f,elapsed)*(1f-RaptorEase(5.35f,5.85f,elapsed))
        :.82f*(1f-RaptorEase(5.25f,6f,elapsed));

    private static float RaptorCinematicBodyScale(float elapsed,bool local)
    {
        if(local)return 1f;
        float stageOut=RaptorEase(5.35f,5.85f,elapsed);
        return Mathf.Lerp(1f,1.28f,RaptorEase(0f,.12f,elapsed))*(1f-stageOut)+stageOut;
    }

    private void DrawRaptorApexCinematic()
    {
        if(Event.current.type!=EventType.Repaint)return;
        RaptorApexCinematic state=raptorCinematic;
        if(state==null||state.renderer==null||mainCamera==null)return;
        float t=state.elapsed,w=BombBrickUiLayout.Width,h=BombBrickUiLayout.Height;
        Rect screen=new Rect(0f,0f,w,h);
        Color oldColor=GUI.color;
        try
        {
            float stageIn=RaptorEase(1.05f,1.6f,t),stageOut=RaptorEase(5.35f,5.85f,t);
            float stageAlpha=stageIn*(1f-stageOut);
            DrawRaptorFill(screen,new Color(.006f,.014f,.018f,
                RaptorCinematicShadeAlpha(t,CombatReadabilityPresentation.Local)));
            if(stageAlpha>0f)
            {
                GUI.color=new Color(1f,1f,1f,stageAlpha);
                Vector2 quake=raptorCinematicStage==null?Vector2.zero:raptorCinematicStage.CameraOffset;
                float pad=h*.018f,dx=-quake.x*h/6.8f,dy=quake.y*h/6.8f;
                GUI.DrawTexture(new Rect(-pad+dx,-pad+dy,w+pad*2f,h+pad*2f),raptorApexStage,ScaleMode.ScaleAndCrop,true);
                DrawRaptorFill(screen,new Color(.005f,.015f,.02f,.15f*stageAlpha));
            }
            float bars=.09f*h*RaptorEase(.65f,1.35f,t)*(1f-RaptorEase(5.4f,6f,t));
            if(t<1.55f)
            {
                if(TryGetSpriteBodyGuiRect(mainCamera,state.renderer,out Rect body))
                {
                    float emphasis=RaptorCinematicBodyScale(t,CombatReadabilityPresentation.Local);
                    body=new Rect(body.center-body.size*emphasis*.5f,body.size*emphasis);
                    float alpha=1f-RaptorEase(1.25f,1.55f,t);
                    DrawRaptorBody(state.pose,body,state.renderer.flipX,state.renderer.flipY,alpha);
                }
            }
            if(t>=1.95f&&t<5.5f&&raptorCinematicStage!=null)
            {
                GUI.color=new Color(1f,1f,1f,stageAlpha*RaptorEase(1.95f,2.25f,t));
                GUI.DrawTexture(screen,raptorCinematicStage.Texture,ScaleMode.StretchToFill,true);
            }
            if(t>=1.05f&&t<2.3f)
                DrawRaptorRetouchedEye(state,new Rect(0f,0f,w,h),
                    RaptorEase(1.05f,1.5f,t)*(1f-RaptorEase(1.95f,2.3f,t)));
            if(t>=4.7f&&t<5.25f)DrawRaptorRushLines(w,h,RaptorEase(4.7f,5.2f,t));
            DrawRaptorFill(new Rect(0f,0f,w,bars),Color.black);
            DrawRaptorFill(new Rect(0f,h-bars,w,bars),Color.black);
            float flash=Mathf.Max(0f,1f-Mathf.Abs(t-5.25f)/.085f)*.4f;
            if(flash>0f)DrawRaptorFill(screen,new Color(1f,.94f,.75f,flash));
        }
        finally{GUI.color=oldColor;}
    }

    private void DrawRaptorRetouchedEye(RaptorApexCinematic state,Rect band,float alpha)
    {
        if(alpha<=0f)return;
        Texture portrait=raptorCinematicStage?.EyeTexture;
        if(portrait!=null)
        {
            // Keep the portrait's neck outside the screen; the full-body stage camera pulls back.
            float width=band.width*.95f;
            Vector2 center=new Vector2(band.width*.5f,band.height*.43f);
            float height=width*portrait.height/portrait.width;
            Rect face=new Rect(center-new Vector2(width,height)*.5f,new Vector2(width,height));
            Rect uv=new Rect(0f,0f,1f,1f);
            if(state.renderer.flipX){uv.x=1f;uv.width=-1f;}
            GUI.color=new Color(1f,1f,1f,alpha);
            GUI.DrawTextureWithTexCoords(face,portrait,uv,true);
        }
        else
        {
            Sprite pose=RaptorApexCinematicStage.Frames(state.unit.definition.monsterId)[0];
            Bounds body=PixelMantisReaper.VisibleBody(pose);
            float height=band.height*2.3f,width=height*body.size.x/Mathf.Max(.001f,body.size.y);
            float eyeX=state.renderer.flipX?.3f:.7f;
            UnityEngine.GUI.BeginGroup(band);
            try{DrawRaptorBody(pose,new Rect(band.width*.5f-width*eyeX,band.height*.52f-height*.25f,width,height),
                state.renderer.flipX,state.renderer.flipY,alpha);}
            finally{UnityEngine.GUI.EndGroup();}
        }
    }

    private static Rect RaptorBodyUv(Sprite sprite,bool flipX,bool flipY)
    {
        Bounds body=PixelMantisReaper.VisibleBody(sprite);
        Rect texture=sprite.textureRect;
        float ppu=sprite.pixelsPerUnit;
        Rect uv=new Rect((texture.x+sprite.pivot.x+body.min.x*ppu)/sprite.texture.width,
            (texture.y+sprite.pivot.y+body.min.y*ppu)/sprite.texture.height,
            body.size.x*ppu/sprite.texture.width,body.size.y*ppu/sprite.texture.height);
        if(flipX){uv.x+=uv.width;uv.width=-uv.width;}
        if(flipY){uv.y+=uv.height;uv.height=-uv.height;}
        return uv;
    }

    private static void DrawRaptorBody(Sprite sprite,Rect rect,bool flipX,bool flipY,float alpha)
    {
        if(sprite==null)return;
        Rect uv=RaptorBodyUv(sprite,flipX,flipY);
        float outline=Mathf.Clamp(rect.height*.007f,2f,5f);
        GUI.color=new Color(1f,.82f,.29f,.38f*alpha);
        GUI.DrawTextureWithTexCoords(new Rect(rect.x-outline,rect.y,rect.width,rect.height),sprite.texture,uv,true);
        GUI.DrawTextureWithTexCoords(new Rect(rect.x+outline,rect.y,rect.width,rect.height),sprite.texture,uv,true);
        GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.y-outline,rect.width,rect.height),sprite.texture,uv,true);
        GUI.color=new Color(1f,1f,1f,alpha);
        GUI.DrawTextureWithTexCoords(rect,sprite.texture,uv,true);
    }

    private static void DrawRaptorRushLines(float width,float height,float strength)
    {
        for(int i=0;i<8;i++)
        {
            bool left=i%2==0;
            float y=height*(.16f+i*.085f),length=width*(.08f+strength*.15f);
            DrawRaptorFill(new Rect(left?0f:width-length,y,length,2f),new Color(1f,.9f,.56f,strength*.48f));
        }
    }

    private static void DrawRaptorFill(Rect rect,Color color)
    {GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);}
}
