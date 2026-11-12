using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class MonsterAutoBattlePrototype
{
    private readonly List<PixelEightfoldWeb> eightfoldWebs=new List<PixelEightfoldWeb>();

    private IEnumerator ResolveEightfoldWeb(Unit source,Vector3 center,float scale,float radiusScale,int generation)
    {
        if(battleEnding||generation!=combatGeneration||!IsCombatActive(source))yield break;
        var primary=source.spiderWebPrimaryTarget??source.target;
        if(IsCombatActive(primary)&&primary.team!=source.team)center=Flat(primary.model.transform.position);
        float radius=TileSize*ArachneRank3Rules.EightfoldRadiusTiles*radiusScale;
        bool precise=HasRank3(source,SpiderCombatRules.AraneaTraitId);
        bool desert=HasRank3(source,SpiderCombatRules.SilantiTraitId);
        bool poison=HasRank3(source,SpiderCombatRules.RagonTraitId);
        bool frost=HasRank3(source,SpiderCombatRules.IcearachneTraitId);
        Color color=SpiderSilkColor(source);
        bool completed=false;
        eightfoldWebs.RemoveAll(web=>web==null);
        var fx=mlEpisodeActive?null:PixelEightfoldWeb.Play(center,radius,Rank3Renderer(source),color,desert,
            ()=>this!=null&&!battleEnding&&generation==combatGeneration,
            final=>combatCameraDirector?.ReportSilkLanceImpact(final));
        if(fx!=null)eightfoldWebs.Add(fx);
        PlaySpiderSound(source,SpiderSound.Form,center);
        source.currentAction="eightfold_tension";source.traitActivations++;
        int hits=0;float damage=0f;
        try
        {
            yield return new WaitForSeconds(ArachneRank3Rules.EightfoldPreparation-PixelEightfoldWeb.FlightToBody);
            for(int pair=0;pair<4;pair++)
            {
                if(battleEnding||generation!=combatGeneration)yield break;
                source.currentAction="eightfold_thrust_"+pair;
                if(fx!=null)fx.Aim(pair,Rank3Enemies(source,center,radius).Select(Rank3Renderer).ToArray());
                yield return new WaitForSeconds(PixelEightfoldWeb.FlightToBody);
                if(battleEnding||generation!=combatGeneration)yield break;
                foreach(Unit enemy in Rank3Enemies(source,center,radius))
                {
                    PlaySpiderSound(source,SpiderSound.Pierce,enemy.model.transform.position);
                    if(!mlEpisodeActive)PixelCombatEffect.PlayEightfoldContact(Rank3Renderer(enemy),color,false);
                    damage+=Rank3Hit(source,enemy,ArachneRank3Rules.EightfoldLineRatio*scale);hits++;
                    ApplyArachneRank3Intrinsic(source,enemy,false,poison,frost,scale);
                }
                if(desert)
                {
                    float a=(pair*45f+22.5f)*Mathf.Deg2Rad;
                    var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    StartReleasedSkill(source,ResolveArachneDesertWebLine(source,center-direction*radius,direction,
                        radius*2f,scale,generation));
                }
                yield return new WaitForSeconds(ArachneRank3Rules.EightfoldInterval-
                    (pair<3?PixelEightfoldWeb.FlightToBody:0f));
            }
            if(battleEnding||generation!=combatGeneration)yield break;
            source.currentAction="eightfold_final_tension";
            if(fx!=null)fx.Aim(0,Rank3Enemies(source,center,radius).Select(Rank3Renderer).ToArray(),true);
            yield return new WaitForSeconds(ArachneRank3Rules.EightfoldFinalTension);
            if(battleEnding||generation!=combatGeneration)yield break;
            foreach(Unit enemy in Rank3Enemies(source,center,radius))
            {
                PlaySpiderSound(source,SpiderSound.Finish,enemy.model.transform.position);
                if(!mlEpisodeActive)PixelCombatEffect.PlayEightfoldContact(Rank3Renderer(enemy),color,true);
                damage+=Rank3Hit(source,enemy,ArachneRank3Rules.EightfoldFinalRatio*scale,
                    ArachneRank3Rules.EightfoldDefenseIgnore);hits++;
                if(!IsCombatActive(enemy))continue;
                if(precise)Rank3Root(source,enemy,1.6f);
                if(poison)Rank3Poison(source,enemy,4f,.7f*scale);
                if(frost)
                {
                    var frozen=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Freeze,
                        HostileStatusDuration(enemy,1.25f),source);
                    if(frozen.remaining>0f&&frozen.stacks>0)PlayFreezeVisual(enemy,frozen.remaining);
                }
            }
            source.currentAction="eightfold_complete";
            RecordSkillEvent(source,ArachneRank3Rules.EightfoldWeb,hits,damage);
            completed=true;
        }
        finally
        {
            if(!completed){eightfoldWebs.Remove(fx);if(fx!=null)fx.Cancel();}
        }
    }
}
