using System;
using System.Collections.Generic;
using System.Linq;

public static class ExpeditionTraitProgression
{
    public static void AssignPreviewTraits(
        ExpeditionMonsterStateSave monster,MonsterDefinition definition,System.Random random,
        IEnumerable<MonsterDefinition> roster=null,OwnedMonsterSave owned=null)
    {
        if(monster==null)throw new ArgumentNullException("monster");
        if(definition==null)throw new ArgumentNullException("definition");
        if(random==null)throw new ArgumentNullException("random");
        if(string.Equals(monster.rank3TraitId,
               SpiritDeerRank3Rules.LegacySoulSeparation,
               StringComparison.Ordinal))
            monster.rank3TraitId=SpiritDeerRank3Rules.InviolableSanctuary;
        MonsterTraitDefinition currentRank2=MonsterTraitCatalog.Get(monster.rank2TraitId);
        MonsterTraitDefinition currentRank3=MonsterTraitCatalog.Get(monster.rank3TraitId);
        bool validRank2=currentRank2!=null&&currentRank2.IsRank2&&currentRank2.IsCompatible(definition);
        bool validRank3=currentRank3!=null&&currentRank3.IsRank3&&currentRank3.IsCompatible(definition)&&
            (!currentRank3.IsComboBridge||MonsterTraitCatalog.ComboPrerequisiteSupported(
                currentRank3.id,definition,roster));
        if(!validRank2)
        {
            monster.rank2TraitId=OwnedTrait(definition,owned,2).id;
        }
        if(!validRank3)
        {
            monster.rank3TraitId=OwnedTrait(definition,owned,3).id;
        }
    }

    public static MonsterTraitDefinition OwnedTrait(MonsterDefinition definition,OwnedMonsterSave owned,int rank)
    {
        string id=rank==2?owned?.rank2TraitId:owned?.rank3TraitId;
        MonsterTraitDefinition trait=MonsterTraitCatalog.Get(id);
        return trait!=null&&trait.IsCompatible(definition)&&!trait.IsComboBridge&&
            (rank==2?trait.IsRank2:rank==3&&trait.IsRank3)?trait:FixedTrait(definition,rank);
    }

    public static MonsterTraitDefinition FixedTrait(MonsterDefinition definition,int rank)
    {
        if(definition==null)throw new ArgumentNullException("definition");
        if(rank!=2&&rank!=3)throw new ArgumentOutOfRangeException("rank");
        MonsterGrowthMasterRow row=GameMasterData.MonsterGrowth(definition.monsterId);
        string id=rank==2?row?.fixedRank2TraitId:row?.fixedRank3TraitId;
        MonsterTraitDefinition trait=MonsterTraitCatalog.Get(id);
        if(trait==null||!trait.IsCompatible(definition)||
           (rank==2?!trait.IsRank2:!trait.IsRank3))
            throw new InvalidOperationException("Missing or incompatible fixed rank-"+
                rank+" trait for "+definition.monsterId+".");
        return trait;
    }

    public static List<string> ActiveTraitIds(ExpeditionMonsterStateSave monster)
    {
        List<string> result=new List<string>(2);
        if(monster==null)return result;
        if(monster.rank>=ExpeditionMonsterRank.Rank2&&!string.IsNullOrWhiteSpace(monster.rank2TraitId))
            result.Add(monster.rank2TraitId);
        if(monster.rank>=ExpeditionMonsterRank.Rank3&&!string.IsNullOrWhiteSpace(monster.rank3TraitId))
            result.Add(monster.rank3TraitId);
        return result;
    }

    public static string NextTraitId(ExpeditionMonsterStateSave monster)
    {
        if(monster==null||monster.rank==ExpeditionMonsterRank.Rank3)return "";
        return monster.rank==ExpeditionMonsterRank.Rank1
            ?monster.rank2TraitId:monster.rank3TraitId;
    }
}
