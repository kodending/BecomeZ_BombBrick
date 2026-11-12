using System;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private static bool PausedBySkillTime(CombatSkillClock clock)=>CombatSkillClock.Active&&
        !ReferenceEquals(clock,CombatSkillClock.Capture());

    private Func<float> CaptureSkillTime(Unit source)
    {
        CombatSkillClock clock=CaptureCombatSkillClock(source);
        return clock==null?(Func<float>)(()=>battleElapsed):()=>clock.Time;
    }

}
