using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private static bool InsideWyvernPressure(WyvernBreathCast cast,Unit enemy,float front)
    {
        Vector3 offset=Flat(enemy.model.transform.position)-cast.origin;
        float distance=offset.magnitude,radius=enemy.collisionRadius;
        if(Vector3.Dot(offset,cast.direction)<0f||distance-radius>front)return false;
        if(distance<=radius)return true;
        float margin=Mathf.Asin(Mathf.Clamp01(radius/distance))*Mathf.Rad2Deg;
        return Vector3.Angle(cast.direction,offset)<=WyvernRank3Rules.PressureAngle*.5f+margin;
    }

    private void AdvanceWyvernPressure(WyvernBreathCast cast)
    {
        Unit source=cast.source;float age=cast.time-cast.windup;
        source.velocity=Vector3.zero;SetMoving(source,false);
        if(!cast.launched)
        {
            if(IsCombatActive(cast.target))
            {
                Vector3 aim=Flat(cast.target.model.transform.position)-cast.origin;
                if(aim.sqrMagnitude>.001f)cast.direction=aim.normalized;
            }
            RotateUnit(source,cast.direction,1f);
            if(age>=0f)
            {
                cast.launched=true;source.currentAction="wyvern_pressure_breath";
                PlayWyvernSound(source,WyvernSound.Pressure,cast.origin+cast.direction*TileSize);
                source.spriteAnimation?.HoldSkillAttackHitFrame(WyvernRank3Rules.PressureDuration);
            }
        }
        float inhale=Mathf.Clamp01(cast.time/cast.windup);
        float recoil=1f-Mathf.Clamp01(age/WyvernRank3Rules.PressureDuration);
        float pose=age<0f?-inhale:recoil*.65f;
        source.spriteBillboard?.SetRollPose(Mathf.Sign(cast.direction.x)*8f*pose,Vector3.zero,cast.direction*pose*.17f);
        cast.pressureEffect?.Pose(cast.time,cast.direction);
        if(age<=0f)return;
        if(!cast.pressureAbsorbed&&AbsorbGriffinEnergy(source,cast.origin+Vector3.up*.5f,
            cast.origin+cast.direction*cast.length+Vector3.up*.5f,PixelWyvernBreath.Tint(cast.element),out Vector3 anchor))
        {cast.pressureAbsorbed=true;cast.pressureEffect?.AbsorbTo(anchor);}
        float front=cast.length*WyvernRank3Rules.PressureFront(age);
        var victims=units.Where(enemy=>!cast.pressureAbsorbed&&IsCombatActive(enemy)&&enemy.team!=source.team&&
            !cast.intrinsicHits.Contains(enemy)&&InsideWyvernPressure(cast,enemy,front))
            .OrderBy(enemy=>(Flat(enemy.model.transform.position)-cast.origin).sqrMagnitude).ToList();
        foreach(var enemy in victims)
        {
            if(!WyvernBreathValid(cast))break;
            cast.intrinsicHits.Add(enemy);
            Vector3 impact=Rank3Center(enemy),impactDirection=Flat(enemy.model.transform.position)-cast.origin;
            cast.damage+=Rank3Hit(source,enemy,WyvernRank3Rules.PressureRatio*cast.scale);cast.hits++;
            ApplyWyvernBreathIntrinsic(cast,enemy);
            if(!mlEpisodeActive)
            {
                PixelWyvernPressure.Hit(impact,Rank3Renderer(enemy),cast.element,impactDirection.normalized);
                enemy.spriteAnimation?.PlayImpactRecoil(cast.direction,.1f);
                if(!cast.shaken){cast.shaken=true;combatCameraDirector?.ReportPressureBreath(cast.direction);}
            }
        }
        if(cast.finished||age+.00001f<WyvernRank3Rules.PressureDuration)return;
        cast.fireField=cast.element==1&&!cast.pressureAbsorbed;
        RecordSkillEvent(source,WyvernRank3Rules.CataclysmBreath,cast.hits,cast.damage);
        source.traitActivations++;EndWyvernBreath(cast,true);
        if(cast.fireField)cast.pressureEffect?.PoseFire(0f);
    }

    private void AdvanceWyvernPressureFire(WyvernBreathCast cast,float dt)
    {
        if(battleEnding||cast.generation!=combatGeneration||cast.source==null)
        {cast.fireField=false;cast.pressureEffect?.Cancel();return;}
        cast.fireAge+=dt;cast.pressureEffect?.PoseFire(cast.fireAge);
        while(cast.fireTicks<4&&cast.fireAge+.00001f>=(cast.fireTicks+1)*WyvernCombatRules.TickInterval)
        {
            cast.fireTicks++;
            foreach(var enemy in units.Where(enemy=>IsCombatActive(enemy)&&enemy.team!=cast.source.team&&
                InsideWyvernPressure(cast,enemy,cast.length)).ToList())
                Rank3GroundHit(cast.source,enemy,WyvernCombatRules.FireTickRatio*cast.scale);
        }
        if(cast.fireAge>=WyvernCombatRules.DotDuration)
        {cast.fireField=false;cast.pressureEffect?.Cancel();}
    }
}
