#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class MonsterCombatAuditEntry
{
    public string serial;
    public string monsterId;
    public string displayName;
    public string familyId;
    public string basicAttackId;
    public string skillId;
    public string skillBehavior;
    public string intrinsicTraitId;
    public bool basicAttackReady;
    public bool skillMasterReady;
    public bool skillRuntimeReady;
    public bool intrinsicMasterReady;
    public bool intrinsicRuntimeReady;
    public bool animationReady;

    public bool Ready=>basicAttackReady&&skillMasterReady&&skillRuntimeReady&&
        intrinsicMasterReady&&intrinsicRuntimeReady&&animationReady;

    public string Missing()
    {
        List<string> missing=new List<string>();
        if(!basicAttackReady)missing.Add("basic attack");
        if(!skillMasterReady)missing.Add("skill master");
        else if(!skillRuntimeReady)missing.Add("skill runtime");
        if(!intrinsicMasterReady)missing.Add("trait master");
        else if(!intrinsicRuntimeReady)missing.Add("trait runtime");
        if(!animationReady)missing.Add("animation");
        return string.Join(", ",missing);
    }
}

public static class MonsterCombatImplementationAudit
{
    public const string ReportPath=
        "Assets/MonsterData/Generated/MonsterCombatImplementationAudit.md";
    private const string PrototypePath="Assets/Prototype";
    private const string BattleSourcePath=
        "Assets/Prototype/MonsterAutoBattlePrototype.cs";

    [MenuItem("Tools/Game Database/Audit Monster Combat 001-080")]
    public static void GenerateReport()
    {
        List<MonsterCombatAuditEntry> entries=Analyze();
        File.WriteAllText(ReportPath,BuildMarkdown(entries),new UTF8Encoding(false));
        AssetDatabase.ImportAsset(ReportPath,ImportAssetOptions.ForceUpdate);
        Debug.Log("[Monster Combat Audit] "+entries.Count+" monsters / "+
            entries.Count(entry=>entry.Ready)+" ready / report: "+ReportPath);
    }

    public static List<MonsterCombatAuditEntry> Analyze()
    {
        MonsterDatabase monsters=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        GameMasterDatabase master=GameMasterData.Database;
        if(monsters==null||master==null)
            throw new InvalidOperationException("Monster or combat master database is missing.");

        string battleSource=File.ReadAllText(BattleSourcePath);
        HashSet<string> activeBehaviors=new HashSet<string>(
            Regex.Matches(
                    battleSource,
                    "skillExecutor\\.Register\\(\"([^\"]+)\",MonsterSkillTrigger\\.Active")
                .Cast<Match>().Select(match=>match.Groups[1].Value),
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string,bool> intrinsicRuntime=FindIntrinsicRuntimeUsage();

        return monsters.monsters
            .Where(monster=>monster!=null&&monster.enabled&&
                int.TryParse(monster.serialNumber,out int serial)&&
                serial>=1&&serial<=80)
            .OrderBy(monster=>int.Parse(monster.serialNumber))
            .Select(monster=>
            {
                SkillMasterRow skill=master.skills.FirstOrDefault(row=>
                    row!=null&&row.enabled&&row.id==monster.skillId);
                bool traitMaster=master.traits.Any(row=>row!=null&&row.enabled&&
                    row.id==monster.intrinsicTraitId);
                GameObject prefab=Resources.Load<GameObject>(monster.resourcePath);
                SpriteMonsterAnimationDriver animation=prefab==null
                    ?null:prefab.GetComponentInChildren<
                        SpriteMonsterAnimationDriver>(true);
                return new MonsterCombatAuditEntry
                {
                    serial=monster.serialNumber,
                    monsterId=monster.monsterId,
                    displayName=monster.displayName,
                    familyId=monster.familyId,
                    basicAttackId=monster.basicAttackId,
                    skillId=monster.skillId,
                    skillBehavior=skill==null?"":skill.behavior,
                    intrinsicTraitId=monster.intrinsicTraitId,
                    basicAttackReady=!string.IsNullOrWhiteSpace(
                        monster.basicAttackId),
                    skillMasterReady=skill!=null,
                    skillRuntimeReady=skill!=null&&
                        activeBehaviors.Contains(skill.behavior),
                    intrinsicMasterReady=traitMaster,
                    intrinsicRuntimeReady=intrinsicRuntime.TryGetValue(
                        monster.intrinsicTraitId,out bool implemented)&&implemented,
                    animationReady=HasAllFrames(animation)
                };
            }).ToList();
    }

    private static Dictionary<string,bool> FindIntrinsicRuntimeUsage()
    {
        string[] paths=Directory.GetFiles(
            PrototypePath,"*.cs",SearchOption.AllDirectories);
        Dictionary<string,string> sources=paths.ToDictionary(
            path=>path,File.ReadAllText,StringComparer.OrdinalIgnoreCase);
        Dictionary<string,bool> result=new Dictionary<string,bool>(
            StringComparer.Ordinal);
        Regex constant=new Regex(
            "const\\s+string\\s+(\\w+)\\s*=\\s*\"(intrinsic_[^\"]+)\"");
        foreach(KeyValuePair<string,string> sourceFile in sources)
        {
            foreach(Match match in constant.Matches(sourceFile.Value))
            {
                string symbol=match.Groups[1].Value;
                string id=match.Groups[2].Value;
                bool used=sources.Any(other=>other.Key!=sourceFile.Key&&
                    Regex.IsMatch(other.Value,
                        "\\b"+Regex.Escape(symbol)+"\\b"));
                result[id]=used;
            }
        }
        return result;
    }

    private static bool HasAllFrames(SpriteMonsterAnimationDriver animation)
    {
        return animation!=null&&animation.IdleFrameCount>0&&
            animation.RunFrameCount>0&&animation.AttackFrameCount>0&&
            animation.SkillAttackFrameCount>0&&animation.DeathFrameCount>0&&
            animation.CaptureFrameCount>0&&animation.StruggleFrameCount>0;
    }

    private static string BuildMarkdown(
        IReadOnlyCollection<MonsterCombatAuditEntry> entries)
    {
        StringBuilder text=new StringBuilder();
        text.AppendLine("# Monster Combat Implementation Audit 001-080");
        text.AppendLine();
        text.AppendLine("- Total: "+entries.Count);
        text.AppendLine("- Ready: "+entries.Count(entry=>entry.Ready));
        text.AppendLine("- Missing skill master: "+
            entries.Count(entry=>!entry.skillMasterReady));
        text.AppendLine("- Missing skill runtime: "+
            entries.Count(entry=>entry.skillMasterReady&&!entry.skillRuntimeReady));
        text.AppendLine("- Missing intrinsic master: "+
            entries.Count(entry=>!entry.intrinsicMasterReady));
        text.AppendLine("- Missing intrinsic runtime: "+
            entries.Count(entry=>entry.intrinsicMasterReady&&
                !entry.intrinsicRuntimeReady));
        text.AppendLine("- Missing animation: "+
            entries.Count(entry=>!entry.animationReady));
        text.AppendLine();
        text.AppendLine("| No. | Monster | Family | Basic | Skill | Trait | Animation | Blocker |");
        text.AppendLine("|---:|---|---|---|---|---|---|---|");
        foreach(MonsterCombatAuditEntry entry in entries)
            text.AppendLine("| "+entry.serial+" | "+entry.displayName+" | "+
                entry.familyId+" | "+Mark(entry.basicAttackReady)+" | "+
                Mark(entry.skillMasterReady&&entry.skillRuntimeReady)+" | "+
                Mark(entry.intrinsicMasterReady&&entry.intrinsicRuntimeReady)+" | "+
                Mark(entry.animationReady)+" | "+entry.Missing()+" |");
        return text.ToString();
    }

    private static string Mark(bool ready){return ready?"READY":"MISSING";}
}
#endif
