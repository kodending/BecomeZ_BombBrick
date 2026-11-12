using System.Collections.Generic;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private const float AllyPreparationSpeed=.08f;
    private static MonsterAutoBattlePrototype allyPreparationOwner;
    private readonly List<LocalSkillPresentation> allyPreparationSequenceMembers=new List<LocalSkillPresentation>();
    private bool allyPreparationSequenceActive,releasingAllyPreparationSequence;
    private double allyPreparationSequenceEndsAt;
    private float allyPreparationSavedSpeed,allyPreparationAppliedSpeed;
    private SpriteRenderer allyPreparationShade;

    public static void PrepareForAllyPreparationGameplayPause()
    {
        if(allyPreparationOwner!=null)allyPreparationOwner.EndAllyPreparationSequence();
    }

    private void JoinAllyPreparationSequence(LocalSkillPresentation presentation)
    {
        if(presentation.unit.team!=Team.Player||presentation.allyPreparationMember)return;
        if(!allyPreparationSequenceActive)
        {
            if(allyPreparationOwner!=null&&allyPreparationOwner!=this)
                allyPreparationOwner.EndAllyPreparationSequence();
            allyPreparationSavedSpeed=Time.timeScale;
            allyPreparationAppliedSpeed=allyPreparationSavedSpeed*AllyPreparationSpeed;
            if(CombatReadabilityPresentation.Active)
                allyPreparationAppliedSpeed=allyPreparationSavedSpeed*CombatReadabilityPresentation.PreparationSpeed;
            allyPreparationSequenceActive=true;allyPreparationOwner=this;
            allyPreparationSequenceEndsAt=presentation.startedAt+SkillPreparationDuration(presentation.unit);
        }
        allyPreparationSequenceEndsAt=System.Math.Max(allyPreparationSequenceEndsAt,
            presentation.startedAt+SkillPreparationDuration(presentation.unit));
        presentation.allyPreparationMember=true;
        allyPreparationSequenceMembers.Add(presentation);
        if(!releasingAllyPreparationSequence)ApplyAllyPreparationSequence();
    }

    private void UpdateAllyPreparationSequence(double now)
    {
        if(!allyPreparationSequenceActive)return;
        if(!battleStarted||battleEnding||paused||GameFrontend.BlocksGameplay||
           !Mathf.Approximately(Time.timeScale,allyPreparationAppliedSpeed))
        {EndAllyPreparationSequence();return;}
        bool removed=false;
        for(int i=allyPreparationSequenceMembers.Count-1;i>=0;i--)
        {
            LocalSkillPresentation presentation=allyPreparationSequenceMembers[i];
            if(presentation.active&&presentation.pending!=null&&
               CanContinueSkillFocus(presentation.pending,false)&&PreparedSkillReleaseReady(presentation.pending))continue;
            presentation.allyPreparationMember=false;
            if(presentation.unit!=null&&presentation.unit.spriteAnimation!=null)
                presentation.unit.spriteAnimation.SetSkillAnticipating(false);
            allyPreparationSequenceMembers.RemoveAt(i);
            removed=true;
        }
        if(allyPreparationSequenceMembers.Count==0)
        {EndAllyPreparationSequence();return;}
        if(removed)
        {
            allyPreparationSequenceEndsAt=0d;
            for(int i=0;i<allyPreparationSequenceMembers.Count;i++)
                allyPreparationSequenceEndsAt=System.Math.Max(allyPreparationSequenceEndsAt,
                    allyPreparationSequenceMembers[i].startedAt+SkillPreparationDuration(allyPreparationSequenceMembers[i].unit));
        }
        if(now>=allyPreparationSequenceEndsAt)
        {
            EndAllyPreparationSequence();
            // A combined cast may enqueue another preparation while this batch releases.
            releasingAllyPreparationSequence=true;
            return;
        }
        ApplyAllyPreparationSequence();
    }

    private void FinishAllyPreparationUpdate()
    {
        releasingAllyPreparationSequence=false;
        if(allyPreparationSequenceActive)ApplyAllyPreparationSequence();
    }

    private void ApplyAllyPreparationSequence()
    {
        Time.timeScale=allyPreparationAppliedSpeed;
        float shadeAlpha=.5f;
        if(CombatReadabilityPresentation.Active)shadeAlpha=CombatReadabilityPresentation.ShadeAlpha;
        if(shadeAlpha<=0f)
        {
            if(allyPreparationShade!=null)allyPreparationShade.gameObject.SetActive(false);
            RefreshAllyPreparationEmphasis();
            return;
        }
        if(allyPreparationShade==null)
        {
            GameObject shadeObject=CreateCaptureFocusShade(null);
            if(shadeObject!=null)
            {
                shadeObject.name="Skill Focus Shade";
                allyPreparationShade=shadeObject.GetComponent<SpriteRenderer>();
            }
        }
        if(allyPreparationShade!=null)
        {
            allyPreparationShade.gameObject.SetActive(true);
            allyPreparationShade.color=new Color(0f,0f,0f,shadeAlpha);
            Camera camera=mainCamera==null?Camera.main:mainCamera;
            if(camera!=null)
            {
                float distance=Mathf.Max(camera.nearClipPlane+.1f,1f);
                float height=camera.orthographic?camera.orthographicSize*2f:
                    2f*distance*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
                Transform shade=allyPreparationShade.transform;
                shade.SetPositionAndRotation(camera.transform.position+camera.transform.forward*distance,camera.transform.rotation);
                shade.localScale=new Vector3(height*camera.aspect*1.1f,height*1.1f,1f);
            }
            for(int i=0;i<allyPreparationSequenceMembers.Count;i++)
                if(allyPreparationSequenceMembers[i].renderer!=null)
                {allyPreparationShade.sortingLayerID=allyPreparationSequenceMembers[i].renderer.sortingLayerID;break;}
        }
        RefreshAllyPreparationEmphasis();
    }

    private void RefreshAllyPreparationEmphasis()
    {
        for(int i=0;i<localSkillPresentations.Count;i++)
        {
            LocalSkillPresentation presentation=localSkillPresentations[i];
            if(!presentation.active)continue;
            bool spotlight=!allyPreparationSequenceActive||presentation.allyPreparationMember;
            presentation.unit?.spriteAnimation?.SetSkillFocus(LocalSkillBodyEmphasis(presentation));
            if(presentation.mirror!=null)presentation.mirror.SetSpotlight(spotlight);
            if(presentation.unit!=null&&presentation.unit.raptorApexWings!=null)
                presentation.unit.raptorApexWings.SetForeground(spotlight);
            if(presentation.unit?.outline!=null)
                presentation.unit.outline.SetOutlined(spotlight,true,
                    CombatFlashPresentation.Peak(new Color(1f,.86f,.32f,1f)));
        }
    }

    private void LeaveAllyPreparationSequence(LocalSkillPresentation presentation)
    {
        if(!presentation.allyPreparationMember)return;
        presentation.allyPreparationMember=false;
        allyPreparationSequenceMembers.Remove(presentation);
        if(allyPreparationSequenceMembers.Count==0)EndAllyPreparationSequence();
    }

    private void EndAllyPreparationSequence()
    {
        if(allyPreparationSequenceActive&&Mathf.Approximately(Time.timeScale,allyPreparationAppliedSpeed))
            Time.timeScale=allyPreparationSavedSpeed;
        allyPreparationSequenceActive=false;
        if(allyPreparationOwner==this)allyPreparationOwner=null;
        for(int i=0;i<allyPreparationSequenceMembers.Count;i++)
            allyPreparationSequenceMembers[i].allyPreparationMember=false;
        allyPreparationSequenceMembers.Clear();
        if(allyPreparationShade!=null)allyPreparationShade.gameObject.SetActive(false);
        RefreshAllyPreparationEmphasis();
    }
}
