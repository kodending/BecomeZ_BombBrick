using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class SkillFocusCastRuntimeQa
{
    public static void Attach(string label)
    {
        SkillFocusRuntimeQa.Guard();
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(string.IsNullOrWhiteSpace(label))throw new ArgumentException("A QA label is required.");
        foreach(char value in label)
            if(!char.IsLetterOrDigit(value)&&value!='_'&&value!='-')throw new ArgumentException("Invalid QA label.");
        var primary=UnityEngine.Object.FindObjectOfType<SkillFocusRuntimeQaObserver>();
        if(primary==null)throw new InvalidOperationException("Start the primary skill-focus observer before attaching cast diagnostics.");
        if((string)typeof(SkillFocusRuntimeQaObserver).GetField("label",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(primary)!=label)
            throw new InvalidOperationException("The cast diagnostic label must match the primary observer label.");
        if(primary.GetComponent<SkillFocusCastRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("Cast diagnostics are already attached to this observer.");
        string output=Path.GetFullPath(Path.Combine("Logs/SkillFocusCastQA/20261019",label));
        if(Directory.Exists(output))throw new IOException("Cast QA output already exists.");
        primary.gameObject.AddComponent<SkillFocusCastRuntimeQaObserver>().Begin(primary,label,output);
    }
}

// Reflection and CSV construction add diagnostic overhead. This observer is not an FPS benchmark.
[DefaultExecutionOrder(10040)]
public sealed class SkillFocusCastRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private readonly StringBuilder csv=new StringBuilder("frame,realtime,unscaledTime,primaryElapsed,battleElapsed,timeScale,sourceId,commonSourceId,commonUnitPresent,pending,committedAt,recoveryAt,focusStartedAt,focusElapsed,directorFocus,visualStrength,directorStrength,driverScale,driverSkillAction,commonSkillAction,driverMotion,driverFrame,casterSkillUses,casterCooldown,casterSkillTimer,shadeAlpha,shadeVisible,mirrorEnabled,gatherCachedCount,gatherEnabled,gatherElapsed,gather1Enabled,gather1Scale,gather1Alpha,gather2Enabled,gather2Scale,gather2Alpha,gather3Enabled,gather3Scale,gather3Alpha,cameraFov,homeFov,skillCameraBaseFov,skillCameraActive,skillCameraWeight,skillCameraOffsetX,skillCameraOffsetY,lensShiftX,lensShiftY,projectionM02,projectionM12,projectionM03,projectionM13,normalSpeedFullSizeSkill,afterCommonClear,casterScaleRestored,cameraRestored,primaryFinished\n");
    private readonly List<CastEvent> events=new List<CastEvent>();
    private SkillFocusRuntimeQaObserver primary;
    private MonsterAutoBattlePrototype battle;
    private CombatCameraDirector director;
    private object previousCommon,lastCaster;
    private CastEvent current;
    private string label,output;
    private bool begun;
    private double startedAt;
    private int samples,pendingSamples,committedVisualSamples,normalSpeedFullSizeSkillSamples;
    private int afterCommonClearSamples,restoredAfterCommonClearSamples,maximumGatherCachedCount,maximumGatherVisible;
    private float maximumDriverScale,maximumCameraZoom,maximumProjectionOffset;

    public bool Finished { get; private set; }

    [Serializable] private sealed class CastEvent
    {
        public int sourceId;
        public double focusStartedAt,firstObservedAt,committedAt=-1d,commonEndedAt=-1d;
        public float firstCommitElapsed=-1f,endedDriverScale=-1f,endedCameraFov=-1f;
        public int pendingSamples,committedVisualSamples,normalSpeedFullSizeSkillSamples,maximumGatherVisible;
    }

    [Serializable] private sealed class Report
    {
        public string label,completion,primaryResult;
        public string limitation="Read-only companion to the primary natural production battle observer. Reflection and CSV construction add diagnostic overhead; this is not a performance or FPS benchmark. No screenshot, skill, camera, clock or game configuration is changed by this observer.";
        public string frameMatching="Join samples.frame to the primary samples.frame; unscaledTime is absolute Time.unscaledTimeAsDouble and primaryElapsed uses the primary observer's battle clock origin. Native screenshots and frame-times.csv remain in the primary observer output.";
        public int samples,pendingSamples,committedVisualSamples,normalSpeedFullSizeSkillSamples,afterCommonClearSamples,restoredAfterCommonClearSamples,maximumGatherCachedCount,maximumGatherVisible;
        public float maximumDriverScale,maximumCameraZoom,maximumProjectionOffset;
        public double elapsed;
        public bool primaryFinished;
        public CastEvent[] casts;
    }

    public void Begin(SkillFocusRuntimeQaObserver owner,string requestedLabel,string directory)
    {
        SkillFocusRuntimeQa.Guard();primary=owner;label=requestedLabel;output=directory;
        Directory.CreateDirectory(output);startedAt=Time.unscaledTimeAsDouble;begun=true;
    }

    private void LateUpdate()
    {
        if(!begun||Finished)return;
        if(primary==null){Complete("PRIMARY_OBSERVER_MISSING",false);return;}
        bool primaryFinished=Read<bool>(primary,"finished",false);
        battle=Read<MonsterAutoBattlePrototype>(primary,"battle",null);
        director=Read<CombatCameraDirector>(primary,"director",null);
        if(battle!=null&&director!=null&&Read<bool>(primary,"seenBattle",false))Observe(primaryFinished);
        if(primaryFinished)Complete("PRIMARY_FINISHED",true);
    }

    private void Observe(bool primaryFinished)
    {
        double now=Time.unscaledTimeAsDouble;
        object common=Field(battle,"skillFocusUnit");
        Camera camera=Read<Camera>(battle,"mainCamera",null);
        if(common!=previousCommon)
        {
            if(current!=null)
            {
                current.commonEndedAt=now;
                var endedDriver=Unit<SpriteMonsterAnimationDriver>(previousCommon,"spriteAnimation",null);
                current.endedDriverScale=endedDriver==null?1f:Read<float>(endedDriver,"skillFocusScale",1f);
                current.endedCameraFov=camera==null?-1f:camera.fieldOfView;
            }
            current=null;
            if(common!=null)
            {
                current=new CastEvent{sourceId=Unit<int>(common,"id",-1),firstObservedAt=now,
                    focusStartedAt=Read<double>(director,"skillFocusStartedAt",-1d)};
                events.Add(current);
            }
            previousCommon=common;
        }
        if(common!=null)lastCaster=common;
        var driver=Unit<SpriteMonsterAnimationDriver>(lastCaster,"spriteAnimation",null);
        var shade=Read<SpriteRenderer>(battle,"skillFocusShade",null);
        var mirror=Read<SkillFocusSpriteMirror>(battle,"skillFocusForeground",null);
        var foreground=mirror==null?null:mirror.GetComponent<SpriteRenderer>();
        var ghosts=mirror==null?null:Read<SpriteRenderer[]>(mirror,"gatherSprites",null);
        int gatherVisible=0;
        if(ghosts!=null)foreach(var ghost in ghosts)
            if(ghost!=null&&ghost.enabled&&ghost.gameObject.activeInHierarchy)gatherVisible++;
        int cached=ghosts==null?0:ghosts.Length;
        bool pending=Read<bool>(battle,"skillFocusCastPending",false);
        double committed=Read<double>(battle,"skillFocusCommittedAt",-1d);
        double focusStarted=Read<double>(director,"skillFocusStartedAt",-1d);
        float scale=driver==null?1f:Read<float>(driver,"skillFocusScale",1f);
        bool driverSkill=driver!=null&&driver.IsSkillActionActive;
        var commonAction=typeof(MonsterAutoBattlePrototype).GetMethod("CommonSkillActionActive",Static);
        bool commonSkill=common!=null&&commonAction!=null&&(bool)commonAction.Invoke(null,new[]{common});
        float shadeAlpha=shade==null?0f:shade.color.a;
        bool cameraActive=Read<bool>(director,"skillCameraActive",false);
        float homeFov=Read<float>(director,"homeFieldOfView",0f);
        Vector2 offset=Read<Vector2>(director,"skillCameraOffset",Vector2.zero);
        Matrix4x4 projection=camera==null?Matrix4x4.identity:camera.projectionMatrix;
        bool fullSizeSkill=common!=null&&!pending&&committed>=0d&&shadeAlpha<=.001f&&driverSkill&&scale>=1.499f&&Mathf.Abs(Time.timeScale-1f)<.01f;
        bool afterClear=common==null&&lastCaster!=null;
        bool scaleRestored=afterClear&&Mathf.Abs(scale-1f)<.001f;
        bool cameraRestored=afterClear&&camera!=null&&!cameraActive&&Mathf.Abs(camera.fieldOfView-homeFov)<.01f;

        samples++;if(pending)pendingSamples++;
        if(common!=null&&committed>=0d&&scale>=1.499f)committedVisualSamples++;
        if(fullSizeSkill)normalSpeedFullSizeSkillSamples++;
        if(afterClear)afterCommonClearSamples++;
        if(scaleRestored&&cameraRestored)restoredAfterCommonClearSamples++;
        maximumGatherCachedCount=Mathf.Max(maximumGatherCachedCount,cached);
        maximumGatherVisible=Mathf.Max(maximumGatherVisible,gatherVisible);
        maximumDriverScale=Mathf.Max(maximumDriverScale,scale);
        if(camera!=null)maximumCameraZoom=Mathf.Max(maximumCameraZoom,homeFov-camera.fieldOfView);
        maximumProjectionOffset=Mathf.Max(maximumProjectionOffset,Mathf.Abs(projection.m02),Mathf.Abs(projection.m12),Mathf.Abs(projection.m03),Mathf.Abs(projection.m13));
        if(current!=null)
        {
            if(pending)current.pendingSamples++;
            if(committed>=0d&&current.committedAt<0d)
            {current.committedAt=committed;current.firstCommitElapsed=(float)(committed-current.focusStartedAt);}
            if(committed>=0d&&scale>=1.499f)current.committedVisualSamples++;
            if(fullSizeSkill)current.normalSpeedFullSizeSkillSamples++;
            current.maximumGatherVisible=Mathf.Max(current.maximumGatherVisible,gatherVisible);
        }

        Append(Time.frameCount,Time.realtimeSinceStartupAsDouble,now,now-Read<double>(primary,"startedAt",now),
            Read<float>(battle,"battleElapsed",0f),Time.timeScale,Unit<int>(lastCaster,"id",-1),Unit<int>(common,"id",-1),common!=null,pending,
            committed,Read<double>(battle,"skillFocusRecoveryAt",-1d),focusStarted,common==null?-1d:now-focusStarted,director.SkillFocusActive,
            Read<float>(battle,"skillFocusVisualStrength",0f),director.SkillFocusStrength,scale,driverSkill,commonSkill,
            driver==null?"":driver.CurrentMotionName,driver==null?-1:driver.CurrentFrameIndex,Unit<int>(lastCaster,"skillUses",-1),
            Unit<float>(lastCaster,"cooldown",-1f),Unit<float>(lastCaster,"skillTimer",-1f),
            shadeAlpha,shade!=null&&shade.enabled&&shade.gameObject.activeInHierarchy,foreground!=null&&foreground.enabled&&foreground.gameObject.activeInHierarchy,
            cached,gatherVisible,mirror==null?-1d:now-Read<double>(mirror,"gatherStartedAt",now));
        for(int i=0;i<3;i++)
        {
            var ghost=ghosts!=null&&i<ghosts.Length?ghosts[i]:null;
            Append(ghost!=null&&ghost.enabled&&ghost.gameObject.activeInHierarchy,ghost==null?1f:ghost.transform.localScale.x,ghost==null?0f:ghost.color.a);
        }
        Append(camera==null?-1f:camera.fieldOfView,homeFov,Read<float>(director,"skillCameraBaseFieldOfView",-1f),cameraActive,
            Read<float>(director,"skillCameraWeight",0f),offset.x,offset.y,camera==null?0f:camera.lensShift.x,camera==null?0f:camera.lensShift.y,
            projection.m02,projection.m12,projection.m03,projection.m13,fullSizeSkill,afterClear,scaleRestored,cameraRestored,primaryFinished);
        csv.AppendLine();
    }

    private void Append(params object[] values)
    {
        foreach(object value in values)
        {
            if(csv.Length>0&&csv[csv.Length-1]!='\n')csv.Append(',');
            if(value is double clock)csv.Append(clock.ToString("R",CultureInfo.InvariantCulture));
            else if(value is float number)csv.Append(number.ToString("R",CultureInfo.InvariantCulture));
            else if(value is IFormattable formatted)csv.Append(formatted.ToString(null,CultureInfo.InvariantCulture));
            else csv.Append(value);
        }
    }

    private void Complete(string completion,bool primaryFinished)
    {
        if(Finished)return;Finished=true;
        string primaryOutput=primary==null?null:Read<string>(primary,"output",null);
        string primaryResult=primaryOutput==null?null:Path.Combine(primaryOutput,"complete.txt");
        File.WriteAllText(Path.Combine(output,"samples.csv"),csv.ToString());
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonUtility.ToJson(new Report{
            label=label,completion=completion,primaryFinished=primaryFinished,
            primaryResult=primaryResult!=null&&File.Exists(primaryResult)?File.ReadAllText(primaryResult):"",
            samples=samples,pendingSamples=pendingSamples,committedVisualSamples=committedVisualSamples,
            normalSpeedFullSizeSkillSamples=normalSpeedFullSizeSkillSamples,afterCommonClearSamples=afterCommonClearSamples,
            restoredAfterCommonClearSamples=restoredAfterCommonClearSamples,maximumGatherCachedCount=maximumGatherCachedCount,
            maximumGatherVisible=maximumGatherVisible,maximumDriverScale=maximumDriverScale,
            maximumCameraZoom=maximumCameraZoom,maximumProjectionOffset=maximumProjectionOffset,
            elapsed=Time.unscaledTimeAsDouble-startedAt,casts=events.ToArray()},true));
        File.WriteAllText(Path.Combine(output,"complete.txt"),completion);
        Debug.Log("[Skill Focus Cast QA] COMPLETE "+output+": "+completion);
    }

    private static object Field(object value,string name)=>value==null?null:value.GetType().GetField(name,Private)?.GetValue(value);
    private static T Read<T>(object value,string name,T fallback)=>Field(value,name) is T found?found:fallback;
    private static T Unit<T>(object value,string name,T fallback)
    {
        object field=value==null?null:value.GetType().GetField(name)?.GetValue(value);
        return field is T found?found:fallback;
    }
    private void OnDestroy(){if(begun&&!Finished)Complete("OBSERVER_DESTROYED_PARTIAL",false);}
}
