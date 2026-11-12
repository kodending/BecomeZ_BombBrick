using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(900)]
public sealed class FrogTongueWhip:MonoBehaviour
{
    public const float ImpactDelay=0.13f;
    private const float FinishDelay=0.24f;
    private const float PixelsPerUnit=32f;
    private const float PixelWorldSize=2f/PixelsPerUnit;
    private const int MaxPixels=96;
    private static Sprite pixelSprite;

    private static readonly HashSet<FrogTongueWhip> EndingTails=new HashSet<FrogTongueWhip>();
    private bool endingTail;
    private Vector3 endingSource,endingTarget;
    private Action impactAction;
    private CombatSkillClock skillClock;
    private Transform target;
    private Transform branchOrigin;
    private readonly List<FrogTongueWhip> branches=new List<FrogTongueWhip>();
    private SpriteRenderer sourceRenderer;
    private readonly List<SpriteRenderer> pixels=new List<SpriteRenderer>();
    private Camera effectCamera;
    private float elapsed;
    private float playbackSpeed=1f;
    private float pixelScale=1.8f;
    private float waveAmplitude=0.045f;
    private float maximumDistance;
    private Color bodyColor;
    private Color accentColor;
    private Color tipColor;
    private bool impactTriggered;
    private bool holdOnImpact;
    private float buffTransferElapsed=-1f;
    private Color buffTransferColor;

    public bool IsPlaying{get;private set;}
    public float EndpointDistance=>Vector3.Distance(SourcePosition(),TargetPosition());
    public int ActiveSegmentCount
    {
        get
        {
            int count=0;
            for(int i=0;i<pixels.Count;i++)if(pixels[i].enabled)count++;
            return count;
        }
    }

    public void Initialize()
    {
        if(sourceRenderer==null)
        {
            SpriteMonsterAnimationDriver driver=
                GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            sourceRenderer=driver==null?null:driver.GetComponent<SpriteRenderer>();
        }
        SetVisible(false);
    }

    public void Play(
        Transform attackTarget,
        Action onImpact,
        bool skill=false,
        float speed=1f,
        float maxDistance=0f,
        bool hold=false)
    {
        if(attackTarget==null){onImpact?.Invoke();return;}
        foreach(FrogTongueWhip branch in branches)branch.Cancel();
        Initialize();
        if(IsPlaying&&!impactTriggered&&(!CombatSkillClock.Active||(skillClock==null?Time.deltaTime:skillClock.DeltaTime)>0f))
            using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
                impactAction?.Invoke();
        skillClock=CombatSkillClock.Capture();
        target=attackTarget;
        impactAction=onImpact;
        elapsed=0f;
        playbackSpeed=Mathf.Max(0.05f,speed);
        maximumDistance=Mathf.Max(0f,maxDistance);
        impactTriggered=false;
        holdOnImpact=hold;
        buffTransferElapsed=-1f;
        bodyColor=skill?new Color32(255,78,178,255):new Color32(255,126,176,255);
        accentColor=skill?new Color32(197,40,126,255):new Color32(220,76,132,255);
        tipColor=skill?new Color32(255,221,245,255):new Color32(255,184,209,255);
        bodyColor=PixelSpiritConcert.Tint(sourceRenderer,bodyColor);
        accentColor=PixelSpiritConcert.Tint(sourceRenderer,accentColor);
        tipColor=PixelSpiritConcert.Tint(sourceRenderer,tipColor);
        pixelScale=skill?2.3f:1.8f;
        waveAmplitude=skill?0.13f:0.09f;
        IsPlaying=true;
        UpdateVisual(0.01f);
    }

    private SpriteRenderer Pixel(int index)
    {
        while(pixels.Count<=index)
        {
            GameObject visual=new GameObject($"Tongue Pixel {pixels.Count:00}");
            visual.transform.SetParent(transform,false);
            SpriteRenderer renderer=visual.AddComponent<SpriteRenderer>();
            renderer.sprite=PixelSprite();
            pixels.Add(renderer);
        }
        return pixels[index];
    }

    private static Sprite PixelSprite()
    {
        if(pixelSprite!=null)return pixelSprite;
        Texture2D texture=new Texture2D(1,1,TextureFormat.RGBA32,false)
        {
            name="Frog Tongue Pixel",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        texture.SetPixel(0,0,Color.white);
        texture.Apply(false,true);
        pixelSprite=Sprite.Create(texture,new Rect(0f,0f,1f,1f),
            new Vector2(0.5f,0.5f),PixelsPerUnit);
        pixelSprite.name="FrogTonguePixel";
        pixelSprite.hideFlags=HideFlags.HideAndDontSave;
        return pixelSprite;
    }

    public void Release()
    {
        foreach(FrogTongueWhip branch in branches)branch.Release();
        if(!IsPlaying)return;
        holdOnImpact=false;
        elapsed=ImpactDelay;
        playbackSpeed=1f;
    }

    public void ShowBuffTransfer(Color color)
    {
        buffTransferColor=color;
        buffTransferElapsed=0f;
    }

    public void Cancel()
    {
        foreach(FrogTongueWhip branch in branches)branch.Cancel();
        IsPlaying=false;
        holdOnImpact=false;
        impactAction=null;
        target=null;
        buffTransferElapsed=-1f;
        SetVisible(false);
    }

    public void FinishAtBattleEnd()
    {
        foreach(var branch in branches)if(branch!=null)branch.FinishAtBattleEnd();
        if(!IsPlaying)return;
        var host=new GameObject("Finishing Frog Tongue");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host,gameObject.scene);
        var tail=host.AddComponent<FrogTongueWhip>();
        tail.endingTail=true;tail.endingSource=SourcePosition();tail.endingTarget=TargetPosition();
        tail.elapsed=holdOnImpact?Mathf.Min(elapsed,ImpactDelay):elapsed;
        tail.playbackSpeed=holdOnImpact?1f:playbackSpeed;
        tail.pixelScale=pixelScale;tail.waveAmplitude=waveAmplitude;
        tail.bodyColor=bodyColor;tail.accentColor=accentColor;tail.tipColor=tipColor;
        tail.buffTransferColor=buffTransferColor;tail.buffTransferElapsed=buffTransferElapsed;
        tail.effectCamera=effectCamera;tail.sourceRenderer=sourceRenderer;tail.IsPlaying=true;
        foreach(var pixel in pixels)if(pixel!=null)
        {pixel.transform.SetParent(host.transform,true);tail.pixels.Add(pixel);}
        pixels.Clear();EndingTails.Add(tail);
        Cancel();
    }

    public static void ClearBattleEndTails(UnityEngine.SceneManagement.Scene scene)
    {
        foreach(var tail in new List<FrogTongueWhip>(EndingTails))
        {
            if(tail==null){EndingTails.Remove(tail);continue;}
            if(tail.gameObject.scene!=scene)continue;
            tail.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(tail.gameObject);else DestroyImmediate(tail.gameObject);
            EndingTails.Remove(tail);
        }
    }

    public void FadeAtBattleEnd()
    {
        foreach(var branch in branches)if(branch!=null)branch.FadeAtBattleEnd();
        if(pixels.Count>0)
        {
            var tail=new GameObject("Fading Frog Tongue");
            foreach(var pixel in pixels)if(pixel!=null)pixel.transform.SetParent(tail.transform,true);
            pixels.Clear();
            CombatEffectEndFade.Begin(tail);
        }
        Cancel();
    }

    private void OnDisable()=>Cancel();

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            float deltaTime=skillClock==null?Time.deltaTime:skillClock.DeltaTime;
            if(CombatSkillClock.Active&&deltaTime<=0f)return;
            Advance(deltaTime);
            if(endingTail&&!IsPlaying)
            {
                EndingTails.Remove(this);
                if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
            }
        }
    }

    private void OnDestroy()=>EndingTails.Remove(this);

    private void Advance(float deltaTime)
    {
        if(!IsPlaying)return;
        if(!endingTail&&(target==null||!target.gameObject.activeInHierarchy)){Cancel();return;}
        elapsed+=deltaTime*playbackSpeed;
        if(buffTransferElapsed>=0f)buffTransferElapsed+=deltaTime;
        if(!impactTriggered&&elapsed>=ImpactDelay)
        {
            impactTriggered=true;
            Action callback=impactAction;
            impactAction=null;
            callback?.Invoke();
        }
        if(!IsPlaying)return;
        if(impactTriggered&&holdOnImpact)
        {
            UpdateVisual(1f);
            return;
        }
        if(elapsed>=FinishDelay)
        {
            if(!impactTriggered)impactAction?.Invoke();
            impactAction=null;
            IsPlaying=false;
            SetVisible(false);
            return;
        }
        float progress;
        float extension;
        if(elapsed<=ImpactDelay)
        {
            progress=Mathf.Clamp01(elapsed/ImpactDelay);
            extension=1f-Mathf.Pow(1f-progress,3f);
        }
        else
        {
            progress=Mathf.Clamp01(
                (elapsed-ImpactDelay)/(FinishDelay-ImpactDelay));
            extension=1f-progress*progress;
        }
        UpdateVisual(extension);
    }

    private void UpdateVisual(float extension)
    {
        Vector3 start=SourcePosition();
        Vector3 end=Vector3.Lerp(start,TargetPosition(),Mathf.Clamp01(extension));
        effectCamera=effectCamera==null?Camera.main:effectCamera;
        Vector3 displayStart=start;
        Vector3 displayEnd=end;
        float angle=0f;
        Quaternion billboard=Quaternion.identity;
        if(effectCamera!=null)
        {
            Vector3 screenStart=effectCamera.WorldToScreenPoint(start);
            Vector3 screenEnd=effectCamera.WorldToScreenPoint(end);
            float depth=(screenStart.z+screenEnd.z)*0.5f;
            displayStart=effectCamera.ScreenToWorldPoint(
                new Vector3(screenStart.x,screenStart.y,depth));
            displayEnd=effectCamera.ScreenToWorldPoint(
                new Vector3(screenEnd.x,screenEnd.y,depth));
            angle=Mathf.Atan2(
                screenEnd.y-screenStart.y,
                screenEnd.x-screenStart.x)*Mathf.Rad2Deg;
            billboard=effectCamera.transform.rotation;
        }
        Vector3 delta=displayEnd-displayStart;
        float length=Mathf.Max(PixelWorldSize,delta.magnitude);
        int pixelCount=Mathf.Clamp(
            Mathf.CeilToInt(length/PixelWorldSize),1,MaxPixels);
        Quaternion rotation=billboard*Quaternion.Euler(0f,0f,angle);
        Vector3 side=rotation*Vector3.up;
        for(int i=0;i<pixelCount;i++)
        {
            float t=pixelCount<=1?0f:i/(float)(pixelCount-1);
            float wave=Mathf.Sin(t*Mathf.PI)*
                Mathf.Sin(elapsed*55f-t*5f)*waveAmplitude*
                Mathf.Sin(Mathf.Clamp01(extension)*Mathf.PI);
            SpriteRenderer pixel=Pixel(i);
            pixel.enabled=true;
            pixel.transform.position=Vector3.Lerp(displayStart,displayEnd,t)+side*wave;
            pixel.transform.rotation=rotation;
            float scale=pixelScale*(i==pixelCount-1?1.3f:1f);
            pixel.transform.localScale=new Vector3(scale,scale,1f);
            pixel.color=i==pixelCount-1?tipColor:(i%3==0?accentColor:bodyColor);
            if(buffTransferElapsed>=0f&&buffTransferElapsed<0.4f&&
               Mathf.Abs(t-(1f-buffTransferElapsed/0.4f))<0.07f)
            {
                pixel.color=buffTransferColor;
                pixel.transform.localScale*=1.4f;
            }
        }
        for(int i=pixelCount;i<pixels.Count;i++)pixels[i].enabled=false;
        ApplySorting();
    }

    private Vector3 SourcePosition()
    {
        if(endingTail)return endingSource;
        if(branchOrigin!=null)
        {
            SpriteRenderer renderer=branchOrigin.GetComponent<SpriteRenderer>();
            return renderer!=null&&renderer.sprite!=null
                ?renderer.transform.TransformPoint(renderer.sprite.bounds.center)
                :branchOrigin.position+Vector3.up*0.35f;
        }
        if(sourceRenderer==null||sourceRenderer.sprite==null)
            return transform.position+Vector3.up*0.4f;
        Bounds bounds=sourceRenderer.sprite.bounds;
        Vector3 local=bounds.center;
        local.x+=bounds.extents.x*(sourceRenderer.flipX?-0.22f:0.22f);
        local.y-=bounds.extents.y*0.08f;
        return sourceRenderer.transform.TransformPoint(local);
    }

    private Vector3 TargetPosition()
    {
        if(endingTail)return endingTarget;
        if(target==null)return SourcePosition();
        SpriteRenderer renderer=target.GetComponent<SpriteRenderer>();
        Vector3 point=renderer!=null&&renderer.sprite!=null
            ?renderer.transform.TransformPoint(renderer.sprite.bounds.center)
            :target.position+Vector3.up*0.35f;
        if(maximumDistance<=0f||(impactTriggered&&holdOnImpact))return point;
        Vector3 start=SourcePosition();
        Vector3 direction=point-start;
        return start+Vector3.ClampMagnitude(direction,maximumDistance);
    }

    private void ApplySorting()
    {
        if(sourceRenderer==null)return;
        for(int i=0;i<pixels.Count;i++)
        {
            pixels[i].sortingLayerID=sourceRenderer.sortingLayerID;
            pixels[i].sortingOrder=sourceRenderer.sortingOrder+24+i;
        }
    }

    private void SetVisible(bool visible)
    {
        for(int i=0;i<pixels.Count;i++)pixels[i].enabled=visible;
    }

    public FrogTongueWhip Fork(Transform branchTarget)
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            FrogTongueWhip branch=branches.Find(candidate=>!candidate.IsPlaying);
            if(branch==null)
            {
                GameObject host=new GameObject("Frog Tongue Branch");
                host.transform.SetParent(transform,false);
                branch=host.AddComponent<FrogTongueWhip>();
                branches.Add(branch);
            }
            branch.sourceRenderer=sourceRenderer;
            branch.branchOrigin=target;
            branch.Play(branchTarget,null,true,1f,0f,true);
            return branch;
        }
    }
}
