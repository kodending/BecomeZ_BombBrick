using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class PlayerProfileStore
{
    private const int CurrentVersion=6;
    private const int CurrentFirstExpeditionGuideVersion=2;
    private const int FirstExpeditionGuideCompleteStep=8;
    public const int FirstExpeditionGuideBattleStep=5;
    public const int ExpeditionRosterSize=8;
    private const string ProfileFileName="player_profile.json";
    private const string BackupFileName="player_profile.backup.json";
    private const string TemporaryFileName="player_profile.tmp.json";
    private const string MigrationBackupFileName="player_profile.v5.migration.json";
    private readonly string rootDirectory;
    private bool loadBlocked;
    private string resolvedDirectory;

    [Serializable]
    private sealed class LegacyOwnedMonsterSave
    {
        public string instanceId;
        public string monsterId;
        public string customName;
        public string intrinsicTraitId;
        public string personalityId;
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
        public bool deployed;
        public int deploymentX;
        public int deploymentY;
        public int benchSlot=-1;
        public bool locked;
        public bool favorite;
        public long acquiredUtcTicks;
    }

    [Serializable]
    private sealed class LegacyPlayerProfileSave
    {
        public int version;
        public string profileId="default";
        public long lastSavedUtcTicks;
        public List<LegacyOwnedMonsterSave> monsters=new List<LegacyOwnedMonsterSave>();
    }

    public PlayerProfileStore(string rootDirectory=null)
    {
        this.rootDirectory=rootDirectory;
    }

    public string DirectoryPath=>resolvedDirectory??(resolvedDirectory=string.IsNullOrWhiteSpace(rootDirectory)
        ?Path.Combine(BombBrick.Game.Core.GameDataPaths.Root,"Profiles")
        :rootDirectory);
    public string ProfilePath=>Path.Combine(DirectoryPath,ProfileFileName);
    public string BackupPath=>Path.Combine(DirectoryPath,BackupFileName);
    public string TemporaryPath=>Path.Combine(DirectoryPath,TemporaryFileName);
    public string MigrationBackupPath=>Path.Combine(DirectoryPath,MigrationBackupFileName);
    // Recovery files must still offer Continue; loading decides whether they can be recovered.
    public bool HasSavedProgress=>FilePresent(ProfilePath)||FilePresent(BackupPath)||
        FilePresent(TemporaryPath)||FilePresent(MigrationBackupPath)||
        FilePresent(new PlayerProgressReset(DirectoryPath).PendingPath);
    public bool RecoveredFromBackup { get; private set; }
    public string RecoveryArchivePath { get; private set; }
    public event Action<PlayerProfileSave> Changed;

    public PlayerProfileSave LoadOrCreate(MonsterDatabase database)
    {
        PlayerProfileSave result=null;
        var loading=LoadOrCreateRoutine(database,false,value=>result=value);
        try {while(loading.MoveNext()) {}}
        finally {(loading as IDisposable)?.Dispose();}
        return result;
    }

    public IEnumerator LoadOrCreateAcrossFrames(MonsterDatabase database,Action<PlayerProfileSave> completed)
        =>LoadOrCreateRoutine(database,true,completed);

    private IEnumerator LoadOrCreateRoutine(MonsterDatabase database,bool acrossFrames,
        Action<PlayerProfileSave> completed)
    {
        if(database==null)throw new ArgumentNullException(nameof(database));
        loadBlocked=true;
        if(string.IsNullOrWhiteSpace(rootDirectory))BombBrick.Game.Core.GameDataPaths.EnsureMigrated();
        string directory=DirectoryPath;
        RecoveredFromBackup=false;
        RecoveryArchivePath=null;
        bool migrated=false,recovered=false;
        PlayerProfileSave profile=null;
        InvalidDataException primaryError=null;
        string before=null;
        Action read=()=>
        {
            new PlayerProgressReset(directory).RecoverPending();
            string sourcePath=ProfilePath;
            try { profile=TryLoad(ProfilePath,out migrated); }
            catch(InvalidDataException exception)
            {
                primaryError=exception;
                profile=null;
                migrated=false;
            }
            if(profile==null)
            {
                sourcePath=BackupPath;
                profile=TryLoad(BackupPath,out migrated);
                recovered=profile!=null;
            }
            if(profile==null)
            {
                // Only genuinely absent files mean a first launch. Never replace unreadable saves.
                if(primaryError!=null)throw primaryError;
                if(FilePresent(TemporaryPath)||FilePresent(MigrationBackupPath))
                    throw new InvalidDataException("Profile files are missing but recovery data still exists.");
                migrated=false;
            }
            else
            {
                if(migrated)PreserveMigrationSource(sourcePath);
                before=JsonUtility.ToJson(profile,true);
            }
        };
        if(acrossFrames)
        {
            var task=Task.Run(read);
            while(!task.IsCompleted)yield return null;
            task.GetAwaiter().GetResult();
        }
        else read();
        if(profile==null)profile=CreateDefaultProfile(database);
        RestoreMemorialChundongi(profile,database);
        var normalization=NormalizeRoutine(profile,database,acrossFrames);
        while(normalization.MoveNext())yield return normalization.Current;
        Action commit=()=>
        {
            if(recovered&&primaryError!=null)
            {
                RecoveryArchivePath=Path.Combine(directory,
                    "player_profile.unreadable."+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+
                    "."+Guid.NewGuid().ToString("N")+".json");
                // Never rotate the damaged primary over the good backup.
                File.Move(ProfilePath,RecoveryArchivePath);
            }
            if(recovered||migrated||!string.Equals(before,JsonUtility.ToJson(profile,true),StringComparison.Ordinal))
                WriteProfileFile(profile);
        };
        if(acrossFrames)
        {
            // This DTO stays private until the worker finishes; Unity assets are only read above.
            var task=Task.Run(commit);
            while(!task.IsCompleted)yield return null;
            task.GetAwaiter().GetResult();
        }
        else commit();
        RecoveredFromBackup=recovered;
        loadBlocked=false;
        Changed?.Invoke(profile);
        completed(profile);
    }

    public void Save(PlayerProfileSave profile,bool notify=true)
    {
        if(loadBlocked)
            throw new InvalidOperationException("Profile loading failed. Reload successfully before saving.");
        WriteProfile(profile,notify);
    }

    private void WriteProfile(PlayerProfileSave profile,bool notify=true)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(string.IsNullOrWhiteSpace(rootDirectory))BombBrick.Game.Core.GameDataPaths.EnsureMigrated();
        WriteProfileFile(profile);
        if(notify)Changed?.Invoke(profile);
    }

    private void WriteProfileFile(PlayerProfileSave profile)
    {
        WriteProfileJson(SerializeProfile(profile));
    }

    public Action PrepareSaveFile(PlayerProfileSave profile)
    {
        if(loadBlocked)throw new InvalidOperationException("Reload successfully before saving.");
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(string.IsNullOrWhiteSpace(rootDirectory))BombBrick.Game.Core.GameDataPaths.EnsureMigrated();
        string directory=DirectoryPath;
        string json=SerializeProfile(profile);
        return ()=>WriteProfileJson(json);
    }

    public void SaveAcrossFrames(PlayerProfileSave profile)
    {
        BombBrick.Game.Core.GameFrontend.TrySaveProgressAcrossFrames(
            ()=>PrepareSaveFile(profile),DirectoryPath,()=>Changed?.Invoke(profile));
    }

    private static string SerializeProfile(PlayerProfileSave profile)
    {
        profile.version=CurrentVersion;
        profile.lastSavedUtcTicks=DateTime.UtcNow.Ticks;
        return JsonUtility.ToJson(profile,true);
    }

    private void WriteProfileJson(string json)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(TemporaryPath,json,new UTF8Encoding(false));
        if(File.Exists(ProfilePath))File.Replace(TemporaryPath,ProfilePath,BackupPath,true);
        else File.Move(TemporaryPath,ProfilePath);
    }

    public OwnedMonsterSave AcquireMonster(PlayerProfileSave profile,MonsterDatabase database,
        string monsterId,string personalityId=null)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(database==null)throw new ArgumentNullException(nameof(database));
        MonsterDefinition definition=database.Get(monsterId);
        if(definition==null||!definition.enabled)
            throw new InvalidOperationException("Cannot acquire missing or disabled monster: "+monsterId);

        string instanceId="player_"+monsterId+"_"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"_"+
            Guid.NewGuid().ToString("N").Substring(0,6);
        OwnedMonsterSave monster=CreateMonster(definition,instanceId);
        if(!string.IsNullOrWhiteSpace(personalityId))monster.personalityId=personalityId;
        profile.monsters.Add(monster);
        Normalize(profile,database);
        Save(profile);
        return monster;
    }

    public OwnedMonsterSave AddGeneratedMonster(PlayerProfileSave profile,MonsterDatabase database,
        OwnedMonsterSave generatedMonster,bool save=true)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(database==null)throw new ArgumentNullException(nameof(database));
        if(generatedMonster==null)throw new ArgumentNullException(nameof(generatedMonster));
        MonsterDefinition definition=database.Get(generatedMonster.monsterId);
        if(definition==null||!definition.enabled)
            throw new InvalidOperationException("Cannot acquire missing or disabled monster: "+generatedMonster.monsterId);
        if(string.IsNullOrWhiteSpace(generatedMonster.instanceId)||
           profile.monsters.Any(item=>item!=null&&item.instanceId==generatedMonster.instanceId))
            throw new InvalidOperationException("Generated monster has a missing or duplicate instanceId.");

        generatedMonster.acquiredUtcTicks=DateTime.UtcNow.Ticks;
        MonsterInstanceGenerator.EnsureInitialized(generatedMonster,definition);
        profile.monsters.Add(generatedMonster);
        Normalize(profile,database);
        if(save)Save(profile);
        return generatedMonster;
    }

    public bool UpdateMonster(PlayerProfileSave profile,MonsterDatabase database,string instanceId,
        Action<OwnedMonsterSave> mutation)
    {
        if(profile==null||database==null||mutation==null||string.IsNullOrWhiteSpace(instanceId))return false;
        OwnedMonsterSave monster=profile.monsters.Find(item=>item!=null&&item.instanceId==instanceId);
        if(monster==null)return false;
        mutation(monster);
        Normalize(profile,database);
        Save(profile);
        return true;
    }

    public bool RemoveMonster(PlayerProfileSave profile,MonsterDatabase database,string instanceId)
    {
        if(profile==null||database==null||string.IsNullOrWhiteSpace(instanceId))return false;
        if(instanceId==MemorialChundongiInstanceId)return false;
        int removed=profile.monsters.RemoveAll(item=>item!=null&&item.instanceId==instanceId);
        if(removed==0)return false;
        Normalize(profile,database);
        Save(profile);
        return true;
    }

    public FormationPlacementSave GetPlacement(PlayerProfileSave profile,string instanceId)
    {
        if(profile==null||profile.expeditionPreset==null||string.IsNullOrWhiteSpace(instanceId))return null;
        return profile.expeditionPreset.placements.Find(item=>item!=null&&item.instanceId==instanceId);
    }

    public void SetExpeditionRoster(
        PlayerProfileSave profile,
        MonsterDatabase database,
        IReadOnlyList<string> instanceIds)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        if(database==null)throw new ArgumentNullException(nameof(database));
        if(instanceIds==null)throw new ArgumentNullException(nameof(instanceIds));
        if(instanceIds.Count>ExpeditionRosterSize)
            throw new InvalidOperationException(
                "An expedition roster cannot contain more than "+
                ExpeditionRosterSize+" monsters.");

        List<string> roster=instanceIds
            .Where(id=>!string.IsNullOrWhiteSpace(id))
            .ToList();
        if(roster.Distinct(StringComparer.Ordinal).Count()!=roster.Count)
            throw new InvalidOperationException(
                "An expedition roster cannot contain duplicate monsters.");

        Dictionary<string,OwnedMonsterSave> owned=profile.monsters
            .Where(item=>item!=null&&!string.IsNullOrWhiteSpace(item.instanceId))
            .GroupBy(item=>item.instanceId,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.Ordinal);
        foreach(string instanceId in roster)
        {
            if(!owned.TryGetValue(instanceId,out OwnedMonsterSave monster)||
               !IsEnabled(database,monster.monsterId))
                throw new InvalidOperationException(
                    "The expedition roster contains an unavailable monster: "+
                    instanceId);
        }
        if(roster.Select(instanceId=>owned[instanceId].monsterId)
               .Distinct(StringComparer.Ordinal).Count()!=roster.Count)
            throw new InvalidOperationException(
                "An expedition roster cannot contain duplicate monster types.");

        if(profile.expeditionPreset==null)
            profile.expeditionPreset=new ExpeditionPresetSave();
        Dictionary<string,FormationPlacementSave> previous=
            (profile.expeditionPreset.placements??new List<FormationPlacementSave>())
            .Where(item=>item!=null&&!string.IsNullOrWhiteSpace(item.instanceId))
            .GroupBy(item=>item.instanceId,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.Ordinal);

        profile.expeditionPreset.rosterInstanceIds=instanceIds
            .Select(id=>string.IsNullOrWhiteSpace(id)?string.Empty:id).ToList();
        profile.expeditionPreset.placements=new List<FormationPlacementSave>();
        profile.expeditionPreset.configured=true;
        for(int index=0;index<roster.Count;index++)
        {
            string instanceId=roster[index];
            if(!previous.TryGetValue(instanceId,out FormationPlacementSave placement))
            {
                placement=new FormationPlacementSave
                {
                    instanceId=instanceId,
                    deployed=index<3,
                    deploymentX=8+index,
                    deploymentY=0,
                    benchSlot=index<3?-1:index-3
                };
            }
            profile.expeditionPreset.placements.Add(placement);
        }

        Normalize(profile,database);
        Save(profile);
    }

    private void PreserveMigrationSource(string sourcePath)
    {
        if(string.IsNullOrWhiteSpace(sourcePath)||!File.Exists(sourcePath)||File.Exists(MigrationBackupPath))return;
        Directory.CreateDirectory(DirectoryPath);
        File.Copy(sourcePath,MigrationBackupPath,false);
    }

    private static PlayerProfileSave TryLoad(string path,out bool migrated)
    {
        migrated=false;
        string json;
        try { json=File.ReadAllText(path); }
        catch(FileNotFoundException) { return null; }
        catch(DirectoryNotFoundException) { return null; }
        // Access errors and newer versions must stop loading, not roll progress back to an older backup.
        try
        {
            JObject document=JObject.Parse(json,new JsonLoadSettings
            {
                DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error
            });
            JToken version=document["version"];
            if(version?.Type!=JTokenType.Integer||
               document["profileId"]?.Type!=JTokenType.String||
               string.IsNullOrWhiteSpace((string)document["profileId"])||
               !(document["monsters"] is JArray monsters))
                throw new InvalidDataException("Required profile fields are missing or invalid.");
            long savedVersion=(long)version;
            if(savedVersion>CurrentVersion)
                throw new NotSupportedException("Profile was saved by a newer game version.");
            if(savedVersion<1)
                throw new InvalidDataException("Profile version is invalid.");
            HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(JToken monster in monsters)
            {
                if(!(monster is JObject entry)||
                   entry["instanceId"]?.Type!=JTokenType.String||
                   string.IsNullOrWhiteSpace((string)entry["instanceId"])||
                   !ids.Add((string)entry["instanceId"])||
                   entry["monsterId"]?.Type!=JTokenType.String||
                   string.IsNullOrWhiteSpace((string)entry["monsterId"]))
                    throw new InvalidDataException("Profile monster entries are invalid.");
            }
            if(savedVersion<CurrentVersion)
            {
                LegacyPlayerProfileSave legacy=JsonUtility.FromJson<LegacyPlayerProfileSave>(json);
                migrated=true;
                return MigrateLegacyProfile(legacy);
            }

            PlayerProfileSave profile=JsonUtility.FromJson<PlayerProfileSave>(json);
            if(profile==null||profile.monsters==null)
                throw new InvalidDataException("Profile could not be deserialized.");
            return profile;
        }
        catch(Exception exception) when(exception is JsonException||
                                       exception is ArgumentException||
                                       exception is OverflowException)
        {
            throw new InvalidDataException("Invalid profile: "+Path.GetFileName(path),exception);
        }
    }

    private static bool FilePresent(string path)
    {
        try { File.GetAttributes(path); return true; }
        catch(FileNotFoundException) { return false; }
        catch(DirectoryNotFoundException) { return false; }
    }

    private static PlayerProfileSave MigrateLegacyProfile(LegacyPlayerProfileSave legacy)
    {
        PlayerProfileSave profile=new PlayerProfileSave
        {
            version=CurrentVersion,
            profileId=string.IsNullOrWhiteSpace(legacy.profileId)?"default":legacy.profileId,
            lastSavedUtcTicks=legacy.lastSavedUtcTicks,
            expeditionPreset=new ExpeditionPresetSave()
        };

        foreach(LegacyOwnedMonsterSave oldMonster in legacy.monsters)
        {
            if(oldMonster==null)continue;
            OwnedMonsterSave monster=new OwnedMonsterSave
            {
                instanceId=oldMonster.instanceId,
                monsterId=oldMonster.monsterId,
                customName=oldMonster.customName,
                intrinsicTraitId=oldMonster.intrinsicTraitId,
                personalityId=oldMonster.personalityId,
                personalitySeed=oldMonster.personalitySeed,
                courage=oldMonster.courage,
                aggression=oldMonster.aggression,
                discipline=oldMonster.discipline,
                composure=oldMonster.composure,
                cooperation=oldMonster.cooperation,
                adaptability=oldMonster.adaptability,
                hpMultiplier=oldMonster.hpMultiplier,
                damageMultiplier=oldMonster.damageMultiplier,
                attackSpeedMultiplier=oldMonster.attackSpeedMultiplier,
                defenseMultiplier=oldMonster.defenseMultiplier,
                statusResistanceMultiplier=oldMonster.statusResistanceMultiplier,
                moveSpeedMultiplier=oldMonster.moveSpeedMultiplier,
                locked=oldMonster.locked,
                favorite=oldMonster.favorite,
                acquiredUtcTicks=oldMonster.acquiredUtcTicks
            };
            profile.monsters.Add(monster);

            if(oldMonster.deployed||oldMonster.benchSlot>=0)
            {
                profile.expeditionPreset.rosterInstanceIds.Add(oldMonster.instanceId);
                profile.expeditionPreset.placements.Add(new FormationPlacementSave
                {
                    instanceId=oldMonster.instanceId,
                    deployed=oldMonster.deployed,
                    deploymentX=oldMonster.deploymentX,
                    deploymentY=oldMonster.deploymentY,
                    benchSlot=oldMonster.benchSlot
                });
            }
        }
        return profile;
    }

    private static PlayerProfileSave CreateDefaultProfile(MonsterDatabase database)
    {
        PlayerProfileSave profile=new PlayerProfileSave
        {
            firstExpeditionGuideInitialized=true,
            firstExpeditionGuideVersion=CurrentFirstExpeditionGuideVersion,
            firstExpeditionGuideStep=0,
            starterSelectionPending=true,
            expeditionPreset=new ExpeditionPresetSave {configured=true}
        };
        AddStarterRoster(profile,database);
        return profile;
    }

    public static OwnedMonsterSave CreateMonster(
        MonsterDefinition definition,string instanceId,bool allowShiny=true)
    {
        OwnedMonsterSave monster=new OwnedMonsterSave
        {
            instanceId=instanceId,
            monsterId=definition.monsterId,
            personalitySeed=MonsterInstanceGenerator.StableSeed(instanceId),
            acquiredUtcTicks=DateTime.UtcNow.Ticks
        };
        MonsterInstanceGenerator.InitializeNewInstance(
            monster,definition,allowShiny);
        return monster;
    }

    private static void Normalize(PlayerProfileSave profile,MonsterDatabase database)
    {
        var normalization=NormalizeRoutine(profile,database,false);
        while(normalization.MoveNext()) {}
    }

    private static IEnumerator NormalizeRoutine(PlayerProfileSave profile,MonsterDatabase database,bool acrossFrames)
    {
        profile.version=CurrentVersion;
        profile.hunterTokens=Math.Max(0,profile.hunterTokens);
        if(!profile.firstExpeditionGuideInitialized)
        {
            profile.firstExpeditionGuideInitialized=true;
            profile.firstExpeditionGuideVersion=
                CurrentFirstExpeditionGuideVersion;
            profile.firstExpeditionGuideStep=
                FirstExpeditionGuideCompleteStep;
        }
        else if(profile.firstExpeditionGuideVersion<
                CurrentFirstExpeditionGuideVersion)
        {
            profile.firstExpeditionGuideStep=
                profile.firstExpeditionGuideStep>=5
                    ?FirstExpeditionGuideCompleteStep
                    :0;
            profile.firstExpeditionGuideVersion=
                CurrentFirstExpeditionGuideVersion;
        }
        profile.firstExpeditionGuideStep=Math.Max(
            0,Math.Min(
                FirstExpeditionGuideCompleteStep,
                profile.firstExpeditionGuideStep));
        profile.firstExpeditionCaptureGuideStep=Math.Max(
            0,Math.Min(8,profile.firstExpeditionCaptureGuideStep));
        if(!profile.captureNetTwoTierMigrationApplied)
        {
            profile.captureNets?.Clear();
            profile.captureNetTwoTierMigrationApplied=true;
        }
        CaptureNetInventory.Normalize(profile);
        if(!profile.captureNetStarterPackGranted)
        {
            profile.captureNetStarterPackGranted=true;
        }
        if(profile.monsters==null)profile.monsters=new List<OwnedMonsterSave>();
        if(acrossFrames)yield return null;
        HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
        for(int i=profile.monsters.Count-1;i>=0;i--)
        {
            OwnedMonsterSave monster=profile.monsters[i];
            if(monster==null||string.IsNullOrWhiteSpace(monster.instanceId)||!ids.Add(monster.instanceId))
            {
                profile.monsters.RemoveAt(i);
                continue;
            }

            if(monster.hpMultiplier<=0f)monster.hpMultiplier=1f;
            if(monster.damageMultiplier<=0f)monster.damageMultiplier=1f;
            if(monster.attackSpeedMultiplier<=0f)monster.attackSpeedMultiplier=1f;
            if(monster.defenseMultiplier<=0f)monster.defenseMultiplier=1f;
            if(monster.statusResistanceMultiplier<=0f)
                monster.statusResistanceMultiplier=1f;
            if(monster.moveSpeedMultiplier<=0f)monster.moveSpeedMultiplier=1f;
            MonsterDefinition definition=database.Get(monster.monsterId);
            if(definition==null||!definition.enabled)
            {
                profile.monsters.RemoveAt(i);
                continue;
            }
            MonsterInstanceGenerator.EnsureInitialized(monster,definition);
            if(acrossFrames)yield return null;
        }

        if(profile.starterRerollInstanceIds==null)
            profile.starterRerollInstanceIds=new List<string>();
        if(!profile.monsters.Any(m=>m.instanceId!=MemorialChundongiInstanceId))
        {
            OwnedMonsterSave memorial=profile.monsters.FirstOrDefault();
            profile.monsters.Clear();
            profile.starterSelectionPending=true;
            profile.starterRerollInstanceIds.Clear();
            AddStarterRoster(profile,database);
            if(memorial!=null)profile.monsters.Add(memorial);
        }
        if(profile.starterSelectionPending)
            foreach(OwnedMonsterSave monster in profile.monsters
                .Where(m=>m.instanceId!=MemorialChundongiInstanceId).Take(ExpeditionRosterSize))
                InitializeStarter(monster,database.Get(monster.monsterId),
                    !profile.starterRerollInstanceIds.Contains(monster.instanceId));
        HunterShopSystem.NormalizeProfile(profile,database);
        if(acrossFrames)yield return null;
        PermanentResearchSystem.Normalize(profile);
        if(acrossFrames)yield return null;
        NormalizePreset(profile,database);
    }

    public int CaptureNetCount(PlayerProfileSave profile,string itemId)
    {
        return CaptureNetInventory.Count(profile,itemId);
    }

    public bool TryConsumeCaptureNet(
        PlayerProfileSave profile,string itemId,out string error)
    {
        if(!CaptureNetInventory.TryConsume(profile,itemId,out error))
            return false;
        Save(profile);
        return true;
    }

    public void GrantCaptureNet(
        PlayerProfileSave profile,string itemId,int count)
    {
        if(profile==null)throw new ArgumentNullException(nameof(profile));
        CaptureNetInventory.Grant(profile,itemId,count);
        Save(profile);
    }

    private static void AddStarterRoster(PlayerProfileSave profile,MonsterDatabase database)
    {
        for(int slot=0;slot<6;slot++)
        {
            OwnedMonsterSave monster=RollStarter(profile,database,true);
            profile.monsters.Add(monster);
        }
        for(int slot=0;slot<2;slot++)
        {
            OwnedMonsterSave monster=RollStarter(profile,database);
            profile.monsters.Add(monster);
            profile.starterRerollInstanceIds.Add(monster.instanceId);
        }
    }

    private static void InitializeStarter(OwnedMonsterSave monster,MonsterDefinition definition,bool basic)
    {
        if(basic)
        {
            monster.isShiny=false;
            MonsterInstanceGenerator.LimitStarterQuality(monster);
        }
        var random=new System.Random(MonsterInstanceGenerator.StableSeed(monster.instanceId+"_starter_traits"));
        if(string.IsNullOrEmpty(monster.rank2TraitId))
        {
            var pool=MonsterTraitCatalog.Rank2.Where(t=>t.IsCompatible(definition)).ToArray();
            if(pool.Length==0)throw new InvalidOperationException("No compatible rank-2 starter trait: "+definition.monsterId);
            monster.rank2TraitId=pool[random.Next(pool.Length)].id;
        }
        if(string.IsNullOrEmpty(monster.rank3TraitId))
        {
            var pool=MonsterTraitCatalog.Rank3.Where(t=>t.IsCompatible(definition)&&!t.IsComboBridge).ToArray();
            if(pool.Length==0)throw new InvalidOperationException("No compatible rank-3 starter trait: "+definition.monsterId);
            monster.rank3TraitId=pool[random.Next(pool.Length)].id;
        }
    }

    private static OwnedMonsterSave RollStarter(PlayerProfileSave profile,MonsterDatabase database,
        bool basic=false)
    {
        var occupied=new HashSet<string>(profile.monsters
            .Select(item=>item.monsterId),StringComparer.Ordinal);
        var pool=database.monsters.Where(item=>item!=null&&item.enabled&&
            !occupied.Contains(item.monsterId)).OrderBy(item=>item.monsterId).ToList();
        if(pool.Count==0)throw new InvalidOperationException("No available starter species.");
        string id="player_"+Guid.NewGuid().ToString("N");
        var random=new System.Random(MonsterInstanceGenerator.StableSeed(id));
        MonsterDefinition definition=pool[random.Next(pool.Count)];
        OwnedMonsterSave monster=CreateMonster(definition,id,!basic);
        InitializeStarter(monster,definition,basic);
        return monster;
    }

    public void RerollStarters(PlayerProfileSave profile,MonsterDatabase database)
    {
        if(profile==null||!profile.starterSelectionPending)
            throw new InvalidOperationException("Starter selection is already complete.");
        var previous=profile.monsters;
        var previousIds=profile.starterRerollInstanceIds;
        profile.monsters=new List<OwnedMonsterSave>();
        profile.starterRerollInstanceIds=new List<string>();
        try
        {
            AddStarterRoster(profile,database);
            profile.monsters.AddRange(previous.Where(m=>m.instanceId==MemorialChundongiInstanceId));
            Save(profile);
        }
        catch
        {
            profile.monsters=previous;
            profile.starterRerollInstanceIds=previousIds;
            throw;
        }
    }

    public void ConfirmStarters(PlayerProfileSave profile)
    {
        if(profile==null||!profile.starterSelectionPending)
            throw new InvalidOperationException("Starter selection is already complete.");
        var starters=profile.monsters.Where(m=>m.instanceId!=MemorialChundongiInstanceId).ToList();
        if(starters.Count!=8||starters.Select(m=>m.monsterId).Distinct().Count()!=8||
           profile.starterRerollInstanceIds.Count!=2||profile.starterRerollInstanceIds.Any(
               id=>!starters.Any(m=>m.instanceId==id)))
            throw new InvalidOperationException("Starter selection is incomplete.");
        profile.starterSelectionPending=false;
        try { Save(profile); }
        catch { profile.starterSelectionPending=true;throw; }
    }

    private static void NormalizePreset(PlayerProfileSave profile,MonsterDatabase database)
    {
        if(profile.expeditionPreset==null)profile.expeditionPreset=new ExpeditionPresetSave();
        if(profile.expeditionPreset.rosterInstanceIds==null)
            profile.expeditionPreset.rosterInstanceIds=new List<string>();
        if(profile.expeditionPreset.placements==null)
            profile.expeditionPreset.placements=new List<FormationPlacementSave>();

        Dictionary<string,OwnedMonsterSave> validMonsters=profile.monsters
            .Where(item=>item!=null&&IsEnabled(database,item.monsterId))
            .GroupBy(item=>item.instanceId,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.Ordinal);
        HashSet<string> rosterMonsterIds=new HashSet<string>(StringComparer.Ordinal);
        profile.expeditionPreset.rosterInstanceIds=profile.expeditionPreset.rosterInstanceIds
            .Take(ExpeditionRosterSize)
            .Select(id=>!string.IsNullOrWhiteSpace(id)&&
                validMonsters.TryGetValue(id,out OwnedMonsterSave monster)&&
                rosterMonsterIds.Add(monster.monsterId)?id:string.Empty)
            .ToList();
        List<string> slots=profile.expeditionPreset.rosterInstanceIds;
        int occupiedLength=slots.FindLastIndex(id=>!string.IsNullOrWhiteSpace(id))+1;
        slots.RemoveRange(occupiedLength,slots.Count-occupiedLength);
        if(profile.expeditionPreset.RosterCount>0)
            profile.expeditionPreset.configured=true;

        Dictionary<string,FormationPlacementSave> placements=profile.expeditionPreset.placements
            .Where(item=>item!=null&&!string.IsNullOrWhiteSpace(item.instanceId)&&
                         profile.expeditionPreset.rosterInstanceIds.Contains(item.instanceId))
            .GroupBy(item=>item.instanceId,StringComparer.Ordinal)
            .ToDictionary(group=>group.Key,group=>group.First(),StringComparer.Ordinal);

        profile.expeditionPreset.placements.Clear();
        foreach(string instanceId in slots)
        {
            if(string.IsNullOrWhiteSpace(instanceId))continue;
            int i=profile.expeditionPreset.placements.Count;
            if(!placements.TryGetValue(instanceId,out FormationPlacementSave placement))
            {
                placement=new FormationPlacementSave
                {
                    instanceId=instanceId,
                    deployed=i<3,
                    deploymentX=8+i,
                    deploymentY=0,
                    benchSlot=i<3?-1:i-3
                };
            }
            placement.benchSlot=Math.Max(-1,placement.benchSlot);
            profile.expeditionPreset.placements.Add(placement);
        }
    }

    private static bool IsEnabled(MonsterDatabase database,string monsterId)
    {
        MonsterDefinition definition=database.Get(monsterId);
        return definition!=null&&definition.enabled;
    }

    private static int EnabledMonsterCount(PlayerProfileSave profile,MonsterDatabase database)
    {
        return profile.monsters.Count(item=>item!=null&&IsEnabled(database,item.monsterId));
    }
}
