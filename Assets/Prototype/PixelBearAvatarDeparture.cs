using System;
using UnityEngine;

// One textured mesh preserves the departing pose, then separates it into crisp pixel cells.
public sealed class PixelBearAvatarDeparture:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    public const float Duration=1.1f;
    private const int CellPixels=4;
    private Mesh mesh;
    private Material material;
    private Vector3[] rest,vertices,centers;
    private Vector2[] drift;
    private Color[] colors;
    private float[] delays;
    private Func<bool> valid;
    private float elapsed;
    private Func<float> clock;
    private float born,seconds=Duration;
    private bool reverse;
    private Color soulTint=new Color(.52f,.93f,1f);
    private Color soulEnd=new Color(.8f,1f,1f);

    public static PixelBearAvatarDeparture Create(SpriteRenderer source,Func<bool> valid,Color? tint=null,
        Func<float> clock=null,bool reverse=false,float seconds=Duration)
    {
        if(source==null||source.sprite==null)return null;
        var effect=new GameObject("Bear Avatar Departing Soul").AddComponent<PixelBearAvatarDeparture>();
        effect.valid=valid;
        effect.clock=clock;if(effect.skillClock!=null)effect.clock=()=>effect.skillClock.Time;effect.born=effect.clock==null?0f:effect.clock();effect.reverse=reverse;effect.seconds=Mathf.Max(.01f,seconds);
        if(tint.HasValue){effect.soulTint=tint.Value;effect.soulEnd=Color.Lerp(tint.Value,Color.white,.58f);}
        effect.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        effect.transform.localScale=source.transform.lossyScale;
        effect.material=new Material(Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette"))
            {name="Bear Departing Soul Pixels",hideFlags=HideFlags.HideAndDontSave};
        effect.material.SetTexture("_MainTex",source.sprite.texture);
        effect.material.SetFloat("_KeepSpriteDetail",1f);
        var renderer=effect.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=effect.material;
        renderer.sortingLayerID=source.sortingLayerID;
        renderer.sortingOrder=source.sortingOrder-1;
        effect.Build(source.sprite,source.flipX,source.flipY);
        effect.gameObject.AddComponent<MeshFilter>().sharedMesh=effect.mesh;
        effect.Animate(reverse?Duration:0f);
        return effect;
    }

    private void Build(Sprite sprite,bool flipX,bool flipY)
    {
        Vector2 minimum=Vector2.one*float.MaxValue,maximum=Vector2.one*float.MinValue;
        foreach(Vector2 point in sprite.vertices)
        {
            Vector2 pixel=point*sprite.pixelsPerUnit+sprite.pivot;
            minimum=Vector2.Min(minimum,pixel);maximum=Vector2.Max(maximum,pixel);
        }
        int left=Mathf.Max(0,Mathf.FloorToInt(minimum.x/CellPixels)*CellPixels);
        int bottom=Mathf.Max(0,Mathf.FloorToInt(minimum.y/CellPixels)*CellPixels);
        int columns=Mathf.CeilToInt((Mathf.Min(sprite.rect.width,maximum.x)-left)/CellPixels);
        int rows=Mathf.CeilToInt((Mathf.Min(sprite.rect.height,maximum.y)-bottom)/CellPixels);
        int count=columns*rows;
        rest=new Vector3[count*4];vertices=new Vector3[count*4];colors=new Color[count*4];
        centers=new Vector3[count];drift=new Vector2[count];delays=new float[count];
        var uv=new Vector2[count*4];var triangles=new int[count*6];
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
        {
            int cell=y*columns+x,start=cell*4;
            float noise=Mathf.Repeat(Mathf.Sin((x+1)*127.1f+(y+1)*311.7f)*43758.5453f,1f);
            delays[cell]=.13f+.2f*y/Mathf.Max(1,rows-1)+noise*.08f;
            drift[cell]=new Vector2((noise-.5f)*.26f,.07f+noise*.13f);
            for(int corner=0;corner<4;corner++)
            {
                Vector2 pixel=new Vector2(
                    Mathf.Min(sprite.rect.width,left+(x+(corner==1||corner==2?1:0))*CellPixels),
                    Mathf.Min(sprite.rect.height,bottom+(y+(corner>=2?1:0))*CellPixels));
                Vector2 local=(pixel-sprite.pivot)/sprite.pixelsPerUnit;
                rest[start+corner]=new Vector3(local.x*(flipX?-1f:1f),local.y*(flipY?-1f:1f),0f);
                uv[start+corner]=new Vector2((sprite.rect.x+pixel.x)/sprite.texture.width,
                    (sprite.rect.y+pixel.y)/sprite.texture.height);
                centers[cell]+=rest[start+corner]*.25f;
            }
            int index=cell*6;
            triangles[index]=start;triangles[index+1]=start+1;triangles[index+2]=start+2;
            triangles[index+3]=start;triangles[index+4]=start+2;triangles[index+5]=start+3;
        }
        mesh=new Mesh{name="Bear Soul Pixel Cells",hideFlags=HideFlags.HideAndDontSave};
        mesh.MarkDynamic();mesh.vertices=rest;mesh.uv=uv;mesh.triangles=triangles;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(valid!=null&&!valid()){Cancel();return;}
            elapsed=clock==null?elapsed+(skillClock==null?Time.deltaTime:skillClock.DeltaTime):clock()-born;
            if(elapsed>=seconds){Cancel();return;}
            Animate((reverse?1f-elapsed/seconds:elapsed/seconds)*Duration);
        }
    }

    private void Animate(float age)
    {
        float lift=.075f*(1f-Mathf.Exp(-age*5f));
        for(int cell=0;cell<centers.Length;cell++)
        {
            float t=Mathf.Clamp01((age-delays[cell])/.58f);
            float spread=t*t;
            Vector3 offset=new Vector3(drift[cell].x*spread,lift+drift[cell].y*spread,0f);
            // Step the shrinking cells in pixel-sized quarters; no stretched streaks or soft particles.
            float size=1f-Mathf.Floor(t*3f)/4f;
            Color color=Color.Lerp(soulTint,soulEnd,t);
            color.a=.72f*Mathf.Pow(1f-t,1.25f);
            for(int corner=0;corner<4;corner++)
            {
                int index=cell*4+corner;
                vertices[index]=centers[cell]+(rest[index]-centers[cell])*size+offset;
                colors[index]=color;
            }
        }
        mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        ReleaseResources();
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private void OnDestroy()=>ReleaseResources();

    private void ReleaseResources()
    {
        if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        mesh=null;material=null;
    }
}
