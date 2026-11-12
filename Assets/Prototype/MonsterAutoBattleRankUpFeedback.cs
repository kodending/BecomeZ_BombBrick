using System;
using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class RankUpPulse
    {
        public int rank,previousRank,soundMask;
        public float fromScale=1f;
        public RenderTexture cardSource,cardSilhouette;
        public Material cardMaterial;
        public float startedAt=-1f;
        public Unit unit;
        public MonsterRankUpFeedback worldEffect;
        public bool soundFromWorld;
    }
    private readonly Dictionary<string,int> observedMonsterRanks=new Dictionary<string,int>(StringComparer.Ordinal);
    private readonly Dictionary<string,RankUpPulse> monsterRankUpPulses=new Dictionary<string,RankUpPulse>(StringComparer.Ordinal);
    private readonly HashSet<string> observedRankUpUnits=new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> expiredRankUpPulses=new List<string>();
    private string rankUpFeedbackRunId;
    private bool rankUpFeedbackInitialized;

    public bool PreviewCombatEffectTestEvolution(MonsterDefinition definition,int rank)
    {
        if(!CombatEffectTestConfiguration.Active||!CanPresentRankUpFeedback||
           definition==null||rank<2||rank>4)return false;
        Unit unit=units.Find(value=>value!=null&&value.team==Team.Player&&value.alive&&
            value.definition!=null&&value.definition.monsterId==definition.monsterId&&
            ActiveRankUpRenderer(Rank3Renderer(value)));
        if(unit==null||string.IsNullOrEmpty(unit.stableId))return false;
        if(!rankUpFeedbackInitialized||rankUpFeedbackRunId!=expeditionRun?.runId)ResetMonsterRankUpFeedback();
        QueueMonsterRankUpFeedback(unit.stableId,unit,rank,rank-1);
        return true;
    }

    private static bool ActiveRankUpRenderer(SpriteRenderer renderer)=>
        renderer!=null&&renderer.sprite!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy;

    private void QueueMonsterRankUpFeedback(string instanceId,Unit unit,int rank,int previousRank)
    {
        if(monsterRankUpPulses.TryGetValue(instanceId,out RankUpPulse previous))ClearRankUpWorldEffect(previous);
        float oldScale=RankVisualScale((ExpeditionMonsterRank)Mathf.Min(previousRank,3));
        float newScale=RankVisualScale((ExpeditionMonsterRank)Mathf.Min(rank,3))*(rank==4?1.1f:1f);
        monsterRankUpPulses[instanceId]=new RankUpPulse{rank=rank,previousRank=previousRank,unit=unit,fromScale=oldScale/newScale};
    }

    private void ObserveMonsterRankUpWithScale(Unit unit,float previousScale)
    {
        monsterRankUpPulses.TryGetValue(unit.stableId??"",out RankUpPulse before);
        ObserveMonsterRankUpFeedback(unit);
        if(monsterRankUpPulses.TryGetValue(unit.stableId??"",out RankUpPulse after)&&after!=before&&previousScale>0f)
            after.fromScale=Mathf.Clamp(previousScale/Mathf.Max(.01f,unit.combatBodyScale),.1f,2f);
    }

    private static void ClearRankUpWorldEffect(RankUpPulse pulse)
    {
        if(pulse.worldEffect!=null)
            pulse.worldEffect.Cancel();
        foreach(var texture in new[]{pulse.cardSource,pulse.cardSilhouette})
            if(texture!=null){texture.Release();if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);}
        if(pulse.cardMaterial!=null){if(Application.isPlaying)Destroy(pulse.cardMaterial);else DestroyImmediate(pulse.cardMaterial);}
    }

    private void ResetMonsterRankUpFeedback()
    {
        foreach(var pulse in monsterRankUpPulses.Values)ClearRankUpWorldEffect(pulse);
        observedMonsterRanks.Clear();monsterRankUpPulses.Clear();observedRankUpUnits.Clear();
        rankUpFeedbackRunId=expeditionRun?.runId;rankUpFeedbackInitialized=true;
        if(expeditionRun?.monsters==null)return;
        foreach(var state in expeditionRun.monsters)
            if(state!=null&&!string.IsNullOrEmpty(state.instanceId))
                observedMonsterRanks[state.instanceId]=state.transcended?4:Mathf.Clamp((int)state.rank,1,3);
    }

    private void ObserveMonsterRankUpFeedback(Unit unit)
    {
        if(unit==null||unit.team!=Team.Player||expeditionRun==null||string.IsNullOrEmpty(unit.stableId))return;
        if(!rankUpFeedbackInitialized||rankUpFeedbackRunId!=expeditionRun.runId)ResetMonsterRankUpFeedback();
        ObserveRankUpFeedbackState(unit.stableId,unit,observedRankUpUnits.Add(unit.stableId));
    }

    private void ObserveRankUpFeedbackState(string instanceId,Unit unit,bool baseline=false)
    {
        if(expeditionRun==null)return;
        if(!rankUpFeedbackInitialized||rankUpFeedbackRunId!=expeditionRun.runId)ResetMonsterRankUpFeedback();
        ExpeditionMonsterStateSave state=expeditionRun.monsters?.Find(value=>value!=null&&value.instanceId==instanceId);
        if(state==null)return;
        int rank=(unit!=null&&unit.augmentTranscended)||state.transcended?4:Mathf.Clamp((int)state.rank,1,3);
        bool known=observedMonsterRanks.TryGetValue(instanceId,out int previous);
        observedMonsterRanks[instanceId]=rank;
        if(!known||baseline||rank<=previous||mlEpisodeActive)return;
        QueueMonsterRankUpFeedback(instanceId,unit,rank,previous);
    }

    private bool CanPresentRankUpFeedback=>!mlEpisodeActive&&!battleStarted&&
        !wavePreparationAcrossFrames&&!GameFrontend.BlocksGuiInput;

    private static void StartMonsterRankUpPulse(RankUpPulse pulse,float now)
    {
        if(pulse.startedAt>=0f)return;
        pulse.startedAt=now;
        pulse.worldEffect=MonsterRankUpFeedback.Play(Rank3Renderer(pulse.unit),pulse.rank);
        pulse.soundFromWorld=pulse.worldEffect!=null;
        if(pulse.worldEffect!=null)pulse.worldEffect.StartingScale=pulse.fromScale;
    }

    private void DrawMonsterRankUpFeedback(Rect portrait,string instanceId)
    {
        if(string.IsNullOrEmpty(instanceId)||expeditionRun==null)return;
        Unit unit=units.Find(value=>value!=null&&value.team==Team.Player&&value.stableId==instanceId);
        ObserveRankUpFeedbackState(instanceId,unit);
        if(Event.current==null||Event.current.type!=EventType.Repaint||!CanPresentRankUpFeedback||
           !monsterRankUpPulses.TryGetValue(instanceId,out RankUpPulse pulse))return;
        StartMonsterRankUpPulse(pulse,Time.unscaledTime);
        float age=Time.unscaledTime-pulse.startedAt;
        if(age>=MonsterEvolutionCelebration.DurationFor(pulse.rank))return;
        float scale=Mathf.Lerp(pulse.fromScale,1f,MonsterEvolutionCelebration.GrowthProgress(pulse.rank,age))*
            MonsterEvolutionCelebration.BodyScale(pulse.rank,age);
        Rect expanded=new Rect(portrait.center.x-portrait.width*scale*.5f,portrait.yMax-portrait.height*scale,
            portrait.width*scale,portrait.height*scale);
        MonsterRankUpFeedback.Draw(expanded,pulse.rank,age);
        if(!pulse.soundFromWorld)MonsterRankUpFeedback.PlaySounds(pulse.rank,age,ref pulse.soundMask);
    }

    private void PresentMonsterRankUpFeedback(float now)
    {
        expiredRankUpPulses.Clear();
        foreach(var pair in monsterRankUpPulses)
        {
            RankUpPulse pulse=pair.Value;
            if(pulse.startedAt<0f)
            {
                // A covered or stored monster waits for its existing card portrait to be painted.
                if(!CanPresentRankUpFeedback||GrowthShopVisible||AugmentDraftVisible||shopVisible||
                   !ActiveRankUpRenderer(Rank3Renderer(pulse.unit)))continue;
                StartMonsterRankUpPulse(pulse,now);
            }
            float age=now-pulse.startedAt;
            if(age>=MonsterEvolutionCelebration.DurationFor(pulse.rank))expiredRankUpPulses.Add(pair.Key);
            else if(!pulse.soundFromWorld)MonsterRankUpFeedback.PlaySounds(pulse.rank,age,ref pulse.soundMask);
        }
        foreach(string id in expiredRankUpPulses)
        {ClearRankUpWorldEffect(monsterRankUpPulses[id]);monsterRankUpPulses.Remove(id);}
    }

    private SpriteRenderer RankUpHudRenderer(Unit unit)
    {
        if(unit!=null&&!string.IsNullOrEmpty(unit.stableId)&&
           monsterRankUpPulses.TryGetValue(unit.stableId,out RankUpPulse pulse)&&pulse.worldEffect!=null)
            return pulse.worldEffect.BodyRenderer;
        return Rank3Renderer(unit);
    }

    private int RankUpPresentationStarCount(Unit unit,out float stampScale)
    {
        stampScale=1f;
        if(unit==null)return 0;
        if(!string.IsNullOrEmpty(unit.stableId)&&monsterRankUpPulses.TryGetValue(unit.stableId,out RankUpPulse pulse))
        {
            float age=pulse.startedAt<0f?-1f:Time.unscaledTime-pulse.startedAt;
            if(age<MonsterEvolutionCelebration.DurationFor(pulse.rank))
            {
                stampScale=age<0f?0f:MonsterEvolutionCelebration.StampScale(pulse.rank,age);
                return stampScale<=0f?0:pulse.rank;
            }
        }
        var state=unit.team==Team.Player?expeditionRun?.monsters?.Find(value=>value!=null&&value.instanceId==unit.stableId):unit.enemyProgression;
        int rank=state!=null?DisplayStarCount(state):unit.augmentTranscended?4:(int)unit.expeditionRank;
        return rank>=2||unit==hoveredUnit?Mathf.Max(1,rank):0;
    }

    private int PresentationStarCount(ExpeditionMonsterStateSave state)
    {
        if(state!=null&&state.instanceId!=null&&monsterRankUpPulses.TryGetValue(state.instanceId,out RankUpPulse pulse))
        {
            if(pulse.startedAt<0f||Time.unscaledTime-pulse.startedAt<MonsterEvolutionCelebration.StampAt(pulse.rank))return pulse.previousRank;
            if(Time.unscaledTime-pulse.startedAt<MonsterEvolutionCelebration.DurationFor(pulse.rank))return pulse.rank;
        }
        return DisplayStarCount(state);
    }

    private bool DrawEvolutionPortrait(Rect rect,Sprite sprite,string instanceId)
    {
        if(string.IsNullOrEmpty(instanceId)||expeditionRun==null)return false;
        var unit=units.Find(value=>value!=null&&value.team==Team.Player&&value.stableId==instanceId);
        ObserveRankUpFeedbackState(instanceId,unit);
        if(Event.current.type!=EventType.Repaint||!CanPresentRankUpFeedback||
           !monsterRankUpPulses.TryGetValue(instanceId,out RankUpPulse pulse))return false;
        StartMonsterRankUpPulse(pulse,Time.unscaledTime);
        float age=Time.unscaledTime-pulse.startedAt;
        if(age>=MonsterEvolutionCelebration.DurationFor(pulse.rank))return false;
        Rect texture=sprite.textureRect;
        Rect uv=new Rect(texture.x/sprite.texture.width,texture.y/sprite.texture.height,texture.width/sprite.texture.width,texture.height/sprite.texture.height);
        Bounds body=PixelMantisReaper.VisibleBody(sprite);
        Vector2 foot=new Vector2(rect.x+(sprite.pivot.x+body.center.x*sprite.pixelsPerUnit)/sprite.rect.width*rect.width,
            rect.y+(1f-(sprite.pivot.y+body.min.y*sprite.pixelsPerUnit)/sprite.rect.height)*rect.height);
        float scale=Mathf.Lerp(pulse.fromScale,1f,MonsterEvolutionCelebration.GrowthProgress(pulse.rank,age))*MonsterEvolutionCelebration.BodyScale(pulse.rank,age);
        Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;
        try
        {
            GUIUtility.ScaleAroundPivot(Vector2.one*scale,foot);
            GUI.DrawTextureWithTexCoords(rect,sprite.texture,uv,true);
            float alpha=MonsterEvolutionCelebration.SilhouetteAlpha(pulse.rank,age);
            if(alpha>0f)
            {
                if(pulse.cardMaterial==null)
                {
                    var shader=Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette");
                    if(shader==null)return true;
                    pulse.cardMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                    pulse.cardSource=NewEvolutionCardTexture();pulse.cardSilhouette=NewEvolutionCardTexture();
                }
                Color tint=MonsterRankUpFeedback.RankColor(pulse.rank);
                float hit=age-MonsterEvolutionCelebration.ImpactAt(pulse.rank);
                if(hit>=0f)tint=Color.Lerp(Color.white,tint,Mathf.Clamp01(hit/.12f));
                tint=CombatFlashPresentation.Peak(tint);tint.a=alpha;
                pulse.cardMaterial.color=tint;
                var previous=RenderTexture.active;
                try
                {
                    Graphics.Blit(sprite.texture,pulse.cardSource,uv.size,uv.position);
                    RenderTexture.active=pulse.cardSilhouette;GL.Clear(true,true,Color.clear);
                    Graphics.Blit(pulse.cardSource,pulse.cardSilhouette,pulse.cardMaterial);
                }
                finally{RenderTexture.active=previous;}
                GUI.color=Color.white;GUI.DrawTexture(rect,pulse.cardSilhouette,ScaleMode.StretchToFill,true);
            }
        }
        finally{GUI.matrix=matrix;GUI.color=color;}
        return true;
    }

    private static RenderTexture NewEvolutionCardTexture()
    {
        var texture=new RenderTexture(128,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB)
            {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.Create();return texture;
    }

    private void DrawWorldMonsterRankUpFeedback()
    {
        if(Event.current!=null&&Event.current.type==EventType.Repaint)
            PresentMonsterRankUpFeedback(Time.unscaledTime);
    }
}
