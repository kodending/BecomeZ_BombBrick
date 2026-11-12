using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private static int WyvernElement(Unit unit)=>unit.definition.monsterId=="igvire"?1:
        unit.definition.monsterId=="frovire"?2:unit.definition.monsterId=="temvire"?3:0;

    private void PrepareWyvernBolt(Unit source,Unit target)
    {
        if(mlEpisodeActive||!IsWyvernFamily(source)||source.spriteAnimation==null)return;
        int generation=combatGeneration;
        var fx=PixelWyvernBolt.Charge(Rank3Renderer(source),WyvernElement(source),
            ()=>Flat(source.model.transform.position),
            ()=>Flat(target.model.transform.position-source.model.transform.position).normalized,
            AttackDistanceForRange(source.attackRange,source.collisionRadius,0f),
            ()=>source.spriteAnimation.BasicAttackWindupProgress,
            ()=>this!=null&&generation==combatGeneration&&!battleEnding&&IsCombatActive(source)&&
                IsCombatActive(target)&&source.spriteAnimation!=null&&source.spriteAnimation.HasPendingBasicAttackHit);
        fx.transform.SetParent(transform,true);
    }

    private void BeginWyvernBolt(Unit source,Unit target,float damage,bool critical)
    {
        Vector3 origin=Flat(source.model.transform.position),direction=Flat(target.model.transform.position)-origin;
        if(direction.sqrMagnitude<.0001f)direction=source.team==Team.Player?Vector3.right:Vector3.left;
        direction.Normalize();
        // attackRange is a category; use the same edge reach as movement/attack selection.
        float length=AttackDistanceForRange(source.attackRange,source.collisionRadius,0f);
        int generation=combatGeneration,element=WyvernElement(source);
        PixelWyvernBolt effect=null;
        if(!mlEpisodeActive)
        {
            effect=PixelWyvernBolt.Fire(Rank3Renderer(source),origin,direction,length,
                element,()=>battleElapsed,()=>this!=null&&generation==combatGeneration&&!battleEnding);
            PlayReadabilityLaunch(source,target);
            effect.transform.SetParent(transform,true);
        }
        if(DeflectGriffinProjectile(source.team,origin,origin+direction*length,null))
        {
            effect?.Deflect(source.team==Team.Player?Vector3.left:Vector3.right,
                combatBounds.size.x>0f?combatBounds:CenteredArenaBounds());
            return;
        }
        // The entire line flashes and strikes on the attack event. Only its first contact runs attack procs.
        var contacts=units.Where(enemy=>enemy.team!=source.team&&IsCombatTargetable(enemy)&&!enemy.crocodilePreyFlying)
            .Select(enemy=>new {enemy,distance=WyvernBoltContact(origin,direction,enemy)})
            .Where(contact=>contact.distance<=length)
            .OrderBy(contact=>contact.distance).ThenBy(contact=>contact.enemy.id).ToList();
        bool first=true;
        foreach(var contact in contacts)
        {
            if(!IsCombatActive(source))break;
            Unit enemy=contact.enemy;if(!IsCombatTargetable(enemy))continue;
            bool previousCritical=source.lastBasicAttackCritical;source.lastBasicAttackCritical=critical;
            try
            {
                if(first)
                {
                    if(critical)ApplyCriticalItemEffects(source,enemy);
                    ResolveBasicAttackImpact(source,enemy,damage);
                }
                else ApplyDamage(source,enemy,damage*WyvernCombatRules.BoltPierceRatio,false,true,false);
            }
            finally{source.lastBasicAttackCritical=previousCritical;}
            first=false;
            if(!mlEpisodeActive)PixelWyvernBolt.Hit(Rank3Center(enemy),Rank3Renderer(enemy),element,direction);
        }
    }

    private static float WyvernBoltContact(Vector3 origin,Vector3 direction,Unit enemy)
    {
        Vector3 offset=Flat(enemy.model.transform.position)-origin;
        float along=Vector3.Dot(offset,direction);
        float sideSquared=Mathf.Max(0f,offset.sqrMagnitude-along*along);
        float radius=enemy.collisionRadius+TileSize*WyvernCombatRules.BoltWidthTiles*.5f;
        if(along<0f||sideSquared>radius*radius)return float.PositiveInfinity;
        return Mathf.Max(0f,along-Mathf.Sqrt(radius*radius-sideSquared));
    }

}
