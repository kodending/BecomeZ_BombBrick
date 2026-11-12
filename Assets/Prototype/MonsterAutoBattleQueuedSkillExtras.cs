using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class QueuedCombinedSkillGroup
    {
        public Unit commander;
        public float perSkill,maximum;
        public readonly List<string> committedIds=new List<string>();
    }

    private bool ExecuteRepeatedSkillFocus(PendingSkillFocus request)
    {
        Unit source=request.unit,target=request.repeatTarget;
        if(!IsCombatActive(source)||!IsCombatActive(target))return false;
        if(skillExecutor==null)ConfigureSkillExecutor();
        float damageMultiplier=source.skillTraitDamageMultiplier,cooldown=source.skillTimer;
        Unit previousTarget=source.target;
        source.target=target;
        source.skillTraitDamageMultiplier*=Mathf.Max(0f,request.repeatRatio);
        try
        {
            // A repeated effect must not reapply augments, schedule another echo or reset the real cooldown.
            bool used=skillExecutor.TryExecute(source.skillId,MonsterSkillTrigger.Active,
                new SkillExecutionContext{source=source,target=target});
            if(used)PlayExecutedSkillAction(source);
            return used;
        }
        finally
        {
            source.skillTraitDamageMultiplier=damageMultiplier;
            source.skillTimer=cooldown;
            source.target=previousTarget;
        }
    }

    private void CommitCombinedSkillFocus(QueuedCombinedSkillGroup group,Unit contributor)
    {
        if(contributor==null||group.committedIds.Contains(contributor.stableId))return;
        group.committedIds.Add(contributor.stableId);
        Unit commander=group.commander;
        if(!IsCombatActive(commander))return;
        commander.skillTimer=ExpeditionAugmentRuntime.AddCombinedCastCooldown(
            commander.skillTimer,1,group.perSkill,group.maximum);
        combinedCastPulseCommanderId=commander.stableId;
        combinedCastPulseContributorIds.Clear();
        combinedCastPulseContributorIds.AddRange(group.committedIds);
        combinedCastPulseStartedAt=Time.unscaledTime;
        AddCombatFloatText(commander,"합동 시전 ×"+(group.committedIds.Count+1),
            new Color(0.72f,0.48f,1f),true);
    }
}
