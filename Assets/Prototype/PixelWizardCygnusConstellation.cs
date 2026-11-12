using System;
using UnityEngine;

// Cygnus-inspired wings: center, left tip and right tip are the three weapons.
public sealed class PixelWizardCygnusConstellation:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private const int DotsPerEdge=8;
    private static readonly Vector2[] Points={
        new Vector2(0f,.22f),new Vector2(-1.22f,.72f),new Vector2(1.22f,.72f),
        new Vector2(0f,.82f),new Vector2(0f,-.25f),
        new Vector2(-.4f,.35f),new Vector2(-.8f,.65f),new Vector2(-.75f,-.12f),
        new Vector2(.4f,.35f),new Vector2(.8f,.65f),new Vector2(.75f,-.12f)};
    private static readonly int[,] Edges={{0,3},{0,4},{0,5},{5,6},{6,1},{1,7},{7,5},
        {0,8},{8,9},{9,2},{2,10},{10,8}};
    private readonly SpriteRenderer[] stars=new SpriteRenderer[11],dots=new SpriteRenderer[96],
        cores=new SpriteRenderer[3],sparks=new SpriteRenderer[12];
    private readonly float[] revealAges={10f,10f,10f},shotAges={10f,10f,10f};
    private Transform source;
    private SpriteRenderer reference;
    private Func<bool> valid;
    private Sprite dotSprite;
    private float age,completeAge=10f,charge;
    private int count,chargingIndex;

    public static PixelWizardCygnusConstellation Create(Transform source,SpriteRenderer reference,Func<bool> valid)
    {
        var effect=new GameObject("Wizard Cygnus Star Wings").AddComponent<PixelWizardCygnusConstellation>();
        effect.source=source;effect.reference=reference;effect.valid=valid;
        effect.dotSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1f);
        for(int i=0;i<effect.stars.Length;i++)
            effect.stars[i]=effect.CreateSprite(i<3?"Cygnus Attack Star "+(i+1):"Cygnus Outline Star "+i,PixelStarProjectile.SharedSprite,1);
        for(int i=0;i<effect.dots.Length;i++)effect.dots[i]=effect.CreateSprite("Cygnus Pixel Link",effect.dotSprite,0);
        for(int i=0;i<3;i++)effect.cores[i]=effect.CreateSprite("Cygnus Gold Core "+i,PixelStarProjectile.SharedSprite,2);
        for(int i=0;i<effect.sparks.Length;i++)effect.sparks[i]=effect.CreateSprite("Cygnus Shot Spark",effect.dotSprite,2);
        effect.UpdateVisual();return effect;
    }

    private SpriteRenderer CreateSprite(string name,Sprite sprite,int order)
    {
        var renderer=new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(transform,false);renderer.sprite=sprite;
        renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;
        // Wings sit behind their caster, including the lower feather tips.
        renderer.sortingOrder=(reference==null?10:reference.sortingOrder)-3+order;
        return renderer;
    }

    public void SetStars(int value)
    {
        value=Mathf.Clamp(value,0,3);
        for(int i=count;i<value;i++)revealAges[i]=0f;
        if(value==3&&count<3)completeAge=0f;
        count=value;UpdateVisual();
    }
    public void Charge(int index,float amount){chargingIndex=index;charge=Mathf.Clamp01(amount);}
    public Vector3 Fire(int index)
    {
        shotAges[index]=0f;charge=0f;UpdateVisual();return stars[index].transform.position;
    }
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(source==null||!source.gameObject.activeInHierarchy||(valid!=null&&!valid())){Cancel();return;}
            float dt=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);age+=dt;completeAge+=dt;
            for(int i=0;i<3;i++){shotAges[i]+=dt;revealAges[i]+=dt;}
            UpdateVisual();
        }
    }

    private static int Wing(int point)=>point==1||point>=5&&point<=7?1:point==2||point>=8?2:0;
    private float Reveal(int wing,float distance)=>wing>=count?0f:
        Mathf.SmoothStep(0f,1f,(revealAges[wing]-distance*.22f)/.3f);

    private void UpdateVisual()
    {
        if(source==null)return;
        Camera camera=Camera.main;
        Quaternion facing=camera==null?Quaternion.identity:camera.transform.rotation;
        Vector3 up=facing*Vector3.up,right=facing*Vector3.right;
        float height=reference==null?1f:Mathf.Clamp(reference.bounds.size.y,.8f,1.6f);
        float width=reference==null?height:Mathf.Clamp(reference.bounds.size.x,height*.8f,height*1.25f);
        Vector3 center=(reference==null?source.position:reference.bounds.center)+up*height*(.5f+Mathf.Sin(age*1.5f)*.035f);
        transform.position=center;
        float unfurl=Mathf.Sin(Mathf.Clamp01(completeAge/.65f)*Mathf.PI)*.1f;
        float breath=Mathf.Sin(age*1.1f)*.025f;
        float flare=Mathf.Max(0f,1f-completeAge/.7f);
        for(int i=0;i<stars.Length;i++)
        {
            var star=stars[i];int wing=Wing(i);Vector2 point=Points[i];
            float reveal=Reveal(wing,Mathf.Abs(point.x));
            float spread=wing==0?1f:Mathf.Lerp(.78f,1f,reveal)+unfurl+breath;
            star.transform.position=center+right*(point.x*width*spread)+up*(point.y*height+Mathf.Abs(point.x)*height*(unfurl+breath));
            star.transform.rotation=facing;
            bool active=i<3&&i<count;
            float pulse=active?Mathf.Max(0f,1f-shotAges[i]/.24f):0f;
            float charging=active&&i==chargingIndex?charge:0f;
            float size=active?.85f:.28f;
            star.transform.localScale=Vector3.one*height*size*(1f+charging*.25f+pulse*.65f+flare*.2f)*Mathf.Lerp(.55f,1f,reveal);
            Color color=Color.Lerp(new Color(.5f,.42f,1f),new Color(.65f,.9f,1f),reveal);
            if(active)color=Color.Lerp(color,Color.white,charging*.7f+pulse*.7f+flare*.4f);
            color.a=Mathf.Lerp(.08f,active?1f:.85f,reveal);star.color=color;
            if(i>=3)continue;
            var core=cores[i];core.transform.position=star.transform.position;core.transform.rotation=facing;
            core.transform.localScale=star.transform.localScale*.38f;
            core.color=new Color(1f,.84f,.4f,reveal);core.enabled=active;
            for(int j=0;j<4;j++)
            {
                var spark=sparks[i*4+j];float t=Mathf.Clamp01(shotAges[i]/.24f);
                float angle=(45f+90f*j)*Mathf.Deg2Rad;
                spark.transform.position=star.transform.position+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*height*(.1f+t*.27f);
                spark.transform.rotation=facing;spark.transform.localScale=Vector3.one*height*.045f*(1f-t*.5f);
                spark.color=new Color(.8f,.93f,1f,1f-t);spark.enabled=active&&t<1f;
            }
        }
        for(int i=0;i<dots.Length;i++)
        {
            var dot=dots[i];int edge=i/DotsPerEdge,a=Edges[edge,0],b=Edges[edge,1];
            int wing=Mathf.Max(Wing(a),Wing(b));float t=(i%DotsPerEdge+1f)/(DotsPerEdge+1f);
            Vector2 point=Vector2.Lerp(Points[a],Points[b],t);
            float distance=Mathf.Abs(point.x),reveal=Reveal(wing,distance);
            float completion=Mathf.Max(0f,1f-Mathf.Abs(completeAge*2.8f-distance)/.22f);
            float charging=wing==chargingIndex&&wing<count?charge:0f;
            float path=wing==0?Mathf.Abs(point.y-Points[0].y)/.6f:distance/1.22f;
            float flow=charging*Mathf.Max(0f,1f-Mathf.Abs(path-charging)/.22f);
            dot.transform.position=Vector3.Lerp(stars[a].transform.position,stars[b].transform.position,t);
            dot.transform.rotation=facing;dot.transform.localScale=Vector3.one*height*(.055f+flow*.025f+completion*.025f);
            Color color=Color.Lerp(new Color(.48f,.35f,1f),new Color(.65f,.88f,1f),flow+completion);
            color.a=Mathf.Clamp01(Mathf.Lerp(.035f,.78f,reveal)+flow*.3f+completion*.3f);dot.color=color;
        }
    }
    private void OnDestroy()
    {
        if(dotSprite==null)return;
        if(Application.isPlaying)Destroy(dotSprite);else DestroyImmediate(dotSprite);
    }
}
