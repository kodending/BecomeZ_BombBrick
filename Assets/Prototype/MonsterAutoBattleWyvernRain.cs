using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private void PrepareWyvernRain(WyvernBreathCast cast,float radiusScale)
    {
        int count=WyvernRank3Rules.RainCount;
        cast.rainPoints=new Vector3[count];cast.rainOffsets=new Vector3[count];cast.rainBlocked=new bool[count];
        cast.rainRadius=TileSize*WyvernRank3Rules.RainRadiusTiles*radiusScale;
        Vector3 center=Flat(cast.target.model.transform.position);
        float area=TileSize*WyvernRank3Rules.RainAreaTiles;
        var candidates=units.Where(enemy=>IsCombatTargetable(enemy)&&enemy.team!=cast.source.team&&
            Vector3.Distance(center,Flat(enemy.model.transform.position))<=area).ToList();
        var remaining=candidates.ToList();
        for(int i=0;i<count;i++)
        {
            if(remaining.Count==0)remaining.AddRange(candidates);
            Vector3 point=center;
            if(remaining.Count>0)
            {
                int index=Random.Range(0,remaining.Count);
                point=Flat(remaining[index].model.transform.position);remaining.RemoveAt(index);
            }
            Vector2 scatter=Random.insideUnitCircle*(TileSize*.65f);
            point=center+Vector3.ClampMagnitude(point+new Vector3(scatter.x,0,scatter.y)-center,area);
            if(combatBounds.size.x>0f)
            {
                point.x=Mathf.Clamp(point.x,combatBounds.min.x+.1f,combatBounds.max.x-.1f);
                point.z=Mathf.Clamp(point.z,combatBounds.min.z+.1f,combatBounds.max.z-.1f);
            }
            cast.rainPoints[i]=point;
            cast.rainOffsets[i]=PixelWyvernRain.SkyOffset(point,Camera.main);
        }
    }

    private void AdvanceWyvernRain(WyvernBreathCast cast)
    {
        Unit source=cast.source;source.velocity=Vector3.zero;SetMoving(source,false);
        RotateUnit(source,cast.direction,1f);
        if(!cast.launched&&cast.time>=cast.windup)
        {
            cast.launched=true;source.currentAction="wyvern_falling_breath";
            PlayWyvernSound(source,WyvernSound.RainLaunch,cast.origin,.85f);
            source.spriteAnimation?.HoldSkillAttackHitFrame(.25f);
        }
        for(int i=cast.nextHit;i<WyvernRank3Rules.RainCount;i++)
        {
            float impact=WyvernRank3Rules.RainImpact(i),fall=impact-WyvernRank3Rules.RainFall;
            if(!cast.rainBlocked[i]&&cast.previousTime<fall&&cast.time>=fall)
                PlayWyvernSound(source,WyvernSound.RainFall,cast.rainPoints[i],.65f);
            if(!cast.rainBlocked[i]&&cast.time>=fall&&cast.previousTime<impact)
            {
                Vector3 from=PixelWyvernRain.FlightPoint(cast.rainPoints[i],cast.rainOffsets[i],i,Mathf.Max(fall,cast.previousTime));
                Vector3 to=PixelWyvernRain.FlightPoint(cast.rainPoints[i],cast.rainOffsets[i],i,cast.time);
                if(DeflectGriffinProjectile(source.team,from,to,null))
                {
                    cast.rainBlocked[i]=true;
                    cast.rainEffect?.Deflect(i,cast.time,source.team==Team.Player?Vector3.left:Vector3.right,
                        combatBounds.size.x>0f?combatBounds:CenteredArenaBounds());
                }
            }
            if(cast.time+.00001f<impact)continue;
            cast.nextHit=i+1;
            if(cast.rainBlocked[i])continue;
            PlayWyvernSound(source,WyvernSound.RainHit,cast.rainPoints[i],.8f);
            foreach(var enemy in units.Where(enemy=>IsCombatTargetable(enemy)&&enemy.team!=source.team&&
                Vector3.Distance(Flat(enemy.model.transform.position),cast.rainPoints[i])<=cast.rainRadius+enemy.collisionRadius).ToList())
            {
                if(!WyvernBreathValid(cast))break;
                cast.damage+=Rank3Hit(source,enemy,WyvernRank3Rules.RainRatio*cast.scale);cast.hits++;
                if(cast.intrinsicHits.Add(enemy))ApplyWyvernBreathIntrinsic(cast,enemy);
                if(!mlEpisodeActive)
                {
                    PixelWyvernBreath.Hit(Rank3Center(enemy),Rank3Renderer(enemy),cast.element,true);
                    enemy.spriteAnimation?.PlayImpactRecoil(Vector3.left,.07f);
                }
            }
        }
        if(cast.finished)return;
        cast.rainEffect?.Pose(cast.time);
        if(cast.nextHit<WyvernRank3Rules.RainCount)return;
        cast.fireField=cast.element==1&&cast.rainBlocked.Any(blocked=>!blocked);
        RecordSkillEvent(source,WyvernRank3Rules.WingSweep,cast.hits,cast.damage);
        source.traitActivations++;EndWyvernBreath(cast,true);
    }

    private void AdvanceWyvernRainFire(WyvernBreathCast cast,float dt)
    {
        if(battleEnding||cast.generation!=combatGeneration||cast.source==null)
        {cast.fireField=false;cast.rainEffect?.Cancel();return;}
        cast.fireAge+=dt;cast.rainEffect?.Pose(WyvernRank3Rules.RainImpact(WyvernRank3Rules.RainCount-1)+cast.fireAge);
        while(cast.fireTicks<4&&cast.fireAge+.00001f>=(cast.fireTicks+1)*WyvernCombatRules.TickInterval)
        {
            cast.fireTicks++;
            foreach(var enemy in units.Where(enemy=>IsCombatTargetable(enemy)&&enemy.team!=cast.source.team).ToList())
                for(int i=0;i<cast.rainPoints.Length;i++)
                {
                    if(cast.rainBlocked[i]||Vector3.Distance(Flat(enemy.model.transform.position),cast.rainPoints[i])>
                        cast.rainRadius+enemy.collisionRadius)continue;
                    Rank3GroundHit(cast.source,enemy,WyvernCombatRules.FireTickRatio*cast.scale);break;
                }
        }
        if(cast.fireAge>=WyvernCombatRules.DotDuration){cast.fireField=false;cast.rainEffect?.Cancel();}
    }
}
