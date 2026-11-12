using UnityEngine;
using UnityEngine.Rendering;

// Visualizes existing absorb health only: no collider, damage filter or projectile interception.
[DefaultExecutionOrder(10020)]
public sealed class PixelUnitShield : MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Mesh shellMesh;
    private Transform owner,surface;
    private SpriteRenderer reference;
    private SortingGroup sorting;
    private MeshRenderer shellRenderer;
    private Material material;
    private readonly Vector4[] hits=new Vector4[6];
    private int nextHit;
    private float born,end=-1f,lastImpact=float.NegativeInfinity;
    private const float IntroDuration=.5f,ImpactDuration=.4f;
    private Vector3 radii;
    public Vector3 Center=>surface==null?transform.position:surface.position;

    public static PixelUnitShield Create(Transform owner,SpriteRenderer reference,float time,bool instant=false)
    {
        var fx=new GameObject("Unit Hex Shield").AddComponent<PixelUnitShield>();
        // A shield first discovered during damage should show only its local contact reaction.
        fx.owner=owner;fx.reference=reference;fx.born=time-(instant?IntroDuration:0f);
        fx.sorting=fx.gameObject.AddComponent<SortingGroup>();
        fx.material=new Material(Resources.Load<Shader>("CombatEffects/HexShield"));
        fx.material.SetFloat("_EndAge",1000000f);
        var surface=new GameObject("Hex Surface");surface.transform.SetParent(fx.transform,false);fx.surface=surface.transform;
        if(shellMesh==null)shellMesh=CreateShell();
        surface.AddComponent<MeshFilter>().sharedMesh=shellMesh;
        fx.shellRenderer=surface.AddComponent<MeshRenderer>();fx.shellRenderer.sharedMaterial=fx.material;
        fx.shellRenderer.shadowCastingMode=ShadowCastingMode.Off;fx.shellRenderer.receiveShadows=false;
        fx.Follow();fx.Pose(time,true);return fx;
    }

    public void Pose(float time,bool shielded)
    {
        if(this==null)return;
        if(shielded)
        {
            if(end>=0)
            {
                born=time;lastImpact=float.NegativeInfinity;nextHit=0;
                System.Array.Clear(hits,0,hits.Length);
            }
            end=-1f;
        }
        else if(end<0)end=time;
        if(end>=0&&time-end>=.2f){Cancel();return;}
        shellRenderer.enabled=time-born<IntroDuration||time-lastImpact<ImpactDuration;
        material.SetFloat("_Age",time-born);
        material.SetFloat("_EndAge",end<0?1000000f:end-born);
        material.SetVectorArray("_Hits",hits);
    }

    public void Impact(Vector3 attacker,float time)
    {
        if(this==null)return;
        if(end>=0)Pose(time,true);
        Follow();
        Vector3 direction=Quaternion.Inverse(surface.rotation)*(attacker-Center);
        if(direction.sqrMagnitude<.001f)direction=Vector3.back;
        Vector3 n=new Vector3(direction.x/radii.x,direction.y/radii.y,direction.z/radii.z).normalized;
        Vector2 p=new Vector2(n.x*radii.x,n.y*radii.y)*(2f/(1f+Mathf.Abs(n.z)));
        hits[nextHit]=new Vector4(p.x,p.y,time-born,1);nextHit=(nextHit+1)%hits.Length;
        lastImpact=time;shellRenderer.enabled=true;
        material.SetFloat("_Age",time-born);
        material.SetVectorArray("_Hits",hits);
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Follow();
        }
    }
    private void Follow()
    {
        if(owner==null||reference==null){Cancel();return;}
        var camera=Camera.main;Quaternion rotation=camera==null?Quaternion.identity:camera.transform.rotation;
        Vector3 scale=reference.transform.lossyScale;
        var bounds=reference.sprite==null?new Bounds(Vector3.zero,Vector3.one):PixelMantisReaper.VisibleBody(reference.sprite);
        float width=Mathf.Max(.45f,bounds.size.x*Mathf.Abs(scale.x)),height=Mathf.Max(.65f,bounds.size.y*Mathf.Abs(scale.y));
        // Grow to accommodate attack poses without shrinking on each animation frame.
        radii.x=Mathf.Max(radii.x,width*.62f+.12f);radii.y=Mathf.Max(radii.y,height*.62f+.12f);
        radii.z=Mathf.Max(.35f,Mathf.Min(radii.x,radii.y)*.65f);
        transform.position=owner.position+rotation*Vector3.back*.025f;
        sorting.sortingLayerID=reference.sortingLayerID;sorting.sortingOrder=reference.sortingOrder;
        Vector3 center=bounds.center;if(reference.flipX)center.x=-center.x;if(reference.flipY)center.y=-center.y;
        surface.SetPositionAndRotation(reference.transform.TransformPoint(center),rotation);surface.localScale=radii;
        material.SetVector("_Radii",radii);
        material.SetFloat("_CellSize",Mathf.Max(.2f,Mathf.Min(radii.x,radii.y)*.48f));
    }

    private static Mesh CreateShell()
    {
        const int columns=32,rows=16;
        var vertices=new Vector3[(columns+1)*(rows+1)];var triangles=new int[columns*rows*6];
        for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
        {
            float a=x/(float)columns*Mathf.PI*2,latitude=y/(float)rows*Mathf.PI-Mathf.PI*.5f;
            vertices[y*(columns+1)+x]=new Vector3(Mathf.Cos(a)*Mathf.Cos(latitude),Mathf.Sin(latitude),Mathf.Sin(a)*Mathf.Cos(latitude));
        }
        int index=0;
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
        {
            int v=y*(columns+1)+x;
            triangles[index++]=v;triangles[index++]=v+columns+1;triangles[index++]=v+1;
            triangles[index++]=v+1;triangles[index++]=v+columns+1;triangles[index++]=v+columns+2;
        }
        var mesh=new Mesh{name="Shared Unit Shield Shell"};mesh.vertices=vertices;mesh.normals=vertices;
        mesh.SetUVs(0,new System.Collections.Generic.List<Vector3>(vertices));mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
    }

    public void Cancel(){if(this==null)return;if(CombatEffectEndFade.IsFading(this))return;gameObject.SetActive(false);if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    private void OnDestroy(){if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
}
