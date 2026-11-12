using System;
using UnityEngine;

// All phases follow the battle clock, including pause and status removal.
[DefaultExecutionOrder(10030)]
public sealed class PixelSpiritBlessing:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private ParticleSystem particles;
    private ParticleSystemRenderer particleRenderer;
    private ParticleSystem.Particle[] buffer;
    private SpriteRenderer reference,coat;
    private SpriteMonsterAnimationDriver driver;
    private Func<float> clock;
    private Func<bool> valid;
    private Vector3 center;
    private Color tint;
    private float born,duration,radius;
    private int mode,kind;
    private Action arrived;
    private Vector3 guardDirection;
    private float guardStrength;
    private static readonly Vector2[][] AntlerBranches={
        new[]{new Vector2(.08f,-.4f),new Vector2(.25f,-.2f),new Vector2(.4f,.16f),new Vector2(.46f,.5f),new Vector2(.4f,.72f)},
        new[]{new Vector2(.25f,-.2f),new Vector2(.5f,-.03f),new Vector2(.68f,.19f)},
        new[]{new Vector2(.4f,.16f),new Vector2(.19f,.37f),new Vector2(.17f,.59f)},
        new[]{new Vector2(.46f,.5f),new Vector2(.63f,.6f),new Vector2(.72f,.77f)}
    };

    public static float SpreadAt(float time)=>1f-Mathf.Pow(1f-Mathf.Clamp01(time/SpiritDeerCombatRules.Spread),3f);
    private static float Seed(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+311.7f)*43758.5453f,1f);

    private static PixelSpiritBlessing Create(string name,int mode,SpriteRenderer reference,
        Color color,Func<float> clock,Func<bool> valid)
    {
        var fx=new GameObject(name).AddComponent<PixelSpiritBlessing>();
        fx.mode=mode;fx.reference=reference;fx.tint=color;fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;fx.valid=valid;fx.born=fx.clock();
        if(reference!=null&&mode!=1)fx.transform.SetParent(reference.transform,false);
        fx.buffer=new ParticleSystem.Particle[mode==4?512:mode==1?320:96];
        fx.particles=PixelMushroomGas.CreateParticles("Spirit Light Pixels",fx.transform,fx.buffer.Length,
            PixelMushroomGas.Material(true),reference,1,true);
        fx.particleRenderer=fx.particles.GetComponent<ParticleSystemRenderer>();return fx;
    }

    public static PixelSpiritBlessing Charge(SpriteRenderer reference,Color color,Func<float> clock,Func<bool> valid)
    {
        if(reference==null)return null;
        var fx=Create("Spirit Antler Gathering",0,reference,color,clock,valid);
        fx.driver=reference.GetComponent<SpriteMonsterAnimationDriver>();return fx;
    }

    public static PixelSpiritBlessing Sanctuary(Vector3 center,float radius,float duration,Color color,
        SpriteRenderer reference,Func<float> clock,Func<bool> valid,bool inviolable=false)
    {
        var fx=Create("Spirit Sanctuary Bloom",1,reference,color,clock,valid);
        fx.center=center;fx.radius=radius;fx.duration=duration;fx.kind=inviolable?1:0;return fx;
    }

    public static PixelSpiritBlessing GuardHit(SpriteRenderer recipient,Vector3 direction,float blockedRatio,
        Func<float> clock,Func<bool> valid)
    {
        if(recipient==null)return null;
        // Repeated hits on one unit refresh a single barrier instead of stacking bright silhouettes.
        PixelSpiritBlessing fx=null;
        foreach(var existing in recipient.GetComponentsInChildren<PixelSpiritBlessing>())
            if(existing.mode==4){fx=existing;break;}
        if(fx==null)fx=Create("Spirit Antler Guard",4,recipient,new Color(.48f,.83f,1f),clock,valid);
        fx.skillClock=CombatSkillClock.Capture();
        fx.clock=clock;if(fx.skillClock!=null)fx.clock=()=>fx.skillClock.Time;
        CombatSkillClock.TrackParticle(fx.particles);
        fx.born=fx.clock();fx.valid=valid;fx.guardStrength=Mathf.Clamp01(blockedRatio/.5f);
        fx.guardDirection=direction.sqrMagnitude<.001f?recipient.transform.right:direction.normalized;
        fx.duration=.32f+fx.guardStrength*.1f;return fx;
    }

    public static PixelSpiritBlessing ReturnLife(Vector3 from,SpriteRenderer recipient,
        Func<float> clock,Func<bool> valid,Action arrived)
    {
        if(recipient==null)return null;
        var fx=Create("Spirit Life Return",3,recipient,new Color(.35f,1f,.68f),clock,valid);
        fx.center=from;fx.duration=.65f;fx.arrived=arrived;return fx;
    }

    // kind: protection, instant healing, haste, hostile eclipse, life return, overflow shield.
    public static PixelSpiritBlessing Bless(SpriteRenderer reference,int kind,Color color,float duration,
        Func<float> clock,Func<bool> valid)
    {
        if(reference==null)return null;
        foreach(var existing in reference.GetComponentsInChildren<PixelSpiritBlessing>())
            if(existing.mode==2&&existing.kind==kind)
            {
                existing.skillClock=CombatSkillClock.Capture();
                existing.clock=clock;if(existing.skillClock!=null)existing.clock=()=>existing.skillClock.Time;
                CombatSkillClock.TrackParticle(existing.particles);
                existing.born=existing.clock();existing.duration=duration;existing.valid=valid;existing.tint=color;return existing;
            }
        var fx=Create("Spirit Blessing "+kind,2,reference,color,clock,valid);fx.kind=kind;fx.duration=duration;
        fx.coat=new GameObject("Blessed Sprite Tint").AddComponent<SpriteRenderer>();
        fx.coat.transform.SetParent(reference.transform,false);return fx;
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(clock==null||valid==null||!valid()||mode!=1&&(reference==null||!reference.gameObject.activeInHierarchy))
            {Cancel();return;}
            float t=clock()-born;if(t<0f||mode!=0&&t>=duration+.35f){Cancel();return;}
            if(reference!=null)
            {particleRenderer.sortingLayerID=reference.sortingLayerID;particleRenderer.sortingOrder=mode==1?3:reference.sortingOrder+1;}
            if(mode==0)DrawCharge(t);else if(mode==1)DrawSanctuary(t);
            else if(mode==3)DrawReturn(t);else if(mode==4)DrawGuard(t);else DrawBlessing(t);
        }
    }

    private Bounds Body=>reference.sprite==null?new Bounds(Vector3.up*.5f,Vector3.one):
        PixelMantisReaper.VisibleBody(reference.sprite);
    private Vector3 BodyPoint(float x,float y)
    {
        Bounds b=Body;Vector3 local=new Vector3(b.center.x+x*b.size.x,b.min.y+y*b.size.y,0);
        if(reference.flipX)local.x=-local.x;if(reference.flipY)local.y=-local.y;
        return reference.transform.TransformPoint(local);
    }
    private void Put(int i,Vector3 at,float size,float alpha,float white=0)
    {
        Color color=Color.Lerp(tint,Color.white,white);color.a=alpha;
        buffer[i]=new ParticleSystem.Particle{position=at,startSize3D=Vector3.one*size,startColor=color,
            startLifetime=10,remainingLifetime=10,randomSeed=(uint)(i+1)};
    }

    private void DrawCharge(float t)
    {
        float progress=driver==null?Mathf.Clamp01(t/SpiritDeerCombatRules.Windup):
            Mathf.Clamp01(driver.CurrentFrameIndex/(float)Mathf.Max(1,driver.SkillAttackHitFrameIndex));
        Vector3 right=reference.transform.right,up=reference.transform.up;
        for(int i=0;i<buffer.Length;i++)
        {
            float seed=Seed(i),a=Seed(i+113)*Mathf.PI*2f;
            float distance=(.15f+seed*.8f)*Mathf.Pow(1f-progress,1.3f);
            Vector3 tip=BodyPoint(i%2==0?-.12f:.2f,.92f);
            Put(i,tip+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*distance,.03f+seed*.035f,
                (.25f+progress*.5f)*(i<64?1f:.5f),progress*.6f);
        }
        particles.SetParticles(buffer,buffer.Length);
    }

    private void DrawSanctuary(float t)
    {
        float spread=SpreadAt(t),end=Mathf.Clamp01((t-duration)/.35f);int count=0;
        for(int i=0;i<buffer.Length;i++)
        {
            float seed=Seed(i),a=Seed(i+113)*Mathf.PI*2f;
            bool wave=i<192;float radial;
            if(wave)
            {
                if(t>SpiritDeerCombatRules.Spread+.32f)continue;
                radial=radius*spread*(.55f+seed*.45f);
            }
            else radial=radius*Mathf.Sqrt(seed);
            if(radial>radius*spread||radial>radius*(1f-end))continue;
            float alpha=wave?(1f-Mathf.Clamp01((t-SpiritDeerCombatRules.Spread)/.32f))*.8f:
                (.12f+seed*.12f)*(.65f+.35f*Mathf.Sin(t*1.8f+i))*Mathf.Clamp01(t/.25f);
            Vector3 at=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radial;
            at.x=Mathf.Round(at.x/.05f)*.05f;at.z=Mathf.Round(at.z/.05f)*.05f;
            at.y+=wave?.045f+Mathf.Sin(spread*Mathf.PI)*seed*.28f:.035f+Mathf.Sin(t*1.4f+i)*.025f;
            if(kind==1&&!wave)
            {
                float rise=Mathf.Repeat(Seed(i+41)+t*.34f,1f);
                at.y=.035f+center.y+rise*.38f;
                alpha=(.16f+seed*.2f)*Mathf.Sin(rise*Mathf.PI)*Mathf.Clamp01(t/.25f);
            }
            Put(count++,at,wave?.065f+seed*.09f:kind==1?.065f+seed*.045f:.045f+seed*.035f,
                alpha*(1f-end),wave?.45f:.12f);
        }
        particles.SetParticles(buffer,count);
    }

    private void DrawBlessing(float t)
    {
        float end=1f-Mathf.Clamp01((t-duration)/.25f),burst=1f-Mathf.Clamp01(t/.55f);
        coat.sprite=reference.sprite;coat.flipX=reference.flipX;coat.flipY=reference.flipY;
        coat.sortingLayerID=reference.sortingLayerID;coat.sortingOrder=reference.sortingOrder+1;
        coat.color=new Color(tint.r,tint.g,tint.b,(.075f+burst*.25f)*end);
        Vector3 right=reference.transform.right,up=reference.transform.up;
        float width=Vector3.Distance(BodyPoint(-.5f,.5f),BodyPoint(.5f,.5f));
        float height=Vector3.Distance(BodyPoint(0,0),BodyPoint(0,1));
        Vector3 middle=BodyPoint(0,.5f),foot=BodyPoint(0,.06f);int count=0;
        for(int i=0;i<buffer.Length;i++)
        {
            float seed=Seed(i),a=Seed(i+113)*Mathf.PI*2f;
            Vector3 at;float alpha;
            if(i<40&&t<.55f)
            {
                float lift=Mathf.Clamp01(t/.42f);
                at=foot+right*Mathf.Cos(a)*width*(.55f-.2f*lift)+up*(lift*height*(.7f+seed*.6f));
                alpha=burst*.85f;
            }
            else if(kind==0||kind==5)
            {
                int segment=i%3;float along=(i/3%20)/19f;
                a=segment*Mathf.PI*2f/3f+(along-.5f)*1.35f;
                at=middle+right*Mathf.Cos(a)*width*.62f+up*Mathf.Sin(a)*height*.59f;
                alpha=(.35f+Mathf.Sin(along*Mathf.PI)*.35f)*Mathf.Clamp01(t/.16f);
            }
            else if(kind==2)
            {
                a+=t*4f;float lift=Mathf.Repeat(seed+t*.5f,1f);
                at=foot+right*Mathf.Cos(a)*width*.56f+Vector3.forward*Mathf.Sin(a)*width*.24f+up*lift*height*.46f;
                alpha=Mathf.Sin(lift*Mathf.PI)*.7f;
            }
            else if(kind==4)
            {
                if(i>=32)continue;
                float lift=Mathf.Repeat(seed+t*.35f,1f);
                at=foot+right*Mathf.Cos(a)*width*.48f+up*lift*height*.38f;
                alpha=Mathf.Sin(lift*Mathf.PI)*.5f;
            }
            else if(kind==3)
            {
                float drop=Mathf.Repeat(seed+t*.32f,1f);
                at=middle+right*Mathf.Cos(a)*width*.58f+up*(.62f-drop)*height;
                alpha=Mathf.Sin(drop*Mathf.PI)*.65f;
            }
            else
            {
                float lift=Mathf.Clamp01(t/.8f);
                at=foot+right*Mathf.Cos(a)*width*.48f+up*(lift+seed*.25f)*height;
                alpha=(1f-lift)*.7f;
            }
            Put(count++,at,.04f+seed*.035f,alpha*end,i%5==0?.7f:.15f);
        }
        particles.SetParticles(buffer,count);
    }

    private void DrawReturn(float t)
    {
        Vector3 destination=BodyPoint(0,.35f),control=(center+destination)*.5f+Vector3.up*.95f;
        int count=0;
        if(t<.4f)
            for(int i=0;i<12;i++)
            {
                float p=(t-(i%4)*.004f-(i/4)*.045f)/.3f;
                if(p<0f||p>1f)continue;
                Vector3 at=(1f-p)*(1f-p)*center+2f*(1f-p)*p*control+p*p*destination;
                at+=reference.transform.right*Mathf.Sin(p*Mathf.PI)*(i/4-1f)*.045f;
                float trail=1f-(i%4)/4f;
                Put(count++,at,.035f+trail*.045f,trail*.85f,.3f+trail*.6f);
            }
        if(t>=.3f)
        {
            if(arrived!=null){Action callback=arrived;arrived=null;callback();}
            float p=Mathf.Clamp01((t-.3f)/.35f);
            for(int i=0;i<28;i++)
            {
                float seed=Seed(i),a=Seed(i+17)*Mathf.PI*2f;
                Vector3 at=BodyPoint(0,.08f)+reference.transform.right*Mathf.Cos(a)*(.12f+p*.2f)
                    +reference.transform.up*(p*(.35f+seed*.5f));
                Put(count++,at,.035f+seed*.035f,(1f-p)*.85f,.5f);
            }
        }
        particles.SetParticles(buffer,count);
    }

    private void DrawGuard(float t)
    {
        float grow=1f-Mathf.Pow(1f-Mathf.Clamp01(t/.065f),3f);
        float fade=Mathf.Clamp01((t-.15f)/Mathf.Max(.01f,duration-.15f));
        float press=Mathf.Sin(Mathf.Clamp01(t/.16f)*Mathf.PI)*(.1f+guardStrength*.12f);
        float size=Mathf.Clamp(Vector3.Distance(BodyPoint(-.5f,.5f),BodyPoint(.5f,.5f)),.65f,1.6f);
        Vector3 right=reference.transform.right,up=reference.transform.up;
        Vector3 middle=BodyPoint(0,.46f)+guardDirection*size*(.48f-press);
        int count=0,seedIndex=0;
        // Pixel-wide antler trunks and tapered prongs form the wall itself.
        for(int side=-1;side<=1;side+=2)
            foreach(var branch in AntlerBranches)
                for(int segment=1;segment<branch.Length;segment++)
                {
                    Vector2 a=branch[segment-1],b=branch[segment];
                    int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/.045f);
                    for(int step=0;step<=steps;step++)
                    {
                        Vector2 p=Vector2.Lerp(a,b,step/(float)steps);p.x*=side;
                        float seed=Seed(seedIndex++),spread=fade*fade;
                        Vector3 at=middle+right*(Mathf.Round(p.x/.04f)*.04f*size*grow)
                            +up*(Mathf.Round(p.y/.04f)*.04f*size*(1f-press)*grow);
                        at+=(guardDirection*(.1f+seed*.7f)+right*(seed-.5f)*.65f+up*seed*.45f)*spread;
                        float taper=1f-Mathf.Clamp01((p.y-.35f)/.55f)*.5f;
                        Put(count++,at,size*.09f*taper,(.62f+guardStrength*.22f)*(1f-fade)*grow,.25f);
                        Put(count++,at,size*.043f*taper,(1f-fade)*grow*.85f,.85f);
                    }
                }
        for(int i=0;i<36;i++)
        {
            float seed=Seed(i+57),a=Seed(i+193)*Mathf.PI*2f;
            float p=Mathf.Clamp01((t-.055f)/duration);
            Vector3 at=middle+right*Mathf.Cos(a)*size*(.18f+p*.52f)+up*Mathf.Sin(a)*size*(.22f+p*.45f)
                +guardDirection*p*size*(.3f+guardStrength*.45f);
            Put(count++,at,size*(.035f+seed*.035f)*(1f-p*.5f),grow*(1f-p)*(.5f+guardStrength*.4f),.55f);
        }
        particles.SetParticles(buffer,count);
    }

    public void PrepareBattleEndFade()
    {
        if(coat!=null)coat.transform.SetParent(transform,true);
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying){if(coat!=null)Destroy(coat.gameObject);Destroy(gameObject);}
        else{if(coat!=null)DestroyImmediate(coat.gameObject);DestroyImmediate(gameObject);}
    }
    private void OnDestroy(){if(coat!=null){if(Application.isPlaying)Destroy(coat.gameObject);else DestroyImmediate(coat.gameObject);}}
}
