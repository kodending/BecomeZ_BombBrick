using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ExpeditionRunStore
{
    public const int CurrentVersion=15;
    private const string ActiveFileName="active_expedition.json";
    private const string BackupFileName="active_expedition.backup.json";
    private const string TemporaryFileName="active_expedition.tmp.json";
    private const string LastResultFileName="last_expedition.json";
    private readonly string rootDirectory;
    private readonly string profileId;

    public ExpeditionRunStore(string profileId,string rootDirectory=null)
    {
        this.profileId=string.IsNullOrWhiteSpace(profileId)?"default":profileId;
        this.rootDirectory=string.IsNullOrWhiteSpace(rootDirectory)
            ?Path.Combine(BombBrick.Game.Core.GameDataPaths.Root,"Profiles")
            :rootDirectory;
    }

    public string DirectoryPath=>Path.Combine(
        rootDirectory,"Expeditions",SafeFileName(profileId));
    public string ActivePath=>Path.Combine(DirectoryPath,ActiveFileName);
    public string BackupPath=>Path.Combine(DirectoryPath,BackupFileName);
    public string TemporaryPath=>Path.Combine(DirectoryPath,TemporaryFileName);
    public string LastResultPath=>Path.Combine(DirectoryPath,LastResultFileName);

    public ExpeditionRunSave LoadActive(
        PlayerProfileSave profile,MonsterDatabase database)
    {
        ExpeditionRunSave run=TryLoad(ActivePath);
        if(run==null)run=TryLoad(BackupPath);
        if(run==null||!ExpeditionRunFactory.Normalize(run,profile,database)||
           run.status!=ExpeditionRunStatus.Active)
            return null;
        return run;
    }

    public IEnumerator LoadActiveAcrossFrames(PlayerProfileSave profile,MonsterDatabase database,
        Action<ExpeditionRunSave> completed)
    {
        var task=Task.Run(()=>TryLoad(ActivePath)??TryLoad(BackupPath));
        while(!task.IsCompleted)yield return null;
        var run=task.GetAwaiter().GetResult();
        if(run==null||!ExpeditionRunFactory.Normalize(run,profile,database)||
           run.status!=ExpeditionRunStatus.Active)run=null;
        completed(run);
    }

    public void SaveActive(
        ExpeditionRunSave run,PlayerProfileSave profile,MonsterDatabase database)
        =>PrepareSaveActive(run,profile,database)();

    public Action PrepareSaveActive(
        ExpeditionRunSave run,PlayerProfileSave profile,MonsterDatabase database)
    {
        if(run==null)throw new ArgumentNullException(nameof(run));
        if(run.status!=ExpeditionRunStatus.Active)
            throw new InvalidOperationException("Only an active expedition can be checkpointed.");
        if(!ExpeditionRunFactory.Normalize(run,profile,database))
            throw new InvalidOperationException(
                "Expedition run does not match the permanent profile.");
        run.version=CurrentVersion;
        run.lastSavedUtcTicks=DateTime.UtcNow.Ticks;
        string json=JsonUtility.ToJson(run,true);
        return ()=>
        {
            Directory.CreateDirectory(DirectoryPath);
            WriteAtomic(json,ActivePath,BackupPath,TemporaryPath);
        };
    }

    public void Complete(
        ExpeditionRunSave run,bool victory,
        PlayerProfileSave profile,MonsterDatabase database)
    {
        if(run==null)throw new ArgumentNullException(nameof(run));
        if(!ExpeditionRunFactory.Normalize(run,profile,database))
            throw new InvalidOperationException(
                "Expedition run does not match the permanent profile.");
        ExpeditionRunSave completed=JsonUtility.FromJson<ExpeditionRunSave>(JsonUtility.ToJson(run));
        completed.status=victory
            ?ExpeditionRunStatus.Completed
            :ExpeditionRunStatus.Failed;
        completed.version=CurrentVersion;
        completed.completionPending=false;
        completed.lastSavedUtcTicks=DateTime.UtcNow.Ticks;
        Directory.CreateDirectory(DirectoryPath);
        WriteAtomic(completed,LastResultPath,LastResultPath+".backup",LastResultPath+".tmp");
        // Keep the current checkpoint until all older recovery files are removed.
        DeleteIfPresent(BackupPath);
        DeleteIfPresent(TemporaryPath);
        DeleteIfPresent(ActivePath);
        run.status=completed.status;
        run.completionPending=false;
        run.lastSavedUtcTicks=completed.lastSavedUtcTicks;
    }

    public ExpeditionCompletionSave SettleAndComplete(
        ExpeditionRunSave run,bool victory,PlayerProfileSave profile,
        PlayerProfileStore profileStore,MonsterDatabase database)
    {
        if(run.status!=ExpeditionRunStatus.Active)return run.completion;
        PlayerProfileSave staged=JsonUtility.FromJson<PlayerProfileSave>(JsonUtility.ToJson(profile));
        bool firstClear=victory&&run.lastCompletedWave>=12&&
            run.difficulty==ExpeditionDifficulty.Beginner&&
            !ExpeditionDifficultyProgress.IsCleared(staged,run.regionId,ExpeditionDifficulty.Beginner);
        HunterRegionDefinition next=firstClear?HunterRegionCatalog.Next(run.regionId):null;
        string nextId=next!=null&&!staged.unlockedRegionIds.Contains(next.id)?next.id:"";
        PermanentResearchSettlement research=PermanentResearchSystem.Settle(
            staged,profileStore,database,run,victory,false);
        int tokens=HunterTokenRewardSystem.Settle(
            staged,profileStore,database,run,victory,out int nets,false);
        if(!run.completionPending)
        {
            run.completion=new ExpeditionCompletionSave
            {
                victory=victory,research=research,hunterTokens=tokens,rareCaptureNets=nets,
                firstBeginnerClear=firstClear,nextRegionId=nextId
            };
            run.completionPending=true;
        }
        // Durable intent first: restart resumes settlement, never replays the last battle.
        // Both reward systems write to the same staged profile and use existing run-ID receipts.
        SaveActive(run,profile,database);
        profileStore.Save(staged);
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(staged),profile);
        Complete(run,victory,profile,database);
        HunterEconomyTelemetry.RecordExpeditionSettlement(profile,run,victory,
            run.completion.hunterTokens,profile.hunterTokens-run.completion.hunterTokens);
        return run.completion;
    }

    public void Abandon(ExpeditionRunSave run)
    {
        if(run!=null)run.status=ExpeditionRunStatus.Abandoned;
        DeleteIfPresent(ActivePath);
        DeleteIfPresent(BackupPath);
        DeleteIfPresent(TemporaryPath);
    }

    private static ExpeditionRunSave TryLoad(string path)
    {
        try
        {
            ExpeditionRunSave run=JsonUtility.FromJson<ExpeditionRunSave>(
                File.ReadAllText(path));
            if(run!=null&&run.version>CurrentVersion)
                throw new NotSupportedException("Expedition save belongs to a newer game version.");
            return run!=null&&!string.IsNullOrWhiteSpace(run.runId)?run:null;
        }
        catch(FileNotFoundException){return null;}
        catch(DirectoryNotFoundException){return null;}
        catch(ArgumentException exception)
        {
            Debug.LogWarning(
                "Expedition run load failed ("+Path.GetFileName(path)+"): "+
                exception.Message);
            return null;
        }
    }

    private static void WriteAtomic(
        ExpeditionRunSave run,string destination,string backup,string temporary)
        =>WriteAtomic(JsonUtility.ToJson(run,true),destination,backup,temporary);

    private static void WriteAtomic(
        string json,string destination,string backup,string temporary)
    {
        File.WriteAllText(
            temporary,json,new UTF8Encoding(false));
        if(File.Exists(destination))File.Replace(temporary,destination,backup,true);
        else File.Move(temporary,destination);
    }

    private static void DeleteIfPresent(string path)
    {
        if(File.Exists(path))File.Delete(path);
    }

    private static string SafeFileName(string value)
    {
        char[] invalid=Path.GetInvalidFileNameChars();
        string safe=new string(value.Select(character=>
            invalid.Contains(character)?'_':character).ToArray());
        return string.IsNullOrWhiteSpace(safe)?"default":safe;
    }
}

public static class ExpeditionRunFactory
{
    private const int MaximumWave=12;
    private const int MaximumRoster=8;
    private const int BaseDeploymentCount=3;
    private const int MaximumDeploymentCount=8;

    public static ExpeditionRunSave Create(
        PlayerProfileSave profile,MonsterDatabase database,
        IReadOnlyList<OwnedMonsterSave> startingRoster,
        string regionId,int startingLives=3,int baseDeploymentCount=3,
        ExpeditionDifficulty difficulty=ExpeditionDifficulty.Normal)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(database==null)throw new ArgumentNullException(nameof(database));
        if(startingRoster==null)throw new ArgumentNullException(nameof(startingRoster));
        long now=DateTime.UtcNow.Ticks;
        ExpeditionRunSave run=new ExpeditionRunSave
        {
            version=ExpeditionRunStore.CurrentVersion,
            profileId=string.IsNullOrWhiteSpace(profile.profileId)
                ?"default":profile.profileId,
            runId="run_"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"_"+
                  Guid.NewGuid().ToString("N").Substring(0,6),
            regionId=string.IsNullOrWhiteSpace(regionId)
                ?"prototype_region":regionId,
            difficulty=ExpeditionDifficultyRules.Clamp(difficulty),
            status=ExpeditionRunStatus.Active,
            checkpoint=ExpeditionRunCheckpoint.Preparation,
            startedUtcTicks=now,
            lastSavedUtcTicks=now,
            wave=1,
            lives=Math.Max(1,startingLives),
            maximumLives=Math.Max(1,startingLives),
            maxDeploymentCount=Math.Max(
                BaseDeploymentCount,
                Math.Min(MaximumDeploymentCount,baseDeploymentCount)),
            expeditionCurrency=Math.Max(0,GameMasterData.ExpeditionInt(
                "starting_expedition_currency",5)+
                PermanentResearchSystem.StartingGoldBonus(profile))
        };
        List<OwnedMonsterSave> validRoster=startingRoster
            .Where(item=>item!=null).Take(MaximumRoster).ToList();
        List<MonsterDefinition> rosterDefinitions=validRoster
            .Select(item=>database.Get(item.monsterId))
            .Where(item=>item!=null&&item.enabled).ToList();
        foreach(OwnedMonsterSave owned in validRoster)
        {
            MonsterDefinition definition=database.Get(owned.monsterId);
            if(definition==null||!definition.enabled)continue;
            ExpeditionMonsterStateSave state=new ExpeditionMonsterStateSave
            {
                instanceId=owned.instanceId,
                monsterId=owned.monsterId,
                rank=ExpeditionMonsterRank.Rank1,
                deployed=false,
                benchSlot=-1,
                currentHpRatio=1f,
                itemInstanceIds=new List<string>{"","",""}
            };
            int seed=MonsterInstanceGenerator.StableSeed(
                run.runId+"_"+owned.instanceId+"_expedition_traits");
            ExpeditionTraitProgression.AssignPreviewTraits(
                state,definition,new System.Random(seed),rosterDefinitions,owned);
            run.monsters.Add(state);
        }
        if(run.monsters.Count==0)
            throw new InvalidOperationException(
                "An expedition requires at least one enabled owned monster.");
        return run;
    }

    public static List<OwnedMonsterSave> ResolveRoster(
        ExpeditionRunSave run,PlayerProfileSave profile,MonsterDatabase database)
    {
        if(run==null||profile==null||database==null)
            return new List<OwnedMonsterSave>();
        List<OwnedMonsterSave> roster=new List<OwnedMonsterSave>();
        foreach(ExpeditionMonsterStateSave state in run.monsters)
        {
            if(state==null)continue;
            OwnedMonsterSave owned=profile.monsters.Find(item=>
                item!=null&&item.instanceId==state.instanceId);
            if(owned==null&&state.temporary)
            {
                MonsterDefinition definition=database.Get(state.monsterId);
                if(definition!=null&&definition.enabled)
                    owned=PlayerProfileStore.CreateMonster(
                        definition,state.instanceId,false);
            }
            if(owned!=null)roster.Add(owned);
        }
        return roster;
    }

    public static bool IsTemporaryRosterMember(
        ExpeditionRunSave run,string instanceId)
    {
        return run!=null&&!string.IsNullOrEmpty(instanceId)&&
               run.monsters.Any(state=>state!=null&&state.temporary&&
                   string.Equals(state.instanceId,instanceId,
                       StringComparison.Ordinal));
    }

    public static bool Normalize(
        ExpeditionRunSave run,PlayerProfileSave profile,MonsterDatabase database)
    {
        if(run==null||profile==null||database==null||
           string.IsNullOrWhiteSpace(run.runId))
            return false;
        int loadedVersion=run.version;
        string expectedProfileId=string.IsNullOrWhiteSpace(profile.profileId)
            ?"default":profile.profileId;
        if(!string.IsNullOrWhiteSpace(run.profileId)&&
           !string.Equals(
               run.profileId,expectedProfileId,StringComparison.Ordinal))
            return false;
        run.profileId=expectedProfileId;
        run.version=ExpeditionRunStore.CurrentVersion;
        run.regionId=string.IsNullOrWhiteSpace(run.regionId)
            ?"prototype_region":run.regionId;
        run.difficulty=loadedVersion<12
            ?ExpeditionDifficulty.Normal
            :ExpeditionDifficultyRules.Clamp(run.difficulty);
        run.wave=Math.Max(1,Math.Min(MaximumWave,run.wave));
        if(run.checkpoint==ExpeditionRunCheckpoint.InBattle)
        {
            run.activeBattleWave=run.wave;
            if(run.activeBattleSeed==0)
            {
                run.activeBattleSeed=MonsterInstanceGenerator.StableSeed(
                    run.runId+"_battle_"+run.wave);
                if(run.activeBattleSeed==0)run.activeBattleSeed=1;
            }
        }
        else
        {
            run.activeBattleWave=0;
            run.activeBattleSeed=0;
        }
        run.lastCompletedWave=Math.Max(
            0,Math.Min(run.wave,run.lastCompletedWave));
        run.maximumLives=Math.Max(1,run.maximumLives);
        run.lives=Math.Max(0,Math.Min(run.maximumLives,run.lives));
        run.maxDeploymentCount=Math.Max(
            BaseDeploymentCount,
            Math.Min(MaximumDeploymentCount,run.maxDeploymentCount));
        run.expeditionCurrency=Math.Max(0,run.expeditionCurrency);
        run.consecutiveWins=Math.Max(0,run.consecutiveWins);
        run.consecutiveLosses=Math.Max(0,run.consecutiveLosses);
        run.equipmentPityClears=Math.Max(0,run.equipmentPityClears);
        EnsureCollections(run);
        if(loadedVersion<11)
        {
            run.growthCards.Clear();
            run.growthCardDraft.Clear();
            run.growthCardDraftRerolled=false;
        }

        Dictionary<string,OwnedMonsterSave> permanent=
            (profile.monsters??new List<OwnedMonsterSave>())
            .Where(item=>item!=null&&!string.IsNullOrWhiteSpace(item.instanceId))
            .GroupBy(item=>item.instanceId,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.Ordinal);
        HashSet<string> monsterIds=new HashSet<string>(StringComparer.Ordinal);
        List<MonsterDefinition> rosterDefinitions=run.monsters
            .Where(state=>state!=null)
            .Select(state=>database.Get(state.monsterId))
            .Where(definition=>definition!=null&&definition.enabled)
            .ToList();
        for(int i=run.monsters.Count-1;i>=0;i--)
        {
            ExpeditionMonsterStateSave state=run.monsters[i];
            if(state==null||string.IsNullOrWhiteSpace(state.instanceId)||
               !monsterIds.Add(state.instanceId))
            {
                run.monsters.RemoveAt(i);
                continue;
            }
            MonsterDefinition definition=database.Get(state.monsterId);
            if(definition==null||!definition.enabled)
                return false;
            if(!state.temporary&&
               (!permanent.TryGetValue(state.instanceId,out OwnedMonsterSave owned)||
                !string.Equals(
                    owned.monsterId,state.monsterId,StringComparison.Ordinal)))
                return false;
            NormalizeMonsterState(state,loadedVersion);
            int traitSeed=MonsterInstanceGenerator.StableSeed(
                run.runId+"_"+state.instanceId+"_expedition_traits");
            ExpeditionTraitProgression.AssignPreviewTraits(
                state,definition,new System.Random(traitSeed),rosterDefinitions,
                permanent.TryGetValue(state.instanceId,out OwnedMonsterSave traitOwner)?traitOwner:null);
        }
        ExpeditionAugmentSelectionSave transcendence=
            ExpeditionAugmentRuntime.Selection(
                run,ExpeditionAugmentRuntime.TranscendenceGene);
        if(transcendence!=null)
            ExpeditionAugmentRuntime.TryActivateTranscendence(
                run,transcendence.targetMonsterInstanceId);
        if(run.monsters.Count==0)return false;

        HashSet<string> itemIds=new HashSet<string>(StringComparer.Ordinal);
        run.itemInstances=run.itemInstances.Where(item=>
            item!=null&&!string.IsNullOrWhiteSpace(item.instanceId)&&
            !string.IsNullOrWhiteSpace(item.itemId)&&itemIds.Add(item.instanceId))
            .ToList();
        HashSet<string> validItemIds=new HashSet<string>(
            run.itemInstances.Select(item=>item.instanceId),
            StringComparer.Ordinal);
        run.inventoryItemInstanceIds=run.inventoryItemInstanceIds
            .Where(id=>!string.IsNullOrWhiteSpace(id)&&validItemIds.Contains(id))
            .Distinct(StringComparer.Ordinal).ToList();
        HashSet<string> equipped=new HashSet<string>(StringComparer.Ordinal);
        foreach(ExpeditionMonsterStateSave state in run.monsters)
        {
            for(int slot=0;slot<state.itemInstanceIds.Count;slot++)
            {
                string itemId=state.itemInstanceIds[slot];
                if(string.IsNullOrWhiteSpace(itemId))continue;
                if(!validItemIds.Contains(itemId)||!equipped.Add(itemId))
                    state.itemInstanceIds[slot]="";
            }
        }
        run.inventoryItemInstanceIds.RemoveAll(equipped.Contains);
        return true;
    }

    private static void EnsureCollections(ExpeditionRunSave run)
    {
        if(run.monsters==null)
            run.monsters=new List<ExpeditionMonsterStateSave>();
        if(run.itemInstances==null)
            run.itemInstances=new List<ExpeditionItemInstanceSave>();
        if(run.inventoryItemInstanceIds==null)
            run.inventoryItemInstanceIds=new List<string>();
        if(run.growthCards==null)
            run.growthCards=new List<CombatGrowthCardSelectionSave>();
        if(run.augments==null)
            run.augments=new List<ExpeditionAugmentSelectionSave>();
        run.augments.RemoveAll(value=>value!=null&&
            value.augmentId=="augment_battle_contract");
        if(run.specialItems==null)
            run.specialItems=new List<ExpeditionSpecialItemSave>();
        if(run.shopItemIds==null)run.shopItemIds=new List<string>();
        if(run.growthShopCandidateMonsterIds==null)
            run.growthShopCandidateMonsterIds=new List<string>();
        run.growthShopCandidateMonsterIds=run.growthShopCandidateMonsterIds
            .Where(id=>!string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        if(run.growthShopPurchasedMonsterIds==null)
            run.growthShopPurchasedMonsterIds=new List<string>();
        run.growthShopPurchasedMonsterIds=run.growthShopPurchasedMonsterIds
            .Where(id=>!string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        run.growthShopWave=Math.Max(0,run.growthShopWave);
        run.growthShopCompletedWave=Math.Max(0,run.growthShopCompletedWave);
        run.growthShopRerollCount=Math.Max(0,run.growthShopRerollCount);
        if(run.augmentDraftIds==null)run.augmentDraftIds=new List<string>();
        run.augmentDraftIds.RemoveAll(id=>id=="augment_battle_contract");
        if(run.growthCardDraft==null)
            run.growthCardDraft=new List<CombatGrowthCardDraftEntry>();
        if(run.lastBlessingRewardIds==null)
            run.lastBlessingRewardIds=new List<string>();
        if(run.completedItemChoiceIds==null)
            run.completedItemChoiceIds=new List<string>();
        run.completedItemChoiceRerollCount=Math.Max(
            0,run.completedItemChoiceRerollCount);
        if(run.capturedMonsterInstanceIds==null)
            run.capturedMonsterInstanceIds=new List<string>();
        run.capturedMonsterInstanceIds=run.capturedMonsterInstanceIds
            .Where(id=>!string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if(run.combatContributionHistory==null)
            run.combatContributionHistory=
                new List<ExpeditionWaveCombatContributionSave>();
        run.combatContributionHistory=run.combatContributionHistory
            .Where(entry=>entry!=null&&entry.wave>=1&&
                entry.wave<=MaximumWave)
            .GroupBy(entry=>entry.wave)
            .Select(group=>group.Last())
            .OrderBy(entry=>entry.wave)
            .ToList();
        foreach(ExpeditionWaveCombatContributionSave entry in
                run.combatContributionHistory)
        {
            if(entry.allies==null)
                entry.allies=new List<ExpeditionCombatContributionSave>();
            if(entry.enemies==null)
                entry.enemies=new List<ExpeditionCombatContributionSave>();
        }
        if(run.captureAttemptedWave<0)run.captureAttemptedWave=0;
        if(run.captureAttemptSequence<0)run.captureAttemptSequence=0;
        if(!CaptureNetCatalog.IsCaptureNet(run.selectedCaptureNetItemId))
        {
            run.selectedCaptureNetItemId="";
            run.selectedCaptureEnemyInstanceId="";
        }
    }

    private static void NormalizeMonsterState(
        ExpeditionMonsterStateSave state,int loadedVersion)
    {
        if((int)state.rank<(int)ExpeditionMonsterRank.Rank1)
            state.rank=ExpeditionMonsterRank.Rank1;
        if((int)state.rank>(int)ExpeditionMonsterRank.Rank3)
            state.rank=ExpeditionMonsterRank.Rank3;
        if(loadedVersion<11)
        {
            if(state.rank>=ExpeditionMonsterRank.Rank3)
                state.growthTotal=Math.Max(state.growthTotal,9);
            else if(state.rank>=ExpeditionMonsterRank.Rank2)
                state.growthTotal=Math.Max(state.growthTotal,3);
        }
        state.growthTotal=Math.Max(
            0,Math.Min(ExpeditionGrowthSystem.MaximumGrowth,state.growthTotal));
        state.rank=ExpeditionGrowthSystem.RankForGrowth(
            state.monsterId,state.growthTotal);
        state.currentHpRatio=Math.Max(0f,Math.Min(1f,state.currentHpRatio));
        state.benchSlot=Math.Max(-1,state.benchSlot);
        if(state.itemInstanceIds==null)
            state.itemInstanceIds=new List<string>();
        while(state.itemInstanceIds.Count<ExpeditionItemSystem.MaxSlots)
            state.itemInstanceIds.Add("");
        if(state.itemInstanceIds.Count>ExpeditionItemSystem.MaxSlots)
            state.itemInstanceIds.RemoveRange(
                ExpeditionItemSystem.MaxSlots,
                state.itemInstanceIds.Count-ExpeditionItemSystem.MaxSlots);
        if(state.appliedSpecialItemIds==null)
            state.appliedSpecialItemIds=new List<string>();
    }
}
