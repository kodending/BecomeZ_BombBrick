#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class MonsterInstanceGeneratorTestWindow : EditorWindow
{
    private const string DatabasePath="Assets/Resources/MonsterData/MonsterDatabase.asset";
    private MonsterDatabase database;
    private int samplesPerSpecies=1000;
    private Vector2 scroll;
    private string report="검증을 실행하면 결과가 여기에 표시됩니다.";

    [MenuItem("Tools/Monster Auto Battle/개체 생성 검증")]
    private static void Open()
    {
        GetWindow<MonsterInstanceGeneratorTestWindow>("개체 생성 검증");
    }

    private void OnEnable()
    {
        database=AssetDatabase.LoadAssetAtPath<MonsterDatabase>(DatabasePath);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("몬스터 개체 생성 파이프라인",EditorStyles.boldLabel);
        database=(MonsterDatabase)EditorGUILayout.ObjectField("Monster Database",database,typeof(MonsterDatabase),false);
        samplesPerSpecies=EditorGUILayout.IntSlider("종별 생성 수",samplesPerSpecies,10,10000);

        using(new EditorGUI.DisabledScope(database==null))
        {
            if(GUILayout.Button("활성 종 전체 검증",GUILayout.Height(30)))
                report=RunValidation();
        }

        EditorGUILayout.HelpBox(
            "영구 개체의 성격 축과 독립 능력치 편차 범위를 검증합니다. 실제 프로필은 변경하지 않습니다.",
            MessageType.Info);
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report,GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private string RunValidation()
    {
        if(database==null)return "MonsterDatabase를 찾지 못했습니다.";
        List<MonsterDefinition> definitions=database.monsters
            .Where(item=>item!=null&&item.enabled)
            .OrderBy(item=>item.monsterId,StringComparer.Ordinal)
            .ToList();
        if(definitions.Count==0)return "활성화된 몬스터가 없습니다.";

        int generated=0;
        int errors=0;
        Dictionary<string,int> personalityCounts=new Dictionary<string,int>(StringComparer.Ordinal);
        StringBuilder details=new StringBuilder();

        foreach(MonsterDefinition definition in definitions)
        {
            for(int i=0;i<samplesPerSpecies;i++)
            {
                OwnedMonsterSave monster=new OwnedMonsterSave
                {
                    instanceId="validation_"+definition.monsterId+"_"+i,
                    monsterId=definition.monsterId
                };
                MonsterInstanceGenerator.InitializeNewInstance(monster,definition);
                generated++;

                if(!MonsterInstanceGenerator.AxesAreValid(monster)||
                   !MonsterInstanceGenerator.MultipliersAreValid(monster)||
                   monster.intrinsicTraitId!=definition.intrinsicTraitId||
                   string.IsNullOrWhiteSpace(monster.personalityId))
                {
                    errors++;
                    if(errors<=20)details.AppendLine("ERROR "+definition.monsterId+" / #"+i);
                }

                Increment(personalityCounts,monster.personalityId);
            }
        }

        StringBuilder result=new StringBuilder();
        result.AppendLine("개체 생성 검증 완료");
        result.AppendLine("종: "+definitions.Count);
        result.AppendLine("생성 개체: "+generated.ToString("N0"));
        result.AppendLine("오류: "+errors.ToString("N0"));
        result.AppendLine();
        result.AppendLine("[성격 분포]");
        foreach(KeyValuePair<string,int> pair in personalityCounts.OrderByDescending(item=>item.Value))
            result.AppendLine(MonsterInstanceGenerator.PersonalityDisplayName(pair.Key)+" ("+pair.Key+"): "+
                              pair.Value.ToString("N0")+" / "+(pair.Value*100f/generated).ToString("0.0")+"%");

        if(details.Length>0)
        {
            result.AppendLine();
            result.Append(details);
        }

        Debug.Log("Monster instance generation validation: "+generated+" generated, "+errors+" errors.");
        return result.ToString();
    }

    private static void Increment(Dictionary<string,int> counts,string key)
    {
        if(string.IsNullOrWhiteSpace(key))key="<empty>";
        counts.TryGetValue(key,out int value);
        counts[key]=value+1;
    }
}
#endif
