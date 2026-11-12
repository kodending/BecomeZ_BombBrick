using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool TryResolveMantisRank3(Unit source,Unit target,SkillMasterRow skill,
        float damageScale,float radiusScale,int generation)
    {
        string trait=HasRank3(source,MantisRank3Rules.DivineGuillotine)
            ?MantisRank3Rules.DivineGuillotine
            :HasRank3(source,MantisRank3Rules.MyriadEyes)
                ?MantisRank3Rules.MyriadEyes
                :HasRank3(source,MantisRank3Rules.ReaperEmergence)&&
                 source.rank3MantisReaper
                    ?MantisRank3Rules.ReaperEmergence:string.Empty;
        if(string.IsNullOrEmpty(trait)||!IsCombatActive(target))return false;
        if(trait==MantisRank3Rules.DivineGuillotine)
        {
            StartMantisGuillotine(source,target,damageScale,radiusScale,generation);
            return true;
        }
        if(trait==MantisRank3Rules.MyriadEyes)
        {
            StartReleasedSkill(source,ResolveMantisFan(source,target,damageScale,radiusScale,generation));
            return true;
        }
        StartMantisReaperFlight(source,target,damageScale,radiusScale,generation);
        return true;
    }

    private bool TryResolveMushroomRank3(Unit source,Unit target,SkillMasterRow skill,
        float damageScale,float radiusScale,int generation)
    {
        string trait=HasRank3(source,MushroomRank3Rules.MycokingDescent)
            ?MushroomRank3Rules.MycokingDescent:string.Empty;
        if(string.IsNullOrEmpty(trait)||!IsCombatActive(target))return false;
        SpawnRank3Mycoking(source,Flat(target.model.transform.position),damageScale,radiusScale,generation);
        return true;
    }

}
