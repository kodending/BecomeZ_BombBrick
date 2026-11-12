using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BombBrick.Game.Core;

public static class MushroomPropagationRules
{
    public const string TraitId="trait_transcend_mushroom_endless_proliferation";
    public const float MarkDuration=4f,RangeTiles=3f,Power=.5f,RadiusScale=.75f,FlightDuration=.72f;
    public const int MaximumTransfers=2;
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MushroomPropagation
    {
        public Unit source,target;
        public string trait;
        public int generation,stage;
        public float scale,radiusScale,expires,flight=-1f;
        public Vector3 origin;
        public PixelMushroomMigration effect;
        public readonly Dictionary<Unit,float> marks=new Dictionary<Unit,float>();
    }
    private readonly List<MushroomPropagation> mushroomPropagations=new List<MushroomPropagation>();
    private float mushroomPropagationClock;

    private MushroomPropagation BeginMushroomPropagation(Unit source,float scale,float radiusScale)
    {
        // The saved rank-4 trait now owns the death bloom, rather than reproducing rank-3 casts.
        return null;
    }

    private float ApplyMushroomHit(MushroomPropagation chain,int stage,Unit source,Unit target,float damage,bool resolvedPower=false)
        =>ApplyMushroomHitCore(chain,stage,source,target,damage,resolvedPower,true);

    private float ApplyGroundMushroomHit(MushroomPropagation chain,int stage,Unit source,Unit target,float damage,bool resolvedPower=false)
        =>ApplyMushroomHitCore(chain,stage,source,target,damage,resolvedPower,false);

    private float ApplyMushroomHitCore(MushroomPropagation chain,int stage,Unit source,Unit target,
        float damage,bool resolvedPower,bool allowHitSlide)
    {
        bool canMark=chain!=null&&chain.stage==stage&&stage<MushroomPropagationRules.MaximumTransfers&&
            chain.flight<0f&&chain.generation==combatGeneration&&
            IsCombatTargetable(target)&&target.team!=source.team;
        float previous=0f;bool hadPrevious=canMark&&chain.marks.TryGetValue(target,out previous);
        // Arm before damage so this hit's own lethal damage qualifies. Revert rejected hits.
        if(canMark)chain.marks[target]=mushroomPropagationClock+MushroomPropagationRules.MarkDuration;
        float applied=resolvedPower?ApplyDamageComponents(source,target,damage,true,false,false,true,
            0f,0f,1f,false,false,false,0f,skillImpact:true,allowHitSlide:allowHitSlide,bypassProjectileWalls:true):
            ApplySkillDamage(source,target,new SkillDamageFormulaResult(damage,0f,0f),false,allowHitSlide:allowHitSlide,
                bypassProjectileWalls:true);
        if(canMark&&applied<=0f&&chain.stage==stage)
        {
            if(hadPrevious)chain.marks[target]=previous;else chain.marks.Remove(target);
        }
        return applied;
    }

    private Unit FindMushroomPropagationTarget(MushroomPropagation chain)
    {
        float range=TileSize*MushroomPropagationRules.RangeTiles;
        return units.Where(u=>IsCombatTargetable(u)&&u.team!=chain.source.team&&
            Vector3.Distance(chain.origin,Flat(u.model.transform.position))<=range)
            .OrderBy(u=>Vector3.SqrMagnitude(chain.origin-Flat(u.model.transform.position)))
            .ThenBy(u=>u.stableId).FirstOrDefault();
    }

    private void NotifyMushroomPropagationDeath(Unit dead,Vector3 position)
    {
        foreach(var chain in mushroomPropagations.ToArray())
        {
            if(!chain.marks.TryGetValue(dead,out float until))continue;
            chain.marks.Remove(dead);
            if(until<mushroomPropagationClock||chain.flight>=0f||chain.stage>=MushroomPropagationRules.MaximumTransfers||
               chain.generation!=combatGeneration)continue;
            chain.origin=position;chain.target=FindMushroomPropagationTarget(chain);
            if(chain.target==null)continue;
            // Retire every mark of the previous stage before nested death callbacks can branch.
            chain.stage++;chain.marks.Clear();chain.flight=0f;
            chain.expires=mushroomPropagationClock+13f;
            if(!mlEpisodeActive)
            {
                chain.effect=PixelMushroomMigration.Play(position,Rank3Renderer(chain.source),
                    MushroomGasColor(chain.source,false));
                chain.effect.transform.SetParent(transform,true);
            }
            chain.source.traitActivations++;
            PlayMushroomSound(chain.source,MushroomSound.Transfer,position,.8f);
            RecordSkillEvent(chain.source,MushroomPropagationRules.TraitId,0,0f);
        }
    }

    private void UpdateMushroomPropagations(float dt)
    {
        if(dt<=0f)return;
        mushroomPropagationClock+=dt;
        foreach(var chain in mushroomPropagations.ToArray())
        {
            if(chain.generation!=combatGeneration||
               mushroomPropagationClock>chain.expires)
            {chain.effect?.Cancel();mushroomPropagations.Remove(chain);continue;}
            if(chain.flight<0f)continue;
            if(!IsCombatTargetable(chain.target))chain.target=FindMushroomPropagationTarget(chain);
            if(chain.target==null)
            {chain.effect?.Cancel();mushroomPropagations.Remove(chain);continue;}
            chain.flight+=dt;
            Vector3 at=Flat(chain.target.model.transform.position);
            chain.effect?.Pose(at,chain.flight);
            if(chain.flight+.0001f<MushroomPropagationRules.FlightDuration)continue;
            chain.effect?.Cancel();chain.effect=null;chain.flight=-1f;
            chain.expires=mushroomPropagationClock+13f;
            float power=chain.scale*MushroomPropagationRules.Power;
            float radius=chain.radiusScale*MushroomPropagationRules.RadiusScale;
            int arrivingStage=chain.stage;
            if(chain.trait==MushroomRank3Rules.MycokingDescent)
                SpawnRank3Mycoking(chain.source,at,power,radius,chain.generation,chain,arrivingStage);
            else if(chain.trait==MushroomRank3Rules.ParasiticCrown)
                CreateMushroomCrown(chain.source,chain.target,at,power,radius,chain.generation,chain,arrivingStage);
            else CreateMushroomGas(chain.source,at,GameMasterData.Skill(MushroomCombatRules.SkillId),
                power,radius,false,chain.generation,null,chain,arrivingStage);
            if(arrivingStage>=MushroomPropagationRules.MaximumTransfers)mushroomPropagations.Remove(chain);
        }
    }

    private void ClearMushroomPropagations()
    {
        foreach(var chain in mushroomPropagations)chain.effect?.Cancel();
        mushroomPropagations.Clear();mushroomPropagationClock=0f;
    }
}
