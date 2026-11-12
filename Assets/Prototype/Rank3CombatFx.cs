using UnityEngine;

public static class Rank3CombatFx
{
    public static GameObject Ring(string name,Vector3 center,float radius,
        Color color,float duration,Material material,SpriteRenderer sorting=null,
        int segments=48)
    {
        GameObject root=new GameObject(name);
        root.transform.position=center+Vector3.up*.08f;
        LineRenderer line=root.AddComponent<LineRenderer>();
        Configure(line,material,color,sorting);
        line.loop=true;
        line.positionCount=Mathf.Max(8,segments);
        line.startWidth=Mathf.Max(.035f,radius*.035f);
        line.endWidth=line.startWidth;
        for(int i=0;i<line.positionCount;i++)
        {
            float angle=Mathf.PI*2f*i/line.positionCount;
            line.SetPosition(i,new Vector3(
                Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius));
        }
        Rank3FxLifetime life=root.AddComponent<Rank3FxLifetime>();
        life.duration=Mathf.Max(.05f,duration);
        life.color=color;
        return root;
    }

    public static GameObject Polygon(string name,Vector3 center,float radius,int sides,
        Color color,float duration,Material material,SpriteRenderer sorting=null)
    {
        return Ring(name,center,radius,color,duration,material,sorting,
            Mathf.Max(3,sides));
    }

    public static GameObject Line(string name,Vector3 start,Vector3 end,
        float width,Color color,float duration,Material material,
        SpriteRenderer sorting=null)
    {
        GameObject root=new GameObject(name);
        LineRenderer line=root.AddComponent<LineRenderer>();
        Configure(line,material,color,sorting);
        line.positionCount=2;
        line.startWidth=width;
        line.endWidth=width*.45f;
        line.SetPosition(0,start+Vector3.up*.1f);
        line.SetPosition(1,end+Vector3.up*.1f);
        Rank3FxLifetime life=root.AddComponent<Rank3FxLifetime>();
        life.duration=Mathf.Max(.05f,duration);
        life.color=color;
        return root;
    }

    public static void Cross(string name,Vector3 center,float radius,Color color,
        float duration,Material material,SpriteRenderer sorting=null,float angle=0f)
    {
        Vector3 a=Quaternion.AngleAxis(angle,Vector3.up)*Vector3.right*radius;
        Vector3 b=Quaternion.AngleAxis(angle+90f,Vector3.up)*Vector3.right*radius;
        Line(name+" A",center-a,center+a,radius*.07f,color,duration,material,sorting);
        Line(name+" B",center-b,center+b,radius*.07f,color,duration,material,sorting);
    }

    public static SpriteRenderer Ghost(string name,SpriteRenderer source,
        Vector3 position,Color color,float scale,float duration)
    {
        if(source==null||source.sprite==null)return null;
        GameObject root=new GameObject(name);
        root.transform.position=position;
        root.transform.localScale=source.transform.lossyScale*scale;
        SpriteRenderer ghost=root.AddComponent<SpriteRenderer>();
        ghost.sprite=source.sprite;
        ghost.flipX=source.flipX;
        ghost.sortingLayerID=source.sortingLayerID;
        ghost.sortingOrder=source.sortingOrder+12;
        ghost.color=color;
        Rank3FxLifetime life=root.AddComponent<Rank3FxLifetime>();
        life.duration=Mathf.Max(.05f,duration);
        life.color=color;
        return ghost;
    }

    private static void Configure(LineRenderer line,Material material,
        Color color,SpriteRenderer sorting)
    {
        line.sharedMaterial=material;
        line.useWorldSpace=false;
        line.numCapVertices=2;
        line.numCornerVertices=2;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows=false;
        line.startColor=color;
        line.endColor=color;
        if(sorting==null)return;
        line.sortingLayerID=sorting.sortingLayerID;
        line.sortingOrder=sorting.sortingOrder+20;
    }
}

public sealed class Rank3FxLifetime:MonoBehaviour
{
    public float duration=.3f;
    public Color color=Color.white;
    private float elapsed;
    private Vector3 initialScale;
    private LineRenderer line;
    private SpriteRenderer sprite;

    private void Awake()
    {
        initialScale=transform.localScale;
        line=GetComponent<LineRenderer>();
        sprite=GetComponent<SpriteRenderer>();
    }

    private void Update()=>Advance(Time.deltaTime);

    internal void Advance(float deltaTime)
    {
        elapsed+=deltaTime;
        float t=Mathf.Clamp01(elapsed/Mathf.Max(.01f,duration));
        float alpha=(1f-t)*(1f-t);
        transform.localScale=initialScale*(.88f+t*.18f);
        Color faded=new Color(color.r,color.g,color.b,color.a*alpha);
        if(line!=null){line.startColor=faded;line.endColor=faded;}
        if(sprite!=null)sprite.color=faded;
        if(t>=1f)
        {
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
        }
    }
}
