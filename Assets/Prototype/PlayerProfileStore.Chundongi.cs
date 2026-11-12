using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class PlayerProfileStore
{
    public const string MemorialChundongiInstanceId="memorial_chundongi";
    public string MemorialChundongiPath=>Path.GetFullPath(DirectoryPath)
        .TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+".chundongi.json";
    public bool HasMemorialChundongi=>ReadMemorialChundongi()!=null;

    [Serializable]
    private sealed class ChundongiMemorialSave
    {
        public int version=1;
        public OwnedMonsterSave companion;
    }

    public bool UnlockMemorialChundongi(MonsterDatabase database)
    {
        if(database==null)throw new ArgumentNullException(nameof(database));
        if(string.IsNullOrWhiteSpace(rootDirectory))BombBrick.Game.Core.GameDataPaths.EnsureMigrated();
        ChundongiMemorialSave memorial=ReadMemorialChundongi();
        bool newlyUnlocked=memorial==null;
        if(newlyUnlocked)
        {
            MonsterDefinition definition=database.Get("chundongi");
            if(definition==null||!definition.enabled)
                throw new InvalidOperationException("Chundongi is missing from the monster database.");
            memorial=new ChundongiMemorialSave {companion=CreateMemorialCompanion(definition)};
            WriteMemorialChundongi(memorial);
        }
        // Discovering the easter egg before New Game must not create a Continue save.
        if(HasSavedProgress)LoadOrCreate(database);
        return newlyUnlocked;
    }

    internal void RestoreMemorialChundongi(PlayerProfileSave profile,MonsterDatabase database,bool newGame=false)
    {
        ChundongiMemorialSave memorial=ReadMemorialChundongi();
        if(memorial==null)return;
        MonsterDefinition definition=database.Get("chundongi");
        if(definition==null||!definition.enabled)
            throw new InvalidOperationException("Unlocked Chundongi is missing from the monster database.");
        OwnedMonsterSave existing=profile.monsters?.FirstOrDefault(m=>m!=null&&m.instanceId==MemorialChundongiInstanceId);
        if(existing!=null)
        {
            existing.locked=true;
            return;
        }
        if(profile.monsters==null)profile.monsters=new System.Collections.Generic.List<OwnedMonsterSave>();
        profile.monsters.Add(newGame?CreateMemorialCompanion(definition):
            JsonUtility.FromJson<OwnedMonsterSave>(JsonUtility.ToJson(memorial.companion)));
    }

    private static OwnedMonsterSave CreateMemorialCompanion(MonsterDefinition definition)
    {
        // Roll with a fresh seed, then restore the permanent identity used by save protection.
        var companion=CreateMonster(definition,MemorialChundongiInstanceId+"_"+Guid.NewGuid().ToString("N"),false);
        InitializeStarter(companion,definition,false);
        companion.instanceId=MemorialChundongiInstanceId;
        companion.hpMultiplier=MemorialStat(MonsterIndividualStat.Health);
        companion.damageMultiplier=MemorialStat(MonsterIndividualStat.AttackDamage);
        companion.attackSpeedMultiplier=MemorialStat(MonsterIndividualStat.AttackSpeed);
        companion.defenseMultiplier=MemorialStat(MonsterIndividualStat.Defense);
        companion.statusResistanceMultiplier=MemorialStat(MonsterIndividualStat.StatusResistance);
        companion.moveSpeedMultiplier=MemorialStat(MonsterIndividualStat.MoveSpeed);
        companion.locked=companion.favorite=companion.isNew=true;
        return companion;
    }

    private static float MemorialStat(MonsterIndividualStat stat)
    {
        return Mathf.Lerp(MonsterStatQuality.MinimumPercent(stat),MonsterStatQuality.MaximumPercent(stat),.95f)/100f;
    }

    private ChundongiMemorialSave ReadMemorialChundongi()
    {
        InvalidDataException primaryError=null;
        ChundongiMemorialSave memorial;
        try { memorial=ReadMemorialChundongiFile(MemorialChundongiPath); }
        catch(InvalidDataException exception) {primaryError=exception;memorial=null;}
        if(memorial!=null)
        {
            EnsureMemorialChundongiBackup();
            return memorial;
        }
        memorial=ReadMemorialChundongiFile(MemorialChundongiPath+".backup");
        if(memorial!=null)return memorial;
        if(primaryError!=null)throw primaryError;
        memorial=ReadMemorialChundongiFile(MemorialChundongiPath+".tmp");
        if(memorial==null)return null;
        File.Move(MemorialChundongiPath+".tmp",MemorialChundongiPath);
        EnsureMemorialChundongiBackup();
        return memorial;
    }

    private static ChundongiMemorialSave ReadMemorialChundongiFile(string path)
    {
        string json;
        try {json=File.ReadAllText(path);}
        catch(FileNotFoundException) {return null;}
        catch(DirectoryNotFoundException) {return null;}
        try
        {
            JObject document=JObject.Parse(json,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            if(document["version"]?.Type!=JTokenType.Integer)
                throw new InvalidDataException("Invalid Chundongi unlock version.");
            if((long)document["version"]>1)
                throw new NotSupportedException("Chundongi unlock was saved by a newer game version.");
            var saved=JsonUtility.FromJson<ChundongiMemorialSave>(json);
            OwnedMonsterSave companion=saved?.companion;
            if(saved?.version!=1||companion==null||companion.instanceId!=MemorialChundongiInstanceId||
               companion.monsterId!="chundongi"||companion.isShiny||
               !MonsterInstanceGenerator.AxesAreValid(companion)||!MonsterInstanceGenerator.MultipliersAreValid(companion)||
               MonsterStatQuality.OverallGrade(companion)!="S"||string.IsNullOrEmpty(companion.rank2TraitId)||
               string.IsNullOrEmpty(companion.rank3TraitId))
                throw new InvalidDataException("Invalid Chundongi unlock data.");
            return saved;
        }
        catch(Exception exception) when(exception is JsonException||exception is ArgumentException||exception is OverflowException)
        {
            throw new InvalidDataException("Invalid Chundongi unlock data.",exception);
        }
    }

    private void WriteMemorialChundongi(ChundongiMemorialSave memorial)
    {
        string path=MemorialChundongiPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        byte[] json=new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(memorial,true));
        using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
        {
            stream.Write(json,0,json.Length);
            stream.Flush(true);
        }
        if(File.Exists(path))File.Replace(path+".tmp",path,path+".backup",true);
        else File.Move(path+".tmp",path);
        EnsureMemorialChundongiBackup();
    }

    private void EnsureMemorialChundongiBackup()
    {
        string backup=MemorialChundongiPath+".backup";
        if(FilePresent(backup))return;
        File.Copy(MemorialChundongiPath,backup+".tmp",true);
        File.Move(backup+".tmp",backup);
    }
}
