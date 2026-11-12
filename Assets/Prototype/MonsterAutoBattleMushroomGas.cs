using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MushroomGasZone
    {
        public Unit source,root;
        public Vector3 center;
        public float radius,ratio,scale,start,duration,nextTick;
        public int generation,tick,tickCount;
        public bool chain,poison,healing,seed,activated,resonance;
        public int resonanceCount,breathSoundCycle;
        public float burstAt=-1f,burstVisualEnd;
        public Vector3 burstCenter;
        public PixelMushroomGas effect;
        public MushroomPropagation propagation;
        public int propagationStage;
        public readonly Dictionary<Unit,int> hits=new Dictionary<Unit,int>();
    }
    private readonly List<MushroomGasZone> mushroomGasZones=new List<MushroomGasZone>();
    // One damage/healing gate per caster and recipient, shared by overlapping clouds.
    private readonly Dictionary<Unit,Dictionary<Unit,float>> mushroomGasNextHit=
        new Dictionary<Unit,Dictionary<Unit,float>>();
    private float mushroomGasClock;
    private readonly Dictionary<Unit,float> mushroomResonanceNextHit=new Dictionary<Unit,float>();

    private void CreateMushroomGas(Unit source,Vector3 center,SkillMasterRow skill,
        float scale,float radiusScale,bool echo,int generation,Unit root=null,
        MushroomPropagation propagation=null,int propagationStage=0)
    {
        var gas=new MushroomGasZone
        {
            source=source,root=root,center=Flat(center),radius=TileSize*skill.radiusTiles*radiusScale,
            propagation=propagation??(!echo?BeginMushroomPropagation(source,scale,radiusScale):null),
            propagationStage=propagationStage,
            ratio=skill.damageMultiplier,scale=scale,start=mushroomGasClock,
            duration=skill.durationSeconds,nextTick=mushroomGasClock+MushroomCombatRules.GasTickInterval,
            generation=generation,tickCount=Mathf.Max(1,Mathf.RoundToInt(skill.durationSeconds/MushroomCombatRules.GasTickInterval)),
            chain=!echo&&HasRank3(source,MushroomCombatRules.VersusTraitId),
            poison=!echo&&HasRank3(source,MushroomCombatRules.DoksirumTraitId),
            healing=!echo&&HasRank3(source,MushroomCombatRules.LumirumTraitId),
            seed=!echo&&HasRank3(source,MushroomCombatRules.KingmoreTraitId),
            resonance=!echo&&HasRank3(source,MushroomRank3Rules.SporeResonance)
        };
        if(root!=null)
        {
            gas.radius=TileSize*MushroomRank3Rules.MycokingRadiusTiles*radiusScale;
            gas.ratio=MushroomRank3Rules.MycokingGasRatio;gas.duration=MushroomRank3Rules.MycokingDuration;
            gas.tickCount=Mathf.RoundToInt(gas.duration/MushroomCombatRules.GasTickInterval);gas.resonance=false;
        }
        if(!mlEpisodeActive)
        {
            gas.effect=PixelMushroomGas.Play(gas.center,gas.radius,gas.duration,
                MushroomGasColor(source,echo),Rank3Renderer(root??source));
            if(root!=null)gas.effect.RootedBreaths();
            gas.effect.transform.SetParent(transform,true);
        }
        mushroomGasZones.Add(gas);
        if(!mushroomGasNextHit.ContainsKey(source))mushroomGasNextHit[source]=new Dictionary<Unit,float>();
        source.currentAction="spore_gas";
        if(propagationStage>0)PlayMushroomSound(source,MushroomSound.Sprout,gas.center,.85f);
        PlayMushroomSound(source,root==null?MushroomSound.Spread:MushroomSound.Breath,
            gas.center,echo?.5f:root==null?.75f:.8f);
    }

    private static Color MushroomGasColor(Unit source,bool echo)
    {
        if(!echo&&MonsterTraitRuntime.Has(source.activeTraitIds,MushroomCombatRules.DoksirumTraitId))return new Color(.57f,.75f,.24f);
        if(!echo&&MonsterTraitRuntime.Has(source.activeTraitIds,MushroomCombatRules.LumirumTraitId))return new Color(.35f,.84f,.61f);
        if(!echo&&MonsterTraitRuntime.Has(source.activeTraitIds,MushroomCombatRules.KingmoreTraitId))return new Color(.85f,.65f,.3f);
        return new Color(.67f,.46f,.76f);
    }

    private void UpdateMushroomGasZones(float dt)
    {
        if(dt<=0f)return;
        mushroomGasClock+=dt;
        for(int i=mushroomGasZones.Count-1;i>=0;i--)
        {
            var gas=mushroomGasZones[i];
            if(ReleasedSkillValid(gas.source,gas.generation)&&(gas.root==null||IsCombatActive(gas.root)))continue;
            gas.effect?.Cancel();mushroomGasZones.RemoveAt(i);
            if(gas.root!=null)DeactivateRank3Special(gas.root);
        }
        // Process due ticks chronologically, including long frames and staggered overlapping casts.
        while(true)
        {
            MushroomGasZone next=null;
            foreach(var gas in mushroomGasZones)
                if(gas.tick<gas.tickCount&&gas.nextTick<=mushroomGasClock+.0001f&&
                   (next==null||gas.nextTick<next.nextTick||
                    Mathf.Approximately(gas.nextTick,next.nextTick)&&gas.scale>next.scale))next=gas;
            if(next==null)break;
            float at=next.nextTick;next.tick++;
            next.nextTick=next.start+(next.tick+1)*MushroomCombatRules.GasTickInterval;
            ApplyMushroomGasTick(next,at);
        }
        for(int i=mushroomGasZones.Count-1;i>=0;i--)
        {
            var gas=mushroomGasZones[i];float age=mushroomGasClock-gas.start;
            if(gas.root!=null)
            {
                gas.root.rank3SummonTimer=Mathf.Max(0f,gas.duration-age);
                gas.root.mycokingFx?.Pose(age);
                int cycle=Mathf.FloorToInt(age/MushroomRank3Rules.MycokingBreathInterval);
                if(age<gas.duration&&cycle>gas.breathSoundCycle&&IsCombatActive(gas.root))
                {
                    // Follow the visible exhalation; skip missed cycles on a long frame.
                    gas.breathSoundCycle=cycle;
                    PlayMushroomSound(gas.source,MushroomSound.Breath,gas.center,.8f);
                }
            }
            gas.effect?.Pose(age);
            if(gas.burstAt>=0f&&gas.burstAt<=mushroomGasClock+.0001f)
            {
                gas.burstAt=-1f;
                ResolveMushroomResonance(gas);
            }
            if(age<gas.duration||gas.burstAt>=0f||mushroomGasClock<gas.burstVisualEnd)continue;
            gas.effect?.Cancel();mushroomGasZones.RemoveAt(i);
            if(gas.root!=null)DeactivateRank3Special(gas.root);
        }
    }

    // Called only by a landed basic attack, after its damage/death resolution.
    private void TriggerMushroomResonance(Unit attacker,Unit target,Vector3 point)
    {
        if(attacker==null||target==null||attacker.team==target.team)return;
        foreach(var gas in mushroomGasZones)
        {
            if(!gas.resonance||gas.source.team!=attacker.team||
               gas.generation!=combatGeneration||mushroomGasClock>=gas.start+gas.duration||
               gas.resonanceCount>=MushroomRank3Rules.ResonanceBurstLimit||
               Vector3.Distance(gas.center,point)>gas.radius+target.collisionRadius||
               mushroomResonanceNextHit.TryGetValue(gas.source,out float next)&&mushroomGasClock+.0001f<next)continue;
            mushroomResonanceNextHit[gas.source]=mushroomGasClock+MushroomRank3Rules.ResonanceInterval;
            gas.resonanceCount++;
            gas.burstCenter=point;
            gas.burstAt=mushroomGasClock+MushroomRank3Rules.ResonanceWindup;
            gas.burstVisualEnd=gas.burstAt+.45f;
            gas.effect?.Resonate(point,mushroomGasClock-gas.start,TileSize*MushroomRank3Rules.ResonanceRadiusTiles);
        }
    }

    private void ResolveMushroomResonance(MushroomGasZone gas)
    {
        float radius=TileSize*MushroomRank3Rules.ResonanceRadiusTiles;
        int hits=0;float damage=0f;
        foreach(Unit enemy in units.ToArray())
        {
            if(!IsCombatTargetable(enemy)||enemy.team==gas.source.team)continue;
            Vector3 point=Flat(enemy.model.transform.position);
            if(Vector3.Distance(point,gas.center)>gas.radius+enemy.collisionRadius||
               Vector3.Distance(point,gas.burstCenter)>radius+enemy.collisionRadius)continue;
            float applied=ApplyMushroomHit(gas.propagation,gas.propagationStage,gas.source,enemy,
                gas.source.damage*MushroomRank3Rules.ResonanceDamageRatio*gas.scale);
            if(applied<=0f)continue;
            hits++;damage+=applied;
            if(!mlEpisodeActive)
            {
                PixelMushroomGas.Hit(Rank3Center(enemy),Rank3Renderer(enemy),MushroomGasColor(gas.source,false),true);
                enemy.reaction?.PlayTintHit(new Color(1f,.94f,.74f));
                enemy.spriteAnimation?.PlayImpactRecoil(point-gas.burstCenter,.06f);
            }
        }
        gas.source.skillHits+=hits;gas.source.skillDamage+=damage;gas.source.traitActivations++;
        PlayMushroomSound(gas.source,MushroomSound.Resonance,gas.burstCenter,.85f);
        RecordSkillEvent(gas.source,MushroomRank3Rules.SporeResonance,hits,damage);
    }

    private void ApplyMushroomGasTick(MushroomGasZone gas,float at)
    {
        Unit source=gas.source;if(!ReleasedSkillValid(source,gas.generation))return;
        var gate=mushroomGasNextHit[source];int hits=0;float dealt=0f;
        foreach(Unit target in units.ToArray())
        {
            if(!IsCombatTargetable(target)||Vector3.Distance(gas.center,Flat(target.model.transform.position))>
               gas.radius+target.collisionRadius||gate.TryGetValue(target,out float next)&&at+.0001f<next)continue;
            if(target.team==source.team)
            {
                if(!gas.healing||target.augmentShadowClone)continue;
                gate[target]=at+MushroomCombatRules.GasTickInterval;
                float hp=target.hp;
                Heal(target,SkillSupportAmount(source,MushroomCombatRules.Healing(source.damage)*
                    MushroomCombatRules.SporeCount/gas.tickCount*gas.scale));
                if(target.hp>hp)
                {
                    gas.activated=true;
                    if(!mlEpisodeActive)PixelMushroomGas.Hit(Rank3Center(target),Rank3Renderer(target),MushroomGasColor(source,false)
#if UNITY_EDITOR
                        ,recurringGas:true
#endif
                    );
                }
                continue;
            }
            gate[target]=at+MushroomCombatRules.GasTickInterval;
            int previous=gas.hits.TryGetValue(target,out int count)?count:0;
            float raw=MushroomCombatRules.ExplosionDamage(source.damage,gas.ratio,Mathf.Min(2,previous),gas.chain);
            if(gas.poison&&gas.tick%2==0&&gas.tick<=6)raw+=MushroomCombatRules.PoisonTickDamage(source.damage);
            Unit oldSeed=target.mushroomSporeMarkSource;float oldTimer=target.mushroomSporeMarkTimer;
            float oldDamage=target.mushroomSporeChildDamage,oldRadius=target.mushroomSporeChildRadius,oldDelay=target.mushroomSporeChildDelay;
            if(gas.seed&&!target.combatStatuses.RejectHarmfulEffect())
            {
                target.mushroomSporeMarkSource=source;target.mushroomSporeMarkTimer=MushroomCombatRules.SporeSeedDuration;
                target.mushroomSporeChildDamage=source.damage*MushroomCombatRules.GasChildDamageRatio*gas.scale;
                target.mushroomSporeChildRadius=TileSize*MushroomCombatRules.GasChildRadiusTiles;
                target.mushroomSporeChildDelay=MushroomCombatRules.GasChildDelay;
            }
            float applied=ApplyGroundMushroomHit(gas.propagation,gas.propagationStage,source,target,raw*gas.scale);
            if(applied<=0f)
            {
                if(gas.seed){target.mushroomSporeMarkSource=oldSeed;target.mushroomSporeMarkTimer=oldTimer;
                    target.mushroomSporeChildDamage=oldDamage;target.mushroomSporeChildRadius=oldRadius;target.mushroomSporeChildDelay=oldDelay;}
                continue;
            }
            gas.hits[target]=previous+1;hits++;dealt+=applied;
            if(gas.chain&&previous>0||gas.poison||gas.seed)gas.activated=true;
            if(gas.poison&&IsCombatActive(target))target.combatStatuses.ApplyStacks(
                CombatStatusKind.Poison,.55f,1,CombatStatusState.MaximumPoisonStacks,source,0f,0f,true);
            if(!mlEpisodeActive)PixelMushroomGas.Hit(Rank3Center(target),Rank3Renderer(target),MushroomGasColor(source,false)
#if UNITY_EDITOR
                ,recurringGas:true
#endif
            );
        }
        source.skillHits+=hits;source.skillDamage+=dealt;
        if(gas.tick==gas.tickCount&&gas.activated)source.traitActivations++;
        RecordSkillEvent(source,MushroomCombatRules.SkillId+"_gas_tick",hits,dealt);
    }

    private void ClearMushroomGasZones()
    {
        ClearMushroomDeathBlooms(battleEnding);
        ClearMushroomPropagations();
        foreach(var gas in mushroomGasZones)
        {
            gas.effect?.Cancel();
            if(gas.root==null)continue;
            gas.root.mycokingFx?.Cancel();gas.root.mycokingFx=null;
            DeactivateRank3Special(gas.root);
        }
        mushroomGasZones.Clear();mushroomGasNextHit.Clear();mushroomResonanceNextHit.Clear();mushroomGasClock=0f;
        foreach(var effect in GetComponentsInChildren<PixelMycoking>(true))effect.Cancel();
    }
}
