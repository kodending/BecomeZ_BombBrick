using System;
using UnityEngine;

[DefaultExecutionOrder(10065)]
public sealed class ReadabilityAttackOriginEffect:MonoBehaviour
{
    public const int StraightPunchStyle=7;
    private static readonly Sprite[][] CachedFrames=new Sprite[8][];
    private Sprite[] frames;
    private SpriteRenderer output,source,target;
    private SpriteMonsterAnimationDriver driver;
    private Func<bool> valid;
    private Action onSwing;
    private Camera effectCamera;
    private Bounds originBody;
    private float elapsed,endElapsed;
    private int currentFrame=-1,contactFrame=-1,endStartFrame,style;
    private bool launch,ending,contactShown;

    public static void Warmup()
    {
        for(int kind=0;kind<CachedFrames.Length;kind++)
            if(CachedFrames[kind]==null)CachedFrames[kind]=CreateFrames(kind);
    }

    public static ReadabilityAttackOriginEffect TrackMelee(
        SpriteMonsterAnimationDriver source,SpriteRenderer target,bool bear,Func<bool> valid,Action onSwing=null)
        =>TrackAttack(source,target,bear?1:0,valid,onSwing);

    public static ReadabilityAttackOriginEffect TrackAttack(
        SpriteMonsterAnimationDriver source,SpriteRenderer target,int style,Func<bool> valid,Action onSwing=null)
    {
        if(source==null||valid==null||source.CurrentMotionName!="Attack"||!source.HasPendingBasicAttackHit)return null;
        var effect=Create(source.GetComponent<SpriteRenderer>(),target,style);
        if(effect==null)return null;
        effect.driver=source;effect.valid=valid;effect.onSwing=onSwing;
        effect.PoseMelee();return effect;
    }

    public static ReadabilityAttackOriginEffect PlayLaunch(SpriteRenderer source,SpriteRenderer target)
        =>PlayRelease(source,target,2,new Color(.88f,.83f,1f));

    public static ReadabilityAttackOriginEffect PlayRelease(SpriteRenderer source,SpriteRenderer target,int style,Color tint)
    {
        var effect=Create(source,target,style);
        if(effect==null)return null;
        effect.launch=true;
        effect.output.color=new Color(tint.r,tint.g,tint.b,.7f*tint.a);
        effect.Place();effect.ShowFrame(0);
        // The released muzzle puff no longer depends on a living caster or target.
        effect.source=null;effect.target=null;
        return effect;
    }

    private static ReadabilityAttackOriginEffect Create(SpriteRenderer source,SpriteRenderer target,int kind)
    {
        if(!CombatReadabilityPresentation.Local||kind<0||kind>=CachedFrames.Length||
           source==null||source.sprite==null||target==null||target.sprite==null||Camera.main==null)return null;
        var root=new GameObject(kind==StraightPunchStyle?"Readability Straight Punch":
            kind==2||kind==5||kind==6?"Readability Magic Launch":"Readability Attack Sweep");
        root.layer=source.gameObject.layer;
        var effect=root.AddComponent<ReadabilityAttackOriginEffect>();
        effect.source=source;effect.target=target;effect.effectCamera=Camera.main;
        effect.style=kind;
        effect.originBody=PixelMantisReaper.VisibleBody(source.sprite);
        effect.frames=CachedFrames[kind]??(CachedFrames[kind]=CreateFrames(kind));
        effect.output=root.AddComponent<SpriteRenderer>();
        effect.output.flipY=kind==0;
        effect.output.sortingLayerID=source.sortingLayerID;effect.output.sortingOrder=source.sortingOrder+3;
        effect.output.color=new Color(1f,1f,1f,kind==2?.7f:.65f);effect.output.enabled=false;
        return effect;
    }

    private void LateUpdate()
    {
        if(ending)return;
        if(launch)Advance(Time.deltaTime);else PoseMelee();
    }

    private void PoseMelee()
    {
        if(!CombatReadabilityPresentation.Local||
           driver==null||source==null||valid==null||!valid()||driver.CurrentMotionName!="Attack")
        {Release();return;}
        float time=driver.BasicAttackAnimationTime,hit=driver.BasicAttackHitTime;
        if(!driver.HasPendingBasicAttackHit&&time<hit){Release();return;}
        float lead=Mathf.Min(.1f,hit*.75f),begin=hit-lead;
        if(time<begin){output.enabled=false;return;}
        var swing=onSwing;onSwing=null;swing?.Invoke();
        if(time>=hit&&!contactShown){contactShown=true;contactFrame=Time.frameCount;}
        if(time>=hit+.1f&&contactFrame!=Time.frameCount){Release();return;}
        int frame=time<hit?Mathf.Clamp(Mathf.FloorToInt((time-begin)/Mathf.Max(.001f,lead)*3f),0,2):
            contactFrame==Time.frameCount?2:Mathf.Min(5,2+Mathf.FloorToInt((time-hit)*40f));
        Place();ShowFrame(frame);
        output.enabled=source.enabled&&!source.forceRenderingOff;
    }

    private void Advance(float deltaTime)
    {
        if(!CombatReadabilityPresentation.Local)
        {Release();return;}
        if(CombatSkillClock.ActorFrozen(this))return;
        elapsed+=Mathf.Max(0f,deltaTime);
        int frame=Mathf.FloorToInt(elapsed*40f);
        if(frame>=frames.Length){Release();return;}
        ShowFrame(frame);
    }

    private void Place()
    {
        Bounds body=style==StraightPunchStyle?originBody:PixelMantisReaper.VisibleBody(source.sprite);
        Vector3 localCenter=body.center;
        if(source.flipX)localCenter.x=-localCenter.x;if(source.flipY)localCenter.y=-localCenter.y;
        Vector3 center=source.transform.TransformPoint(localCenter);
        Vector3 destination=target!=null&&target.sprite!=null?
            CombatReadabilityPresentation.VisibleBodyCenter(target):center+source.transform.right;
        Vector3 screen=effectCamera.WorldToScreenPoint(destination)-effectCamera.WorldToScreenPoint(center);
        Vector2 direction=new Vector2(screen.x,screen.y);
        if(direction.sqrMagnitude<.001f)direction=source.flipX?Vector2.left:Vector2.right;
        direction.Normalize();
        float width=source.transform.TransformVector(Vector3.right*body.size.x).magnitude;
        float height=source.transform.TransformVector(Vector3.up*body.size.y).magnitude;
        float span=Mathf.Max(width,height*.6f);
        Vector3 forward=effectCamera.transform.right*direction.x+effectCamera.transform.up*direction.y;
        transform.position=center+forward*span*.38f-effectCamera.transform.forward*.015f;
        transform.rotation=effectCamera.transform.rotation*
            Quaternion.Euler(0f,0f,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
        float size=launch?(style==5?.38f:.5f):style==1||style==StraightPunchStyle?.85f:style==3?.58f:style==4?.8f:.72f;
        transform.localScale=Vector3.one*span*size*1.5f;
        if(style==StraightPunchStyle)transform.localScale=Vector3.Scale(transform.localScale,new Vector3(1.6f,.28f,1f));
        output.sortingLayerID=source.sortingLayerID;output.sortingOrder=source.sortingOrder+3;
    }

    private void ShowFrame(int frame)
    {
        currentFrame=frame;output.sprite=frames[frame];output.enabled=true;
    }

    public void PrepareEndPlayback()
    {
        if(ending)return;
        if(output==null||!output.enabled||currentFrame<0){Release();return;}
        ending=true;endStartFrame=currentFrame;endElapsed=0f;
        driver=null;source=null;target=null;valid=null;onSwing=null;
    }

    public void AdvanceEnd(float deltaTime)
    {
        if(!ending||!gameObject.activeSelf)return;
        endElapsed+=Mathf.Max(0f,deltaTime);
        int frame=endStartFrame+Mathf.FloorToInt(endElapsed*36f);
        if(frame>=frames.Length){Release();return;}
        ShowFrame(frame);
    }

    private void Release()
    {
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private static Sprite[] CreateFrames(int kind)
    {
        if(kind==StraightPunchStyle)return CachedFrames[4]??(CachedFrames[4]=CreateFrames(4));
        const int size=32;var result=new Sprite[6];
        float[] heads={55f,20f,-25f,-60f,-80f,-100f};
        float[] spans={18f,45f,85f,100f,75f,25f};
        byte[] alpha={150,220,255,220,135,55};
        for(int frame=0;frame<result.Length;frame++)
        {
            var pixels=new Color32[size*size];
            for(int y=1;y<size-1;y++)for(int x=1;x<size-1;x++)
            {
                float dx=x-5f,dy=y-15.5f;
                float radius=Mathf.Sqrt(dx*dx+dy*dy),angle=Mathf.Atan2(dy,dx)*Mathf.Rad2Deg;
                bool core=false,edge=false;
                if(kind==2||kind==5)
                {
                    float inner=frame*2.4f,outer=inner+3.5f;
                    if(dx<=0f||radius<inner||radius>outer)continue;
                    float step=kind==5?36f:27f,limit=kind==5?40f:57f;
                    float ray=Mathf.Abs(Mathf.DeltaAngle(angle,Mathf.Round(angle/step)*step));
                    core=ray<4f&&Mathf.Abs(angle)<limit;
                    edge=ray<9f&&Mathf.Abs(angle)<limit;
                }
                else if(kind==3)
                {
                    float gap=frame<2?7f-frame*2.5f:frame==2?2f:.5f;
                    float jaw=Mathf.Abs(Mathf.Abs(dy)-gap-dx*.08f);
                    core=dx>3f+frame&&dx<17f+frame&&jaw<.65f;
                    edge=dx>2f+frame&&dx<18f+frame&&jaw<1.4f;
                    if(frame>3){core&=(x+y)%3==0;edge&=(x+y)%2==0;}
                }
                else if(kind==4)
                {
                    float head=8f+frame*3.3f,reach=frame<3?3f+frame*3f:12f-frame;
                    float pressure=Mathf.Abs(dx+Mathf.Abs(dy)*.65f-head);
                    core=Mathf.Abs(dy)<reach&&pressure<.85f;
                    edge=Mathf.Abs(dy)<reach+1f&&pressure<1.8f;
                }
                else if(kind==6)
                {
                    for(int puff=0;puff<3;puff++)
                    {
                        float cx=7f+frame*2.2f+puff*2f,cy=15.5f+(puff-1)*(2f+frame);
                        float r=frame<3?1f+frame*.7f:3f-(frame-2)*.5f;
                        float distance=(x-cx)*(x-cx)+(y-cy)*(y-cy);
                        core|=distance<r*r;edge|=distance<(r+1f)*(r+1f);
                    }
                }
                else if(angle>=heads[frame]&&angle<=heads[frame]+spans[frame])
                {
                    int tracks=kind==1?1:3;
                    for(int track=0;track<tracks;track++)
                    {
                        float thickness=kind==1?(frame==2||frame==3?2.2f:1.3f):.6f;
                        float distance=Mathf.Abs(radius-(17f-track*3.5f));
                        core|=distance<thickness;edge|=distance<thickness+1f;
                    }
                }
                if(!core&&!edge)continue;
                pixels[y*size+x]=kind==2||kind==5||kind==6?
                    (core?new Color32(255,255,255,alpha[frame]):new Color32(185,185,185,(byte)(alpha[frame]*.6f))):
                    (core?new Color32(255,238,184,alpha[frame]):new Color32(200,139,68,(byte)(alpha[frame]*.55f)));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {name="AttackOrigin_"+kind+"_"+frame,filterMode=FilterMode.Point,
                 wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels32(pixels);texture.Apply(false,true);
            result[frame]=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,size);
            result[frame].name=texture.name;result[frame].hideFlags=HideFlags.HideAndDontSave;
        }
        return result;
    }
}
