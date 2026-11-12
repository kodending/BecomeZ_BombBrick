using System;
using System.Collections;
using System.Linq;

public sealed partial class MonsterAutoBattlePrototype
{
    private readonly int[] releasedEffectCounts=new int[2];
    private int releasedEffectGeneration=-1;

    private bool ReleasedSkillValid(Unit source,int generation)=>source!=null&&this!=null&&
        !battleEnding&&generation==combatGeneration;

    private Action TrackReleasedEffect(Team team)
    {
        int generation=combatGeneration,index=(int)team;
        if(releasedEffectGeneration!=generation)
        {releasedEffectGeneration=generation;releasedEffectCounts[0]=releasedEffectCounts[1]=0;}
        releasedEffectCounts[index]++;
        bool finished=false;
        return ()=>
        {
            if(finished)return;finished=true;
            if(releasedEffectGeneration==generation)
                releasedEffectCounts[index]=Math.Max(0,releasedEffectCounts[index]-1);
        };
    }

    private static CombatSkillClock CaptureCombatSkillClock(Unit source)
    {
        CombatSkillClock clock=CombatSkillClock.Capture();
        return clock!=null&&source?.model!=null&&clock.Owner==source.model.transform?clock:null;
    }

    private UnityEngine.Coroutine StartCombatCoroutine(Unit source,IEnumerator routine)=>
        StartCoroutine(CombatSkillClock.Run(routine,CaptureCombatSkillClock(source)));

    private UnityEngine.Coroutine StartReleasedSkill(Unit source,IEnumerator routine)=>
        StartCombatCoroutine(source,TrackReleasedSkill(source,routine));

    private IEnumerator TrackReleasedSkill(Unit source,IEnumerator routine)
    {
        Action finished=TrackReleasedEffect(source.team);
        try{yield return routine;}
        finally{(routine as IDisposable)?.Dispose();finished();}
    }

    private bool HasPendingReleasedEffects(Team team)=>
        releasedEffectGeneration==combatGeneration&&releasedEffectCounts[(int)team]>0||
        wyvernBreaths.Any(c=>c.source.team==team&&c.generation==combatGeneration&&
            (c.launched&&!c.finished||c.fireField))||
        wyvernHeavenBreaths.Any(c=>c.source.team==team&&c.generation==combatGeneration)||
        haetaeRoars.Any(c=>c.source.team==team&&c.generation==combatGeneration&&c.released&&!c.cancelled)||
        mushroomGasZones.Any(c=>c.source.team==team&&c.generation==combatGeneration)||
        mushroomCrowns.Any(c=>c.source.team==team&&c.generation==combatGeneration)||
        mushroomPropagations.Any(c=>c.source.team==team&&c.generation==combatGeneration&&c.flight>=0f)||
        spiderWebZones.Any(c=>c.source!=null&&c.source.team==team&&c.remaining>0f)||
        beetleGravityFields.Any(c=>c.source.team==team&&c.generation==combatGeneration)||
        units.Any(u=>u.team==team&&u.griffinRoyal!=null&&u.griffinRoyal.waves.Count>0)||
        mammothBoulders.Any(c=>c.source.team==team&&c.generation==combatGeneration&&c.launched&&!c.finished);

    internal CombatProjectileSource.Flight CaptureProjectileFlight(int team,CombatSkillClock skillClock=null)
    {
        int generation=combatGeneration;
        return new CombatProjectileSource.Flight {skillClock=skillClock,clock=()=>skillClock==null?battleElapsed:skillClock.Time,
            valid=()=>this!=null&&generation==combatGeneration&&!battleEnding,
            finished=TrackReleasedEffect((Team)team),
            deflect=(a,b,sprite)=>
            {
                using(CombatSkillClock.Enter(skillClock))
                    return this!=null&&generation==combatGeneration&&DeflectGriffinProjectile((Team)team,a,b,sprite);
            }};
    }
    private void BindProjectileSource(Unit source)
    {
        var owner=source.model.GetComponent<CombatProjectileSource>()??source.model.AddComponent<CombatProjectileSource>();
        owner.battle=this;owner.team=(int)source.team;
    }
}
