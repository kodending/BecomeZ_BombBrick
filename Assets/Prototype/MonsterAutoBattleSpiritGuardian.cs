using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    // Battle-owned protection must outlive the caster, and must cover later reinforcements.
    private readonly HashSet<Team> spiritGuardianTeams=new HashSet<Team>();
    private readonly List<PixelSpiritGuardian> spiritGuardians=new List<PixelSpiritGuardian>();
    private readonly Dictionary<Unit,float> spiritGuardianFeedback=new Dictionary<Unit,float>();

    private bool SpiritGuardianProtects(Unit unit)=>unit!=null&&!unit.inStorage&&
        battleStarted&&!battleEnding&&spiritGuardianTeams.Contains(unit.team);

    private void BindSpiritGuardianProtection(Unit unit)
    {
        BindHaetaeDevour(unit);
        unit.combatStatuses.externalDebuffImmunity=()=>SpiritGuardianProtects(unit);
        unit.combatStatuses.harmfulBlocked=()=>ShowSpiritGuardianBlock(unit);
    }

    private void BeginSpiritGuardians()
    {
        var casters=units.Where(u=>IsCombatActive(u)&&IsSpiritDeerFamily(u)&&
            HasFamilyTranscendence(u)).GroupBy(u=>u.team).Select(g=>g.First()).ToArray();
        foreach(Unit caster in casters)
        {
            if(!spiritGuardianTeams.Add(caster.team))continue;
            caster.traitActivations++;
            QueueRank4Cinematic(caster);
            if(mlEpisodeActive)continue;
            int generation=combatGeneration;
            var fx=PixelSpiritGuardian.Create(Rank3Renderer(caster),combatBounds,
                caster.team==Team.Player,()=>battleElapsed,
                ()=>this!=null&&battleStarted&&!battleEnding&&generation==combatGeneration,
                casters.Length>1?(caster.team==Team.Player?-1.8f:1.8f):0f,
                ()=>PlaySpiritDeerSound(caster,SpiritDeerSound.Aegis,combatBounds.center));
            if(fx!=null)
            {
                fx.transform.SetParent(transform,true);spiritGuardians.Add(fx);
                PlaySpiritDeerSound(caster,SpiritDeerSound.Descend,combatBounds.center);
            }
        }
        foreach(Unit unit in units)
        {
            BindSpiritGuardianProtection(unit);
            if(!SpiritGuardianProtects(unit))continue;
            CleanseAllHarmfulEffects(unit);
            unit.rank3ActionSlowTimer=0f;
        }
    }

    private void ShowSpiritGuardianBlock(Unit unit)
    {
        if(mlEpisodeActive||!SpiritGuardianProtects(unit)||!IsCombatActive(unit))return;
        if(spiritGuardianFeedback.TryGetValue(unit,out float next)&&battleElapsed<next)return;
        spiritGuardianFeedback[unit]=battleElapsed+.4f;
        int generation=combatGeneration;
        PixelSpiritBlessing.Bless(Rank3Renderer(unit),0,new Color(1f,.9f,.62f),.24f,
            ()=>battleElapsed,()=>this!=null&&generation==combatGeneration&&SpiritGuardianProtects(unit));
        PlaySpiritDeerSound(unit,SpiritDeerSound.Block,unit.model.transform.position,.85f);
    }

    private void ClearSpiritGuardians()
    {
        spiritGuardianTeams.Clear();spiritGuardianFeedback.Clear();
        foreach(var fx in spiritGuardians)if(fx!=null)fx.Cancel();
        spiritGuardians.Clear();
    }
}
