using UnityEngine;

// The stage and field use the same species-specific leaf shell.
[DefaultExecutionOrder(10030)]
public sealed class PixelMantisCocoon:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static readonly Sprite[][] shellSprites=new Sprite[5][];
    private static Sprite chipSprite;
    private Sprite[] shellFrames;
    private SpriteRenderer body,shell;
    private readonly SpriteRenderer[] halves=new SpriteRenderer[2];
    private readonly SpriteRenderer[] pieces=new SpriteRenderer[18];
    private float age,hatch,height,hit,release=-1f;
    private bool bodyVisible,hatched;
    private Vector3 ground,bodyScale;

    public static PixelMantisCocoon Play(SpriteRenderer body,int speciesRow=0)
    {
        if(body==null)return null;
        speciesRow=Mathf.Clamp(speciesRow,0,4);
        if(shellSprites[speciesRow]==null||shellSprites[speciesRow][3]==null)shellSprites[speciesRow]=BuildShell(speciesRow);
        Sprite[] frames=shellSprites[speciesRow];Sprite shellSprite=frames[3];
        if(chipSprite==null)
        {
            var texture=new Texture2D(3,3){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixels(new[]{Color.clear,Color.white,Color.clear,Color.white,Color.white,Color.white,Color.white,Color.white,Color.clear});
            texture.Apply(false,true);chipSprite=Sprite.Create(texture,new Rect(0,0,3,3),Vector2.one*.5f,20f);
            chipSprite.hideFlags=HideFlags.HideAndDontSave;
        }
        var fx=new GameObject("Mantis Living Cocoon").AddComponent<PixelMantisCocoon>();
        fx.body=body;fx.bodyVisible=body.enabled;fx.bodyScale=body.transform.localScale;fx.shellFrames=frames;
        Bounds visible=PixelMantisReaper.VisibleBody(body.sprite);
        fx.height=Mathf.Max(.7f,visible.size.y*Mathf.Abs(body.transform.lossyScale.y))*.82f;
        fx.ground=body.transform.TransformPoint(new Vector3(0f,visible.min.y,0f));
        fx.shell=fx.Make("Segmented Cocoon",frames[0],1);
        for(int i=0;i<2;i++)
        {
            Rect rect=shellSprite.rect;rect.x+=i*rect.width*.5f;rect.width*=.5f;
            var half=Sprite.Create(shellSprite.texture,rect,new Vector2(i==0?1f:0f,.5f),shellSprite.pixelsPerUnit);
            half.hideFlags=HideFlags.HideAndDontSave;
            fx.halves[i]=fx.Make("Split Cocoon Half",half,2);
        }
        for(int i=0;i<fx.pieces.Length;i++)fx.pieces[i]=fx.Make("Cocoon Chip",chipSprite,2);
        fx.Pose(0f,0f);fx.RefreshPose();return fx;
    }

    private SpriteRenderer Make(string name,Sprite sprite,int order)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingLayerID=body.sortingLayerID;
        r.sortingOrder=body.sortingOrder+order;r.enabled=false;return r;
    }

    public void Pose(float time,float hatchProgress){age=time;hatch=hatchProgress;}
    public void Birth(int index){hit=.18f;}
    public void Hit(){hit=.14f;}
    public void Finish(bool success)
    {
        if(this==null||release>=0f)return;
        release=0f;hatched=success;shell.sprite=shellFrames[3];
        if(body!=null){body.enabled=bodyVisible;body.transform.localScale=bodyScale;}
    }
    public void Cancel()
    {
        if(this==null)return;
        if(body!=null){body.enabled=bodyVisible;body.transform.localScale=bodyScale;}
        if(CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(body==null&&release<0f){Cancel();return;}
            float dt=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            RenderPose(dt);
        }
    }

    // The cinematic advances this cast while the scene's ordinary LateUpdate clock is held.
    public void RefreshPose()=>RenderPose(0f);

    private void RenderPose(float dt)
    {
        hit=Mathf.Max(0f,hit-dt);
        Quaternion rotation=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        Vector3 right=rotation*Vector3.right,up=rotation*Vector3.up;
        if(release<0f)
        {
            Bounds visible=PixelMantisReaper.VisibleBody(body.sprite);
            ground=body.transform.TransformPoint(new Vector3(0f,visible.min.y,0f));
            body.enabled=age<.16f&&bodyVisible;
            if(age<.2f)body.transform.localScale=Vector3.Scale(bodyScale,new Vector3(1f,Mathf.Lerp(1f,.72f,age/.2f),1f));
            shell.sortingOrder=body.sortingOrder+1;
            shell.sprite=shellFrames[age<.1f?0:age<.2f?1:age<.3f?2:3];
            float pulse=1f+Mathf.Sin(age*3.2f)*.012f+hatch*.12f;
            Bounds shellBounds=PixelMantisReaper.VisibleBody(shell.sprite);
            float shellScale=height/Mathf.Max(.01f,shellBounds.size.y);
            shell.enabled=true;shell.transform.position=ground+up*(height*.5f-shellBounds.center.y*shellScale/pulse)+
                right*Mathf.Sin(age*85f)*height*(hatch*.025f+hit*.04f);
            shell.transform.rotation=rotation;
            shell.transform.localScale=new Vector3(pulse,1f/pulse,1f)*shellScale;
            shell.color=Color.Lerp(Color.white,new Color(1f,1f,.72f),Mathf.Clamp01(hit*5f+hatch*.5f));
            for(int i=0;i<pieces.Length;i++)pieces[i].enabled=false;
        }
        else
        {
            release+=dt;shell.enabled=false;
            for(int i=0;i<2;i++)
            {
                float sign=i==0?-1f:1f;var r=halves[i];r.enabled=true;
                r.transform.position=ground+up*height*(.5f+(hatched?.3f:-.2f)*release)+right*sign*height*release*1.5f;
                r.transform.rotation=rotation*Quaternion.Euler(0f,0f,-sign*release*110f);
                r.transform.localScale=Vector3.one*(height/Mathf.Max(.01f,PixelMantisReaper.VisibleBody(shell.sprite).size.y));
                r.color=new Color(1f,1f,1f,1f-release/.55f);
            }
            for(int i=0;i<pieces.Length;i++)
            {
                float seed=(i*17%19)/19f,side=i%2==0?-1f:1f;
                var r=pieces[i];r.enabled=true;
                float spread=side*(.18f+seed*.55f)*height*release*2.6f;
                float rise=height*((hatched?1.9f:.65f)+seed)*release-height*3.7f*release*release;
                r.transform.position=ground+right*spread+up*(height*(.2f+seed*.65f)+rise);
                r.transform.rotation=rotation*Quaternion.Euler(0f,0f,side*(seed*180f+release*220f));
                r.transform.localScale=Vector3.one*height*(.55f+seed*.8f);
                r.color=new Color(.38f+seed*.25f,.46f+seed*.28f,.2f+seed*.18f,1f-release/.65f);
            }
            if(release>=.65f)Cancel();
        }
    }

    private void OnDestroy()
    {
        if(body!=null){body.enabled=bodyVisible;body.transform.localScale=bodyScale;}
        foreach(var half in halves)
            if(half!=null&&half.sprite!=null)
            {if(Application.isPlaying)Destroy(half.sprite);else DestroyImmediate(half.sprite);}
    }

    private static Sprite[] BuildShell(int speciesRow)
    {
        var texture=Resources.Load<Texture2D>("CombatCinematics/Rank4V3/mantis/CocoonMotion");
        if(texture==null)throw new System.InvalidOperationException("Mantis leaf shell artwork is missing.");
        if(texture.width%4!=0||texture.height%5!=0)throw new System.InvalidOperationException("Mantis leaf shell artwork must contain four columns and five rows.");
        int width=texture.width/4,height=texture.height/5;var frames=new Sprite[4];
        for(int frame=0;frame<frames.Length;frame++)
        {
            var sprite=Sprite.Create(texture,new Rect(frame*width,(4-speciesRow)*height,width,height),Vector2.one*.5f,height);
            sprite.name="CocoonMotion "+speciesRow+":"+frame;sprite.hideFlags=HideFlags.HideAndDontSave;frames[frame]=sprite;
        }
        return frames;
    }
}
