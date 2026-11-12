using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MantisGuillotineState
    {
        public Vector3 center,forward;
        public float time,scale,length,width;
        public int generation,revision,launched,landed;
        public bool killed,detached,finished;
        public readonly HashSet<Unit> firstHit=new HashSet<Unit>();
        public readonly PixelMantisGroundCut[] cuts=new PixelMantisGroundCut[2];
    }

    private bool MantisGuillotineValid(Unit source,MantisGuillotineState cast)=>
        !cast.finished&&ReleasedSkillValid(source,cast.generation)&&(cast.launched>0||source.mantisGuillotine==cast&&IsCombatActive(source)&&
        source.positionRevision==cast.revision&&!SpiderSilkBound(source)&&
        !source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&source.giraffeStunTimer<=0f);

    private void StartMantisGuillotine(Unit source,Unit target,float scale,float radiusScale,int generation)
    {
        if(source.mantisGuillotine!=null||!IsCombatActive(source)||!IsCombatActive(target))return;
        Vector3 center=Flat(target.model.transform.position);
        Vector3 forward=center-Flat(source.model.transform.position);
        if(forward.sqrMagnitude<.001f)forward=Vector3.right;
        float size=radiusScale*(HasRank3(source,MantisCombatRules.SantiusTraitId)?1.5f:1f);
        var cast=new MantisGuillotineState {center=center,forward=forward.normalized,scale=scale,
            length=TileSize*MantisRank3Rules.GuillotineLengthTiles*size,
            width=TileSize*MantisRank3Rules.GuillotineWidthTiles*size,
            generation=generation,revision=source.positionRevision};
        source.spriteAnimation?.InterruptCurrentAction(true);
        source.mantisGuillotine=cast;source.mantisScythePending=false;source.mantisScytheTarget=null;
        source.mantisScytheDamageMultiplier=1f;source.mantisScytheRadiusMultiplier=1f;
        ClearRoute(source);source.hexStepping=false;
        source.velocity=Vector3.zero;SetMoving(source,false);
        PlayMantisSound(source,MantisSound.Jump,Rank3Center(source));
        if(source.spriteAnimation!=null)
            source.spriteAnimation.PlayMantisGuillotine(()=>MantisGuillotineValid(source,cast),
                time=>{if(!cast.detached)AdvanceMantisGuillotine(source,cast,time);},()=>EndMantisGuillotine(source,cast));
        else if(Application.isPlaying)StartCombatCoroutine(source,MantisGuillotineFallback(source,cast));
    }

    private IEnumerator MantisGuillotineFallback(Unit source,MantisGuillotineState cast)
    {
        while(!cast.detached&&MantisGuillotineValid(source,cast)&&cast.time<MantisRank3Rules.GuillotineEnd)
        {
            if(!source.combatStatuses.Has(CombatStatusKind.Freeze))
                AdvanceMantisGuillotine(source,cast,cast.time+CombatSkillClock.Delta*
                    RaptorCombatRules.IceSpeedMultiplier(source.raptorSlowTimer>0f)*
                    (source.rank3ActionSlowTimer>0f?1f-source.rank3ActionSlowRatio:1f));
            yield return null;
        }
        EndMantisGuillotine(source,cast);
    }

    private Color MantisGuillotineColor(Unit source,int cut,bool echo=false)=>echo?
        new Color(.75f,.35f,1f):HasRank3(source,MantisCombatRules.SantiusTraitId)?new Color(1f,.78f,.3f):
        cut==1&&HasRank3(source,MantisCombatRules.MantiusTraitId)?new Color(1f,.94f,.55f):new Color(.6f,1f,.42f);

    private void AdvanceMantisGuillotine(Unit source,MantisGuillotineState cast,float time)
    {
        if(!MantisGuillotineValid(source,cast))return;
        cast.time=Mathf.Max(cast.time,time);time=cast.time;
        if(source.mantisGuillotine==cast){source.attackMovementLockTimer=.1f;source.currentAction="mantis_aerial_guillotine";}
        float rise=Mathf.Sin(Mathf.Clamp01((time-.06f)/.24f)*Mathf.PI*.5f);
        float fall=1f-Mathf.Pow(Mathf.Clamp01((time-.59f)/.25f),2f);
        float lift=TileSize*1.35f*rise*fall;
        float bank=time<.3f?-12f*Mathf.Sin(time/.3f*Mathf.PI):time<.42f?12f:time<.62f?-10f:0f;
        if(source.mantisGuillotine==cast&&IsCombatActive(source)&&source.giraffeStunTimer<=0f&&
           !source.combatStatuses.Has(CombatStatusKind.Freeze))
            source.spriteBillboard?.SetRollPose(bank,Vector3.zero,Vector3.up*lift);
        for(int i=0;i<2;i++)
        {
            float launch=MantisRank3Rules.GuillotineLaunch+i*MantisRank3Rules.GuillotineInterval;
            float contact=MantisRank3Rules.GuillotineLaunch+MantisRank3Rules.GuillotineFlight+
                i*MantisRank3Rules.GuillotineInterval;
            Vector3 direction=MantisRank3Rules.GuillotineDirection(cast.forward,i);
            Color color=MantisGuillotineColor(source,i);
            if(cast.launched<=i&&time>=launch)
            {
                cast.launched=i+1;
                if(!cast.detached&&isActiveAndEnabled)
                {cast.detached=true;StartReleasedSkill(source,ContinueMantisGuillotine(source,cast));}
                PlayMantisSound(source,MantisSound.Descent,Rank3Center(source));
                if(!mlEpisodeActive)
                {
                    // The blade travels from the raised caster to the locked ground location.
                    Vector3 sky=Flat(source.model.transform.position)+Vector3.up*(source.visualHeightOffset+TileSize*1.7f);
                    cast.cuts[i]=PixelMantisGroundCut.Play(sky,cast.center,direction,cast.length,cast.width,color,Rank3Renderer(source));
                    PixelCombatEffect.PlayMantisScytheSlash(Rank3Renderer(source)?.transform??source.model.transform,
                        Rank3Renderer(source),2.5f,i==1,1.1f,color);
                }
            }
            cast.cuts[i]?.Advance(time>=contact?Mathf.Max(MantisRank3Rules.GuillotineFlight,time-launch):time-launch);
            if(cast.landed<=i&&time>=contact)
            {
                cast.landed=i+1;
                PlayMantisSound(source,MantisSound.Ground,cast.center,i==0?.85f:1f);
                foreach(Unit enemy in units.ToArray())
                {
                    if(!IsCombatTargetable(enemy)||enemy.team==source.team||
                       !MantisRank3Rules.InsideGuillotine(Flat(enemy.model.transform.position),cast.center,
                           direction,cast.length,cast.width,enemy.collisionRadius))continue;
                    bool doubled=i==1&&cast.firstHit.Contains(enemy);
                    if(i==0)cast.firstHit.Add(enemy);
                    Rank3Hit(source,enemy,MantisCombatRules.HitDamage(1f,MantisRank3Rules.GuillotineBladeRatio,
                        i,HasRank3(source,MantisCombatRules.MantiusTraitId))*cast.scale);
                    cast.killed|=!IsCombatActive(enemy);
                    if(doubled&&IsCombatActive(enemy))ApplyMantisDoubleCutStatus(source,enemy);
                    if(!mlEpisodeActive&&enemy.model!=null)
                    {
                        PixelCombatEffect.PlayMantisCutImpact(enemy.model.transform,Rank3Renderer(enemy),color,doubled);
                        enemy.spriteAnimation?.PlayImpactRecoil(direction,doubled?.065f:.04f);
                    }
                    if(!MantisGuillotineValid(source,cast))return;
                }
                if(!mlEpisodeActive&&i==1)combatCameraDirector?.ReportJawCrush();
            }
        }
    }

    private void ApplyMantisDoubleCutStatus(Unit source,Unit target)
    {
        target.combatStatuses.ApplyStacks(CombatStatusKind.Bleed,3f,1,1,source,
            MantisCombatRules.BleedTickDamage(source.damage),MantisCombatRules.BleedTickInterval);
        if(!HasRank3(source,MantisCombatRules.FlotisTraitId))return;
        target.combatStatuses.ApplyTimed(CombatStatusKind.HallucinatoryPollen,
            HostileStatusDuration(target,MantisCombatRules.PollenDuration),source);
        if(!mlEpisodeActive)PixelCombatEffect.PlayMantisHallucinatoryPollen(target.model.transform,
            Rank3Renderer(target),MantisCombatRules.PollenDuration);
    }

    private void EndMantisGuillotine(Unit source,MantisGuillotineState cast)
    {
        if(cast.finished)return;
        if(cast.launched>0&&ReleasedSkillValid(source,cast.generation)&&cast.time<MantisRank3Rules.GuillotineEnd)
        {
            if(source.mantisGuillotine==cast)
            {source.mantisGuillotine=null;source.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);source.attackMovementLockTimer=0f;}
            return;
        }
        bool complete=MantisGuillotineValid(source,cast)&&cast.time>=MantisRank3Rules.GuillotineEnd;
        cast.finished=true;
        foreach(var cut in cast.cuts)if(cut!=null){if(complete)cut.Release();else cut.Cancel();}
        if(source.mantisGuillotine==cast)
        {source.mantisGuillotine=null;source.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);source.attackMovementLockTimer=0f;source.decisionTimer=0f;}
        if(!complete)return;
        source.currentAction="rank3_mantis_complete";source.traitActivations++;
        RecordSkillEvent(source,MantisRank3Rules.DivineGuillotine,source.skillHits,source.skillDamage);
        if(cast.killed&&HasRank3(source,MantisCombatRules.NectisTraitId)&&Application.isPlaying)
            StartReleasedSkill(source,MantisGuillotineEcho(source,cast));
    }

    private IEnumerator ContinueMantisGuillotine(Unit source,MantisGuillotineState cast)
    {
        try
        {
            while(MantisGuillotineValid(source,cast)&&cast.time<MantisRank3Rules.GuillotineEnd)
            {yield return null;if(!paused)AdvanceMantisGuillotine(source,cast,cast.time+CombatSkillClock.Delta);}
        }
        finally{EndMantisGuillotine(source,cast);}
    }

    private IEnumerator MantisGuillotineEcho(Unit source,MantisGuillotineState cast)
    {
        if(battleEnding)yield break;
        Unit target=Rank3Enemies(source,cast.center,TileSize*7f).OrderBy(x=>
            (Flat(x.model.transform.position)-cast.center).sqrMagnitude).FirstOrDefault();
        if(target==null)yield break;
        Vector3 center=Flat(target.model.transform.position),direction=MantisRank3Rules.GuillotineDirection(cast.forward,1);
        var fx=mlEpisodeActive?null:PixelMantisGroundCut.Play(center-cast.forward*TileSize+Vector3.up*TileSize*2f,
            center,direction,cast.length*.65f,cast.width*.65f,MantisGuillotineColor(source,1,true),Rank3Renderer(source));
        PlayMantisSound(source,MantisSound.Descent,center,.65f);
        try
        {
            float age=0f;
            while(age<MantisRank3Rules.GuillotineFlight)
            {
                if(!ReleasedSkillValid(source,cast.generation))yield break;
                if(!paused){age+=CombatSkillClock.Delta;fx?.Advance(age);}yield return null;
            }
            if(!ReleasedSkillValid(source,cast.generation))yield break;
            PlayMantisSound(source,MantisSound.Ground,center,.65f);
            foreach(Unit enemy in units.ToArray())
                if(IsCombatTargetable(enemy)&&enemy.team!=source.team&&MantisRank3Rules.InsideGuillotine(
                    Flat(enemy.model.transform.position),center,direction,cast.length*.65f,cast.width*.65f,enemy.collisionRadius))
                {
                    Rank3Hit(source,enemy,MantisRank3Rules.GuillotineBladeRatio*.6f*cast.scale);
                    if(!mlEpisodeActive)PixelCombatEffect.PlayMantisCutImpact(enemy.model.transform,Rank3Renderer(enemy),MantisGuillotineColor(source,1,true),false);
                }
            fx?.Release();fx=null;
        }
        finally {if(fx!=null)fx.Cancel();}
    }
}
