using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class SpiritGuardSanctuary
    {
        public Unit source;
        public Vector3 center;
        public float born;
        public int generation;
    }
    private readonly List<SpiritGuardSanctuary> spiritGuardSanctuaries=new List<SpiritGuardSanctuary>();

    private void ClearSpiritGuardSanctuaries()
    {
        foreach(var zone in spiritGuardSanctuaries)zone.generation=-1;
        spiritGuardSanctuaries.Clear();
    }

    private bool SpiritGuardActive(SpiritGuardSanctuary zone)=>zone.generation==combatGeneration&&
        !battleEnding&&battleElapsed>=zone.born&&
        battleElapsed-zone.born<SpiritDeerRank3Rules.Duration;

    private bool IsSpiritSanctuaryProtected(Unit unit)
    {
        if(!IsCombatActive(unit)||unit.augmentShadowClone)return false;
        foreach(var zone in spiritGuardSanctuaries)
            if(SpiritGuardActive(zone)&&zone.source.team==unit.team&&
               Vector3.Distance(zone.center,Flat(unit.model.transform.position))<=
               TileSize*SpiritDeerRank3Rules.RadiusTiles*PixelSpiritBlessing.SpreadAt(battleElapsed-zone.born)+unit.collisionRadius)
                return !unit.combatStatuses.RejectBenefit();
        return false;
    }

    private IEnumerator ResolveInviolableSanctuary(Unit source,Vector3 center,float scale,int generation)
    {
        var zone=new SpiritGuardSanctuary{source=source,center=center,born=battleElapsed,generation=generation};
        spiritGuardSanctuaries.Add(zone);
        var blessed=new HashSet<Unit>();var cursed=new HashSet<Unit>();
        if(!mlEpisodeActive)PixelSpiritBlessing.Sanctuary(center,TileSize*SpiritDeerRank3Rules.RadiusTiles,
            SpiritDeerRank3Rules.Duration,new Color(.48f,.83f,1f),Rank3Renderer(source),()=>battleElapsed,
            ()=>SpiritGuardActive(zone),true);
        try
        {
            while(SpiritGuardActive(zone))
            {
                if(paused){yield return null;continue;}
                float revealed=TileSize*SpiritDeerRank3Rules.RadiusTiles*PixelSpiritBlessing.SpreadAt(battleElapsed-zone.born);
                foreach(Unit ally in Rank3Allies(source,center,revealed))
                    if(!ally.augmentShadowClone&&blessed.Add(ally))ApplySpiritDeerSanctuaryBlessing(source,ally,scale);
                if(HasRank3(source,SpiritDeerCombatRules.EclipsedeerTraitId))
                    foreach(Unit enemy in Rank3Enemies(source,center,revealed))
                        if(cursed.Add(enemy))ApplySpiritDeerEclipseCurse(source,enemy);
                yield return null;
            }
        }
        finally{zone.generation=-1;spiritGuardSanctuaries.Remove(zone);}
        if(battleEnding||generation!=combatGeneration)yield break;
        source.currentAction="rank3_inviolable_sanctuary";source.traitActivations++;
        RecordSkillEvent(source,SpiritDeerRank3Rules.InviolableSanctuary,0,0f);
    }

    private void ShowSpiritGuardHit(Unit attacker,Unit target,float blocked)
    {
        if(mlEpisodeActive)return;
        int generation=combatGeneration;
        Vector3 direction=attacker==null||attacker.model==null?Vector3.right:
            Flat(attacker.model.transform.position-target.model.transform.position);
        PixelSpiritBlessing.GuardHit(Rank3Renderer(target),direction,blocked/Mathf.Max(1f,target.maxHp),
            ()=>battleElapsed,()=>generation==combatGeneration&&IsCombatActive(target));
        PlaySpiritDeerSound(target,SpiritDeerSound.Guard,target.model.transform.position,1.4f);
    }

    private IEnumerator ResolveSpiritLifeReturn(Unit source,Vector3 center,float scale,int generation)
    {
        float radius=TileSize*SpiritDeerRank3Rules.RadiusTiles,start=battleElapsed;
        var blessed=new HashSet<Unit>();var cursed=new HashSet<Unit>();
        if(!mlEpisodeActive)PixelSpiritBlessing.Sanctuary(center,radius,SpiritDeerRank3Rules.Duration,
            Color.Lerp(SpiritDeerTint(source),new Color(.35f,1f,.7f),.5f),Rank3Renderer(source),()=>battleElapsed,
            ()=>generation==combatGeneration&&!battleEnding);
        while(battleElapsed-start<SpiritDeerRank3Rules.Duration)
        {
            if(battleEnding||generation!=combatGeneration)yield break;
            if(paused){yield return null;continue;}
            float revealed=radius*PixelSpiritBlessing.SpreadAt(battleElapsed-start);
            foreach(Unit ally in Rank3Allies(source,center,revealed))
                if(!ally.augmentShadowClone&&blessed.Add(ally))
                {
                    ApplySpiritDeerSanctuaryBlessing(source,ally,scale);
                    GrantSpiritLifeReturn(ally,SpiritDeerRank3Rules.LifeReturnDuration,source);
                }
            if(HasRank3(source,SpiritDeerCombatRules.EclipsedeerTraitId))
                foreach(Unit enemy in Rank3Enemies(source,center,revealed))
                    if(cursed.Add(enemy))ApplySpiritDeerEclipseCurse(source,enemy);
            yield return null;
        }
        source.currentAction="rank3_spirit_life_return";source.traitActivations++;
        RecordSkillEvent(source,SpiritDeerRank3Rules.LifeTithe,0,0f);
    }

    private void GrantSpiritLifeReturn(Unit ally,float duration,Unit caster=null)
    {
        ally.spiritLifeShieldGrantScale=GuardianShieldMultiplier(caster??ally);
        ally.spiritLifeReturnTimer=GateHaetaeBenefit(ally,Mathf.Max(ally.spiritLifeReturnTimer,duration));
        if(mlEpisodeActive)return;
        int generation=combatGeneration;
        PixelSpiritBlessing.Bless(Rank3Renderer(ally),4,new Color(.35f,1f,.68f),ally.spiritLifeReturnTimer,
            ()=>battleElapsed,()=>generation==combatGeneration&&IsCombatActive(ally)&&ally.spiritLifeReturnTimer>0f);
        AddCombatFloatText(ally,GameLocalization.IsEnglish?"Life Return":"생명 환류",new Color(.45f,1f,.7f),false);
    }

    private void ApplySpiritLifeReturn(Unit source,Unit victim,float applied)
    {
        if(!IsCombatActive(source)||source.augmentShadowClone||source.spiritLifeReturnTimer<=0f||applied<=0f)return;
        float previousHp=source.hp,previousShield=source.spiritLifeShield;
        float overflow=Heal(source,applied*SpiritDeerRank3Rules.LifeReturnRatio);
        float shieldGain=overflow*Mathf.Max(0f,source.combatStatuses.shieldGainMultiplier)*source.spiritLifeShieldGrantScale;
        if(shieldGain>0f)
        {
            source.spiritLifeShield=GateHaetaeBenefit(source,Mathf.Min(source.maxHp*SpiritDeerRank3Rules.LifeReturnShieldRatio*source.spiritLifeShieldGrantScale,
                source.spiritLifeShield+shieldGain),CombatBenefitKind.Shield);
            if(source.spiritLifeShield>0f)source.spiritLifeShieldTimer=SpiritDeerRank3Rules.LifeReturnShieldDuration;
        }
        if(mlEpisodeActive)return;
        bool shieldAdded=source.spiritLifeShield>previousShield;
        bool restored=source.hp>previousHp||shieldAdded;
        int generation=combatGeneration;SpriteRenderer targetRenderer=Rank3Renderer(victim);
        Vector3 from=targetRenderer==null?victim.model.transform.position+Vector3.up*.5f:targetRenderer.bounds.center;
        PixelSpiritBlessing.ReturnLife(from,Rank3Renderer(source),()=>battleElapsed,
            ()=>generation==combatGeneration&&IsCombatActive(source),()=>
            {
                if(restored)PlaySpiritDeerSound(source,SpiritDeerSound.Return,source.model.transform.position);
                if(shieldAdded&&source.spiritLifeShield>0f)
                    PlaySpiritDeerSound(source,SpiritDeerSound.Shield,source.model.transform.position);
                if(source.spiritLifeShield>0f)
                    PixelSpiritBlessing.Bless(Rank3Renderer(source),5,new Color(.55f,1f,.73f),source.spiritLifeShieldTimer,
                        ()=>battleElapsed,()=>generation==combatGeneration&&IsCombatActive(source)&&source.spiritLifeShield>0f);
            });
    }

    private bool TryResolveSpiritDeerRank3(Unit source,
        SkillMasterRow skill,Vector3 center,float scale,int generation)
    {
        string trait=HasRank3(source,SpiritDeerRank3Rules.LifeTithe)
            ?SpiritDeerRank3Rules.LifeTithe
            :HasRank3(source,SpiritDeerRank3Rules.InviolableSanctuary)
                ?SpiritDeerRank3Rules.InviolableSanctuary
                :HasRank3(source,SpiritDeerRank3Rules.MyriadConcert)
                    ?SpiritDeerRank3Rules.MyriadConcert:string.Empty;
        if(string.IsNullOrEmpty(trait))return false;
        StartReleasedSkill(source,trait==SpiritDeerRank3Rules.LifeTithe
            ?ResolveSpiritLifeReturn(source,center,scale,generation)
            :trait==SpiritDeerRank3Rules.InviolableSanctuary
                ?ResolveInviolableSanctuary(source,center,scale,generation)
            :ResolveMyriadConcert(source,center,scale,generation));
        return true;
    }

}
