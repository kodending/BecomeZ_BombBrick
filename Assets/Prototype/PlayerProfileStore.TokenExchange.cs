using System;
using System.Linq;
using UnityEngine;

public sealed partial class PlayerProfileStore
{
    public static int MonsterTokenValue(OwnedMonsterSave monster)
    {
        if(monster==null)return 0;
        if(monster.isShiny)return Mathf.Max(0,GameMasterData.ExpeditionInt("monster_exchange_tokens_shiny",15));
        switch(MonsterStatQuality.OverallGrade(monster)[0])
        {
            case 'S':return Mathf.Max(0,GameMasterData.ExpeditionInt("monster_exchange_tokens_s",8));
            case 'A':return Mathf.Max(0,GameMasterData.ExpeditionInt("monster_exchange_tokens_a",4));
            case 'B':return Mathf.Max(0,GameMasterData.ExpeditionInt("monster_exchange_tokens_b",2));
            default:return Mathf.Max(0,GameMasterData.ExpeditionInt("monster_exchange_tokens_dc",1));
        }
    }

    public static string MonsterExchangeBlock(PlayerProfileSave profile,OwnedMonsterSave monster,
        ExpeditionRunSave activeRun=null)
    {
        if(profile==null||monster==null||!profile.monsters.Contains(monster))return "missing";
        if(monster.instanceId==MemorialChundongiInstanceId||monster.locked||monster.favorite)return "protected";
        if(profile.starterSelectionPending||profile.monsters.Count<=ExpeditionRosterSize)return "minimum";
        if(profile.expeditionPreset.rosterInstanceIds.Contains(monster.instanceId)||
            activeRun!=null&&activeRun.monsters.Any(m=>m.instanceId==monster.instanceId))return "roster";
        return null;
    }

    public bool ExchangeMonsterForTokens(PlayerProfileSave profile,MonsterDatabase database,
        string instanceId,ExpeditionRunSave activeRun=null)
    {
        if(profile==null||database==null||string.IsNullOrWhiteSpace(instanceId))return false;
        OwnedMonsterSave monster=profile.monsters.Find(m=>m!=null&&m.instanceId==instanceId);
        if(MonsterExchangeBlock(profile,monster,activeRun)!=null)return false;
        int reward=MonsterTokenValue(monster);
        int balance=checked(profile.hunterTokens+reward);
        var staged=JsonUtility.FromJson<PlayerProfileSave>(JsonUtility.ToJson(profile));
        staged.monsters.RemoveAll(m=>m.instanceId==instanceId);
        staged.expeditionPreset.placements.RemoveAll(p=>p.instanceId==instanceId);
        staged.hunterTokens=balance;
        // Commit both changes together; a failed save must leave the live collection untouched.
        Save(staged,false);
        profile.monsters.Remove(monster);
        profile.expeditionPreset.placements.RemoveAll(p=>p.instanceId==instanceId);
        profile.hunterTokens=balance;
        profile.version=staged.version;
        profile.lastSavedUtcTicks=staged.lastSavedUtcTicks;
        Changed?.Invoke(profile);
        return true;
    }
}
