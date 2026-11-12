using System;
using System.Collections.Generic;
using UnityEngine;

// A fixed ground constellation. Combat owns the three pulse times and target checks.
public sealed class PixelWizardPentagramField:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly Vector3[] corners=new Vector3[5];
    private readonly SpriteRenderer[] stars=new SpriteRenderer[5];
    private readonly LineRenderer[] strokes=new LineRenderer[5];
    private readonly SpriteRenderer[] fragments=new SpriteRenderer[15];
    private readonly List<Column> columns=new List<Column>();
    private MaterialPropertyBlock fillTint;
    private static readonly Color Violet=new Color(.65f,.38f,1f);
    private Mesh fillMesh;
    private MeshRenderer fill;
    private Material material,ownedMaterial;
    private Func<bool> valid;
    private float radius,age,drawAge,pulseAge=10f,finishAge;
    private int starCount;
    private bool complete,finishing;

    private sealed class Column
    {
        public LineRenderer beam;
        public SpriteRenderer tip,flash;
        public SpriteRenderer[] sparks;
        public Vector3 foot;
        public float age,height,width;
    }

    public static PixelWizardPentagramField Create(Vector3 center,float radius,
        SpriteRenderer sorting,Material material,Func<bool> valid)
    {
        var field=new GameObject("Wizard Mouse Pentagram Field").AddComponent<PixelWizardPentagramField>();
        field.fillTint=new MaterialPropertyBlock();
        field.transform.position=new Vector3(center.x,.035f,center.z);
        field.radius=radius;field.valid=valid;field.material=material;
        if(field.material==null)
            field.material=field.ownedMaterial=new Material(Shader.Find("Sprites/Default"));
        int layer=sorting==null?0:sorting.sortingLayerID;
        // Above the battlefield backdrop, below standing characters.
        for(int i=0;i<5;i++)
        {
            float angle=(90f+i*144f)*Mathf.Deg2Rad;
            field.corners[i]=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*radius;
            var star=new GameObject("Constellation Corner "+(i+1)).AddComponent<SpriteRenderer>();
            star.transform.SetParent(field.transform,false);
            star.transform.localPosition=field.corners[i];
            star.transform.localRotation=Quaternion.Euler(90f,0f,0f);
            star.sprite=PixelStarProjectile.SharedSprite;
            star.sortingLayerID=layer;star.sortingOrder=3;star.enabled=false;
            field.stars[i]=star;
            field.strokes[i]=field.Line("Constellation Stroke "+(i+1),layer,2);
            field.strokes[i].useWorldSpace=false;
            field.strokes[i].enabled=false;
        }
        field.BuildFill(layer);
        return field;
    }

    public void AddStar(){starCount=Mathf.Min(5,starCount+1);drawAge=0f;UpdateVisual();}
    public void Complete(){complete=true;starCount=5;drawAge=1f;UpdateVisual();}
    public void Pulse(){pulseAge=0f;UpdateVisual();}
    public void Finish()
    {
        finishing=true;finishAge=0f;
        for(int i=0;i<fragments.Length;i++)
        {
            var fragment=new GameObject("Constellation Fragment").AddComponent<SpriteRenderer>();
            fragment.transform.SetParent(transform,false);fragment.sprite=PixelStarProjectile.SharedSprite;
            fragment.sortingLayerID=stars[0].sortingLayerID;fragment.sortingOrder=3;
            fragment.transform.localPosition=Vector3.Lerp(corners[i/3],corners[(i/3+1)%5],(i%3+.5f)/3f);
            fragments[i]=fragment;
        }
    }
    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;Release(gameObject);}

    public void PlayHitColumn(Vector3 foot,SpriteRenderer sorting,bool final)
    {
        var beam=Line(final?"Final Star Column":"Star Column",
            sorting==null?0:sorting.sortingLayerID,(sorting==null?0:sorting.sortingOrder)+3);
        beam.widthCurve=new AnimationCurve(new Keyframe(0f,.7f),new Keyframe(.2f,1f),new Keyframe(1f,0f));
        var column=new Column{beam=beam,foot=foot+Vector3.up*.06f,
            height=final?3.9f:3.1f,width=final?.6f:.4f,
            tip=BurstStar("Popping Star",beam,1),flash=BurstStar("Star Pop Flash",beam,0),
            sparks=new SpriteRenderer[final?9:6]};
        for(int i=0;i<column.sparks.Length;i++)
            column.sparks[i]=BurstStar("Popping Star Spark",beam,1);
        columns.Add(column);UpdateColumn(column);
    }

    private static SpriteRenderer BurstStar(string name,LineRenderer beam,int order)
    {
        var star=new GameObject(name).AddComponent<SpriteRenderer>();
        star.transform.SetParent(beam.transform,false);star.sprite=PixelStarProjectile.SharedSprite;
        star.sortingLayerID=beam.sortingLayerID;star.sortingOrder=beam.sortingOrder+order;
        return star;
    }

    private LineRenderer Line(string name,int layer,int order)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(transform,false);line.sharedMaterial=material;
        line.positionCount=2;line.useWorldSpace=true;
        line.sortingLayerID=layer;line.sortingOrder=order;
        line.numCapVertices=0;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows=false;
        return line;
    }

    private void BuildFill(int layer)
    {
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        // Soft area wash reaches the actual radius; the brighter star has no hollow center.
        for(int shape=0;shape<2;shape++)
        {
            int count=shape==0?40:10,start=vertices.Count;
            vertices.Add(Vector3.up*(shape*.005f));
            colors.Add(new Color(1f,1f,1f,shape==0?.13f:.48f));
            for(int i=0;i<count;i++)
            {
                float angle=(90f+i*360f/count)*Mathf.Deg2Rad;
                float distance=shape==1&&i%2==1?radius*.382f:radius;
                vertices.Add(new Vector3(Mathf.Cos(angle)*distance,shape*.005f,Mathf.Sin(angle)*distance));
                colors.Add(new Color(1f,1f,1f,shape==0?.015f:.28f));
                triangles.Add(start);triangles.Add(start+1+(i+1)%count);triangles.Add(start+1+i);
            }
        }
        fillMesh=new Mesh{name="Pentagram Filled Ground"};
        fillMesh.SetVertices(vertices);fillMesh.SetColors(colors);fillMesh.SetTriangles(triangles,0);
        fillMesh.RecalculateBounds();
        var ground=new GameObject("Pentagram Interior");ground.transform.SetParent(transform,false);
        ground.AddComponent<MeshFilter>().sharedMesh=fillMesh;
        fill=ground.AddComponent<MeshRenderer>();fill.sharedMaterial=material;
        fill.sortingLayerID=layer;fill.sortingOrder=1;
        fill.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;fill.receiveShadows=false;
        fill.enabled=false;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            age+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(age>8f||(valid!=null&&!valid())){Cancel();return;}
            drawAge+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);pulseAge+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(finishing)
            {
                finishAge+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                // Keep the last airborne stars alive through their descent after the ground fades.
                if(finishAge>=.72f){Cancel();return;}
            }
            for(int i=columns.Count-1;i>=0;i--)
            {
                Column column=columns[i];column.age+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
                if(column.age>=.66f){Release(column.beam.gameObject);columns.RemoveAt(i);}
                else UpdateColumn(column);
            }
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        float flash=Mathf.Clamp01(1f-pulseAge/.38f);
        float fade=finishing?Mathf.Clamp01(1f-finishAge/.48f):1f;
        float breakUp=finishing?Mathf.Clamp01((finishAge-.08f)/.4f):0f;
        float hopTime=Mathf.Clamp01(pulseAge/.4f);
        float hop=4f*hopTime*(1f-hopTime)*(finishing?1.05f:.75f);
        for(int i=0;i<5;i++)
        {
            stars[i].enabled=i<starCount;
            stars[i].color=CombatFlashPresentation.Peak(Color.Lerp(Violet,Color.white,.3f+flash*.7f)*new Color(1f,1f,1f,fade));
            stars[i].transform.localScale=Vector3.one*(.5f+flash*.34f)*fade;
            stars[i].transform.localPosition=corners[i]*(1f+breakUp*.12f)+Vector3.up*(hop+breakUp*.45f);
            stars[i].transform.rotation=hop>.01f&&Camera.main!=null
                ?Camera.main.transform.rotation:Quaternion.Euler(90f,0f,breakUp*(i%2==0?140f:-140f));
            var stroke=strokes[i];stroke.enabled=i<starCount;
            float drawn=complete||i<starCount-1?1f:Mathf.Clamp01(drawAge/.1f);
            Vector3 drift=(corners[i]+corners[(i+1)%5])*.06f*breakUp;
            Vector3 middle=(corners[i]+corners[(i+1)%5])*.5f;
            stroke.SetPosition(0,Vector3.Lerp(corners[i],middle,breakUp)+drift);
            stroke.SetPosition(1,Vector3.Lerp(Vector3.Lerp(corners[i],corners[(i+1)%5],drawn),middle,breakUp)+drift);
            stroke.widthMultiplier=.035f+flash*.055f;
            Color tint=Color.Lerp(Violet,Color.white,flash*.85f);tint.a=(.65f+flash*.35f)*fade;
            stroke.startColor=stroke.endColor=CombatFlashPresentation.Peak(tint);
        }
        fill.enabled=complete;
        fill.transform.localScale=Vector3.one*(flash>0f?Mathf.Lerp(.25f,1f,Mathf.Clamp01(pulseAge/.1f)):1f);
        Color wash=Color.Lerp(Violet,new Color(.95f,.84f,1f),flash);
        wash.a=(.16f+flash*.84f)*fade;
        fillTint.SetColor("_Color",CombatFlashPresentation.Flash(wash));fill.SetPropertyBlock(fillTint);
        if(finishing)
        {
            for(int i=0;i<fragments.Length;i++)
            {
                var fragment=fragments[i];
                Vector3 origin=Vector3.Lerp(corners[i/3],corners[(i/3+1)%5],(i%3+.5f)/3f);
                fragment.transform.localPosition=origin*(1f+breakUp*.14f)+Vector3.up*breakUp*(.3f+i%3*.2f);
                if(Camera.main!=null)fragment.transform.rotation=Camera.main.transform.rotation;
                fragment.transform.localScale=Vector3.one*(.24f+i%3*.05f)*fade;
                fragment.color=new Color(.9f,.75f,1f,fade);
            }
        }
    }

    private static void UpdateColumn(Column column)
    {
        float t=Mathf.Clamp01(column.age/.66f),rise=Mathf.Clamp01(column.age/.16f);
        float fall=Mathf.Clamp01((column.age-.16f)/.5f);
        float height=column.age<.16f?1f-Mathf.Pow(1f-rise,3f):1f-fall*fall;
        float fade=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((t-.58f)/.42f));
        Vector3 top=column.foot+Vector3.up*column.height*height;
        // A brief launch streak, then a free star with a visible apex and falling arc.
        float streak=Mathf.Clamp01(1f-column.age/.18f);
        column.beam.SetPosition(0,Vector3.Lerp(column.foot,top,1f-streak));
        column.beam.SetPosition(1,top);column.beam.widthMultiplier=column.width*streak;
        column.beam.startColor=new Color(.75f,.42f,1f,streak*.65f);
        column.beam.endColor=new Color(1f,.94f,1f,streak);
        Quaternion billboard=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        column.tip.transform.position=top;
        column.tip.transform.rotation=billboard*Quaternion.Euler(0f,0f,fall*85f);
        float size=column.width*3.4f*Mathf.Lerp(.55f,1f,Mathf.Clamp01(column.age/.045f));
        column.tip.transform.localScale=new Vector3(Mathf.Lerp(.6f,1f,rise),Mathf.Lerp(1.65f,1f,rise),1f)*size*(1f-fall*.3f);
        column.tip.color=Color.Lerp(Color.white,new Color(.78f,.5f,1f),fall)*new Color(1f,1f,1f,fade);
        float flash=Mathf.Clamp01(1f-column.age/.12f);
        column.flash.transform.position=column.foot+Vector3.up*.25f;
        column.flash.transform.rotation=billboard*Quaternion.Euler(0f,0f,45f);
        column.flash.transform.localScale=new Vector3(1.6f,.7f,1f)*column.width*Mathf.Lerp(2f,6f,1f-flash);
        column.flash.color=CombatFlashPresentation.Flash(new Color(1f,.9f,1f,flash));
        for(int i=0;i<column.sparks.Length;i++)
        {
            var spark=column.sparks[i];
            float progress=Mathf.Clamp01(column.age/(.46f+i%3*.07f));
            float angle=(i*137.5f+column.foot.x*13f)*Mathf.Deg2Rad;
            float spread=(1.1f+i%3*.28f)*progress*column.width/.4f;
            Vector3 offset=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*spread;
            offset.y=4f*progress*(1f-progress)*column.height*(.32f+i%3*.1f);
            spark.transform.position=column.foot+offset;
            spark.transform.rotation=billboard*Quaternion.Euler(0f,0f,progress*(i%2==0?180f:-180f));
            spark.transform.localScale=Vector3.one*(.32f+i%3*.11f)*column.width/.4f*(1f-progress*.5f);
            Color tint=Color.Lerp(new Color(1f,.92f,.65f),Violet,progress);
            tint.a=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((progress-.6f)/.4f));
            spark.color=tint;
        }
    }

    private void OnDestroy(){Release(fillMesh);Release(ownedMaterial);}
    private static void Release(UnityEngine.Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
}
