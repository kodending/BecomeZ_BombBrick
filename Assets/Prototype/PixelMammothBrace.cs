using System.Collections.Generic;
using UnityEngine;

// The shell follows the authored monster frame; sharing travels on the same paused combat clock.
[DefaultExecutionOrder(10020)]
public sealed class PixelMammothBrace:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private struct Mote {public Vector3 at,travel;public float birth,life,size;public Color color;public bool chip;}
    private struct Transfer {public Vector3 from;public float birth,duration;}
    private readonly List<Transfer> transfers=new List<Transfer>();
    private readonly SpriteRenderer[] shellEdges=new SpriteRenderer[4];
    private static readonly Vector2[] EdgeDirections={Vector2.left,Vector2.right,Vector2.up,Vector2.down};
    private SpriteRenderer reference,coat;
    private SpriteMonsterAnimationDriver driver;
    private Material shellMaterial,edgeMaterial;
    public readonly List<Vector3> allies=new List<Vector3>();
    private readonly List<Mote> motes=new List<Mote>();
    private readonly ParticleSystem.Particle[] dustBuffer=new ParticleSystem.Particle[256],chipBuffer=new ParticleSystem.Particle[256];
    private ParticleSystem dust,chips;
    private Vector3 ground,forward;
    private Color tint;
    private float clock,load,radiusScale;
    private float lastContact=-10f;
    private bool planted;
    public System.Action<Vector3,float> guardContact;

    public static PixelMammothBrace Play(SpriteRenderer reference,string species,Vector3 ground,Vector3 direction,float scale)
    {
        var fx=new GameObject("Pixel Mammoth Mountain Brace").AddComponent<PixelMammothBrace>();
        fx.ground=ground;fx.forward=direction;fx.radiusScale=scale;fx.tint=PixelMammothWallRush.HitColor(species);
        fx.reference=reference;
        if(reference!=null)
        {
            fx.driver=reference.GetComponent<SpriteMonsterAnimationDriver>();
            fx.shellMaterial=new Material(Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette"))
                {name="Mountain Stand Outer Hide",hideFlags=HideFlags.HideAndDontSave};
            fx.shellMaterial.SetFloat("_KeepSpriteDetail",1f);
            fx.edgeMaterial=new Material(fx.shellMaterial){name="Mountain Stand Hide Rim",hideFlags=HideFlags.HideAndDontSave};
            fx.edgeMaterial.SetFloat("_OutlineOnly",1f);
            for(int i=0;i<4;i++)fx.shellEdges[i]=fx.MakeShell("Solid Hide Edge "+i);
            foreach(var edge in fx.shellEdges)edge.sharedMaterial=fx.edgeMaterial;
            fx.coat=fx.MakeShell("Translucent Guard Hide");
        }
        fx.dust=PixelMushroomGas.CreateParticles("Braced Hoof Earth",fx.transform,256,PixelMushroomGas.Material(false),reference,0,true);
        fx.chips=PixelMushroomGas.CreateParticles("Tusk And Protection Debris",fx.transform,256,PixelMushroomGas.Material(true),reference,1,true);
        return fx;
    }

    private SpriteRenderer MakeShell(string name)
    {
        var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);
        r.sharedMaterial=shellMaterial;r.enabled=false;return r;
    }

    private Vector3 ShellCenter
    {
        get
        {
            if(reference==null||reference.sprite==null)return ground+Vector3.up*.65f;
            Vector3 center=PixelMantisReaper.VisibleBody(reference.sprite).center;
            if(reference.flipX)center.x=-center.x;if(reference.flipY)center.y=-center.y;
            return reference.transform.TransformPoint(center);
        }
    }
    private Vector3 ShellContact(Vector3 from)
    {
        Vector3 center=ShellCenter;
        if(reference==null||reference.sprite==null)return center;
        // Project the incoming direction onto the visible sprite, so rear/front hits also read on screen.
        Vector3 right=reference.transform.right,up=reference.transform.up,delta=from-center;
        Vector2 direction=new Vector2(Vector3.Dot(delta,right),Vector3.Dot(delta,up));
        if(direction.sqrMagnitude<.001f)direction=Vector2.right;
        direction.Normalize();
        Vector3 size=Vector3.Scale(PixelMantisReaper.VisibleBody(reference.sprite).size,reference.transform.lossyScale);
        return center+right*direction.x*Mathf.Abs(size.x)*.46f+up*direction.y*Mathf.Abs(size.y)*.43f;
    }

    private void RenderShell()
    {
        if(reference==null||reference.sprite==null)return;
        float visible=Mathf.Clamp01(clock/MammothRank3Rules.BraceWindup)*
            (1-Mathf.Clamp01((clock-MammothRank3Rules.BraceGuardEnd)/.25f));
        float impact=Mathf.Pow(1-Mathf.Clamp01((clock-lastContact)/.22f),2);
        Color color=Color.Lerp(tint,new Color(1f,.98f,.82f),.25f+impact*.65f);
        for(int i=0;i<=shellEdges.Length;i++)
        {
            SpriteRenderer shell=i==shellEdges.Length?coat:shellEdges[i];bool edge=i<shellEdges.Length;
            shell.sprite=reference.sprite;shell.flipX=reference.flipX;shell.flipY=reference.flipY;
            shell.transform.SetPositionAndRotation(reference.transform.position,reference.transform.rotation);
            shell.transform.localScale=reference.transform.lossyScale;
            if(edge)
            {
                const float expansion=1.10f;
                shell.transform.localScale*=expansion;
                shell.transform.position+=(shell.transform.position-ShellCenter)*(expansion-1f)+
                    reference.transform.TransformVector((Vector3)EdgeDirections[i]*(1.5f+impact)/reference.sprite.pixelsPerUnit);
            }
            color.a=visible*reference.color.a*(edge?.85f+impact*.15f:.2f+impact*.3f);
            shell.color=color;shell.enabled=reference.enabled&&color.a>.005f;
            shell.sortingLayerID=reference.sortingLayerID;shell.sortingOrder=reference.sortingOrder+(edge?2:1);
        }
    }

    public void Pose(float time,Vector3 direction,float strength)
    {
        if(this==null)return;clock=time;forward=direction;load=strength;
        if(!planted&&time>=MammothRank3Rules.BraceWindup){planted=true;Absorb(time);}
        Render();
    }
    public void Absorb(float time)
    {
        if(this==null)return;
        Vector3 side=Vector3.Cross(Vector3.up,forward);
        for(int i=0;i<18;i++)
        {
            float seed=Mathf.Repeat(i*.754878f+.13f,1);
            Add(ground+side*(i%2==0?.42f:-.42f)+Vector3.up*.04f,
                -forward*(.4f+seed*.45f)+side*(seed-.5f)*.7f+Vector3.up*(.1f+seed*.18f),
                time,.28f,.08f+seed*.12f,MeadowAmbientParticles.TerrainColor(Color.Lerp(new Color(.5f,.33f,.18f,.85f),tint,seed*.35f)),i%3==0);
        }
    }
    public void ProtectedHit(Vector3 point,float time)
    {
        if(this==null)return;
        // A compact moving packet, never a persistent ally-to-caster line.
        if(transfers.Count>=24)transfers.RemoveAt(0);
        transfers.Add(new Transfer{from=point,birth=time,duration=.22f});
        for(int i=0;i<12;i++)
        {
            float a=i*2.399963f;
            Vector3 offset=new Vector3(Mathf.Cos(a)*.28f,.15f+Mathf.Sin(a)*.2f,Mathf.Sin(a)*.2f);
            Add(point+offset,offset*.6f+Vector3.up*.16f,time,.24f,.055f+i%3*.018f,new Color(1,.88f,.55f),true);
        }
    }
    public void GuardHit(Vector3 from,float time)
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null)return;
            lastContact=time;Vector3 contact=ShellContact(from),normal=(contact-ShellCenter).normalized;
            guardContact?.Invoke(contact,time);
            Vector3 right=reference==null?Vector3.right:reference.transform.right;
            Vector3 up=reference==null?Vector3.up:reference.transform.up;
            // The bright contact stays on the hide while short chips rebound away from it.
            Add(contact,Vector3.zero,time,.13f,.34f,Color.Lerp(tint,Color.white,.85f),true);
            for(int i=0;i<24;i++)
            {
                float a=i*2.399963f,seed=Mathf.Repeat(i*.754878f,1f);
                Vector3 spread=normal*(.28f+seed*.3f)+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*(.12f+seed*.33f);
                Add(contact,spread,time,.18f+seed*.1f,.055f+seed*.08f,Color.Lerp(tint,Color.white,seed*.8f),true);
            }
            Absorb(time);
            driver?.PlayMammothBraceImpact(-normal);
        }
    }
    public void Sweep(float time)
    {
        if(this==null)return;
        Vector3 side=Vector3.Cross(Vector3.up,forward);
        float radius=MonsterAutoBattlePrototype.TileSize*MammothRank3Rules.BraceSweepTiles*radiusScale;
        for(int i=0;i<132;i++)
        {
            float seed=Mathf.Repeat(i*.754878f+.13f,1),a=(Mathf.Repeat(i*.618034f,1)-.5f)*Mathf.PI*2/3;
            bool chip=i<88;
            float distance=radius*(chip&&i%3==0?1f:.25f+Mathf.Sqrt(seed)*.75f);
            Vector3 travel=(forward*Mathf.Cos(a)+side*Mathf.Sin(a))*distance;
            travel.y=(chip?.25f:.12f)+seed*(chip?.6f:.18f);
            Color color=MeadowAmbientParticles.TerrainColor(
                Color.Lerp(new Color(.58f,.4f,.22f,chip?1f:.68f),tint,chip?.4f+seed*.6f:.32f));
            Add(ground+Vector3.up*.08f,travel,time,.28f+seed*.08f,
                (chip?.07f+seed*.08f:.23f+seed*.2f)*(1+load*.2f),color,chip);
        }
        Render();
    }
    public void Hit(Vector3 point,float time)
    {
        if(this==null)return;
        Quaternion view=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        for(int i=0;i<16;i++)
        {
            float a=i*2.399963f;
            Add(point,view*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(.3f+i%3*.1f),
                time,.18f,.06f+i%3*.02f,Color.Lerp(tint,Color.white,.6f),true);
        }
        Render();
    }
    private void Add(Vector3 at,Vector3 travel,float time,float life,float size,Color color,bool chip)
    {
        if(motes.Count>=200)motes.RemoveAt(0);
        motes.Add(new Mote{at=at,travel=travel,birth=time,life=life,size=size,color=color,chip=chip});
    }
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(dust!=null)Render();
        }
    }
    private static ParticleSystem.Particle Particle(Vector3 point,float size,Color color,int index)=>
        new ParticleSystem.Particle{position=point,startSize3D=Vector3.one*size,startColor=color,
            startLifetime=10,remainingLifetime=10,rotation=index*31,randomSeed=(uint)(index+1)};

    private void Render()
    {
        int d=0,c=0;
        for(int i=transfers.Count-1;i>=0;i--)
        {
            Transfer transfer=transfers[i];float age=clock-transfer.birth;
            if(age>=transfer.duration)
            {
                transfers.RemoveAt(i);GuardHit(transfer.from,transfer.birth+transfer.duration);continue;
            }
            Vector3 end=ShellContact(transfer.from);
            for(int j=0;j<10&&c<chipBuffer.Length;j++)
            {
                float t=(age-j*.008f)/transfer.duration;if(t<0f)continue;t=Mathf.Clamp01(t);
                Vector3 point=Vector3.Lerp(transfer.from,end,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.28f);
                Color color=Color.Lerp(new Color(1f,.72f,.25f),Color.white,1-j/10f);color.a=1-j*.075f;
                chipBuffer[c++]=Particle(point,j==0?.25f:.17f-j*.011f,color,j);
            }
        }
        RenderShell();
        if(clock<MammothRank3Rules.BraceGuardEnd)
        {
            float appear=Mathf.Clamp01(clock/MammothRank3Rules.BraceWindup);
            Vector3 side=Vector3.Cross(Vector3.up,forward);
            // Earth stays at the planted feet and piles up as actual damage is absorbed.
            for(int i=0;i<28&&c<chipBuffer.Length;i++)
            {
                float seed=Mathf.Repeat(i*.754878f,.999f);
                Vector3 point=ground+side*(seed-.5f)*1.45f+forward*(.22f+Mathf.Sin(i*2.4f)*.23f);
                point.y+=.035f+(i%3)*.035f*load;
                Color color=Color.Lerp(new Color(.45f,.29f,.16f),tint,seed*.35f);color.a=(.4f+load*.3f)*appear;
                chipBuffer[c++]=Particle(point,(.07f+seed*.055f)*(1+load*.5f),color,i);
            }
            float radius=MonsterAutoBattlePrototype.TileSize*MammothRank3Rules.BraceRadiusTiles*radiusScale;
            for(int i=0;i<36&&c<chipBuffer.Length;i++)
            {
                float a=i*2.399963f,seed=Mathf.Repeat(i*.754878f,.999f);
                Vector3 point=ground+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius*(.88f+seed*.1f);
                point.y+=.045f+.03f*Mathf.Sin(clock*2+i);
                Color color=tint;color.a=(.18f+.12f*Mathf.Sin(clock*3+i))*appear;
                chipBuffer[c++]=Particle(point,.045f+seed*.035f,color,i);
            }
            foreach(Vector3 ally in allies)
                for(int i=0;i<4&&c<chipBuffer.Length;i++)
                {
                    float a=i*Mathf.PI*.5f+clock*.5f;
                    var color=new Color(1,.87f,.5f,.45f);
                    chipBuffer[c++]=Particle(ally+new Vector3(Mathf.Cos(a)*.32f,.12f,Mathf.Sin(a)*.32f),.055f,color,i);
                }
        }
        for(int i=motes.Count-1;i>=0;i--)
        {
            Mote mote=motes[i];float age=clock-mote.birth,t=age/mote.life;
            if(t>=1){motes.RemoveAt(i);continue;}
            float spread=1-Mathf.Pow(1-Mathf.Clamp01(age/.16f),3);
            Vector3 point=mote.at+new Vector3(mote.travel.x,0,mote.travel.z)*spread+
                Vector3.up*mote.travel.y*4*t*(1-t);
            Color color=mote.color;color.a*=1-t*t;
            var particle=Particle(point,mote.size,color,i);
            if(mote.chip){if(c<chipBuffer.Length)chipBuffer[c++]=particle;}
            else if(d<dustBuffer.Length)dustBuffer[d++]=particle;
        }
        dust.SetParticles(dustBuffer,d);chips.SetParticles(chipBuffer,c);
    }
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    private void OnDestroy()
    {
        if(shellMaterial==null)return;
        if(Application.isPlaying)Destroy(shellMaterial);else DestroyImmediate(shellMaterial);
        if(edgeMaterial!=null){if(Application.isPlaying)Destroy(edgeMaterial);else DestroyImmediate(edgeMaterial);}
    }
}
