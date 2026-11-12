using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool TryResolveArachneRank3(Unit source,SkillMasterRow skill,
        Vector3 center,float scale,float radiusScale,int generation)
    {
        string trait=HasRank3(source,ArachneRank3Rules.HeavenlyNet)
            ?ArachneRank3Rules.HeavenlyNet
            :HasRank3(source,ArachneRank3Rules.CocoonFuneral)
                ?ArachneRank3Rules.CocoonFuneral
                :HasRank3(source,ArachneRank3Rules.EightfoldWeb)
                    ?ArachneRank3Rules.EightfoldWeb:string.Empty;
        if(string.IsNullOrEmpty(trait))return false;
        if(trait==ArachneRank3Rules.HeavenlyNet)
        {
            ResolveHeavenlyCaptureNet(source,center,scale,radiusScale);
            return true;
        }
        if(trait==ArachneRank3Rules.CocoonFuneral)
            StartReleasedSkill(source,ResolveCocoonFuneral(source,source.spiderWebPrimaryTarget??source.target,
                center,scale,radiusScale,generation));
        else StartReleasedSkill(source,ResolveEightfoldWeb(source,center,scale,radiusScale,generation));
        return true;
    }

    private IEnumerator ResolveArachneDesertWebLine(Unit source,Vector3 start,
        Vector3 direction,float length,float scale,int generation)
    {
        for(int tick=0;tick<3;tick++)
        {
            yield return new WaitForSeconds(1f);
            if(battleEnding||generation!=combatGeneration)yield break;
            List<Unit> victims=units.Where(enemy=>IsCombatActive(enemy)&&
                enemy.team!=source.team&&IsPointInsideForwardSkillLine(start,
                    direction,Flat(enemy.model.transform.position),length,
                    TileSize*.35f+enemy.collisionRadius)).ToList();
            for(int i=0;i<victims.Count;i++)Rank3GroundHit(source,victims[i],.3f*scale);
        }
    }

    private void ApplyArachneRank3Intrinsic(Unit source,Unit target,bool desert,
        bool poison,bool frost,float scale)
    {
        if(!IsCombatActive(target))return;
        if(desert)Rank3Hit(source,target,SpiderCombatRules.DesertSilkDamageRatio*scale);
        if(poison)Rank3Poison(source,target,4f,
            SpiderCombatRules.ToxicPrankDamageRatio*.25f*scale);
        if(frost)Rank3Frost(source,target);
    }
}
