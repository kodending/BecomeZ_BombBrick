using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private void SpawnRank3Mycoking(Unit source,Vector3 desired,float scale,float radiusScale,int generation,
        MushroomPropagation propagation=null,int propagationStage=0)
    {
        Tile tile=Rank3SpawnTile(desired);
        if(tile==null)return;
        bool secondary=propagationStage>0;
        RetirePreviousMycokings(source,secondary);
        propagation=propagation??BeginMushroomPropagation(source,scale,radiusScale);
        OwnedMonsterSave owned=PlayerProfileStore.CreateMonster(source.definition,
            "mycoking_"+source.stableId+"_"+generation+"_"+units.Count,false);
        if(source.ownedInstance!=null)owned.personalityId=source.ownedInstance.personalityId;
        Unit summon=SpawnUnit(source.definition,source.team,tile.coordinate,owned,false);
        ConfigureMycoking(source,summon);
        if(secondary)
        {
            summon.rank3MycokingSecondary=true;summon.hp=summon.maxHp*=.5f;
            summon.collisionRadius*=.72f;summon.mycokingFx?.ScaleSize(.72f);
            ConfigureTargetRing(summon);
        }
        Vector3 center=Flat(summon.model.transform.position);
        PlayMushroomSound(source,MushroomSound.King,center,secondary?.7f:1f);
        CreateMushroomGas(source,center,GameMasterData.Skill(MushroomCombatRules.SkillId),scale,radiusScale,false,generation,summon,
            propagation,propagationStage);
        int beforeHits=source.skillHits;float beforeDamage=source.skillDamage;
        foreach(var enemy in Rank3Enemies(source,center,TileSize*MushroomRank3Rules.MycokingRadiusTiles*radiusScale))
        {
            source.skillDamage+=ApplyMushroomHit(propagation,propagationStage,source,enemy,
                SkillPower(source)*MushroomRank3Rules.MycokingSpawnRatio*scale,true);
            source.skillHits++;
        }
        source.currentAction="mycoking_descent";source.traitActivations++;
        RecordSkillEvent(source,MushroomRank3Rules.MycokingDescent,source.skillHits-beforeHits,source.skillDamage-beforeDamage);
    }

    private void RetirePreviousMycokings(Unit source,bool secondary)
    {
        foreach(var old in units.Where(u=>u.rank3MycokingSummon&&u.rank3SummonOwner==source&&
            u.rank3MycokingSecondary==secondary&&!IsMushroomDeathBloom(u)).ToArray())DeactivateRank3Special(old);
    }

    private void ConfigureMycoking(Unit source,Unit summon)
    {
        summon.augmentShadowClone=true;summon.augmentTranscended=false;
        summon.rank3MycokingSummon=true;summon.rank3SummonOwner=source;
        summon.rank3SummonTimer=MushroomRank3Rules.MycokingDuration;
        summon.deploymentSlots=0;summon.skillId="";summon.skillCooldownDuration=0f;summon.activeTraitIds.Clear();
        summon.itemModifiers=new CombatItemModifiers();
        summon.hp=summon.maxHp=source.maxHp*MushroomRank3Rules.MycokingHealthRatio;
        summon.damage=0f;summon.defense=source.defense*.5f;
        summon.speedTilesPerSecond=0f;summon.velocity=Vector3.zero;
        summon.model.name="Rooted Mycoking";
        SpriteRenderer reference=Rank3Renderer(summon);
        if(!mlEpisodeActive)
        {
            summon.mycokingFx=PixelMycoking.Play(reference,Flat(summon.model.transform.position),
                MushroomGasColor(source,false),()=>paused);
            summon.mycokingFx.transform.SetParent(transform,true);
            if(summon.outline!=null){summon.outline.SetOutlined(false);summon.outline.enabled=false;}
            summon.outline=summon.mycokingFx.Outline;
        }
        if(summon.spriteAnimation!=null){summon.spriteAnimation.InterruptCurrentAction(true);summon.spriteAnimation.enabled=false;}
        if(reference!=null)reference.enabled=false;
        if(summon.animator!=null)summon.animator.enabled=false;
    }

    private void FinishMycoking(Unit summon)
    {
        if(summon==null||!summon.rank3MycokingSummon)return;
        StopMushroomDeathBloomDamage(summon);
        foreach(var gas in mushroomGasZones)
        {
            if(gas.root!=summon)continue;
            gas.tick=gas.tickCount;gas.duration=0f;
            gas.effect?.Cancel();gas.effect=null;
        }
        summon.mycokingFx?.Depart();summon.mycokingFx=null;
    }
}
