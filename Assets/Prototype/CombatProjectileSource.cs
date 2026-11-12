using System;
using UnityEngine;

// Keep projectiles on the originating battle clock, including ricochets and a dead caster.
public sealed class CombatProjectileSource : MonoBehaviour
{
    public MonsterAutoBattlePrototype battle;
    public int team;
    public struct Flight
    {
        public CombatSkillClock skillClock;
        public Func<float> clock;
        public Func<bool> valid;
        public Action finished;
        public Func<Vector3,Vector3,SpriteRenderer,bool> deflect;
    }
    public static Flight Capture(Transform origin)
    {
        var owner=origin==null?null:origin.GetComponentInParent<CombatProjectileSource>();
        if(owner==null||owner.battle==null)return default;
        CombatSkillClock clock=CombatSkillClock.Capture();
        if(clock!=null&&clock.Owner!=owner.transform)clock=null;
        return owner.battle.CaptureProjectileFlight(owner.team,clock);
    }
}
