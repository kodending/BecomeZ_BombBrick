using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool TryResolveNinjaCatRank3(Unit source,Unit target,
        SkillMasterRow skill,float scale,int generation)
    {
        if(HasRank3(source,NinjaCatRank3Rules.ShadowlessCross))
        {
            ResolveNinjaCrossImmediate(source,target,scale,generation);return true;
        }
        if(HasRank3(source,NinjaCatRank3Rules.BlackMoonHunt))
        {
            ResolveNinjaBlackMoonImmediate(source,target,scale,generation);return true;
        }
        if(!HasRank3(source,NinjaCatRank3Rules.EightGateFormation)||!IsCombatActive(target))return false;
        ResolveNinjaEightGateImmediate(source,target,scale,generation);return true;
    }

    private void ApplyNinjaRank3Intrinsic(Unit source,Unit target,
        Vector3 center,bool finisher,bool shadowCross=false)
    {
        if(HasRank3(source,NinjaCatCombatRules.SeolmyoTraitId))
            Rank3Frost(source,target,finisher?2:1);
        if(HasRank3(source,NinjaCatCombatRules.SundongiTraitId))
            Rank3Poison(source,target,NinjaCatCombatRules.PoisonDuration,
                NinjaCatCombatRules.PoisonTickDamageRatio);
        if(HasRank3(source,NinjaCatCombatRules.HongryeonTraitId))
        {
            Rank3Burn(source,target,3f,NinjaCatCombatRules.AfterimageDamageRatio*.25f);
            if(shadowCross)PixelNinjaSubstitution.PlayFlameAfterimage(
                source.model.transform.position,Rank3Renderer(source));
            else PixelCombatEffect.PlayNinjaFlameAfterimage(center,
                TileSize*NinjaCatCombatRules.AfterimageRadiusTiles,Rank3Renderer(target));
        }
    }

    private bool TryResolveTyrannoRank3(Unit source,SkillMasterRow skill,
        float scale,int generation,float radiusMultiplier)
    {
        string trait=HasRank3(source,TyrannoRank3Rules.MassExtinction)
            ?TyrannoRank3Rules.MassExtinction
            :HasRank3(source,TyrannoRank3Rules.TyrantMarch)
                ?TyrannoRank3Rules.TyrantMarch
                :HasRank3(source,TyrannoRank3Rules.PrimordialMaw)
                    ?TyrannoRank3Rules.PrimordialMaw:string.Empty;
        if(string.IsNullOrEmpty(trait))return false;
        if(trait==TyrannoRank3Rules.TyrantMarch)
        {StartTyrannoMarch(source,generation);return true;}
        if(trait==TyrannoRank3Rules.MassExtinction)
        {
            PlayTyrannoSound(source,TyrannoSound.Extinction,Flat(source.model.transform.position));
            PlayTyrannoSound(source,TyrannoSound.Spread,Flat(source.model.transform.position),.85f);
            StartReleasedSkill(source,ResolveTyrannoExtinction(source,scale,radiusMultiplier,generation,source.tyrannoStomp));
            return true;
        }
        StartTyrannoMaw(source,generation);
        return true;
    }

    private static bool TyrannoExtinctionCharging(Unit unit)=>
        unit.tyrannoTremorPending&&HasRank3(unit,TyrannoRank3Rules.MassExtinction)&&
        (unit.spriteAnimation==null||unit.spriteAnimation.HasPendingSkillAttackHit);

    private IEnumerator ResolveTyrannoExtinction(Unit source,float scale,float radiusMultiplier,int generation,PixelTyrannoStomp stomp)
    {
        Vector3 origin=Flat(source.model.transform.position);
        float radius=TileSize*TyrannoRank3Rules.ExtinctionRadiusTiles*radiusMultiplier;
        float core=radius*TyrannoRank3Rules.ExtinctionCoreTiles/TyrannoRank3Rules.ExtinctionRadiusTiles;
        var hit=new HashSet<Unit>();
        float elapsed=0f;
        while(true)
        {
            if(!ReleasedSkillValid(source,generation)){stomp?.Cancel();yield break;}
            float progress=Mathf.Clamp01(elapsed/TyrannoRank3Rules.ExtinctionWaveDuration);
            stomp?.Spread(progress);
            HitTyrannoExtinctionWave(source,origin,radius,core,radius*progress,scale,hit);
            if(progress>=1f)break;
            yield return null;
            elapsed+=CombatSkillClock.Delta;
        }
        if(hit.Count>0)ApplyTyrannoRank3Intrinsic(source,hit,origin,radius,hit.Count);
        source.currentAction="rank3_tyranno_complete";source.traitActivations++;
        RecordSkillEvent(source,TyrannoRank3Rules.MassExtinction,hit.Count,source.skillDamage);
    }

    private void HitTyrannoExtinctionWave(Unit source,Vector3 origin,float radius,float core,float front,float scale,HashSet<Unit> hit)
    {
        for(int i=0;i<units.Count;i++)
        {
            Unit enemy=units[i];
            if(!IsCombatTargetable(enemy)||enemy.team==source.team||hit.Contains(enemy))continue;
            float distance=Vector3.Distance(origin,Flat(enemy.model.transform.position));
            if(distance>radius||distance>front)continue;
            hit.Add(enemy);
            bool inner=distance<=core;
            if(!mlEpisodeActive)
            {
                PixelCombatEffect.PlayTyrannoTremorHit(enemy.model.transform,Rank3Renderer(enemy));
                PixelCombatEffect.PlayWolfHuntImpact(enemy.model.transform,Rank3Renderer(enemy),
                    Vector3.down,new Color(1f,.88f,.65f),false,inner?1.2f:.9f);
            }
            Rank3Hit(source,enemy,(inner?TyrannoRank3Rules.ExtinctionCoreRatio:TyrannoRank3Rules.ExtinctionOuterRatio)*scale);
            Rank3Knockdown(source,enemy,inner?1f:.75f);
        }
    }

}
