using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class OwnedMonsterSave
{
    public string instanceId;
    public string monsterId;
    public string customName;
    public string intrinsicTraitId;
    public string personalityId;
    public string rank2TraitId;
    public string rank3TraitId;
    public int personalitySeed;
    public int courage;
    public int aggression;
    public int discipline;
    public int composure;
    public int cooperation;
    public int adaptability;
    public float hpMultiplier=1f;
    public float damageMultiplier=1f;
    public float attackSpeedMultiplier=1f;
    public float defenseMultiplier=1f;
    public float statusResistanceMultiplier=1f;
    public float moveSpeedMultiplier=1f;
    public bool isShiny;
    public bool locked;
    public bool favorite;
    public bool isNew;
    public long acquiredUtcTicks;
}

[Serializable]
public sealed class FormationPlacementSave
{
    public string instanceId;
    public bool deployed;
    public int deploymentX;
    public int deploymentY;
    public int benchSlot=-1;
}

[Serializable]
public sealed class ExpeditionPresetSave
{
    public bool configured;
    // Index is the town slot; empty strings preserve gaps between monsters.
    public List<string> rosterInstanceIds=new List<string>();
    public int RosterCount=>rosterInstanceIds?.Count(id=>!string.IsNullOrWhiteSpace(id))??0;
    public List<FormationPlacementSave> placements=new List<FormationPlacementSave>();
}

[Serializable]
public sealed class PermanentResearchAllocationSave
{
    public string researchId;
    public int rank;
}

[Serializable]
public sealed class PermanentResearchRouteProgressSave
{
    public string routeKey;
    public int bestCompletedWave;
    public int lastFailedBracket=-1;
}

[Serializable]
public sealed class RegionBoxRewardSave
{
    public string runId;
    public string regionId;
    public ExpeditionDifficulty difficulty;
    public List<OwnedMonsterSave> candidates=new List<OwnedMonsterSave>();
}

[Serializable]
public sealed class PlayerProfileSave
{
    public int version=6;
    public string profileId="default";
    public int hunterTokens;
    public int hunterShopCycle;
    public int hunterFamilyPurchaseCount;
    public List<string> unlockedRegionIds=new List<string>();
    public List<string> encounteredMonsterIds=new List<string>();
    public List<string> hunterFamilyStockIds=new List<string>();
    public List<string> notifiedHunterRegionBoxIds=new List<string>();
    public List<string> notifiedHunterFamilyBoxIds=new List<string>();
    public List<string> settledExpeditionRunIds=new List<string>();
    public List<RegionBoxRewardSave> pendingRegionBoxRewards=new List<RegionBoxRewardSave>();
    public List<string> clearedRegionDifficultyKeys=new List<string>();
    public int permanentResearchExperience;
    public List<PermanentResearchAllocationSave> permanentResearchAllocations=
        new List<PermanentResearchAllocationSave>();
    public List<PermanentResearchRouteProgressSave> permanentResearchRoutes=
        new List<PermanentResearchRouteProgressSave>();
    public List<string> settledPermanentResearchRunIds=new List<string>();
    public bool captureNetStarterPackGranted;
    public bool captureNetTwoTierMigrationApplied;
    public bool firstExpeditionGuideInitialized;
    public int firstExpeditionGuideVersion;
    public int firstExpeditionGuideStep;
    public int firstExpeditionCaptureGuideStep;
    public long lastSavedUtcTicks;
    public bool starterSelectionPending;
    public List<string> starterRerollInstanceIds=new List<string>();
    public List<OwnedMonsterSave> monsters=new List<OwnedMonsterSave>();
    public List<CaptureNetInventorySave> captureNets=
        new List<CaptureNetInventorySave>();
    public ExpeditionPresetSave expeditionPreset=new ExpeditionPresetSave();
}
