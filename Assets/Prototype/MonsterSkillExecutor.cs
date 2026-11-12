using System;
using System.Collections.Generic;

public enum MonsterSkillTrigger
{
    Active,
    BasicAttackPrepare,
    BasicAttackImpact
}

public sealed class MonsterSkillExecutor<TContext>
{
    private readonly Dictionary<string,Func<TContext,SkillMasterRow,bool>> handlers=
        new Dictionary<string,Func<TContext,SkillMasterRow,bool>>(StringComparer.OrdinalIgnoreCase);

    public void Register(string behavior,MonsterSkillTrigger trigger,
        Func<TContext,SkillMasterRow,bool> handler)
    {
        if(string.IsNullOrWhiteSpace(behavior))throw new ArgumentException("Skill behavior is required.",nameof(behavior));
        if(handler==null)throw new ArgumentNullException(nameof(handler));
        handlers[Key(behavior,trigger)]=handler;
    }

    public bool TryExecute(string skillId,MonsterSkillTrigger trigger,TContext context)
    {
        SkillMasterRow skill=GameMasterData.Skill(skillId);
        if(skill==null||string.IsNullOrWhiteSpace(skill.behavior))return false;
        Func<TContext,SkillMasterRow,bool> handler;
        return handlers.TryGetValue(Key(skill.behavior,trigger),out handler)&&handler(context,skill);
    }

    public static string Behavior(string skillId)
    {
        SkillMasterRow skill=GameMasterData.Skill(skillId);
        return skill==null?string.Empty:skill.behavior??string.Empty;
    }

    private static string Key(string behavior,MonsterSkillTrigger trigger)
    {
        return trigger+":"+behavior.Trim();
    }
}
