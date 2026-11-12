using UnityEngine;

// A planted nursery: the camera moves through one floor, while the original cast owns every birth.
public sealed class MantisDeathMoltRank4Stage:MoltColonyV3Stage
{
    public const string ResourceRoot="CombatCinematics/Rank4V3/mantis/";
    public const float FieldReturnTime=3.98f;
    public static readonly float[] BoardTimes={.28f,.82f,1.48f,2.65f,3.12f,4.02f};
    private static readonly float[] ActionTimes={0f,1.48f,1.94f,2.12f,2.36f,2.60f,2.72f,3.62f};
    private static readonly float[] Actions={0f,0f,.18f,.32f,.44f,.56f,.57f,.90f};
    private static readonly Vector3[] ChildFeet={new Vector3(-1.55f,0f,-1f),new Vector3(.10f,0f,-1.8f),new Vector3(1.65f,0f,-.65f)};
    private static readonly int[] BirthPoses={2,3,4,5,6,7};
    private static readonly int[] SettledPoses={7,0,4,3};
    private const int CurveSteps=16;
    private readonly Sprite[] motion,cocoons;
    private readonly SpriteRenderer lead,wrap,foldingBody;
    private readonly SpriteRenderer[] baby=new SpriteRenderer[3],shadow=new SpriteRenderer[3];
    private readonly MeshRenderer earth,lightPool,parentContact;
    private readonly MeshRenderer[] strands=new MeshRenderer[3];
    private readonly Mesh[] strandMeshes=new Mesh[3];
    private readonly Vector3[][] strandVertices=new Vector3[3][];
    private readonly Color[][] strandColors=new Color[3][];
    private readonly MaterialPropertyBlock floorTint=new MaterialPropertyBlock(),lightTint=new MaterialPropertyBlock();
    private readonly Material softMaterial,shadowMaterial;
    public override string Family=>"mantis";
    public override float Duration=>4.4f;

    public MantisDeathMoltRank4Stage(Transform parent):base(parent,"One living mantis nursery",new Color(.035f,.055f,.075f,1f))
    {
        Root.position=new Vector3(740f,320f,740f);
        motion=LoadAtlas(ResourceRoot+"Motion",8,5);cocoons=LoadAtlas(ResourceRoot+"CocoonMotion",4,5);
        var ground=Resources.Load<Texture2D>(ResourceRoot+"NurseryGround");
        if(ground==null){Dispose();throw new System.InvalidOperationException("Missing mantis nursery floor.");}
        var colors=new[]{Color.white,Color.white,Color.white,Color.white};
        earth=Geometry("Continuous nursery floor",new[]{new Vector3(-30f,-.025f,-30f),new Vector3(30f,-.025f,-30f),
            new Vector3(30f,-.025f,30f),new Vector3(-30f,-.025f,30f)},new[]{0,1,2,0,2,3},colors,
            new[]{Vector2.zero,new Vector2(5f,0f),new Vector2(5f,5f),new Vector2(0f,5f)},ground);
        earth.sharedMaterial=OwnMaterial(Shader.Find("Hidden/Rank4V3MantisNurseryFloor"));earth.sharedMaterial.mainTexture=ground;earth.sortingOrder=-90;
        softMaterial=OwnMaterial(Shader.Find("Sprites/Default"));softMaterial.mainTexture=Texture2D.whiteTexture;
        shadowMaterial=OwnMaterial(Shader.Find("Hidden/Rank4V3NurseryShadow"));
        lightPool=SoftEllipse("Quiet warm light on the floor",new Vector3(-.35f,.005f,.2f),new Vector2(4.5f,3.5f),new Color(.83f,.76f,.49f,.17f),-70);
        parentContact=SoftEllipse("Parent grounded contact",Vector3.up*.012f,new Vector2(.70f,.36f),new Color(.015f,.025f,.035f,.42f),-12);
        lead=MakeSprite("LeadMantis closed parent",60);wrap=MakeSprite("Mantis forming leaves",61);
        foldingBody=MakeSprite("Mantis body folding beneath the leaves",59);
        for(int i=0;i<3;i++)
        {
            baby[i]=MakeSprite("Baby first steps "+i,70+i);shadow[i]=MakeSprite("Short child contact "+i,-10);
            shadow[i].sharedMaterial=shadowMaterial;
            strandVertices[i]=new Vector3[(CurveSteps+1)*4];strandColors[i]=new Color[(CurveSteps+1)*4];
            var triangles=new int[CurveSteps*18];
            for(int step=0;step<CurveSteps;step++)for(int strip=0;strip<3;strip++)
            {
                int v=step*4+strip,n=step*18+strip*6;
                triangles[n]=v;triangles[n+1]=v+4;triangles[n+2]=v+5;
                triangles[n+3]=v;triangles[n+4]=v+5;triangles[n+5]=v+1;
            }
            strands[i]=Geometry("Soft curving birth shadow "+i,strandVertices[i],triangles,strandColors[i]);
            strands[i].sharedMaterial=softMaterial;strands[i].sortingOrder=-15;
            strandMeshes[i]=strands[i].GetComponent<MeshFilter>().sharedMesh;
        }
    }

    private MeshRenderer SoftEllipse(string name,Vector3 center,Vector2 radius,Color color,int order)
    {
        const int segments=32;var vertices=new Vector3[segments+2];var colors=new Color[vertices.Length];var triangles=new int[segments*3];
        vertices[0]=center;colors[0]=color;
        for(int i=0;i<=segments;i++)
        {
            float angle=i*Mathf.PI*2f/segments;vertices[i+1]=center+new Vector3(Mathf.Cos(angle)*radius.x,0f,Mathf.Sin(angle)*radius.y);
            colors[i+1]=new Color(color.r,color.g,color.b,0f);
            if(i<segments){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
        }
        var renderer=Geometry(name,vertices,triangles,colors);renderer.sharedMaterial=softMaterial;renderer.sortingOrder=order;return renderer;
    }

    private void FixedBody(SpriteRenderer renderer,Sprite sprite,Sprite reference,Vector3 feet,float height,Color color,int order)
    {
        if(sprite==null||color.a<=0f)return;
        Bounds bounds=PixelMantisReaper.VisibleBody(reference);float scale=height/Mathf.Max(.001f,bounds.size.y);
        renderer.enabled=true;renderer.sprite=sprite;renderer.color=color;renderer.sortingOrder=order;
        renderer.transform.localRotation=CameraFacing;renderer.transform.localScale=Vector3.one*scale;
        renderer.transform.localPosition=feet-CameraFacing*(new Vector3(bounds.center.x,bounds.min.y,0f)*scale);
    }

    private void Parent(float t,int row,float alpha)
    {
        if(t<.30f)
        {
            int pose=t<.18f?0:1;FixedBody(lead,motion[row*8+pose],motion[row*8],Vector3.zero,2f,new Color(1f,1f,1f,alpha),60);return;
        }
        float formation=Mathf.Clamp01((t-.30f)/.56f)*3f;
        int frame=Mathf.Min(3,Mathf.FloorToInt(formation));float blend=formation-frame;
        float breathing=t<.86f?1f:1f+Mathf.Sin((t-.86f)*3.2f)*.012f*(1f-Ease(3.42f,3.90f,t));
        Sprite closed=cocoons[row*4+3];Color tint=new Color(1f,1f,1f,alpha*(frame<3?1f-blend:1f));
        if(t<.68f)FixedBody(foldingBody,motion[row*8+2],motion[row*8],Vector3.zero,2f,
            new Color(1f,1f,1f,alpha*(1f-Ease(.38f,.68f,t))),59);
        FixedBody(lead,cocoons[row*4+frame],closed,Vector3.zero,1.7f*breathing,tint,60);
        if(frame<3)FixedBody(wrap,cocoons[row*4+frame+1],closed,Vector3.zero,1.7f*breathing,
            new Color(1f,1f,1f,alpha*blend),61);
    }

    private void BirthShadow(int index,float t,float alpha)
    {
        float born=2.12f+index*.24f,spread=Ease(.80f,1.72f,t),rise=Ease(born,born+.30f,t);
        Vector3 end=ChildFeet[index];float visibility=spread*(1f-rise)*alpha;
        strands[index].enabled=visibility>.001f;
        if(!strands[index].enabled)return;
        var vertices=strandVertices[index];var colors=strandColors[index];
        for(int step=0;step<=CurveSteps;step++)
        {
            float u=step/(float)CurveSteps;
            Vector3 point=Vector3.Lerp(Vector3.zero,end,u*spread);
            Vector3 direction=end.normalized,side=Vector3.Cross(Vector3.up,direction);
            point+=side*Mathf.Sin(u*Mathf.PI)*(.12f+index*.035f);point.y=.018f;
            float width=(.045f+Mathf.Sin(u*Mathf.PI)*.12f)*spread;
            for(int strip=0;strip<4;strip++)
            {
                int v=step*4+strip;float offset=strip==0?-1f:strip==1?-.48f:strip==2?.48f:1f;
                vertices[v]=point+side*offset*width;
                colors[v]=new Color(.025f,.040f,.055f,strip==0||strip==3?0f:visibility*.25f);
            }
        }
        strandMeshes[index].vertices=vertices;strandMeshes[index].colors=colors;strandMeshes[index].RecalculateBounds();
        for(int n=0;n<5;n++)
        {
            float u=Mathf.Repeat(t*.40f+n*.19f+index*.13f,1f);
            Vector3 point=Vector3.Lerp(Vector3.zero,end,u*spread);point.y=.035f;
            Pixel(point,.027f,new Color(.65f,.76f,.58f,visibility*.20f));
        }
    }

    private void AlignReturnCamera(float t)
    {
        float blend=Ease(3.42f,3.94f,t);if(blend<=0f)return;
        Vector3 foot=View.WorldToViewportPoint(Root.position);
        float height=View.WorldToViewportPoint(Root.position+View.transform.up*1.7f).y-foot.y;
        float targetHeight=Mathf.Max(.025f,Context.returnHeight*.82f/9f);
        float targetFov=2f*Mathf.Atan(Mathf.Tan(20f*Mathf.Deg2Rad)*height/targetHeight)*Mathf.Rad2Deg;
        View.fieldOfView=Mathf.Lerp(40f,targetFov,blend);
        foot=View.WorldToViewportPoint(Root.position);
        Vector2 target=new Vector2(.5f+Context.returnPosition.x/16f,.5f+Context.returnPosition.y/9f);
        Matrix4x4 projection=View.projectionMatrix;
        projection.m02+=(foot.x-target.x)*2f*blend;projection.m12+=(foot.y-target.y)*2f*blend;
        View.projectionMatrix=projection;
    }

    protected override void Animate(float t,float dt)
    {
        ActionTime=Curve(t,ActionTimes,Actions);SoundBeat=t>=1.14f?1:-1;Shot=t<3.62f?1:2;
        earth.enabled=lightPool.enabled=parentContact.enabled=false;
        for(int i=0;i<3;i++)strands[i].enabled=false;
        View.ResetProjectionMatrix();float dolly=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(t/3.62f));
        CameraPose(Vector3.Lerp(new Vector3(-1.6f,6.8f,-8.8f),new Vector3(.65f,7.3f,-9.2f),dolly),
            Vector3.Lerp(new Vector3(0f,.75f,-.1f),new Vector3(0f,.60f,-.35f),dolly),40f);
        AlignReturnCamera(t);
        if(t>=FieldReturnTime){View.backgroundColor=Color.clear;Opacity=0f;return;}
        float scenery=1f-Ease(3.42f,3.94f,t),actors=1f-Ease(3.64f,FieldReturnTime,t);
        View.backgroundColor=new Color(.035f,.055f,.075f,scenery);Opacity=1f;
        earth.enabled=lightPool.enabled=parentContact.enabled=true;
        floorTint.SetColor("_Color",new Color(1f,1f,1f,scenery));earth.SetPropertyBlock(floorTint);
        lightTint.SetColor("_Color",new Color(1f,1f,1f,scenery));lightPool.SetPropertyBlock(lightTint);parentContact.SetPropertyBlock(lightTint);
        int row=SpeciesRow(Context.source.species,"flotis","santius","nectis");if(Context.source.shiny&&row==1)row=4;
        int childRow=row==4?1:row;Parent(t,row,1f-Ease(3.90f,FieldReturnTime,t));
        for(int i=0;i<3;i++)
        {
            float born=2.12f+i*.24f,age=t-born;BirthShadow(i,t,actors);
            Vector3 feet=ChildFeet[i];
            float leave=Ease(.22f,.55f,age);
            Vector3 walk=new Vector3(i==0?-.7f:i==2?.7f:0f,0f,-.35f).normalized*leave*.18f;
            float appear=Ease(born-.30f,born,t),rise=Ease(born,born+.3f,t);
            if(appear>0f)PaintWorld(shadow[i],motion[childRow*8+2],feet+walk+Vector3.up*.016f,.56f,
                new Color(.025f,.040f,.055f,actors*(.07f+rise*.21f)*appear),-10,Quaternion.Euler(90f,0f,0f));
            if(age<0f)continue;
            int pose=age<.54f?BirthPoses[Mathf.Clamp(Mathf.FloorToInt(age/.09f),0,BirthPoses.Length-1)]:
                SettledPoses[(Mathf.FloorToInt((age-.54f)/.18f)+i)%SettledPoses.Length];
            Color tint=new Color(1f,1f,1f,actors*Ease(born,born+.035f,t));
            FixedBody(baby[i],motion[childRow*8+pose],motion[childRow*8],feet+walk,.95f,tint,70+i);
        }
    }
}
