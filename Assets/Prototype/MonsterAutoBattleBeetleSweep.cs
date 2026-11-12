using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool BeetleSweepActive(Unit source)=>source!=null&&source.beetleSweepTimer>0f&&
        HasRank3(source,BeetleRank3Rules.MassTheft);

    private void GrantBeetleSweep(Unit source,Vector3 from)
    {
        source.beetleSweepTimer=BeetleRank3Rules.MassDuration;
        source.traitActivations++;
        if(mlEpisodeActive)return;
        if(source.beetleSweepFx==null)
        {
            source.beetleSweepFx=PixelBeetleSweep.Play(Rank3Renderer(source));
            source.beetleSweepFx.transform.SetParent(transform,true);
        }
        source.beetleSweepFx.Absorb(from+Vector3.up*.55f);
        PlayBeetleSound(source,BeetleSound.Empower,Rank3Center(source),.8f);
    }

    private void TickBeetleSweep(Unit source,float dt)
    {
        source.beetleSweepTimer=Mathf.Max(0,source.beetleSweepTimer-dt);
        if(source.beetleSweepFx==null)return;
        bool attacking=source.spriteAnimation!=null&&source.spriteAnimation.BeetleSweepAttacking;
        float phase=attacking?source.spriteAnimation.BeetleSweepPhase:-1f;
        if(source.beetleSweepFx.Pose(dt,phase,attacking,BeetleSweepActive(source)))
        {source.beetleSweepFx.Cancel();source.beetleSweepFx=null;}
    }

    private void ClearBeetleSweep(Unit source)
    {
        source.beetleSweepTimer=0f;source.beetleSweepFx?.Cancel();source.beetleSweepFx=null;
    }

    private void BeginBeetleSweepAttack(Unit source,Unit primary,float damage,bool critical,float speed)
    {
        Vector3 origin=Flat(source.model.transform.position);
        Vector3 direction=Flat(primary.model.transform.position)-origin;
        if(direction.sqrMagnitude<.001f)direction=source.facingDirection;
        direction.Normalize();int generation=combatGeneration;
        source.beetleSweepFx?.BeginSweep(origin,direction,TileSize*BeetleRank3Rules.MassSweepRadiusTiles);
        if(source.spriteAnimation==null){ResolveBeetleSweepAttack(source,primary,direction,damage,critical);return;}
        // Snapshot the empowered swing at windup. An expiring buff cannot cancel a horn already in motion.
        source.spriteAnimation.PlayBeetleSweepAttack(speed,()=>
        {
            if(!battleEnding&&generation==combatGeneration&&IsCombatActive(source))
                ResolveBeetleSweepAttack(source,primary,direction,damage,critical);
        });
    }

    private bool BeetleSweepContains(Unit source,Unit enemy,Vector3 direction)
    {
        if(!IsCombatTargetable(enemy)||enemy.team==source.team)return false;
        Vector3 delta=Flat(enemy.model.transform.position-source.model.transform.position);
        float radius=TileSize*BeetleRank3Rules.MassSweepRadiusTiles+enemy.collisionRadius;
        return delta.sqrMagnitude<=radius*radius&&Vector3.Dot(delta,direction)>=0f;
    }

    private void ResolveBeetleSweepAttack(Unit source,Unit primary,Vector3 direction,float damage,bool critical)
    {
        if(!IsCombatActive(source)||source.combatStatuses.Has(CombatStatusKind.Fear))return;
        PlayBeetleSound(source,BeetleSound.Sweep,Rank3Center(source),.85f);
        var victims=units.Where(enemy=>BeetleSweepContains(source,enemy,direction)).ToList();
        int revision=primary==null?0:primary.positionRevision;
        if(primary!=null&&victims.Contains(primary))
            BeginBasicAttackImpact(source,primary,damage,critical);
        foreach(Unit enemy in victims)
        {
            if(!BeetleSweepContains(source,enemy,direction)||enemy==primary&&enemy.positionRevision!=revision)continue;
            Vector3 center=Rank3Center(enemy);
            Rank3Hit(source,enemy,BeetleRank3Rules.MassSplashRatio);
            if(!mlEpisodeActive)
            {
                source.beetleSweepFx?.Hit(center);
                enemy.spriteAnimation?.PlayImpactRecoil(direction,.075f);
            }
        }
    }
}
