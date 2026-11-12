using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private PendingSkillFocus committingSkill;

    private bool IsCommittingFocusedSkill(Unit source)=>source!=null&&committingSkill!=null&&committingSkill.unit==source;

    private Unit CommittedSkillTarget(Unit source)=>
        IsCommittingFocusedSkill(source)?committingSkill.castTarget:null;

    private List<Unit> CommittedSkillTargets(Unit source)=>
        IsCommittingFocusedSkill(source)?committingSkill.castTargets:null;

    private Unit FocusedRequestTarget(PendingSkillFocus request)=>
        request.castTarget??(request.repeatRatio>=0f?request.repeatTarget:request.unit.target);

    private bool IsWithinCommittedSkillReach(Unit source,Unit target,float reach)=>
        IsCombatActive(target)&&(CommittedSkillTarget(source)==target||
            Vector3.Distance(Flat(source.model.transform.position),Flat(target.model.transform.position))<=reach);

    private bool RefreshFocusedCastTarget(PendingSkillFocus request)
    {
        Unit source=request.unit;
        if(IsCombatTargetable(request.castTarget)&&request.castTarget.team!=source.team)return true;
        Unit target=null;
        if(request.castTargets!=null)
            for(int i=0;i<request.castTargets.Count;i++)
                if(IsCombatTargetable(request.castTargets[i])&&request.castTargets[i].team!=source.team)
                {target=request.castTargets[i];break;}
        float nearest=float.PositiveInfinity;
        if(target==null)
            for(int i=0;i<units.Count;i++)
            {
                Unit enemy=units[i];
                if(!IsCombatTargetable(enemy)||enemy.team==source.team)continue;
                float distance=(Flat(enemy.model.transform.position)-Flat(source.model.transform.position)).sqrMagnitude;
                if(distance>nearest||distance==nearest&&target!=null&&enemy.id>=target.id)continue;
                nearest=distance;target=enemy;
            }
        if(target==null)return HaetaeSupportTargets(source,GameMasterData.Skill(source.skillId))>0;
        request.castTarget=target;
        if(GameMasterData.Skill(source.skillId)?.behavior=="MagicStarBarrage")
        {
            if(request.castTargets==null)request.castTargets=new List<Unit>();
            for(int i=request.castTargets.Count-1;i>=0;i--)
                if(!IsCombatTargetable(request.castTargets[i])||request.castTargets[i].team==source.team)
                    request.castTargets.RemoveAt(i);
            if(!request.castTargets.Contains(target))request.castTargets.Add(target);
        }
        return true;
    }

    private void CaptureFocusedCastPlan(PendingSkillFocus request)
    {
        Unit source=request.unit;
        request.castTarget=request.repeatRatio>=0f?request.repeatTarget:source.target;
        var skill=GameMasterData.Skill(source.skillId);
        switch(skill?.behavior)
        {
            case "AssassinLeap":
                request.castTarget=SelectRaptorLeapTarget(source,Mathf.Max(1f,skill.castRangeTiles));break;
            case "Skyfall":case "SkyDive":
                request.castTarget=GriffinDiveTarget(source,skill);break;
            case "ShadowHunt":
                request.castTarget=ShadowHuntCastTarget(source,skill);break;
            case "MagicStarBarrage":
                request.castTargets=CaptureMagicStarTargets(source,TileSize*Mathf.Max(1f,skill.castRangeTiles));
                if(!request.castTargets.Contains(request.castTarget))request.castTarget=request.castTargets.FirstOrDefault();
                break;
        }
    }

    private Unit ShadowHuntCastTarget(Unit source,SkillMasterRow skill)
    {
        Vector3 origin=Flat(source.model.transform.position);
        float range=TileSize*Mathf.Max(1f,skill.castRangeTiles)+source.collisionRadius;
        return units.Where(candidate=>IsCombatTargetable(candidate)&&candidate.team!=source.team&&
                Vector3.Distance(origin,Flat(candidate.model.transform.position))<=range+candidate.collisionRadius)
            .OrderBy(candidate=>candidate.hp/Mathf.Max(1f,candidate.maxHp))
            .ThenBy(candidate=>Vector3.Distance(origin,Flat(candidate.model.transform.position))).FirstOrDefault();
    }

    private bool RunCommittedFocusedSkill(PendingSkillFocus request)
    {
        PendingSkillFocus previous=committingSkill;
        committingSkill=request;
        try
        {
            // Only the accepted cast bypasses its start-range checks; damage geometry stays unchanged.
            if(request.repeatRatio>=0f)
            {
                Unit previousTarget=request.repeatTarget;
                request.repeatTarget=request.castTarget??previousTarget;
                try{return ExecuteRepeatedSkillFocus(request);}
                finally{request.repeatTarget=previousTarget;}
            }
            if(IsCombatTargetable(request.castTarget))request.unit.target=request.castTarget;
            return ExecuteReadySkill(request.unit,request.combinedCastTrigger);
        }
        finally{committingSkill=previous;}
    }
}
