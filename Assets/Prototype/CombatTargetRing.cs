using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class CombatTargetRing:MonoBehaviour
{
    private static readonly int ColorId=Shader.PropertyToID("_Color");
    private static readonly int BaseColorId=Shader.PropertyToID("_BaseColor");
    private Transform normalizer,pointerPivot;
    private MeshRenderer circle,pointer;
    private Mesh circleMesh,pointerMesh;
    private MaterialPropertyBlock properties;
    private float radius;
    private bool visible,hasTarget,validScale;

    public void Configure(float worldRadius,Color color,Material material)
    {
        radius=Finite(worldRadius)?Mathf.Max(.02f,worldRadius):.3f;
        if(normalizer==null)
        {
            normalizer=Child("Combat Target Ring",transform);
            circleMesh=CreateCircle();pointerMesh=CreatePointer();
            circle=CreateRenderer("Circle",normalizer,circleMesh,3);
            pointerPivot=Child("Target Direction",normalizer);
            // The rear half of a ground marker would otherwise disappear behind its owner's feet.
            pointer=CreateRenderer("Point",pointerPivot,pointerMesh,11);
            properties=new MaterialPropertyBlock();
        }
        properties.SetColor(ColorId,color);properties.SetColor(BaseColorId,color);
        circle.sharedMaterial=pointer.sharedMaterial=material;
        circle.SetPropertyBlock(properties);pointer.SetPropertyBlock(properties);
        visible=false;hasTarget=false;
        NormalizeScale();ApplyVisibility();
    }

    public void UpdateDirection(Vector3 worldDirection,bool hasTarget)
    {
        NormalizeScale();worldDirection.y=0f;
        this.hasTarget=hasTarget&&Finite(worldDirection.x)&&Finite(worldDirection.z)&&
            Finite(worldDirection.sqrMagnitude)&&worldDirection.sqrMagnitude>.000001f;
        if(this.hasTarget&&pointerPivot!=null)
            pointerPivot.rotation=Quaternion.LookRotation(worldDirection,Vector3.up);
        ApplyVisibility();
    }

    public void SetVisible(bool value)
    {
        visible=value;ApplyVisibility();
    }

    public void SetRadius(float worldRadius)
    {
        radius=Finite(worldRadius)?Mathf.Max(.02f,worldRadius):.3f;
        NormalizeScale();
    }

    private void NormalizeScale()
    {
        if(normalizer==null)return;
        Vector3 scale=transform.lossyScale;
        validScale=Finite(scale.x)&&Finite(scale.y)&&Finite(scale.z)&&
            Mathf.Abs(scale.x)>.000001f&&Mathf.Abs(scale.y)>.000001f&&Mathf.Abs(scale.z)>.000001f;
        if(!validScale)return;
        normalizer.SetPositionAndRotation(transform.position+Vector3.up*.012f,Quaternion.identity);
        float visualRadius=radius;
        visualRadius*=CombatReadabilityPresentation.BodyVisualScale;
        if(CombatReadabilityPresentation.Local)
            visualRadius*=.8f;
        normalizer.localScale=new Vector3(visualRadius/scale.x,visualRadius/scale.y,visualRadius/scale.z);
    }

    private void ApplyVisibility()
    {
        if(normalizer==null)return;
        normalizer.gameObject.SetActive(visible&&validScale&&isActiveAndEnabled);
        pointerPivot.gameObject.SetActive(hasTarget);
    }

    private void OnDisable()
    {
        visible=false;hasTarget=false;ApplyVisibility();
    }

    private void OnDestroy()
    {
        if(normalizer!=null)Release(normalizer.gameObject);
        Release(circleMesh);Release(pointerMesh);
    }

    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

    private static Transform Child(string name,Transform parent)
    {
        var child=new GameObject(name);child.layer=parent.gameObject.layer;
        child.transform.SetParent(parent,false);return child.transform;
    }

    private static MeshRenderer CreateRenderer(string name,Transform parent,Mesh mesh,int order)
    {
        Transform child=Child(name,parent);
        child.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=child.gameObject.AddComponent<MeshRenderer>();
        renderer.sortingOrder=order;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        return renderer;
    }

    private static Mesh CreateCircle()
    {
        const int segments=48;
        float[] radii={1f,.97f,.91f,.88f};
        var vertices=new Vector3[segments*4];var colors=new Color[vertices.Length];
        var uv=new Vector2[vertices.Length];var triangles=new int[segments*18];
        Color edge=new Color(.06f,.08f,.09f,1f);
        for(int band=0;band<4;band++)for(int i=0;i<segments;i++)
        {
            float angle=i*Mathf.PI*2f/segments;int index=band*segments+i;
            vertices[index]=new Vector3(Mathf.Sin(angle)*radii[band],0f,Mathf.Cos(angle)*radii[band]);
            colors[index]=band==0||band==3?edge:Color.white;uv[index]=Vector2.one*.5f;
        }
        int cursor=0;
        for(int band=0;band<3;band++)for(int i=0;i<segments;i++)
        {
            int a=band*segments+i,b=band*segments+(i+1)%segments,c=a+segments,d=b+segments;
            triangles[cursor++]=a;triangles[cursor++]=b;triangles[cursor++]=c;
            triangles[cursor++]=b;triangles[cursor++]=d;triangles[cursor++]=c;
        }
        var mesh=new Mesh{name="Combat Target Circle"};
        mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=triangles;
        mesh.RecalculateBounds();return mesh;
    }

    private static Mesh CreatePointer()
    {
        Color edge=new Color(.06f,.08f,.09f,1f);
        var mesh=new Mesh{name="Combat Target Point"};
        mesh.vertices=new[]{new Vector3(0f,0f,1.38f),new Vector3(-.2f,0f,.93f),new Vector3(.2f,0f,.93f),
            new Vector3(0f,.001f,1.28f),new Vector3(-.12f,.001f,1f),new Vector3(.12f,.001f,1f)};
        mesh.colors=new[]{edge,edge,edge,Color.white,Color.white,Color.white};
        mesh.uv=new[]{Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,
            Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f};
        mesh.triangles=new[]{0,1,2,3,4,5};mesh.RecalculateBounds();return mesh;
    }

    private static void Release(Object value)
    {
        if(value==null)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
}
