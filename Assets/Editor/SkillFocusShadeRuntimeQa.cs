using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class SkillFocusShadeRuntimeQa
{
    public static void Attach(string outputSuffix)
    {
        if(Application.dataPath!="C:/Unity/BecomeZ_BombBrick/Assets")throw new InvalidOperationException("Wrong project.");
        if(typeof(MonsterAutoBattlePrototype).GetField("skillFocusShade",BindingFlags.Instance|BindingFlags.NonPublic)==null)
            throw new NotSupportedException("Skill focus darkening was removed. Use SequentialSkillFocusRuntimeQa to verify the undimmed introduction.");
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode is required.");
        if(string.IsNullOrWhiteSpace(outputSuffix)||outputSuffix.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))
            throw new ArgumentException("Invalid shade QA output suffix.");
        if(UnityEngine.Object.FindObjectOfType<SkillFocusShadeRuntimeQaObserver>()!=null)
            throw new InvalidOperationException("A shade observer already exists.");
        var host=new GameObject("Skill focus same-frame shade observer (editor only)");
        UnityEngine.Object.DontDestroyOnLoad(host);
        try{host.AddComponent<SkillFocusShadeRuntimeQaObserver>().Begin(outputSuffix);}
        catch{UnityEngine.Object.DestroyImmediate(host);throw;}
    }
}

// Camera.Render and PNG encoding are diagnostic work and must never be used as a frame-rate benchmark.
[DefaultExecutionOrder(10030)]
public sealed class SkillFocusShadeRuntimeQaObserver:MonoBehaviour
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Type BattleType=typeof(MonsterAutoBattlePrototype);
    private string output;
    private double deadline;
    private bool captureStarted;
    public bool Finished { get; private set; }
    private Report report;

    [Serializable] private sealed class Report
    {
        public string result,exception,output;
        public int unityFrame,casterId,width,height,nativeWidth,nativeHeight;
        public double unscaledTime,focusElapsed,battleElapsed,renderRealSeconds,nativeUnscaledTime;
        public float timeScale,strength,shadeAlpha;
        public bool restoredCameraTarget,restoredActiveRenderTexture,restoredShadeColor,restoredShadeEnabled,unchangedRandomState,unchangedUnitState,nativeSameFrame;
        public string nativeMotion;
        public string limitation="Same-frame off/on/recreated0.52 camera renders, plus a native end-of-frame Game View screenshot. Recreated0.52 is an opacity comparison, not an earlier-build baseline. Synchronous renders and PNG encoding introduce diagnostic overhead.";
    }

    public void Begin(string suffix)
    {
        output=Path.GetFullPath(Path.Combine("Logs/SkillFocusShadeQA/20261019",suffix));
        if(Directory.Exists(output))throw new IOException("Shade QA output already exists.");
        Directory.CreateDirectory(output);
        deadline=Time.realtimeSinceStartupAsDouble+120d;
        report=new Report{output=output,result="WAITING_FOR_NATURAL_FOCUS"};
    }

    private void LateUpdate()
    {
        if(Finished||captureStarted)return;
        if(Application.dataPath!="C:/Unity/BecomeZ_BombBrick/Assets")
        {Fail(new InvalidOperationException("Wrong project."));return;}
        if(Time.realtimeSinceStartupAsDouble>deadline){Fail(new TimeoutException("No natural full-strength hit-stop observed."));return;}
        var battle=UnityEngine.Object.FindObjectOfType<MonsterAutoBattlePrototype>();
        var director=UnityEngine.Object.FindObjectOfType<CombatCameraDirector>();
        if(battle==null||director==null||!director.SkillFocusActive||Time.timeScale!=0f||director.SkillFocusStrength<=.95f)return;
        var shade=Field(battle,"skillFocusShade") as SpriteRenderer;
        var caster=Field(battle,"skillFocusUnit");
        var camera=Field(battle,"mainCamera") as Camera;
        if(shade==null||!shade.enabled||!shade.gameObject.activeInHierarchy||caster==null||camera==null)return;
        captureStarted=true;
        try
        {
            CapturePair(battle,director,camera,shade,caster);
            StartCoroutine(CaptureNativeAtEndOfFrame(caster));
        }
        catch(Exception exception){Fail(exception);}
    }

    private void CapturePair(MonsterAutoBattlePrototype battle,CombatCameraDirector director,Camera camera,SpriteRenderer shade,object caster)
    {
        RenderTexture previousTarget=camera.targetTexture,previousActive=RenderTexture.active;
        Color previousShadeColor=shade.color,previousBackground=camera.backgroundColor;
        bool previousEnabled=shade.enabled;
        Rect previousRect=camera.rect;
        float previousAspect=camera.aspect;
        var random=UnityEngine.Random.state;
        var unitType=caster.GetType();
        var model=(GameObject)unitType.GetField("model").GetValue(caster);
        var driver=(SpriteMonsterAnimationDriver)unitType.GetField("spriteAnimation").GetValue(caster);
        Vector3 position=model.transform.position,scale=model.transform.localScale;
        int uses=(int)unitType.GetField("skillUses").GetValue(caster),frame=driver.CurrentFrameIndex;
        float hp=(float)unitType.GetField("hp").GetValue(caster),timer=(float)unitType.GetField("skillTimer").GetValue(caster);
        string motion=driver.CurrentMotionName;
        double before=Time.realtimeSinceStartupAsDouble;
        report.unityFrame=Time.frameCount;report.unscaledTime=Time.unscaledTimeAsDouble;
        report.focusElapsed=report.unscaledTime-Convert.ToDouble(Field(director,"skillFocusStartedAt"));
        report.battleElapsed=Convert.ToDouble(Field(battle,"battleElapsed"));
        report.casterId=(int)unitType.GetField("id").GetValue(caster);
        report.timeScale=Time.timeScale;report.strength=director.SkillFocusStrength;report.shadeAlpha=previousShadeColor.a;
        report.width=960;report.height=Mathf.RoundToInt(report.width/previousAspect);
        RenderTexture target=RenderTexture.GetTemporary(report.width,report.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        try
        {
            camera.targetTexture=target;
            shade.enabled=false;Render(camera,target,"shade-off.png");
            shade.enabled=previousEnabled;shade.color=previousShadeColor;Render(camera,target,"shade-on.png");
            Color recreated=previousShadeColor;recreated.a=.52f;shade.color=recreated;
            Render(camera,target,"shade-recreated-alpha052.png");
        }
        finally
        {
            shade.color=previousShadeColor;shade.enabled=previousEnabled;
            camera.targetTexture=previousTarget;camera.rect=previousRect;camera.backgroundColor=previousBackground;
            if(!Mathf.Approximately(camera.aspect,previousAspect))camera.aspect=previousAspect;
            RenderTexture.active=previousActive;
            RenderTexture.ReleaseTemporary(target);
            report.restoredCameraTarget=camera.targetTexture==previousTarget;
            report.restoredActiveRenderTexture=RenderTexture.active==previousActive;
            report.restoredShadeColor=shade.color==previousShadeColor;report.restoredShadeEnabled=shade.enabled==previousEnabled;
            report.unchangedRandomState=UnityEngine.Random.state.Equals(random);
            report.unchangedUnitState=model.transform.position==position&&model.transform.localScale==scale&&
                (int)unitType.GetField("skillUses").GetValue(caster)==uses&&
                (float)unitType.GetField("hp").GetValue(caster)==hp&&(float)unitType.GetField("skillTimer").GetValue(caster)==timer&&
                driver.CurrentMotionName==motion&&driver.CurrentFrameIndex==frame&&Time.timeScale==report.timeScale&&
                Time.unscaledTimeAsDouble==report.unscaledTime;
            report.renderRealSeconds=Time.realtimeSinceStartupAsDouble-before;
            File.WriteAllText(Path.Combine(output,"paired-capture.json"),JsonUtility.ToJson(report,true));
        }
        if(!report.restoredCameraTarget||!report.restoredActiveRenderTexture||!report.restoredShadeColor||!report.restoredShadeEnabled||
           !report.unchangedRandomState||!report.unchangedUnitState)throw new InvalidOperationException("Diagnostic capture did not preserve the observed state.");
    }

    private void Render(Camera camera,RenderTexture target,string file)
    {
        Texture2D pixels=null;
        try
        {
            camera.Render();RenderTexture.active=target;
            pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);
            File.WriteAllBytes(Path.Combine(output,file),pixels.EncodeToPNG());
        }
        finally{if(pixels!=null)Destroy(pixels);}
    }

    private IEnumerator CaptureNativeAtEndOfFrame(object caster)
    {
        yield return new WaitForEndOfFrame();
        Texture2D pixels=null;
        try
        {
            report.nativeUnscaledTime=Time.unscaledTimeAsDouble;
            report.nativeSameFrame=Time.frameCount==report.unityFrame&&report.nativeUnscaledTime==report.unscaledTime;
            if(!report.nativeSameFrame)throw new InvalidOperationException("Native screenshot moved to another simulation frame.");
            pixels=ScreenCapture.CaptureScreenshotAsTexture();
            if(pixels==null)throw new InvalidOperationException("No native Game View pixels.");
            report.nativeWidth=pixels.width;report.nativeHeight=pixels.height;
            var driver=(SpriteMonsterAnimationDriver)caster.GetType().GetField("spriteAnimation").GetValue(caster);
            report.nativeMotion=driver.CurrentMotionName;
            File.WriteAllBytes(Path.Combine(output,"native-hitstop.png"),pixels.EncodeToPNG());
            report.result="PASS_SAME_FRAME_CAPTURE";Finished=true;
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"complete.txt"),report.result);
        }
        catch(Exception exception){Fail(exception);}
        finally{if(pixels!=null)Destroy(pixels);}
    }

    private void Fail(Exception exception)
    {
        Finished=true;report.result="CAPTURE_FAILED";report.exception=exception.ToString();
        if(!string.IsNullOrEmpty(output))
        {
            File.WriteAllText(Path.Combine(output,"exception.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(report,true));
        }
        Debug.LogError("[Skill Focus Shade QA] "+exception);
    }

    private static object Field(object instance,string name)=>instance.GetType().GetField(name,Private)?.GetValue(instance);
}
