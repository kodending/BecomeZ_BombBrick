using System;
using System.Collections.Generic;
using UnityEngine;

// Visuals only. The combat cast owns the single damage pulse and its clock.
[DefaultExecutionOrder(10010)]
public sealed class PixelNinjaSpacetime:MonoBehaviour
{
    private const float End=NinjaSpacetimeRules.Windup+.60f;
    private const string ArtRoot="CombatCinematics/NinjaSpacetimeV1/";
    private static Sprite[] motionArt,riftArt;
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private SpriteRenderer reference;
    private Bounds arena,sourceBody;
    private Bounds[] bodies=Array.Empty<Bounds>();
    private Mesh mesh;
    private Material material;
    private MeshRenderer output;
    private Quaternion facing;
    private readonly List<Vector3> vertices=new List<Vector3>(4096);
    private readonly List<Color> colors=new List<Color>(4096);
    private readonly List<int> triangles=new List<int>(6144);
    private float age,retireAge;
    private bool manual=true,impacted,retiring;
    private readonly List<SpriteRenderer> fissures=new List<SpriteRenderer>(),bursts=new List<SpriteRenderer>();
    private Sprite originalSprite;
    private Vector3 originalScale,originalPosition,feetInParent;
    private float bodyScale;
    private int speciesRow;
    private bool bodyPose;
    public float Age=>age;
    public float RemainingEndTime=>Mathf.Max(0f,retiring?.24f-retireAge:End-age);

    public static PixelNinjaSpacetime Create(Bounds arena,SpriteRenderer reference,Bounds[] targets=null,string species=null)
    {
        if(reference==null)return null;
        var effect=new GameObject("Ninja Spacetime Collapse").AddComponent<PixelNinjaSpacetime>();
        effect.arena=arena;effect.reference=reference;effect.sourceBody=reference.bounds;
        effect.bodies=targets??Array.Empty<Bounds>();
        effect.facing=Camera.main==null?reference.transform.rotation:Camera.main.transform.rotation;
        effect.material=new Material(Shader.Find("Sprites/Default")){name="Ninja angular space fragments",hideFlags=HideFlags.DontSave};
        effect.mesh=new Mesh{name="Pixel space fractures",hideFlags=HideFlags.DontSave};effect.mesh.MarkDynamic();
        effect.gameObject.AddComponent<MeshFilter>().sharedMesh=effect.mesh;
        effect.output=effect.gameObject.AddComponent<MeshRenderer>();effect.output.sharedMaterial=effect.material;
        effect.output.sortingLayerID=reference.sortingLayerID;effect.output.sortingOrder=reference.sortingOrder+25;
        if(riftArt==null||riftArt[0]==null)riftArt=Atlas("Rift",4,2,Vector2.one*.5f);
        if(!string.IsNullOrEmpty(species))effect.BindBody(species);
        effect.Pose(0f);return effect;
    }

    public void Pose(float time)
    {
        if(this==null||float.IsNaN(time)||float.IsInfinity(time))return;
        age=Mathf.Clamp(time,0f,End);Draw();ApplyBodyPose();
    }

    public void Impact(Bounds[] targets)
    {
        if(this==null||impacted)return;
        impacted=true;bodies=targets??Array.Empty<Bounds>();age=Mathf.Max(age,NinjaSpacetimeRules.Windup);Draw();
    }

    public void Release(){if(this!=null){RestoreBody();manual=false;if(!impacted)Cancel();}}
    public void PrepareEndPlayback()
    {
        if(reference!=null)sourceBody=reference.bounds;
        RestoreBody();
        reference=null;manual=false;
        if(!impacted){retiring=true;retireAge=0f;}
    }
    public void AdvanceEnd(float dt)
    {
        if(this==null)return;
        if(retiring)retireAge+=Mathf.Max(0f,dt);else age+=Mathf.Max(0f,dt);
        if(RemainingEndTime<=0f){Clear();return;}Draw();
    }
    private void Update()
    {
        if(manual)return;
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            AdvanceEnd(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
        }
    }
    // The animation driver runs at 10000. Apply the authored pose afterward without
    // changing its visibility flags, which belong to the cinematic handoff.
    private void LateUpdate(){ApplyBodyPose();}

    private void BindBody(string species)
    {
        if(reference==null||reference.sprite==null)return;
        if(motionArt==null||motionArt[0]==null)motionArt=Atlas("Motion",8,4,new Vector2(.5f,8f/128f));
        if(motionArt==null)return;
        speciesRow=species=="seolmyo"?1:species=="sundongi"?2:species=="hongryeon"?3:0;
        originalSprite=reference.sprite;originalScale=reference.transform.localScale;originalPosition=reference.transform.localPosition;
        Bounds visible=PixelMantisReaper.VisibleBody(originalSprite);
        Vector3 foot=new Vector3(reference.flipX?-visible.center.x:visible.center.x,
            reference.flipY?-visible.max.y:visible.min.y,0f);
        Vector3 worldFeet=reference.transform.TransformPoint(foot);
        feetInParent=reference.transform.parent==null?worldFeet:reference.transform.parent.InverseTransformPoint(worldFeet);
        float height=visible.size.y*Mathf.Abs(reference.transform.lossyScale.y);
        float parentScale=reference.transform.parent==null?1f:Mathf.Abs(reference.transform.parent.lossyScale.y);
        // One common row scale, never fit the changing pose's bounding box.
        bodyScale=height/Mathf.Max(.001f,PixelMantisReaper.VisibleBody(motionArt[speciesRow*8]).size.y*parentScale);
        bodyPose=true;
    }
    public static int BodyFrame(float time)=>time<.10f?0:time<.34f?1:time<.60f?2:time<.86f?3:
        time<.96f?4:time<NinjaSpacetimeRules.Windup?5:time<1.28f?6:7;
    private void ApplyBodyPose()
    {
        if(!bodyPose||reference==null)return;
        reference.sprite=motionArt[speciesRow*8+BodyFrame(age)];
        reference.transform.localScale=new Vector3(bodyScale*(originalScale.x<0f?-1f:1f),bodyScale,originalScale.z);
        reference.transform.localPosition=feetInParent;
    }
    private void RestoreBody()
    {
        if(!bodyPose)return;bodyPose=false;
        if(reference==null)return;
        reference.sprite=originalSprite;reference.transform.localScale=originalScale;reference.transform.localPosition=originalPosition;
    }
    private static Sprite[] Atlas(string name,int columns,int rows,Vector2 pivot)
    {
        Texture2D texture=Resources.Load<Texture2D>(ArtRoot+name);if(texture==null)return null;
        int width=texture.width/columns,height=texture.height/rows;var frames=new Sprite[columns*rows];
        for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
        {
            var sprite=Sprite.Create(texture,new Rect(col*width,(rows-1-row)*height,width,height),pivot,128f);
            sprite.name="Ninja "+name+" "+row+" "+col;sprite.hideFlags=HideFlags.HideAndDontSave;frames[row*columns+col]=sprite;
        }
        return frames;
    }

    private SpriteRenderer ArtPart(string name)
    {
        var part=new GameObject(name);part.transform.SetParent(transform,false);
        var renderer=part.AddComponent<SpriteRenderer>();renderer.sortingLayerID=output.sortingLayerID;
        renderer.sortingOrder=output.sortingOrder-1;return renderer;
    }
    private void DrawRifts(float charge,float after,float alpha)
    {
        if(riftArt==null)return;
        while(fissures.Count<bodies.Length)
        {fissures.Add(ArtPart("Authored dark pixel fissure"));bursts.Add(ArtPart("Authored angular collapse burst"));}
        for(int i=0;i<fissures.Count;i++)
        {
            bool shown=i<bodies.Length&&alpha>.001f;
            fissures[i].enabled=shown;bursts[i].enabled=shown&&impacted;
            if(!shown)continue;
            var crack=fissures[i];crack.sprite=riftArt[Mathf.Clamp(Mathf.FloorToInt(charge*4f),0,3)];
            crack.transform.SetPositionAndRotation(bodies[i].center,facing);
            float height=Mathf.Max(.6f,bodies[i].size.y)*(impacted?1.65f:.35f+charge*1.05f);
            crack.transform.localScale=Vector3.one*height;crack.color=new Color(1f,1f,1f,alpha*(impacted?.65f:charge*.9f));
            var burst=bursts[i];burst.sprite=riftArt[after<.13f?4:5];
            burst.transform.SetPositionAndRotation(bodies[i].center,facing);
            burst.transform.localScale=Vector3.one*height*(1f+after*1.7f);
            burst.color=new Color(1f,1f,1f,alpha);
        }
    }

    private void Draw()
    {
        vertices.Clear();colors.Clear();triangles.Clear();
        if(reference!=null)sourceBody=reference.bounds;
        Vector3 right=facing*Vector3.right,up=facing*Vector3.up;
        float charge=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/NinjaSpacetimeRules.Windup));
        float after=Mathf.Max(0f,age-NinjaSpacetimeRules.Windup);
        float alpha=retiring?1f-Mathf.Clamp01(retireAge/.24f):impacted?1f-Mathf.Clamp01(after/.60f):1f;
        DrawRifts(charge,after,alpha);
        if(!impacted)
        {
            Crack(sourceBody.center+up*sourceBody.size.y*.28f,sourceBody.size.y*.46f*charge,.025f,charge*alpha,31);
            for(int i=0;i<bodies.Length;i++)
                Crack(bodies[i].center,Mathf.Max(.5f,bodies[i].size.y)*(.2f+.9f*charge),.018f,charge*.55f*alpha,i+7);
        }
        else
        {
            float flash=1f-Mathf.Clamp01(after/.085f);
            for(int i=0;i<bodies.Length;i++)
            {
                float height=Mathf.Max(.6f,bodies[i].size.y);
                Crack(bodies[i].center,height*1.55f,.045f+flash*.11f,alpha,i+7);
                for(int shard=0;shard<10;shard++)
                {
                    float angle=Hash(i*59+shard*13)*Mathf.PI*2f;
                    Vector3 direction=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                    float travel=after*(1.8f+Hash(i*17+shard)*3f);
                    Vector3 shardCenter=bodies[i].center+direction*travel;
                    float size=height*(.045f+Hash(shard*23+i)*.08f)*(1f-after/.75f);
                    Vector3 x=right*size,y=up*size*(1.4f+Hash(shard+11));
                    Color color=shard%3==0?new Color(1f,.94f,.76f,alpha):new Color(.72f,.47f,1f,alpha*.8f);
                    Quad(shardCenter-x-y,shardCenter+x-y*.4f,shardCenter+x*.5f+y,shardCenter-x*.8f+y*.6f,color);
                }
            }
            Vector3 center=arena.center+up*.12f;
            float spread=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(after/.22f));
            float width=Mathf.Max(1f,arena.size.x)*(.12f+.48f*spread);
            Stroke(center-right*width-up*.24f,center+right*width+up*.24f,.025f+flash*.13f,new Color(.93f,.86f,1f,alpha*.6f));
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        output.enabled=vertices.Count>0;
    }

    private void Crack(Vector3 center,float height,float width,float alpha,int seed)
    {
        if(height<=0f||width<=0f||alpha<=0f)return;
        Vector3 up=facing*Vector3.up,right=facing*Vector3.right;
        Vector3 previous=center-up*height*.5f;
        for(int i=1;i<=6;i++)
        {
            Vector3 next=center+up*((i/6f-.5f)*height)+right*((i==6?0f:Hash(seed*17+i)-.5f)*height*.17f);
            Stroke(previous,next,width*2.8f,new Color(.12f,.045f,.21f,alpha*.8f));
            Stroke(previous,next,width,new Color(.98f,.86f,1f,alpha));previous=next;
        }
    }
    private void Stroke(Vector3 a,Vector3 b,float width,Color color)
    {
        Vector3 side=Vector3.Cross(b-a,facing*Vector3.forward).normalized*width*.5f;
        Quad(a-side,b-side,b+side,a+side,color);
    }
    private static Vector3 Pixel(Vector3 point)=>new Vector3(Mathf.Round(point.x/.025f)*.025f,
        Mathf.Round(point.y/.025f)*.025f,Mathf.Round(point.z/.025f)*.025f);
    private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    {
        int start=vertices.Count;vertices.Add(Pixel(a));vertices.Add(Pixel(b));vertices.Add(Pixel(c));vertices.Add(Pixel(d));
        for(int i=0;i<4;i++)colors.Add(color);
        triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
    }
    private static float Hash(int seed)=>Mathf.Repeat(Mathf.Sin(seed*12.9898f+78.233f)*43758.5453f,1f);
    public void Cancel()
    {
        RestoreBody();
        if(this==null||CombatEffectEndFade.IsFading(this)||CombatEffectEndPlayback.IsPreserving(this))return;
        Clear();
    }
    public void Clear()
    {
        if(this==null)return;
        RestoreBody();
        gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void OnDestroy()
    {
        RestoreBody();
        if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
    }
}
