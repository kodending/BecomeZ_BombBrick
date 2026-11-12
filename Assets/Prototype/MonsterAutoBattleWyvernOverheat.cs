using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool InWyvernOverheatRange(WyvernBreathCast cast,Unit enemy)=>
        IsCombatTargetable(enemy)&&enemy.team!=cast.source.team&&
        Vector3.Distance(Flat(enemy.model.transform.position),cast.origin)<=cast.length+enemy.collisionRadius;

    private void AimWyvernOverheat(WyvernBreathCast cast,float dt)
    {
        if(!InWyvernOverheatRange(cast,cast.target))
            cast.target=units.Where(enemy=>InWyvernOverheatRange(cast,enemy))
                .OrderBy(enemy=>(Flat(enemy.model.transform.position)-cast.origin).sqrMagnitude)
                .ThenBy(enemy=>enemy.id).FirstOrDefault();
        if(cast.target==null)return;
        Vector3 aim=Flat(cast.target.model.transform.position)-cast.origin;
        if(aim.sqrMagnitude<.001f)return;
        cast.direction=Vector3.RotateTowards(cast.direction,aim.normalized,
            WyvernRank3Rules.CoreTurnDegrees*Mathf.Deg2Rad*dt,0f).normalized;
        cast.source.target=cast.target;
        RotateUnit(cast.source,cast.direction,1f);
    }

    private void AdvanceWyvernOverheat(WyvernBreathCast cast)
    {
        Unit source=cast.source;source.velocity=Vector3.zero;SetMoving(source,false);
        float end=cast.windup+WyvernRank3Rules.CoreDuration;
        float until=Mathf.Min(cast.time,end);cast.time=cast.previousTime;
        // Retarget between contacts even when a slow frame crosses several hit times.
        while(WyvernBreathValid(cast)&&cast.time<until)
        {
            float dt=Mathf.Min(1f/60f,until-cast.time);cast.time+=dt;
            AimWyvernOverheat(cast,dt);
            if(!cast.launched&&cast.time>=cast.windup)
            {
                cast.launched=true;source.currentAction="wyvern_core_overheat";
                SetWyvernBreathSound(cast,true);
                source.spriteAnimation?.HoldSkillAttackHitFrame(WyvernRank3Rules.CoreDuration);
            }
            while(WyvernBreathValid(cast)&&cast.nextHit<WyvernRank3Rules.CoreHits&&
                cast.time+.00001f>=cast.windup+WyvernRank3Rules.CoreHitTime(cast.nextHit))
            {
                ResolveWyvernOverheatHit(cast,cast.nextHit);
                cast.nextHit++;
            }
        }
        if(cast.finished)return;
        float age=cast.time-cast.windup,heat=Mathf.Clamp01(age/WyvernRank3Rules.CoreDuration);
        float recoil=age<0f?-Mathf.Clamp01(cast.time/cast.windup):
            (.25f+.5f*heat)*(1f+Mathf.Sin(age*30f)*.15f);
        source.spriteBillboard?.SetRollPose(Mathf.Sign(cast.direction.x)*5f*recoil,
            Vector3.zero,-cast.direction*(.08f*recoil));
        cast.effect?.Pose(cast.time,cast.direction);
        if(cast.nextHit<WyvernRank3Rules.CoreHits)return;
        RecordSkillEvent(source,WyvernRank3Rules.DragonCore,cast.hits,cast.damage);
        source.traitActivations++;EndWyvernBreath(cast,true);
    }

    private void ResolveWyvernOverheatHit(WyvernBreathCast cast,int index)
    {
        Unit source=cast.source;
        if(AbsorbGriffinEnergy(source,cast.origin+Vector3.up*.5f,
            cast.origin+cast.direction*cast.length+Vector3.up*.5f,PixelWyvernBreath.Tint(cast.element),out Vector3 anchor))
        {cast.effect?.AbsorbTo(anchor);return;}
        cast.effect?.ResumeEmission();
        bool sounded=false;
        var victims=units.Where(enemy=>IsCombatTargetable(enemy)&&enemy.team!=source.team&&
            IsPointInsideForwardSkillLine(cast.origin,cast.direction,Flat(enemy.model.transform.position),
                cast.length,cast.width+enemy.collisionRadius)).ToList();
        foreach(var enemy in victims)
        {
            if(!WyvernBreathValid(cast))break;
            if(!sounded)
            {
                sounded=true;
                PlayWyvernSound(source,index==WyvernRank3Rules.CoreHits-1?WyvernSound.OverheatHit:WyvernSound.Hit,
                    Rank3Center(enemy),index==WyvernRank3Rules.CoreHits-1?1f:.65f);
            }
            cast.damage+=Rank3Hit(source,enemy,WyvernRank3Rules.CoreHitRatio(index)*cast.scale,
                WyvernRank3Rules.CoreDefenseIgnore);cast.hits++;
            if(WyvernBreathValid(cast)&&cast.intrinsicHits.Add(enemy))
            {
                ApplyWyvernBreathIntrinsic(cast,enemy);
                // Keep Dragon Core's burn once per enemy, without refreshing it on every tick.
                if(cast.element==1)Rank3Burn(source,enemy,WyvernCombatRules.DotDuration,
                    WyvernCombatRules.FireTickRatio*cast.scale);
            }
            if(!mlEpisodeActive)
            {
                PixelWyvernBreath.OverheatHit(Rank3Center(enemy),Rank3Renderer(enemy),cast.element,cast.direction,index);
                enemy.spriteAnimation?.PlayImpactRecoil(cast.direction,.035f+index*.012f);
            }
        }
    }
}
