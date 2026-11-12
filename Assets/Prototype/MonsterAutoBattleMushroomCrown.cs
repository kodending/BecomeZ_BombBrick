using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MushroomCrown
    {
        public Unit source,host;
        public Vector3 center;
        public float age,scale,radius,bloomAge;
        public int ticks,erosionHits,generation;
        public bool blooming,chain,poison,healing,seed;
        public PixelParasiticCrown effect;
        public MushroomPropagation propagation;
        public int propagationStage;
    }
    private readonly List<MushroomCrown> mushroomCrowns=new List<MushroomCrown>();

    private void CreateMushroomCrown(Unit source,Unit host,Vector3 center,float scale,float radiusScale,int generation,
        MushroomPropagation propagation=null,int propagationStage=0)
    {
        if(host!=null&&host.combatStatuses.RejectHarmfulEffect())return;
        // A recast grows a separate parasite; it cannot postpone an older parasite's bloom.
        var crown=new MushroomCrown
        {
            source=source,host=host,center=center,scale=scale,generation=generation,
            propagation=propagation??BeginMushroomPropagation(source,scale,radiusScale),propagationStage=propagationStage,
            radius=TileSize*MushroomRank3Rules.CrownFinalRadiusTiles*radiusScale,
            chain=HasRank3(source,MushroomCombatRules.VersusTraitId),
            poison=HasRank3(source,MushroomCombatRules.DoksirumTraitId),
            healing=HasRank3(source,MushroomCombatRules.LumirumTraitId),
            seed=HasRank3(source,MushroomCombatRules.KingmoreTraitId)
        };
        if(!mlEpisodeActive)
        {
            crown.effect=PixelParasiticCrown.Play(Rank3Renderer(host),center,crown.radius,MushroomGasColor(source,false));
            crown.effect.transform.SetParent(transform,true);
        }
        mushroomCrowns.Add(crown);
        source.currentAction="parasitic_crown";
        if(propagationStage>0)PlayMushroomSound(source,MushroomSound.Sprout,center,.85f);
        PlayMushroomSound(source,MushroomSound.Attach,center,.85f);
        if(!IsCombatActive(host))BloomMushroomCrown(crown);
    }

    private void UpdateMushroomCrowns(float dt)
    {
        if(dt<=0f)return;
        foreach(var crown in mushroomCrowns.ToArray())
        {
            if(crown.generation!=combatGeneration)
            {crown.effect?.Cancel();mushroomCrowns.Remove(crown);continue;}
            if(crown.blooming)crown.bloomAge+=dt;
            else
            {
                crown.age+=dt;
                if(crown.host.model!=null)crown.center=Flat(crown.host.model.transform.position);
                if(!IsCombatActive(crown.host))BloomMushroomCrown(crown);
                while(!crown.blooming&&crown.ticks<MushroomRank3Rules.CrownTickCount&&crown.age+.0001f>=crown.ticks+1)
                {
                    crown.ticks++;
                    float ratio=MushroomRank3Rules.CrownTickRatio*
                        (crown.chain?1f+Mathf.Min(2,crown.erosionHits)*MushroomCombatRules.ChainDamageBonusPerHit:1f);
                    float dealt=ApplyCrownHit(crown,crown.host,ratio,true);
                    if(dealt>0f)
                    {
                        crown.erosionHits++;crown.effect?.Pulse(crown.age);
                        if(!crown.blooming)PlayMushroomSound(crown.source,MushroomSound.Grow,crown.center,.6f);
                    }
                }
                if(!crown.blooming&&crown.age+.0001f>=MushroomRank3Rules.CrownDuration)BloomMushroomCrown(crown);
            }
            crown.effect?.Pose(crown.center,crown.age,crown.blooming?crown.bloomAge:-1f);
            if(crown.blooming&&crown.bloomAge>=.6f)
            {crown.effect?.Cancel();mushroomCrowns.Remove(crown);}
        }
    }

    private void NotifyMushroomCrownDeath(Unit host,Vector3 deathPosition)
    {
        foreach(var crown in mushroomCrowns.ToArray())
            if(!crown.blooming&&crown.host==host&&crown.generation==combatGeneration)
            {crown.center=deathPosition;BloomMushroomCrown(crown);}
    }

    private float ApplyCrownHit(MushroomCrown crown,Unit target,float ratio,bool erosion)
    {
        if(!IsCombatTargetable(target))return 0f;
        // Put the death seed in place before a lethal erosion/bloom hit.
        Unit oldSource=target.mushroomSporeMarkSource;
        float oldTime=target.mushroomSporeMarkTimer,oldDamage=target.mushroomSporeChildDamage,
            oldRadius=target.mushroomSporeChildRadius,oldDelay=target.mushroomSporeChildDelay;
        if(crown.seed&&!target.combatStatuses.RejectHarmfulEffect())
        {
            target.mushroomSporeMarkSource=crown.source;target.mushroomSporeMarkTimer=MushroomCombatRules.SporeSeedDuration;
            target.mushroomSporeChildDamage=crown.source.damage*MushroomCombatRules.GasChildDamageRatio*crown.scale;
            target.mushroomSporeChildRadius=TileSize*MushroomCombatRules.GasChildRadiusTiles;
            target.mushroomSporeChildDelay=MushroomCombatRules.GasChildDelay;
        }
        float raw=crown.source.damage*ratio*crown.scale;
        float dealt=erosion?
            ApplyGroundMushroomHit(crown.propagation,crown.propagationStage,crown.source,target,raw):
            ApplyMushroomHit(crown.propagation,crown.propagationStage,crown.source,target,raw);
        if(dealt<=0f&&crown.seed)
        {
            target.mushroomSporeMarkSource=oldSource;target.mushroomSporeMarkTimer=oldTime;
            target.mushroomSporeChildDamage=oldDamage;target.mushroomSporeChildRadius=oldRadius;target.mushroomSporeChildDelay=oldDelay;
        }
        if(dealt<=0f)return 0f;
        crown.source.skillHits++;crown.source.skillDamage+=dealt;
        if(erosion&&crown.poison&&IsCombatActive(target))
            Rank3Poison(crown.source,target,MushroomCombatRules.PoisonDuration,MushroomCombatRules.PoisonTickDamageRatio*crown.scale);
        if(!mlEpisodeActive)
        {
            PixelMushroomGas.Hit(Rank3Center(target),Rank3Renderer(target),MushroomGasColor(crown.source,false),!erosion
#if UNITY_EDITOR
                ,recurringGas:true
#endif
            );
            if(!erosion)
            {
                target.reaction?.PlayTintHit(new Color(1f,.92f,.72f));
                target.spriteAnimation?.PlayImpactRecoil(Flat(target.model.transform.position)-crown.center,.09f);
            }
        }
        RecordSkillEvent(crown.source,MushroomRank3Rules.ParasiticCrown+(erosion?"_erosion":"_bloom"),1,dealt);
        return dealt;
    }

    private void BloomMushroomCrown(MushroomCrown crown)
    {
        if(crown.blooming)return;
        crown.blooming=true;crown.bloomAge=0f; // Guard before damage: nearby parasites can chain on death.
        crown.effect?.Bloom(crown.center);
        foreach(var enemy in units.ToArray())
            if(IsCombatTargetable(enemy)&&enemy.team!=crown.source.team&&
               Vector3.Distance(crown.center,Flat(enemy.model.transform.position))<=crown.radius+enemy.collisionRadius)
                ApplyCrownHit(crown,enemy,MushroomRank3Rules.CrownFinalRatio,false);
        if(crown.healing)Heal(crown.source,SkillSupportAmount(crown.source,crown.source.damage*.8f*crown.scale));
        crown.source.traitActivations++;
        PlayMushroomSound(crown.source,MushroomSound.Bloom,crown.center);
    }

    private void ClearMushroomCrowns()
    {
        foreach(var crown in mushroomCrowns)crown.effect?.Cancel();
        mushroomCrowns.Clear();
    }
}
