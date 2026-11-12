#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class CombatEffectTestSceneController
{
    private bool readabilityAwaitingFormation,readabilityAutoStart,readabilityFreshLayout;
    private Dictionary<string,Vector2Int> readabilityFormation;
    private int readabilityScenario=-1;
    private static readonly string[] ReadabilityNames={"랩터 1:1","원숭이 1:1","버섯 1:1","혼합 4:8","혼합 8:8","집중 4:1"};

    public static string[] ReadabilityScenarioRoster(int scenario,bool ally)
    {
        scenario=Mathf.Clamp(scenario,0,5);
        if(scenario<3)return new[]{ally?new[]{"velos","chiwoo","kingmore"}[scenario]:"ironkuma"};
        if(scenario==5)return ally?new[]{"velos","geomchi","nukgu","woongi"}:new[]{"ironkuma"};
        if(!ally)return new[]{"ironkuma","geomchi","nukgu","ager","jangpung","aranea","kongjwi","kogason"};
        return scenario==3?new[]{"velos","chiwoo","kingmore","kongjwi"}:
            new[]{"velos","chiwoo","kingmore","kongjwi","woongi","aranea","frovire","kogason"};
    }

    public void PrepareReadabilityScenario(int scenario)
    {
        CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        CombatEffectTestConfiguration.ReadabilitySpeciesTraits=true;
        readabilityScenario=Mathf.Clamp(scenario,0,5);
        readabilityFormation=null;readabilityFreshLayout=true;
        allyRoster.Clear();allyRoster.AddRange(ReadabilityScenarioRoster(readabilityScenario,true));
        enemyRoster.Clear();enemyRoster.AddRange(ReadabilityScenarioRoster(readabilityScenario,false));
        allyCount=allyRoster.Count;enemyCount=enemyRoster.Count;
        allyRank=ExpeditionMonsterRank.Rank3;
        enemyRank=readabilityScenario==5?ExpeditionMonsterRank.Rank3:ExpeditionMonsterRank.Rank2;
        mixedRank4v8=readabilityScenario==3;
        allyTranscended=enemyTranscended=false;
        allyRank2TraitId=allyRank3TraitId=enemyRank2TraitId=enemyRank3TraitId="";
        ResolveSelection(allyRoster[0],0,out allyIndex,out allyMemberIndex);
        ResolveSelection(enemyRoster[0],0,out enemyIndex,out enemyMemberIndex);
        RestartReadabilityComparison(false);
    }

    public void RestartReadabilityComparison(bool startBattle)
    {
        if(!CombatReadabilityPreview.Active)return;
        if(!readabilityFreshLayout)RememberReadabilityFormation();
        readabilityFreshLayout=false;
        readabilityAwaitingFormation=true;readabilityAutoStart=startBattle;
        Time.timeScale=1f;
        PrepareDeployment();
    }

    private void RememberReadabilityFormation()
    {
        if(activeBattle==null)return;
        var formation=activeBattle.CaptureReadabilityFormation();
        if(formation!=null)readabilityFormation=formation;
    }

    private void LateUpdate()
    {
        if(!CombatReadabilityPreview.Active||activeBattle==null||!activeBattle.IsSceneReady)return;
        if(readabilityAwaitingFormation)
        {
            if(readabilityFormation!=null&&!activeBattle.RestoreReadabilityFormation(readabilityFormation))
            {
                readabilityFormation=null;
                Debug.LogWarning("[전투 점검] 기존 배치를 복원할 수 없어 현재 배치로 초기화했습니다.",this);
            }
            readabilityAwaitingFormation=false;
            RememberReadabilityFormation();
        }
        if(readabilityAutoStart&&activeBattle.ReadabilityReadyToStart&&activeBattle.StartPreparedCombatEffectTestBattle())
        {readabilityAutoStart=false;sidebarCollapsed=true;return;}
        if(!activeBattle.CombatEffectTestBattleRunning)RememberReadabilityFormation();
    }

    private void ResetReadabilityScenario()
    {
        CombatReadabilityPreview.Enabled=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        CombatEffectTestConfiguration.ReadabilitySpeciesTraits=false;
        readabilityFormation=null;readabilityAwaitingFormation=readabilityAutoStart=false;
        readabilityScenario=-1;
    }

    public static Rect ReadabilityPanelRect(float width,float height,bool running,bool expanded)
    {
        return running&&!expanded?TestSidebarRect(width,height,true):TestSidebarContentRect(width,height);
    }

    private void DrawReadabilityChecks(Rect area)
    {
        bool running=activeBattle!=null&&activeBattle.CombatEffectTestBattleRunning;
        bool wasEnabled=GUI.enabled;Matrix4x4 matrix=GUI.matrix;
        GUIStyle previous=GUI.skin.label;
        GUI.skin.label=BombBrickUiTheme.BodyStyle(10,TextAnchor.MiddleLeft);
        try
        {
            float x=area.x,y=area.y,w=area.width;
            bool narrow=w<200f;
            GUI.Label(new Rect(x,y,w,24f),"기본 전투 · 국소 강조");
            bool basic=GUI.Toggle(new Rect(x,y+30f,w*.5f,22f),CombatReadabilityPreview.BasicOnly,"평타만");
            bool contact=GUI.Toggle(new Rect(x+w*.5f,y+30f,w*.5f,22f),CombatReadabilityPreview.ContactFeedback,narrow?"피격":"피격 반응");
            bool skillAudio=GUI.Toggle(new Rect(x,y+54f,w,22f),CombatReadabilityPreview.SkillAudio,"스킬 효과음");
            if(basic!=CombatReadabilityPreview.BasicOnly||contact!=CombatReadabilityPreview.ContactFeedback||skillAudio!=CombatReadabilityPreview.SkillAudio)
            {
                CombatReadabilityPreview.Enabled=true;
                CombatReadabilityPreview.BasicOnly=basic;CombatReadabilityPreview.ContactFeedback=contact;CombatReadabilityPreview.SkillAudio=skillAudio;
                RestartReadabilityComparison(running);
            }
            for(int i=0;i<ReadabilityNames.Length;i++)
                if(PixelButton(new Rect(x+i%2*(w+4f)*.5f,y+80f+i/2*25f,(w-4f)*.5f,22f),
                    narrow?(i<3?ReadabilityNames[i].Replace(" 1:1",""):ReadabilityNames[i].Replace(" ","")):ReadabilityNames[i]))
                    PrepareReadabilityScenario(i);
            GUI.Label(new Rect(x,y+154f,w,20f),"시드 "+CombatReadabilityPreview.Seed);
            string scenario=readabilityScenario<0?(narrow?"수동":"수동 편성"):ReadabilityNames[readabilityScenario];
            if(PixelButton(new Rect(x,y+178f,w,22f),"새 배치 · "+scenario))
            {CombatReadabilityPreview.Enabled=true;CombatReadabilityPreview.Seed=CombatReadabilityPreview.Seed==int.MaxValue?1:CombatReadabilityPreview.Seed+1;readabilityFormation=null;readabilityFreshLayout=true;RestartReadabilityComparison(false);}
            bool enabled=GUI.enabled;GUI.enabled=enabled&&CombatReadabilityPreview.Active;
            if(PixelButton(new Rect(x,y+204f,w,24f),narrow?"동일 조건 재전투":"동일 조건으로 재전투"))RestartReadabilityComparison(true);
            GUI.enabled=enabled;
            GUI.Label(new Rect(x,y+230f,w,18f),narrow?"시작 조건 고정":"동일 시작조건 · 결과는 달라짐");
        }
        finally{GUI.enabled=wasEnabled;GUI.matrix=matrix;GUI.skin.label=previous;}
    }
}
#endif
