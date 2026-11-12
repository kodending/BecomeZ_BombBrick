using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class AutoBattleTelemetryRecorder : IDisposable
{
    private const int SchemaVersion=10;
    private const int FlushLineCount=128;
    private readonly StringBuilder buffer=new StringBuilder(32768);
    private readonly string sessionId;
    private int bufferedLines;
    private bool disposed;

    public string FilePath { get; }

    public AutoBattleTelemetryRecorder()
    {
        sessionId=DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
        string directory=Path.Combine(Application.persistentDataPath,"AutoBattleTelemetry");
        Directory.CreateDirectory(directory);
        FilePath=Path.Combine(directory,"battle_"+sessionId+".jsonl");
    }

    public void RecordEpisodeStart(
        int columns,int rows,int playerCount,int enemyCount,string mode,
        string runId,string regionId,string difficulty,int wave,int seed,
        int academyStep,string scenarioProfile)
    {
        Append("{\"schema\":"+SchemaVersion+",\"event\":\"episode_start\",\"session\":\""+sessionId+
               "\",\"utc\":\""+DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture)+"\",\"columns\":"+columns+
               ",\"rows\":"+rows+",\"player_units\":"+playerCount+",\"enemy_units\":"+enemyCount+
               ",\"mode\":\""+Escape(mode)+"\",\"run_id\":\""+Escape(runId)+
               "\",\"region_id\":\""+Escape(regionId)+"\",\"difficulty\":\""+Escape(difficulty)+
               "\",\"wave\":"+wave+",\"seed\":"+seed+",\"academy_step\":"+academyStep+
               ",\"scenario_profile\":\""+Escape(scenarioProfile)+"\"}");
        Flush();
    }

    public void RecordStep(float time,int unitId,string team,string skillId,float x,float z,float velocityX,float velocityZ,
        float hpRatio,int targetId,float targetDistance,float nearestAlly,float nearestEnemy,int alliesNearby,int enemiesNearby,string action)
    {
        if(disposed)return;
        buffer.Append("{\"schema\":").Append(SchemaVersion).Append(",\"event\":\"step\",\"session\":\"").Append(sessionId)
            .Append("\",\"time\":").Append(F(time)).Append(",\"unit\":").Append(unitId)
            .Append(",\"team\":\"").Append(Escape(team)).Append("\",\"skillId\":\"").Append(Escape(skillId))
            .Append("\",\"x\":").Append(F(x)).Append(",\"z\":").Append(F(z))
            .Append(",\"vx\":").Append(F(velocityX)).Append(",\"vz\":").Append(F(velocityZ))
            .Append(",\"hp\":").Append(F(hpRatio)).Append(",\"target\":").Append(targetId)
            .Append(",\"target_distance\":").Append(F(targetDistance)).Append(",\"nearest_ally\":").Append(F(nearestAlly))
            .Append(",\"nearest_enemy\":").Append(F(nearestEnemy)).Append(",\"allies_nearby\":").Append(alliesNearby)
            .Append(",\"enemies_nearby\":").Append(enemiesNearby).Append(",\"action\":\"").Append(Escape(action)).Append("\"}");
        Append(string.Empty);
    }

    public void RecordDamage(float time,int sourceId,int targetId,float amount,float targetHpRatio)
    {
        Append("{\"schema\":"+SchemaVersion+",\"event\":\"damage\",\"session\":\""+sessionId+"\",\"time\":"+F(time)+
               ",\"source\":"+sourceId+",\"target\":"+targetId+",\"amount\":"+F(amount)+",\"target_hp\":"+F(targetHpRatio)+"}");
    }

    public void RecordSkill(float time,int unitId,string skill,int hits,float damage)
    {
        Append("{\"schema\":"+SchemaVersion+",\"event\":\"skill\",\"session\":\""+sessionId+"\",\"time\":"+F(time)+
               ",\"unit\":"+unitId+",\"skill\":\""+Escape(skill)+"\",\"hits\":"+hits+",\"damage\":"+F(damage)+"}");
    }

    public void RecordPlacement(
        int unitId,string stableId,string monsterId,string familyId,string team,
        string expeditionRank,int deploymentSlots,string personalityId,
        int personalitySeed,string activeTraits,string itemIds,float maxHp,
        float damage,float attackInterval,float defense,float resistance,
        int attackRange,int anchorX,int anchorY,int footprintWidth,
        int footprintHeight,bool inStorage)
    {
        Append("{\"schema\":"+SchemaVersion+",\"event\":\"placement\",\"session\":\""+sessionId+"\",\"unit\":"+unitId+
               ",\"stable_id\":\""+Escape(stableId)+"\",\"monster_id\":\""+Escape(monsterId)+
               "\",\"family_id\":\""+Escape(familyId)+"\",\"team\":\""+Escape(team)+
               "\",\"expedition_rank\":\""+Escape(expeditionRank)+
               "\",\"deployment_slots\":"+deploymentSlots+
               ",\"personality\":\""+Escape(personalityId)+"\",\"personality_seed\":"+personalitySeed+
               ",\"active_traits\":\""+Escape(activeTraits)+"\",\"items\":\""+Escape(itemIds)+
               "\",\"max_hp\":"+F(maxHp)+",\"damage\":"+F(damage)+
               ",\"attack_interval\":"+F(attackInterval)+",\"defense\":"+F(defense)+
               ",\"resistance\":"+F(resistance)+",\"attack_range\":"+attackRange+
               ",\"anchor_x\":"+anchorX+",\"anchor_y\":"+anchorY+",\"footprint_w\":"+footprintWidth+
               ",\"footprint_h\":"+footprintHeight+",\"in_storage\":"+(inStorage?"true":"false")+"}");
    }

    public void RecordEpisodeEnd(float time,string winner,int playerSurvivors,int enemySurvivors)
    {
        Append("{\"schema\":"+SchemaVersion+",\"event\":\"episode_end\",\"session\":\""+sessionId+"\",\"time\":"+F(time)+
               ",\"winner\":\""+Escape(winner)+"\",\"player_survivors\":"+playerSurvivors+",\"enemy_survivors\":"+enemySurvivors+"}");
        Flush();
    }

    public void RecordUnitResult(int unitId,string team,string skillId,bool survived,bool won,float damageDealt,float damageTaken,
        float skillDamage,float preventedDamage,int skillUses,int skillHits,int kills,int targetChanges,
        string activeTraits,int traitActivations)
    {
        float reward=(won?1f:-1f)+(survived?0.2f:0f)+damageDealt*0.01f-damageTaken*0.002f;
        Append("{\"schema\":"+SchemaVersion+",\"event\":\"unit_result\",\"session\":\""+sessionId+"\",\"unit\":"+unitId+
               ",\"team\":\""+Escape(team)+"\",\"skillId\":\""+Escape(skillId)+"\",\"survived\":"+(survived?"true":"false")+
               ",\"won\":"+(won?"true":"false")+",\"damage_dealt\":"+F(damageDealt)+",\"damage_taken\":"+F(damageTaken)+
               ",\"skill_damage\":"+F(skillDamage)+",\"prevented_damage\":"+F(preventedDamage)+",\"skill_uses\":"+skillUses+
               ",\"skill_hits\":"+skillHits+",\"kills\":"+kills+",\"target_changes\":"+targetChanges+
               ",\"active_traits\":\""+Escape(activeTraits)+"\",\"trait_activations\":"+traitActivations+
               ",\"reward\":"+F(reward)+"}");
    }

    private void Append(string line)
    {
        if(disposed)return;
        buffer.AppendLine(line);bufferedLines++;
        if(bufferedLines>=FlushLineCount)Flush();
    }

    public void Flush()
    {
        if(disposed||buffer.Length==0)return;
        File.AppendAllText(FilePath,buffer.ToString(),Encoding.UTF8);
        buffer.Clear();bufferedLines=0;
    }

    public void Dispose()
    {
        if(disposed)return;
        Flush();disposed=true;
    }

    private static string F(float value)
    {
        if(float.IsNaN(value)||float.IsInfinity(value))value=0f;
        return value.ToString("0.#####",CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return string.IsNullOrEmpty(value)?string.Empty:value.Replace("\\","\\\\").Replace("\"","\\\"");
    }
}
