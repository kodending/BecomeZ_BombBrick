using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private const float DeploymentExpansionFeedbackDuration=1.8f;
    private int deploymentExpansionFrom,deploymentExpansionTo;
    private bool deploymentExpansionFeedbackPending;
    private float deploymentExpansionFeedbackStartedAt=-1f;

    private void NotifyDeploymentLimitIncrease(int previousLimit)
    {
        int current=MaxDeploymentCount;
        if(mlEpisodeActive||expeditionRun==null||current<=previousLimit)return;
        bool continuing=deploymentExpansionFeedbackPending||deploymentExpansionFeedbackStartedAt>=0f&&
            Time.unscaledTime-deploymentExpansionFeedbackStartedAt<DeploymentExpansionFeedbackDuration;
        deploymentExpansionFrom=continuing?Mathf.Min(deploymentExpansionFrom,previousLimit):previousLimit;
        deploymentExpansionTo=current;
        deploymentExpansionFeedbackPending=true;deploymentExpansionFeedbackStartedAt=-1f;
    }

    private void UpdateDeploymentExpansionFeedback()
    {
        if(deploymentExpansionFeedbackPending&&TutorialPreparationVisible&&
           !wavePreparationAcrossFrames&&!capturePresentationResolving)
        {
            deploymentExpansionFeedbackPending=false;
            deploymentExpansionFeedbackStartedAt=Time.unscaledTime;
            GameAudio.Play(GameAudio.Cue.Reward);
        }
        if(deploymentExpansionFeedbackStartedAt>=0f&&
           Time.unscaledTime-deploymentExpansionFeedbackStartedAt>=DeploymentExpansionFeedbackDuration)
            deploymentExpansionFeedbackStartedAt=-1f;
    }

    private void DrawDeploymentExpansionFeedback()
    {
        if(deploymentExpansionFeedbackStartedAt<0f||!TutorialPreparationVisible)return;
        float age=Time.unscaledTime-deploymentExpansionFeedbackStartedAt;
        if(age<0f||age>=DeploymentExpansionFeedbackDuration)return;
        float alpha=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1.25f,DeploymentExpansionFeedbackDuration,age));
        float scale=CombatFlashPresentation.Reduced?1f:Mathf.Lerp(.92f,1f,
            BombBrickUiTheme.EaseOutBack(Mathf.Clamp01(age/.22f),.7f));
        Rect safe=UiLayout.safeArea,top=UiLayout.topBar;
        Rect panel=BombBrickUiLayout.ClampRect(new Rect(safe.center.x-230f,top.yMax+12f,460f,112f),safe,8f);
        Matrix4x4 oldMatrix=GUI.matrix;Color oldColor=GUI.color;
        GUIUtility.ScaleAroundPivot(Vector2.one*scale,panel.center);
        GUI.color=new Color(1f,1f,1f,alpha);
        BombBrickUiTheme.DrawPanel(panel,true);
        BombBrickUiTheme.Frame(panel,BombBrickUiTheme.Lime,2f);
        DrawSystemIcon(new Rect(panel.x+16f,panel.y+23f,36f,36f),"deployment",BombBrickUiTheme.Lime,false);
        GUI.Label(new Rect(panel.x+64f,panel.y+10f,panel.width-80f,25f),
            GameLocalization.TextByKey("battle.deployment_expanded.title","배치 한도 증가!"),
            BombBrickUiTheme.TitleStyle(20));
        GUI.Label(new Rect(panel.x+64f,panel.y+37f,panel.width-80f,31f),
            GameLocalization.Format("battle.deployment_expanded.change","{0} → {1}",deploymentExpansionFrom,deploymentExpansionTo),
            BombBrickUiTheme.TitleStyle(26));
        GUI.Label(new Rect(panel.x+16f,panel.y+80f,panel.width-32f,20f),
            GameLocalization.Format("battle.deployment_expanded.extra","동료 {0}마리를 더 배치할 수 있습니다.",
                deploymentExpansionTo-deploymentExpansionFrom),BombBrickUiTheme.BodyStyle(12,TextAnchor.MiddleCenter));
        GUI.color=oldColor;GUI.matrix=oldMatrix;
    }
}
