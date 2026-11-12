using UnityEngine;

// The owning death-roll coroutine drives and destroys this visual, including cancellation.
public sealed class PixelCrocodileCrushingSweep:MonoBehaviour
{
    private const int Segments=24;
    private static Material material;
    private Mesh mesh;
    private MeshRenderer sweepRenderer;
    private readonly Vector3[] vertices=new Vector3[(Segments+1)*4];
    private readonly Color[] colors=new Color[(Segments+1)*4];
    private float radius;
    private bool transcended;
    private bool finalSweep;
    internal float CurrentRollTime {get;private set;}

    public static PixelCrocodileCrushingSweep Create(Vector3 center,float radius,SpriteRenderer reference,bool transcended=false)
    {
        var root=new GameObject("Crocodile Crushing Roll Sweep");
        root.transform.position=center+Vector3.up*0.55f;
        var effect=root.AddComponent<PixelCrocodileCrushingSweep>();
        effect.radius=radius;
        effect.transcended=transcended;
        effect.mesh=new Mesh {name="Crushing Roll Broad Arcs"};
        root.AddComponent<MeshFilter>().sharedMesh=effect.mesh;
        effect.sweepRenderer=root.AddComponent<MeshRenderer>();
        if(material==null)material=new Material(Shader.Find("Sprites/Default"))
            {name="Crushing Roll Sweep",hideFlags=HideFlags.HideAndDontSave};
        effect.sweepRenderer.sharedMaterial=material;
        effect.sweepRenderer.sortingLayerID=reference==null?0:reference.sortingLayerID;
        effect.sweepRenderer.sortingOrder=(reference==null?0:reference.sortingOrder)+22;
        var triangles=new int[Segments*12];
        for(int arc=0;arc<2;arc++)
        for(int i=0;i<=Segments;i++)
        {
            int v=arc*(Segments+1)*2+i*2;
            if(i==Segments)continue;
            int t=(arc*Segments+i)*6;
            triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;
            triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;
        }
        effect.mesh.vertices=effect.vertices;effect.mesh.triangles=triangles;
        effect.UpdateColors();
        effect.SetProgress(CrocodileCombatRules.CrushingRollStart);
        return effect;
    }

    public void SetProgress(float rollTime)
    {
        CurrentRollTime=rollTime;
        float progress=(rollTime-CrocodileCombatRules.CrushingRollStart)/
            CrocodileCombatRules.CrushingRollSpinDuration;
        sweepRenderer.enabled=progress>=0f&&progress<1f;
        if(transcended&&finalSweep!=(progress>=0.5f))
        {
            finalSweep=progress>=0.5f;
            UpdateColors();
        }
        float head=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(progress))*720f;
        for(int arc=0;arc<2;arc++)
        for(int i=0;i<=Segments;i++)
        {
            float tail=i/(float)Segments;
            float angle=(head+arc*180f-tail*135f)*Mathf.Deg2Rad;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            int v=arc*(Segments+1)*2+i*2;
            vertices[v]=direction*radius*Mathf.Lerp(transcended&&finalSweep?0.5f:0.58f,0.95f,tail);
            vertices[v+1]=direction*radius;
        }
        mesh.vertices=vertices;mesh.RecalculateBounds();
    }

    private void UpdateColors()
    {
        Color inner=transcended?PixelCombatEffect.CrocodileAbyssColor:new Color(0.37f,0.62f,0.17f);
        Color outer=transcended?(finalSweep?PixelCombatEffect.CrocodileAbyssHighlight:
            Color.Lerp(PixelCombatEffect.CrocodileAbyssColor,Color.white,0.35f)):new Color(0.92f,0.91f,0.56f);
        for(int arc=0;arc<2;arc++)
        for(int i=0;i<=Segments;i++)
        {
            int v=arc*(Segments+1)*2+i*2;
            float fade=1f-i/(float)Segments;
            inner.a=fade*(transcended&&finalSweep?0.2f:0.12f);
            outer.a=fade*(transcended&&finalSweep?0.85f:0.72f);
            colors[v]=inner;colors[v+1]=outer;
        }
        mesh.colors=colors;
    }

    private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
}
