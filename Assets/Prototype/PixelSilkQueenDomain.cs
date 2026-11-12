using System.Collections.Generic;
using UnityEngine;

// A ground-woven spider sigil and a low, fading rim share one mesh/material.
public sealed class PixelSilkQueenDomain:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private Mesh mesh;
    private Material material;
    private float age,fadeAge=-1f;
    private readonly List<Vector3> vertices=new List<Vector3>();
    private readonly List<Color> colors=new List<Color>();
    private readonly List<int> triangles=new List<int>();
    private static readonly Color Silk=new Color(.76f,.49f,1f,.6f);
    private static readonly Color Glow=new Color(.48f,.12f,.92f,.18f);

    public static PixelSilkQueenDomain Create(Bounds arena,SpriteRenderer reference)
    {
        var fx=new GameObject("Silk Queen Spider Sigil").AddComponent<PixelSilkQueenDomain>();
        fx.transform.position=new Vector3(arena.center.x,.027f,arena.center.z);
        fx.material=new Material(Shader.Find("Sprites/Default"));
        var renderer=fx.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=fx.material;
        renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;renderer.sortingOrder=2;
        float x=Mathf.Max(1f,arena.size.x)*.5f,z=Mathf.Max(1f,arena.size.z)*.5f;
        fx.BuildRim(x,z);
        fx.BuildWeb(x,z);
        fx.BuildSpider(Mathf.Min(x*.32f,z*.54f));
        fx.mesh=new Mesh{name="Neon Spider Silk Domain"};fx.mesh.SetVertices(fx.vertices);
        fx.mesh.SetColors(fx.colors);fx.mesh.SetTriangles(fx.triangles,0);fx.mesh.RecalculateBounds();
        fx.gameObject.AddComponent<MeshFilter>().sharedMesh=fx.mesh;
        fx.vertices.Clear();fx.colors.Clear();fx.triangles.Clear();
        fx.Animate(0f);return fx;
    }

    private void BuildRim(float x,float z)
    {
        Vector3[] corners={new Vector3(-x,0,-z),new Vector3(x,0,-z),
            new Vector3(x,0,z),new Vector3(-x,0,z)};
        for(int i=0;i<4;i++)
        {
            Vector3 a=corners[i],b=corners[(i+1)%4];
            Vector3 inward=Vector3.Cross((b-a).normalized,Vector3.up);
            // A broad floor reflection and a low vertical gradient rise from the same edge.
            Quad(a,b,b+inward*.65f,a+inward*.65f,new Color(.5f,.16f,.88f,.27f),Color.clear);
            Quad(a,b,b+Vector3.up*.58f,a+Vector3.up*.58f,
                new Color(.63f,.25f,1f,.42f),Color.clear);
            Line(a+inward*.045f,b+inward*.045f,.085f,new Color(.88f,.66f,1f,.86f));
            Line(a+inward*.15f,b+inward*.15f,.045f,new Color(.58f,.24f,1f,.3f));
        }
    }

    private void BuildWeb(float x,float z)
    {
        const int spokes=16;
        var ends=new Vector3[spokes];
        for(int i=0;i<spokes;i++)
        {
            float angle=i*Mathf.PI*2f/spokes;
            Vector3 direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
            float distance=Mathf.Min(x/Mathf.Max(.001f,Mathf.Abs(direction.x)),
                z/Mathf.Max(.001f,Mathf.Abs(direction.z)));
            ends[i]=direction*(distance-.12f);
            Strand(ends[i]*.34f,ends[i],.045f,new Color(.65f,.38f,.95f,.40f));
        }
        // The concave strands read as a spider web, with every spoke anchored to the rectangle.
        for(int ring=2;ring<=5;ring++)
        {
            float radius=ring*.17f+.09f;
            for(int i=0;i<spokes;i++)
            {
                Vector3 a=ends[i]*radius,b=ends[(i+1)%spokes]*radius,previous=a;
                for(int step=1;step<=5;step++)
                {
                    float t=step/5f;
                    Vector3 point=Vector3.Lerp(a,b,t)*(1f-Mathf.Sin(t*Mathf.PI)*.065f);
                    Strand(previous,point,.042f,new Color(.73f,.48f,1f,.37f));previous=point;
                }
            }
        }
    }

    private void BuildSpider(float scale)
    {
        // Mirrored angular legs, a pointed abdomen and a separate head make a readable emblem.
        Vector2[,] legs={
            {new Vector2(.16f,.43f),new Vector2(.51f,.69f),new Vector2(.59f,1.02f),new Vector2(.92f,1.18f)},
            {new Vector2(.22f,.32f),new Vector2(.66f,.42f),new Vector2(.95f,.76f),new Vector2(1.21f,.75f)},
            {new Vector2(.22f,.20f),new Vector2(.65f,.11f),new Vector2(1.03f,.28f),new Vector2(1.22f,-.10f)},
            {new Vector2(.20f,.09f),new Vector2(.48f,-.24f),new Vector2(.94f,-.38f),new Vector2(.95f,-.94f)}};
        for(int side=-1;side<=1;side+=2)for(int leg=0;leg<4;leg++)for(int segment=0;segment<3;segment++)
        {
            Vector2 a=legs[leg,segment],b=legs[leg,segment+1];
            Strand(new Vector3(a.x*side,0,a.y)*scale,new Vector3(b.x*side,0,b.y)*scale,
                scale*(segment==2?.035f:.065f),Silk);
        }
        Vector2[] abdomen={new Vector2(0,.27f),new Vector2(.26f,.09f),new Vector2(.35f,-.23f),
            new Vector2(.27f,-.61f),new Vector2(0,-.96f),new Vector2(-.27f,-.61f),
            new Vector2(-.35f,-.23f),new Vector2(-.26f,.09f)};
        Polygon(abdomen,scale,new Color(.48f,.15f,.78f,.38f),true);
        Vector2[] head={new Vector2(0,.65f),new Vector2(.21f,.53f),new Vector2(.23f,.30f),
            new Vector2(0,.17f),new Vector2(-.23f,.30f),new Vector2(-.21f,.53f)};
        Polygon(head,scale,new Color(.64f,.28f,.93f,.48f),true);
        Strand(new Vector3(-.16f,0,-.18f)*scale,new Vector3(0,0,-.46f)*scale,scale*.04f,Silk);
        Strand(new Vector3(.16f,0,-.18f)*scale,new Vector3(0,0,-.46f)*scale,scale*.04f,Silk);
        for(int side=-1;side<=1;side+=2)
        {
            Strand(new Vector3(.08f*side,0,.51f)*scale,new Vector3(.11f*side,0,.43f)*scale,
                scale*.05f,new Color(.94f,.82f,1f,.85f));
            Strand(new Vector3(.11f*side,0,.61f)*scale,new Vector3(.17f*side,0,.78f)*scale,
                scale*.035f,Silk);
        }
    }

    private void Polygon(Vector2[] points,float scale,Color color,bool outline)
    {
        int start=vertices.Count;Vector3 center=Vector3.zero;
        foreach(var p in points)center+=new Vector3(p.x,0,p.y)*scale/points.Length;
        vertices.Add(center);colors.Add(color);
        foreach(var p in points){vertices.Add(new Vector3(p.x,0,p.y)*scale);colors.Add(color);}
        for(int i=0;i<points.Length;i++)
        {
            triangles.Add(start);triangles.Add(start+1+i);triangles.Add(start+1+(i+1)%points.Length);
            if(outline)Strand(new Vector3(points[i].x,0,points[i].y)*scale,
                new Vector3(points[(i+1)%points.Length].x,0,points[(i+1)%points.Length].y)*scale,scale*.045f,Silk);
        }
    }

    private void Strand(Vector3 a,Vector3 b,float width,Color color)
    {
        Line(a,b,width*4f,Glow);Line(a,b,width,color);
    }
    private void Line(Vector3 a,Vector3 b,float width,Color color)
    {
        Vector3 side=Vector3.Cross((b-a).normalized,Vector3.up)*width*.5f;
        Quad(a-side,b-side,b+side,a+side,color,color);
    }
    private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color bottom,Color top)
    {
        int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
        colors.Add(bottom);colors.Add(bottom);colors.Add(top);colors.Add(top);
        triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
        triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
    }
    public void Break(){if(this!=null&&!CombatEffectEndPlayback.IsPreserving(this)&&fadeAge<0f)fadeAge=0f;}
    public void Reveal(float visualAge){age=Mathf.Max(0f,visualAge);Animate(0f);}
    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Animate((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    private void Animate(float dt)
    {
        age+=dt;if(fadeAge>=0f)fadeAge+=dt;
        float alpha=fadeAge<0f?Mathf.Clamp01(age/.4f):1f-Mathf.Clamp01(fadeAge/.25f);
        float breath=.94f+.06f*Mathf.Sin(age*2f);
        transform.localScale=new Vector3(1f,Mathf.Lerp(.1f,1f,Mathf.SmoothStep(0f,1f,age/.55f)),1f);
        material.color=new Color(1f,1f,1f,alpha*breath);
        if(fadeAge>=.25f)Cancel();
    }
    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void OnDestroy()
    {
        if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
    }
}
