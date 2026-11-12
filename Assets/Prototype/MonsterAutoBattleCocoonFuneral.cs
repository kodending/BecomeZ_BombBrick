using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class MonsterAutoBattlePrototype
{
    private readonly List<PixelSpiderSilkBind> funeralCocoons=new List<PixelSpiderSilkBind>();

    private void ClearSpiderWebZonesAfterBattle()
    {
        // Let the winning blow finish its particle burst; resets can still cancel it immediately.
        var finishing=funeralCocoons.FindAll(fx=>fx!=null&&fx.Bursting);
        var severing=eightfoldWebs.FindAll(fx=>fx!=null&&fx.Finishing);
        eightfoldWebs.RemoveAll(fx=>fx!=null&&fx.Finishing);
        funeralCocoons.RemoveAll(fx=>fx!=null&&fx.Bursting);
        ClearSpiderWebZones();
        funeralCocoons.AddRange(finishing);
        eightfoldWebs.AddRange(severing);
    }

    private IEnumerator ResolveCocoonFuneral(Unit source,Unit primary,Vector3 center,
        float scale,float radiusScale,int generation)
        =>ResolveSpiderCocoons(source,primary,center,scale,radiusScale,generation,false);

    private IEnumerator ResolveSilkQueenCocoons(Unit source,Unit primary,Vector3 center,
        float scale,float radiusScale,int generation)
        =>ResolveSpiderCocoons(source,primary,center,scale,radiusScale,generation,true);

    private IEnumerator ResolveSpiderCocoons(Unit source,Unit primary,Vector3 center,
        float scale,float radiusScale,int generation,bool wholeBattlefield)
    {
        if(battleEnding||generation!=combatGeneration||!IsCombatActive(source))yield break;
        if(!IsCombatActive(primary)||primary.team==source.team)
            primary=units.Where(u=>IsCombatActive(u)&&u.team!=source.team&&
                (wholeBattlefield||Vector3.Distance(center,Flat(u.model.transform.position))<=TileSize*ArachneRank3Rules.CocoonRadiusTiles*radiusScale))
                .OrderBy(u=>(Flat(u.model.transform.position)-center).sqrMagnitude).ThenBy(u=>u.id).FirstOrDefault();
        if(primary==null)yield break;
        center=Flat(primary.model.transform.position);
        var victims=new List<Unit>{primary};
        var surrounding=units.Where(u=>u!=primary&&IsCombatActive(u)&&u.team!=source.team&&
            (wholeBattlefield||Vector3.Distance(center,Flat(u.model.transform.position))<=TileSize*ArachneRank3Rules.CocoonRadiusTiles*radiusScale))
            .OrderBy(u=>(Flat(u.model.transform.position)-center).sqrMagnitude).ThenBy(u=>u.id);
        victims.AddRange(wholeBattlefield?surrounding:surrounding.Take(ArachneRank3Rules.CocoonTargets-1));
        bool precise=HasRank3(source,SpiderCombatRules.AraneaTraitId);
        bool desert=HasRank3(source,SpiderCombatRules.SilantiTraitId);
        bool poison=HasRank3(source,SpiderCombatRules.RagonTraitId);
        bool frost=HasRank3(source,SpiderCombatRules.IcearachneTraitId);
        Color color=SpiderSilkColor(source);
        var effects=new Dictionary<Unit,PixelSpiderSilkBind>();
        funeralCocoons.RemoveAll(fx=>fx==null);
        int hits=0;float damage=0f;
        source.traitActivations++;source.currentAction="cocoon_funeral_capture";
        bool finished=false;
        try
        {
            foreach(Unit enemy in victims)
            {
                damage+=wholeBattlefield?Rank3AreaHit(source,enemy,ArachneRank3Rules.CocoonOpenRatio*scale):
                    Rank3Hit(source,enemy,ArachneRank3Rules.CocoonOpenRatio*scale);hits++;
                if(!IsCombatActive(enemy))continue;
                float duration=HostileStatusDuration(enemy,Mathf.Max(ArachneRank3Rules.CocoonDuration,
                    SpiderCombatRules.RootDuration(precise)));
                var binding=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Root,duration,source);
                if(binding.remaining>0f&&binding.stacks>0)
                    PlaySpiderSound(source,SpiderSound.Bind,enemy.model.transform.position);
                // Keep the existing immunity fallback; the damaging shell still forms.
                if(enemy.combatStatuses.ControlImmune&&!enemy.combatStatuses.DebuffImmune)
                {
                    enemy.rank3ActionSlowRatio=Mathf.Max(enemy.rank3ActionSlowRatio,.5f);
                    enemy.rank3ActionSlowTimer=Mathf.Max(enemy.rank3ActionSlowTimer,HostileStatusDuration(enemy,1.5f));
                }
                if(!mlEpisodeActive)
                {
                    PixelSpiderSilkBind fx=PixelSpiderSilkBind.PlayFuneral(Rank3Renderer(enemy),color,
                        ()=>this!=null&&!battleEnding&&generation==combatGeneration&&IsCombatActive(enemy));
                    if(wholeBattlefield)fx?.RevealClosed();
                    effects[enemy]=fx;if(fx!=null)funeralCocoons.Add(fx);
                    PixelSpiderCaptureNet.Hit(Rank3Renderer(enemy),color);
                }
            }
            for(int tick=0;tick<3;tick++)
            {
                yield return new WaitForSeconds(ArachneRank3Rules.CocoonTickInterval);
                if(battleEnding||generation!=combatGeneration)yield break;
                foreach(Unit enemy in victims)
                {
                    if(!IsCombatActive(enemy))continue;
                    PlaySpiderSound(source,SpiderSound.Compress,enemy.model.transform.position,.75f);
                    damage+=Rank3AreaHit(source,enemy,(desert?.85f:ArachneRank3Rules.CocoonTickRatio)*scale);hits++;
                    ApplyArachneRank3Intrinsic(source,enemy,false,poison,frost,scale);
                    if(effects.TryGetValue(enemy,out PixelSpiderSilkBind fx)&&fx!=null)fx.Compress();
                    if(!mlEpisodeActive)PixelSpiderCaptureNet.Hit(Rank3Renderer(enemy),color);
                }
            }
            // Snapshot every living shell before applying damage: one blast cannot erase another.
            var exploding=victims.Where(IsCombatActive).ToArray();
            var centers=exploding.ToDictionary(u=>u,u=>Flat(u.model.transform.position));
            if(exploding.Length>0)PlaySpiderSound(source,SpiderSound.CocoonBurst,centers[exploding[0]]);
            var splashHits=new Dictionary<Unit,int>();
            var blastTargets=new HashSet<Unit>(exploding);
            foreach(Unit owner in exploding)
            {
                foreach(Unit enemy in units)
                {
                    if(enemy==owner||!IsCombatActive(enemy)||enemy.team==source.team||
                       Vector3.Distance(centers[owner],Flat(enemy.model.transform.position))>
                           TileSize*ArachneRank3Rules.CocoonSplashRadiusTiles*radiusScale)continue;
                    int count=splashHits.TryGetValue(enemy,out int current)?current:0;
                    splashHits[enemy]=Mathf.Min(ArachneRank3Rules.CocoonSplashHits,count+1);
                    blastTargets.Add(enemy);
                }
                if(effects.TryGetValue(owner,out PixelSpiderSilkBind fx)&&fx!=null)
                    fx.Burst(TileSize*ArachneRank3Rules.CocoonSplashRadiusTiles*radiusScale,centers[owner]);
            }
            foreach(Unit enemy in blastTargets)
            {
                float ratio=(centers.ContainsKey(enemy)?ArachneRank3Rules.CocoonExplosionRatio:0f)+
                    (splashHits.TryGetValue(enemy,out int count)?count:0)*ArachneRank3Rules.CocoonSplashRatio;
                damage+=Rank3AreaHit(source,enemy,ratio*scale);hits++;
                if(!mlEpisodeActive)PixelSpiderCaptureNet.Hit(Rank3Renderer(enemy),color,true);
                if(!IsCombatActive(enemy))continue;
                if(poison){Rank3Poison(source,enemy,4f,.2f*scale);Rank3Poison(source,enemy,4f,.2f*scale);}
                if(frost)
                {
                    var frozen=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Freeze,HostileStatusDuration(enemy,1.25f),source);
                    if(frozen.remaining>0f&&frozen.stacks>0)PlayFreezeVisual(enemy,frozen.remaining);
                }
            }
            source.currentAction="cocoon_funeral_burst";
            RecordSkillEvent(source,wholeBattlefield?"silk_queen_cocoon_funeral":ArachneRank3Rules.CocoonFuneral,hits,damage);
            finished=true;
        }
        finally
        {
            foreach(PixelSpiderSilkBind fx in effects.Values)
            {
                if(!finished)funeralCocoons.Remove(fx);
                if(fx!=null){if(finished)fx.Stop();else fx.Cancel();}
            }
        }
    }
}
