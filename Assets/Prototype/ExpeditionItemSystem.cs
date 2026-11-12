using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CombatItemModifiers
{
    public float attackDamageBonus;
    public float attackSpeedBonus;
    public float maxHealthBonus;
    public float damageReductionBonus;
    public float criticalChance;
    public float criticalDamageBonus;
    public float skillPowerBonus;
    public float defenseBonus;
    public float statusResistanceBonus;
    public float skillCooldownReductionBonus;
    public readonly List<ItemMasterRow> specialEffects=new List<ItemMasterRow>();
    public readonly List<ItemTransformMasterRow> transformedEffects=new List<ItemTransformMasterRow>();
}

public static class ExpeditionItemSystem
{
    public static int MaxSlots
    {
        get{return Math.Max(1,GameMasterData.ExpeditionInt("item_slots_per_monster",3));}
    }

    public static ExpeditionItemInstanceSave CreateItem(
        ExpeditionRunSave run,string itemId,bool addToInventory=true)
    {
        if(run==null)throw new ArgumentNullException("run");
        if(GameMasterData.Item(itemId)==null)throw new ArgumentException("Unknown item: "+itemId,"itemId");
        EnsureCollections(run);
        ExpeditionItemInstanceSave item=new ExpeditionItemInstanceSave{
            instanceId="item_"+Guid.NewGuid().ToString("N"),
            itemId=itemId
        };
        run.itemInstances.Add(item);
        if(addToInventory)run.inventoryItemInstanceIds.Add(item.instanceId);
        return item;
    }

    public static ExpeditionItemInstanceSave CreateRadiantCompletedItem(
        ExpeditionRunSave run,string itemId)
    {
        ItemMasterRow master=GameMasterData.Item(itemId);
        if(master==null||!master.IsCompleted)
            throw new ArgumentException("Radiant items must be completed items.","itemId");
        ExpeditionItemInstanceSave item=CreateItem(run,itemId,true);
        item.transformState=ExpeditionItemTransformState.Radiant;
        return item;
    }

    public static bool Equip(
        ExpeditionRunSave run,string monsterInstanceId,string itemInstanceId,int slotIndex,
        out string error)
    {
        error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        EnsureCollections(run);
        if(slotIndex<0||slotIndex>=MaxSlots){error="유효하지 않은 아이템 슬롯입니다.";return false;}
        ExpeditionMonsterStateSave monster=FindMonster(run,monsterInstanceId);
        if(monster==null){error="탐험 몬스터를 찾을 수 없습니다.";return false;}
        if(FindItem(run,itemInstanceId)==null){error="탐험 아이템을 찾을 수 없습니다.";return false;}

        RemoveFromAllLocations(run,itemInstanceId);
        EnsureSlots(monster);
        string displaced=monster.itemInstanceIds[slotIndex];
        monster.itemInstanceIds[slotIndex]=itemInstanceId;
        if(!string.IsNullOrEmpty(displaced)&&displaced!=itemInstanceId)
        {
            ExpeditionItemInstanceSave displacedItem=FindItem(run,displaced);
            if(displacedItem!=null&&displacedItem.destroyOnUnequip)RemoveItem(run,displaced);
            else AddInventoryUnique(run,displaced);
        }
        return true;
    }

    public static bool Unequip(
        ExpeditionRunSave run,string monsterInstanceId,int slotIndex,out string error)
    {
        error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        EnsureCollections(run);
        ExpeditionMonsterStateSave monster=FindMonster(run,monsterInstanceId);
        if(monster==null){error="탐험 몬스터를 찾을 수 없습니다.";return false;}
        if(slotIndex<0||slotIndex>=MaxSlots){error="유효하지 않은 아이템 슬롯입니다.";return false;}
        EnsureSlots(monster);
        string itemInstanceId=monster.itemInstanceIds[slotIndex];
        if(string.IsNullOrEmpty(itemInstanceId)){error="슬롯이 비어 있습니다.";return false;}
        monster.itemInstanceIds[slotIndex]="";
        ExpeditionItemInstanceSave item=FindItem(run,itemInstanceId);
        if(item!=null&&item.destroyOnUnequip)RemoveItem(run,itemInstanceId);
        else AddInventoryUnique(run,itemInstanceId);
        return true;
    }

    public static bool TryFuse(
        ExpeditionRunSave run,string firstInstanceId,string secondInstanceId,
        out ExpeditionItemInstanceSave result,out string error)
    {
        result=null;error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        EnsureCollections(run);
        if(string.IsNullOrEmpty(firstInstanceId)||firstInstanceId==secondInstanceId)
        {error="서로 다른 기본 아이템 2개가 필요합니다.";return false;}
        ExpeditionItemInstanceSave first=FindItem(run,firstInstanceId);
        ExpeditionItemInstanceSave second=FindItem(run,secondInstanceId);
        if(first!=null&&second!=null&&
           (first.temporary||second.temporary||first.transformLocked||second.transformLocked))
        {error="임시 또는 변형 잠금 아이템은 조합할 수 없습니다.";return false;}
        if(first==null||second==null){error="조합할 아이템을 찾을 수 없습니다.";return false;}
        ItemMasterRow firstMaster=GameMasterData.Item(first.itemId);
        ItemMasterRow secondMaster=GameMasterData.Item(second.itemId);
        if(firstMaster==null||secondMaster==null||!firstMaster.IsBase||!secondMaster.IsBase)
        {error="완성 아이템은 조합할 수 없습니다.";return false;}
        ItemMasterRow recipe=GameMasterData.ItemRecipe(first.itemId,second.itemId);
        if(recipe==null){error="해당 조합의 완성 아이템이 없습니다.";return false;}

        RemoveItem(run,firstInstanceId);
        RemoveItem(run,secondInstanceId);
        result=CreateItem(run,recipe.id,true);
        return true;
    }

    public static ItemMasterRow PreviewRecipe(string firstItemId,string secondItemId)
    {
        ItemMasterRow first=GameMasterData.Item(firstItemId);
        ItemMasterRow second=GameMasterData.Item(secondItemId);
        return first!=null&&second!=null&&first.IsBase&&second.IsBase
            ?GameMasterData.ItemRecipe(firstItemId,secondItemId):null;
    }

    public static bool TryDismantle(
        ExpeditionRunSave run,string itemInstanceId,
        out ExpeditionItemInstanceSave first,out ExpeditionItemInstanceSave second,
        out string error)
    {
        first=null;second=null;error="";
        if(run==null){error="탐험 정보가 없습니다.";return false;}
        EnsureCollections(run);
        ExpeditionItemInstanceSave source=FindItem(run,itemInstanceId);
        ItemMasterRow master=source==null?null:GameMasterData.Item(source.itemId);
        if(source==null||master==null||!master.IsCompleted)
        {error="완성 아이템만 분해할 수 있습니다.";return false;}
        if(source.temporary||source.transformLocked||
           source.transformState!=ExpeditionItemTransformState.None)
        {error="임시 또는 변형된 아이템은 분해할 수 없습니다.";return false;}
        if(string.IsNullOrEmpty(master.componentAId)||string.IsNullOrEmpty(master.componentBId))
        {error="원래 재료 정보가 없습니다.";return false;}
        RemoveItem(run,itemInstanceId);
        first=CreateItem(run,master.componentAId,true);
        second=CreateItem(run,master.componentBId,true);
        return true;
    }

    public static ExpeditionItemInstanceSave ItemInstance(ExpeditionRunSave run,string instanceId)
    {
        if(run==null)return null;
        EnsureCollections(run);
        return FindItem(run,instanceId);
    }

    public static IReadOnlyList<ExpeditionItemInstanceSave> InventoryItems(
        ExpeditionRunSave run)
    {
        if(run==null)return Array.Empty<ExpeditionItemInstanceSave>();
        EnsureCollections(run);
        return run.inventoryItemInstanceIds
            .Select(instanceId=>FindItem(run,instanceId))
            .Where(item=>item!=null)
            .ToList();
    }

    public static ExpeditionMonsterStateSave MonsterState(ExpeditionRunSave run,string instanceId)
    {
        if(run==null)return null;
        EnsureCollections(run);
        ExpeditionMonsterStateSave monster=FindMonster(run,instanceId);
        if(monster!=null)EnsureSlots(monster);
        return monster;
    }

    public static ItemMasterRow RollBasicItem(Random random,string poolId="default_equipment")
    {
        if(random==null)throw new ArgumentNullException("random");
        List<ItemMasterRow> pool=GameMasterData.ItemsInPool(poolId);
        return pool.Count==0?null:pool[random.Next(pool.Count)];
    }

    public static CombatItemModifiers Aggregate(
        ExpeditionRunSave run,ExpeditionMonsterStateSave monster)
    {
        if(run==null||monster==null)return new CombatItemModifiers();
        EnsureCollections(run);EnsureSlots(monster);
        List<ItemMasterRow> rows=new List<ItemMasterRow>();
        List<ItemTransformMasterRow> transformedEffects=new List<ItemTransformMasterRow>();
        foreach(string instanceId in monster.itemInstanceIds)
        {
            ExpeditionItemInstanceSave instance=FindItem(run,instanceId);
            ItemMasterRow item=ExpeditionItemTransformSystem.ResolveEffectiveMaster(instance);
            if(item!=null)rows.Add(item);
            if(instance!=null&&instance.transformState==
               ExpeditionItemTransformState.CorruptedGreatSuccess)
            {
                ItemTransformMasterRow transform=GameMasterData.ItemTransform(instance.itemId);
                if(transform!=null)transformedEffects.Add(transform);
            }
        }
        CombatItemModifiers result=Aggregate(rows);
        result.transformedEffects.AddRange(transformedEffects);
        return result;
    }

    public static CombatItemModifiers Aggregate(IEnumerable<ItemMasterRow> items)
    {
        CombatItemModifiers result=new CombatItemModifiers();
        if(items==null)return result;
        foreach(ItemMasterRow item in items)
        {
            if(item==null)continue;
            result.attackDamageBonus+=item.attackDamageBonus;
            result.attackSpeedBonus+=item.attackSpeedBonus;
            result.maxHealthBonus+=item.maxHealthBonus;
            result.damageReductionBonus+=item.damageReductionBonus;
            result.criticalChance+=item.criticalChance;
            result.criticalDamageBonus+=item.criticalDamageBonus;
            result.skillPowerBonus+=item.skillPowerBonus;
            result.defenseBonus+=item.defenseBonus;
            result.statusResistanceBonus+=item.statusResistanceBonus;
            result.skillCooldownReductionBonus+=item.skillCooldownReductionBonus;
            if(!string.IsNullOrEmpty(item.specialEffect)&&item.specialEffect!="None")
                result.specialEffects.Add(item);
        }
        return result;
    }

    private static void EnsureCollections(ExpeditionRunSave run)
    {
        if(run.itemInstances==null)run.itemInstances=new List<ExpeditionItemInstanceSave>();
        if(run.inventoryItemInstanceIds==null)run.inventoryItemInstanceIds=new List<string>();
        if(run.monsters==null)run.monsters=new List<ExpeditionMonsterStateSave>();
    }

    private static void EnsureSlots(ExpeditionMonsterStateSave monster)
    {
        if(monster.itemInstanceIds==null)monster.itemInstanceIds=new List<string>();
        while(monster.itemInstanceIds.Count<MaxSlots)monster.itemInstanceIds.Add("");
        if(monster.itemInstanceIds.Count>MaxSlots)
            monster.itemInstanceIds.RemoveRange(MaxSlots,monster.itemInstanceIds.Count-MaxSlots);
    }

    private static ExpeditionMonsterStateSave FindMonster(ExpeditionRunSave run,string instanceId)
    {
        return run.monsters.FirstOrDefault(item=>item!=null&&item.instanceId==instanceId);
    }

    private static ExpeditionItemInstanceSave FindItem(ExpeditionRunSave run,string instanceId)
    {
        return string.IsNullOrEmpty(instanceId)?null:
            run.itemInstances.FirstOrDefault(item=>item!=null&&item.instanceId==instanceId);
    }

    private static void RemoveFromAllLocations(ExpeditionRunSave run,string itemInstanceId)
    {
        run.inventoryItemInstanceIds.RemoveAll(id=>id==itemInstanceId);
        foreach(ExpeditionMonsterStateSave monster in run.monsters)
        {
            if(monster==null)continue;
            EnsureSlots(monster);
            for(int i=0;i<monster.itemInstanceIds.Count;i++)
                if(monster.itemInstanceIds[i]==itemInstanceId)monster.itemInstanceIds[i]="";
        }
    }

    private static void RemoveItem(ExpeditionRunSave run,string itemInstanceId)
    {
        RemoveFromAllLocations(run,itemInstanceId);
        run.itemInstances.RemoveAll(item=>item!=null&&item.instanceId==itemInstanceId);
    }

    private static void AddInventoryUnique(ExpeditionRunSave run,string itemInstanceId)
    {
        if(!run.inventoryItemInstanceIds.Contains(itemInstanceId))
            run.inventoryItemInstanceIds.Add(itemInstanceId);
    }
}
