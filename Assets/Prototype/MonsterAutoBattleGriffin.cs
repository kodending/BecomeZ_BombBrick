using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private float GriffinDiveRange(Unit source,SkillMasterRow skill)=>TileSize*(
        HasRank3(source,GriffinRank3Rules.RaptorHunt)?GriffinRank3Rules.HuntRangeTiles:
        GriffinCombatRules.SkyDiveRangeTiles);

    private Unit GriffinDiveTarget(Unit source,SkillMasterRow skill)
    {
        Unit committed=CommittedSkillTarget(source);
        if(IsCombatTargetable(committed)&&committed.team!=source.team&&
           (!HasRank3(source,GriffinRank3Rules.RaptorHunt)||committed.griffinHeldBy==null))
            return committed;
        Vector3 launch=Flat(source.model.transform.position);
        float range=GriffinDiveRange(source,skill);
        var candidates=units.Where(u=>IsCombatTargetable(u)&&u.team!=source.team&&
            Vector3.Distance(launch,Flat(u.model.transform.position))<=range);
        if(HasRank3(source,GriffinRank3Rules.RaptorHunt))
            return candidates.Where(u=>u.griffinHeldBy==null).OrderByDescending(u=>u.damage)
                .ThenBy(u=>Vector3.Distance(launch,Flat(u.model.transform.position))).FirstOrDefault();
        if(candidates.Contains(source.target))return source.target;
        return candidates.OrderBy(u=>Vector3.Distance(launch,Flat(u.model.transform.position))).FirstOrDefault();
    }

    private Vector3 GriffinLanding(Unit source,Unit prey)
    {
        Vector3 start=Flat(source.model.transform.position),target=Flat(prey.model.transform.position);
        Vector3 delta=target-start;
        // Keep the bodies separate while putting the prey inside the central talon hit.
        float travel=Mathf.Max(0f,delta.magnitude-source.collisionRadius-prey.collisionRadius);
        travel=Mathf.Min(travel,GriffinCombatRules.SkyDiveRangeTiles*TileSize);
        Vector3 landing=ResolveFreePosition(start+delta.normalized*travel,source.collisionRadius);
        return start+Vector3.ClampMagnitude(landing-start,GriffinCombatRules.SkyDiveRangeTiles*TileSize);
    }

    private static Color GriffinDiveTint(Unit source)=>
        HasRank3(source,GriffinCombatRules.GriponasTraitId)?new Color(1f,.54f,.2f):
        HasRank3(source,GriffinCombatRules.HoloponasTraitId)?new Color(.4f,.88f,1f):
        HasRank3(source,GriffinCombatRules.EntPoneTraitId)?new Color(.64f,.92f,.4f):new Color(1f,.85f,.45f);
}
