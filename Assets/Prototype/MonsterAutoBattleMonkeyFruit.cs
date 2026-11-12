using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private IEnumerator ResolveMonkeyReturningFruit(Unit source,Unit initialTarget,float scale,int generation)
    {
        if(battleEnding||generation!=combatGeneration||!IsCombatActive(source))yield break;
        bool precise=HasRank3(source,MonkeyCombatRules.ChiwooTraitId);
        bool poison=HasRank3(source,MonkeyCombatRules.TotoTraitId);
        bool cloud=HasRank3(source,MonkeyCombatRules.UnoTraitId);
        bool powder=HasRank3(source,MonkeyCombatRules.MakaTraitId);
        Color color=poison?new Color(.62f,.25f,.9f):powder?new Color(1f,.32f,.12f):
            cloud?new Color(.72f,.92f,1f):new Color(1f,.8f,.36f);
        Vector3 origin=Rank3Center(source);
        Vector3 direction=initialTarget!=null&&initialTarget.model!=null?Flat(initialTarget.model.transform.position)-Flat(origin):Vector3.right;
        Func<bool> valid=()=>!battleEnding&&generation==combatGeneration;
        var contacted=new HashSet<Unit>[10];
        for(int i=0;i<contacted.Length;i++)contacted[i]=new HashSet<Unit>();
        var cloudShots=new List<PixelStarProjectile>();
        Unit previous=null;int hits=0,combo=0,pendingCloud=0;float dealt=0f;
        bool cloudReleased=false,shaken=false;
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,.58f);
        var barrage=PixelMonkeyReturn.Play(origin,direction,Rank3Renderer(source),color,valid,()=>Rank3Center(source),
            index=>
            {
                source.spriteAnimation?.PlayMonkeyThrowFollowThrough(index==4?.18f:.14f,index==4,direction);
                PlayMonkeySound(source,MonkeySound.Spin,Rank3Center(source),index==4?1f:.7f);
            },
            (index,returning,start,end)=>
            {
                if(!valid())return;
                start=Flat(start);end=Flat(end);
                float radius=TileSize*MonkeyRank3Rules.BoomerangRadiusTiles*(index==4?1.25f:1f);
                var victims=Rank3Enemies(source,(start+end)*.5f,Vector3.Distance(start,end)*.5f+radius);
                victims.Sort((a,b)=>Vector3.SqrMagnitude(Flat(a.model.transform.position)-start).CompareTo(
                    Vector3.SqrMagnitude(Flat(b.model.transform.position)-start)));
                foreach(var victim in victims)
                {
                    if(!valid()||!IsCombatActive(victim))continue;
                    if(DistanceToSegment(Flat(victim.model.transform.position),start,end)>radius+victim.collisionRadius||
                        !contacted[index+(returning?5:0)].Add(victim))continue;
                    bool final=returning&&index==4;
                    combo=victim==previous?combo+1:0;previous=victim;
                    float ratio=returning?(final?MonkeyRank3Rules.ReturnFinalRatio:MonkeyRank3Rules.ReturnInboundRatio):
                        MonkeyRank3Rules.ReturnOutboundRatio;
                    float damage=MonkeyCombatRules.ProjectileDamage(source.damage,ratio,
                        Mathf.Min(combo,MonkeyRank3Rules.ReturnMaximumCombo),precise,final&&powder)*scale;
                    Vector3 impact=Rank3Center(victim);
                    PixelCombatEffect.PlayMonkeyFruitPop(impact,Rank3Renderer(victim),color,false,final?1.9f:returning?1.35f:1f
#if UNITY_EDITOR
                        ,secondary:!final
#endif
                    );
                    if(final&&!shaken){shaken=true;combatCameraDirector?.ReportMonkeyFruitImpact(true);}
                    PlayMonkeySound(source,MonkeySound.Pierce,impact,final?1f:returning?.75f:.5f);
                    dealt+=ApplySkillDamage(source,victim,damage,false);hits++;
                    if(poison)dealt+=ApplyMonkeyPoisonFruit(source,victim,scale);
                    victim.spriteAnimation?.PlayImpactRecoil(end-start,final?.12f:.05f);
                    // One cloud volley per cast, even when the final banana pierces a crowd.
                    if(!final||!cloud||cloudReleased||victim.model==null)continue;
                    cloudReleased=true;
                    foreach(var next in Rank3Enemies(source,Flat(impact),TileSize*MonkeyCombatRules.CloudBounceRadiusTiles)
                        .Where(enemy=>enemy!=victim).OrderBy(enemy=>
                            (Flat(enemy.model.transform.position)-Flat(impact)).sqrMagnitude).Take(MonkeyCombatRules.CloudBounceCount).ToList())
                    {
                        pendingCloud++;
                        Action arrive=()=>
                        {
                            pendingCloud--;
                            if(!valid()||!IsCombatActive(next))return;
                            PlayMonkeySound(source,MonkeySound.Hit,Rank3Center(next),.6f
#if UNITY_EDITOR
                                ,secondary:true
#endif
                            );
                            dealt+=ApplySkillDamage(source,next,damage,false);hits++;
                        };
                        var shot=EmitMonkeyCloudBounce(source,victim,next,arrive);
                        if(shot==null)arrive();else cloudShots.Add(shot);
                    }
                }
            },()=>Rank3Center(source),source.monkeyNimbus==null?float.NaN:
                (initialTarget==null?.45f:Rank3Center(initialTarget).y),
            (index,position)=>PlayMonkeySound(source,MonkeySound.Return,position,index==4?1f:.65f));
        try
        {
            while(barrage!=null&&!barrage.Complete&&valid())yield return null;
            float remaining=1f;
            while(pendingCloud>0&&remaining>0f&&valid())
            {remaining-=CombatSkillClock.Delta;yield return null;}
            if(valid())
            {
                source.currentAction="monkey_boomerang_complete";source.traitActivations++;
                RecordSkillEvent(source,MonkeyRank3Rules.MyriadReturn,hits,dealt);
            }
        }
        finally
        {
            if(barrage!=null)barrage.Cancel();
            foreach(var shot in cloudShots)if(shot!=null)shot.Cancel();
            source.skillHits+=hits;source.skillDamage+=dealt;
        }
    }

    private IEnumerator ResolveMonkeyMortar(Unit source,Vector3 center,float scale,float radiusScale,int generation)
    {
        if(battleEnding||generation!=combatGeneration||!IsCombatActive(source))yield break;
        bool precise=HasRank3(source,MonkeyCombatRules.ChiwooTraitId);
        bool poison=HasRank3(source,MonkeyCombatRules.TotoTraitId);
        bool cloud=HasRank3(source,MonkeyCombatRules.UnoTraitId);
        bool powder=HasRank3(source,MonkeyCombatRules.MakaTraitId);
        Color color=poison?new Color(.62f,.25f,.9f):powder?new Color(1f,.32f,.12f):
            cloud?new Color(.72f,.92f,1f):new Color(1f,.8f,.36f);
        var renderer=Rank3Renderer(source);
        Vector3 origin=renderer==null?source.model.transform.position+Vector3.up*.7f:renderer.bounds.center;
        float radius=TileSize*MonkeyRank3Rules.MortarRadiusTiles*radiusScale;
        float finalRadius=TileSize*(powder?2.5f:MonkeyRank3Rules.MortarFinalRadiusTiles)*radiusScale;
        Func<bool> valid=()=>!battleEnding&&generation==combatGeneration;
        var cloudShots=new List<PixelStarProjectile>();
        var consecutive=new Dictionary<Unit,int>();
        int hits=0,pendingCloud=0;float dealt=0f;
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,.56f);
        var barrage=PixelMonkeyMortar.Play(origin,center,renderer,color,radius,finalRadius,valid,
            index=>
            {
                source.spriteAnimation?.PlayMonkeyThrowFollowThrough(index==4?.18f:.14f,index==4,Vector3.up);
                PlayMonkeySound(source,MonkeySound.Throw,Rank3Center(source),index==4?1f:.7f
#if UNITY_EDITOR
                    ,secondary:index!=4
#endif
                );
            },
            index=>
            {
                if(!valid())return;
                bool final=index==4;
                float reach=final?finalRadius:radius;
                var victims=Rank3Enemies(source,center,reach).OrderBy(enemy=>
                    (Flat(enemy.model.transform.position)-center).sqrMagnitude).ToList();
                PixelStarProjectile.PlayBeanImpact(center+Vector3.up*.2f,renderer,color,true,reach,final?1.35f:.7f
#if UNITY_EDITOR
                    ,secondary:!final
#endif
                );
                PlayMonkeySound(source,final?MonkeySound.Burst:MonkeySound.Land,center,final?1f:.8f);
                if(final)combatCameraDirector?.ReportMonkeyFruitImpact(true);
                float cloudDamage=0f;
                foreach(var missed in consecutive.Keys.Where(enemy=>!victims.Contains(enemy)).ToArray())
                    consecutive.Remove(missed);
                foreach(var victim in victims)
                {
                    int previous=consecutive.TryGetValue(victim,out int count)?count:0;
                    float damage=MonkeyCombatRules.ProjectileDamage(source.damage,
                        final?MonkeyRank3Rules.MortarFinalRatio:MonkeyRank3Rules.MortarRatio,
                        previous,precise,final&&powder)*scale;
                    if(victim==victims[0])cloudDamage=damage;
                    consecutive[victim]=previous+1;
                    var hitRenderer=Rank3Renderer(victim);
                    PixelCombatEffect.PlayMonkeyFruitPop(hitRenderer==null?
                        victim.model.transform.position+Vector3.up*.35f:hitRenderer.bounds.center,
                        hitRenderer,color,false
#if UNITY_EDITOR
                        ,secondary:true
#endif
                    );
                    dealt+=ApplySkillDamage(source,victim,damage,false);hits++;
                    if(poison)dealt+=ApplyMonkeyPoisonFruit(source,victim,scale);
                    victim.spriteAnimation?.PlayImpactRecoil(Vector3.down,final?.12f:.07f);
                }
                if(!final||!cloud||victims.Count==0||victims[0].model==null)return;
                Unit primary=victims[0];
                foreach(var victim in Rank3Enemies(source,center,TileSize*MonkeyCombatRules.CloudBounceRadiusTiles)
                    .Where(enemy=>enemy!=primary).OrderBy(enemy=>
                        (Flat(enemy.model.transform.position)-center).sqrMagnitude).Take(MonkeyCombatRules.CloudBounceCount))
                {
                    pendingCloud++;
                    Action arrive=()=>
                    {
                        pendingCloud--;
                        if(!valid()||!IsCombatActive(victim))return;
                        PlayMonkeySound(source,MonkeySound.Hit,Rank3Center(victim),.6f
#if UNITY_EDITOR
                            ,secondary:true
#endif
                        );
                        dealt+=ApplySkillDamage(source,victim,cloudDamage,false);hits++;
                    };
                    var shot=EmitMonkeyCloudBounce(source,primary,victim,arrive);
                    if(shot==null)arrive();else cloudShots.Add(shot);
                }
            },()=>Rank3Center(source),
            index=>PlayMonkeySound(source,MonkeySound.Fall,center,index==4?1f:.65f));
        try
        {
            while(barrage!=null&&!barrage.Complete&&valid())yield return null;
            float remaining=1f;
            while(pendingCloud>0&&remaining>0f&&valid())
            {remaining-=CombatSkillClock.Delta;yield return null;}
            if(valid())
            {
                source.currentAction="monkey_mortar_complete";source.traitActivations++;
                RecordSkillEvent(source,MonkeyRank3Rules.FiveOrbJuggling,hits,dealt);
            }
        }
        finally
        {
            if(barrage!=null)barrage.Cancel();
            foreach(var shot in cloudShots)if(shot!=null)shot.Cancel();
            source.skillHits+=hits;source.skillDamage+=dealt;
        }
    }

    private Vector3 NextMonkeyFruitLanding(Unit source,Vector3 center,HashSet<Unit> visited)
    {
        Unit next=Rank3Enemies(source,center,TileSize*MonkeyRank3Rules.FruitSearchTiles)
            .Where(enemy=>!visited.Contains(enemy))
            .OrderBy(enemy=>(Flat(enemy.model.transform.position)-center).sqrMagnitude)
            .FirstOrDefault();
        if(next==null)return center;
        visited.Add(next);
        return Vector3.MoveTowards(center,Flat(next.model.transform.position),
            TileSize*MonkeyRank3Rules.FruitStepTiles);
    }

    private IEnumerator ResolveMonkeyKingFruit(Unit source,Unit initialTarget,float scale,
        float radiusScale,int combo,int generation,Action<int,float> report)
    {
        if(!ReleasedSkillValid(source,generation)||!IsCombatActive(initialTarget))yield break;
        bool precise=HasRank3(source,MonkeyCombatRules.ChiwooTraitId);
        bool poison=HasRank3(source,MonkeyCombatRules.TotoTraitId);
        bool cloud=HasRank3(source,MonkeyCombatRules.UnoTraitId);
        bool powder=HasRank3(source,MonkeyCombatRules.MakaTraitId);
        Color color=poison?new Color(.62f,.12f,.82f):powder?new Color(.92f,.15f,.035f):
            cloud?new Color(.72f,.92f,1f):new Color(1f,.72f,.23f);
        SpriteRenderer renderer=Rank3Renderer(source);
        Vector3 center=Flat(initialTarget.model.transform.position);
        Vector3 from=renderer==null?source.model.transform.position+Vector3.up*.7f:renderer.bounds.center;
        var visited=new HashSet<Unit>{initialTarget};
        var cloudShots=new List<PixelStarProjectile>();
        int pendingCloud=0;
        source.spriteAnimation?.PlayMonkeyThrowFollowThrough(.18f,true,center-from);
        PlayMonkeySound(source,MonkeySound.Throw,from);
        try
        {
            for(int bounce=0;bounce<3;bounce++)
            {
                if(battleEnding||generation!=combatGeneration)yield break;
                bool final=bounce==2;
                float radius=TileSize*(final?(powder?2.5f:MonkeyRank3Rules.FruitFinalRadiusTiles):
                    MonkeyRank3Rules.FruitBounceRadiusTiles)*radiusScale;
                float damage=MonkeyCombatRules.ProjectileDamage(source.damage,
                    final?MonkeyRank3Rules.FruitFinalRatio:MonkeyRank3Rules.FruitBounceRatio,
                    combo,precise,final&&powder)*scale;
                Vector3 landing=center;
                bool sweptAway=false;
                PixelStarProjectile fruit=PixelStarProjectile.PlayKingFruit(
                    from,landing,renderer,color,bounce,()=>
                    {
                        if(battleEnding||generation!=combatGeneration)return;
                        var victims=Rank3Enemies(source,landing,radius)
                            .OrderBy(enemy=>(Flat(enemy.model.transform.position)-landing).sqrMagnitude).ToList();
                        Unit primary=victims.FirstOrDefault();
                        // Contact damage and the matching burst happen on this exact arrival frame.
                        if(final)
                        {
                            PixelStarProjectile.PlayBeanImpact(landing+Vector3.up*.55f,renderer,color,true,radius);
                            combatCameraDirector?.ReportMonkeyFruitImpact(true);
                            PlayMonkeySound(source,MonkeySound.Burst,landing);
                        }
                        else
                        {
                            PixelStarProjectile.PlayBeanImpact(landing+Vector3.up*.15f,renderer,color,false,radius
#if UNITY_EDITOR
                                ,secondary:true
#endif
                            );
                            PlayMonkeySound(source,MonkeySound.Bounce,landing);
                        }
                        int hits=0;float dealt=0f;
                        foreach(Unit victim in victims)
                        {
                            var hitRenderer=Rank3Renderer(victim);
                            PixelCombatEffect.PlayMonkeyFruitPop(hitRenderer==null?
                                victim.model.transform.position+Vector3.up*.35f:hitRenderer.bounds.center,
                                hitRenderer,color,false
#if UNITY_EDITOR
                                ,secondary:true
#endif
                            );
                            dealt+=ApplySkillDamage(source,victim,damage,false);hits++;
                            if(poison)dealt+=ApplyMonkeyPoisonFruit(source,victim,scale);
                            if(victim.spriteAnimation!=null)victim.spriteAnimation.PlayImpactRecoil(
                                Flat(victim.model.transform.position)-landing,final?.12f:.05f);
                        }
                        report(hits,dealt);
                        if(!final||!cloud||primary==null||primary.model==null)return;
                        var bounces=Rank3Enemies(source,landing,TileSize*MonkeyCombatRules.CloudBounceRadiusTiles)
                            .Where(enemy=>enemy!=primary).OrderBy(enemy=>
                                (Flat(enemy.model.transform.position)-landing).sqrMagnitude)
                            .Take(MonkeyCombatRules.CloudBounceCount).ToList();
                        foreach(Unit victim in bounces)
                        {
                            pendingCloud++;
                            Action arrive=()=>
                            {
                                pendingCloud--;
                                if(!battleEnding&&generation==combatGeneration&&IsCombatActive(victim))
                                {
                                    PlayMonkeySound(source,MonkeySound.Hit,Rank3Center(victim),.6f
#if UNITY_EDITOR
                                        ,secondary:true
#endif
                                    );
                                    report(1,ApplySkillDamage(source,victim,damage,false));
                                }
                            };
                            var shot=EmitMonkeyCloudBounce(source,primary,victim,arrive);
                            if(shot==null)arrive();else cloudShots.Add(shot);
                        }
                    });
                fruit.MultiplyVisualScale(MonkeyCombatRules.SkillProjectileVisualMultiplier);
                fruit.OnDeflected=()=>sweptAway=true;
                try
                {
                    while(!battleEnding&&fruit!=null&&!fruit.FruitBounceReady&&generation==combatGeneration)yield return null;
                }
                finally
                {
                    if(fruit!=null)fruit.Cancel();
                }
                if(sweptAway)yield break;
                from=landing+Vector3.up*.55f;
                if(!battleEnding&&!final&&generation==combatGeneration)
                    center=NextMonkeyFruitLanding(source,landing,visited);
            }
            float remaining=1f;
            while(!battleEnding&&pendingCloud>0&&remaining>0f&&generation==combatGeneration)
            {remaining-=CombatSkillClock.Delta;yield return null;}
        }
        finally
        {
            foreach(var shot in cloudShots)if(shot!=null)shot.Cancel();
        }
    }
}
