using System.Collections.Generic;
using UnityEngine;

public sealed class NinjaSubstitutionRank4Stage:Rank4CinematicStage
{
    public const string ResourceRoot="CombatCinematics/NinjaSpacetimeV1/";
    public const float FilmImpactTime=3.30f,FieldReturnTime=3.30f;
    public const float SealStartTime=1.20f,SealEndTime=1.90f,SealFrameRate=22f;
    public const float TerrainSnapTime=1.94f,TerrainSnapEndTime=2.10f;
    public const float TensionEndTime=3.13f,CondenseTime=3.15f,CondenseEndTime=3.20f;
    public static readonly float[] BoardTimes={.20f,.85f,1.65f,2.80f,3.35f,3.95f};
    private static readonly Vector2[] Cells={new Vector2(.10f,.13f),new Vector2(.39f,.08f),new Vector2(.83f,.14f),
        new Vector2(.18f,.48f),new Vector2(.59f,.37f),new Vector2(.89f,.59f),
        new Vector2(.08f,.91f),new Vector2(.42f,.84f),new Vector2(.74f,.89f)};
    private static readonly Vector2[][] Faces=SplitSpace();
    private static readonly Rect FloorUV=new Rect(.24f,.36f,.52f,.34f);
    private static readonly Rect SceneryUV=new Rect(0f,.70f,1f,.30f);
    private static readonly float[] LayerStart={1.940f,1.965f,1.950f,1.978f,1.957f,1.986f,1.972f,1.992f,1.982f};
    private static readonly float[] CompressionStart={2.10f,2.16f,2.13f,2.22f,2.11f,2.28f,2.19f,2.14f,2.25f};
    // Top-origin pixel centers of both forepaws in the reviewed species rows, poses1-5.
    private static readonly Vector2[][] Paw={
        new[]{new Vector2(84,80),new Vector2(95,78),new Vector2(104,77),new Vector2(91,81),new Vector2(88,88)},
        new[]{new Vector2(84,76),new Vector2(98,80),new Vector2(103,81),new Vector2(93,86),new Vector2(91,83)},
        new[]{new Vector2(93,82),new Vector2(102,91),new Vector2(105,87),new Vector2(84,100),new Vector2(103,96)},
        new[]{new Vector2(87,82),new Vector2(104,96),new Vector2(106,95),new Vector2(87,106),new Vector2(92,99)}};
    private static readonly Vector2[][] Face={
        new[]{new Vector2(86,59),new Vector2(85,64),new Vector2(88,66),new Vector2(92,66),new Vector2(92,74)},
        new[]{new Vector2(87,54),new Vector2(91,63),new Vector2(91,67),new Vector2(95,69),new Vector2(91,67)},
        new[]{new Vector2(89,63),new Vector2(89,76),new Vector2(88,74),new Vector2(94,86),new Vector2(94,84)},
        new[]{new Vector2(85,63),new Vector2(90,83),new Vector2(90,81),new Vector2(94,94),new Vector2(92,85)}};
    private static readonly Color Violet=new Color32(168,100,242,255),Core=new Color32(249,224,255,255);
    private readonly Sprite[] eyes,motion,rifts;
    private readonly SpriteRenderer leadEye,lead,field,centralRift,handRift,impactRift,waveRift;
    private readonly SpriteRenderer[] victims=new SpriteRenderer[8],fragments=new SpriteRenderer[16];
    private readonly MeshRenderer space,edges,sides,scenery,underlay;
    private readonly Mesh spaceMesh,edgeMesh,sideMesh,sceneryMesh,underlayMesh;
    private readonly MaterialPropertyBlock mapTexture=new MaterialPropertyBlock();
    private readonly Texture2D fallback;
    private readonly Vector3[] spaceVertices=new Vector3[FaceVertexCount()],sideVertices=new Vector3[FaceVertexCount()*4],edgeVertices=new Vector3[4096];
    private readonly Vector2[] spaceUV=new Vector2[FaceVertexCount()],targetUV=new Vector2[8];
    private readonly Vector2[] sceneryUV=new Vector2[4],underlayUV=new Vector2[4];
    private readonly Color[] spaceColors=new Color[FaceVertexCount()],sideColors=new Color[FaceVertexCount()*4],edgeColors=new Color[4096];
    private readonly Vector3[] pivots=new Vector3[Faces.Length],offsets=new Vector3[Faces.Length],scales=new Vector3[Faces.Length],targetFeet=new Vector3[8];
    private readonly Vector3[] layerTremor=new Vector3[Faces.Length];
    private readonly Quaternion[] rotations=new Quaternion[Faces.Length];
    private readonly float[] layerProgress=new float[Faces.Length],compressionProgress=new float[Faces.Length],thickness=new float[Faces.Length];
    private readonly float[] targetHeights=new float[8];
    private readonly int[] targetFace=new int[8];
    private int edgeCount;
    private Vector3 releaseHand;
    public override string Family=>"ninja_cat";
    public override float Duration=>4.25f;

    public NinjaSubstitutionRank4Stage(Transform parent):base(parent,"Ninja eye and fractured spacetime",Color.clear,1920,1080)
    {
        // The distant set lost subpixel precision; this origin stays outside the battlefield.
        Root.position=new Vector3(520f,420f,520f);
        eyes=LoadAtlas(ResourceRoot+"Eyes",4,4);motion=LoadAtlas(ResourceRoot+"Motion",8,4);rifts=LoadAtlas(ResourceRoot+"Rift",4,2);
        fallback=Resources.Load<Texture2D>("Environment/PrimordialMeadow/BattleBackground");
        leadEye=MakeSprite("Lead species eye revealing a vertical fissure",100);
        lead=MakeSprite("Lead ninja controlling fractured space",70);
        field=MakeSprite("Lead exact battlefield intro and return",70);
        centralRift=MakeSprite("Continuous eye fissure and world fault",90);
        handRift=MakeSprite("Small fracture held by the actual paw",90);
        impactRift=MakeSprite("Single collapse impact across the return",120);
        waveRift=MakeSprite("Angular return shockwave",110);
        Material actorSurface=OwnMaterial(Shader.Find("Hidden/NinjaSpacetimeActor"));
        for(int i=0;i<victims.Length;i++)
        {victims[i]=MakeSprite("Actual target standing on a space layer "+i,25);victims[i].sharedMaterial=actorSurface;}
        for(int i=0;i<fragments.Length;i++)fragments[i]=MakeSprite("Weightless spacetime shard "+i,100);
        int[] quad={0,1,2,0,2,3};
        scenery=Geometry("Distant sky and mountains remaining behind fractured ground",new[]{new Vector3(-37f,-10f,24f),
            new Vector3(37f,-10f,24f),new Vector3(37f,6f,24f),new Vector3(-37f,6f,24f)},quad,
            new[]{new Color(.70f,.67f,.78f),new Color(.70f,.67f,.78f),new Color(.70f,.67f,.78f),new Color(.70f,.67f,.78f)},sceneryUV,fallback);
        sceneryMesh=scenery.GetComponent<MeshFilter>().sharedMesh;scenery.sortingOrder=-50;
        underlay=Geometry("Violet ground depth visible through spatial fissures",new[]{new Vector3(-12.8f,-2.2f,-2.3f),
            new Vector3(12.8f,-2.2f,-2.3f),new Vector3(12.8f,-2.2f,13.3f),new Vector3(-12.8f,-2.2f,13.3f)},quad,
            new[]{new Color(.004f,.001f,.009f),new Color(.004f,.001f,.009f),new Color(.004f,.001f,.009f),new Color(.004f,.001f,.009f)},underlayUV);
        underlayMesh=underlay.GetComponent<MeshFilter>().sharedMesh;underlay.sortingOrder=-24;
        var faceIndices=new int[(spaceVertices.Length-Faces.Length*2)*3];
        int faceVertex=0,triangle=0;
        for(int i=0;i<Faces.Length;i++)
        {
            for(int k=1;k<Faces[i].Length-1;k++)
            {faceIndices[triangle++]=faceVertex;faceIndices[triangle++]=faceVertex+k;faceIndices[triangle++]=faceVertex+k+1;}
            faceVertex+=Faces[i].Length;
        }
        space=Geometry("Actual battlefield refracted through nine unequal convex spaces",spaceVertices,faceIndices,spaceColors,spaceUV,fallback);
        spaceMesh=space.GetComponent<MeshFilter>().sharedMesh;space.sortingOrder=-20;
        var sideIndices=new int[sideVertices.Length/4*6];
        for(int i=0;i<sideVertices.Length/4;i++)
        {
            int v=i*4,n=i*6;sideIndices[n]=v;sideIndices[n+1]=v+1;sideIndices[n+2]=v+2;
            sideIndices[n+3]=v;sideIndices[n+4]=v+2;sideIndices[n+5]=v+3;
        }
        sides=Geometry("Dark violet spacetime sections behind the broken image",sideVertices,sideIndices,sideColors);
        sideMesh=sides.GetComponent<MeshFilter>().sharedMesh;sides.sortingOrder=-21;
        var lineIndices=new int[edgeVertices.Length/4*6];
        for(int i=0;i<edgeVertices.Length/4;i++)
        {
            int v=i*4,n=i*6;lineIndices[n]=v;lineIndices[n+1]=v+1;lineIndices[n+2]=v+2;
            lineIndices[n+3]=v;lineIndices[n+4]=v+2;lineIndices[n+5]=v+3;
        }
        edges=Geometry("Luminous edges of the physically displaced space layers",edgeVertices,lineIndices,edgeColors);
        edgeMesh=edges.GetComponent<MeshFilter>().sharedMesh;edges.sortingOrder=40;
    }

    private static int FaceVertexCount()
    {int count=0;for(int i=0;i<Faces.Length;i++)count+=Faces[i].Length;return count;}

    private static Vector2[][] SplitSpace()
    {
        var result=new Vector2[Cells.Length][];
        for(int i=0;i<Cells.Length;i++)
        {
            var polygon=new List<Vector2>{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            for(int j=0;j<Cells.Length;j++)
            {
                if(j==i)continue;
                Vector2 normal=Cells[j]-Cells[i];float limit=(Cells[j].sqrMagnitude-Cells[i].sqrMagnitude)*.5f;
                var clipped=new List<Vector2>(8);
                for(int k=0;k<polygon.Count;k++)
                {
                    Vector2 a=polygon[k],b=polygon[(k+1)%polygon.Count];
                    float da=Vector2.Dot(normal,a)-limit,db=Vector2.Dot(normal,b)-limit;
                    if(da<=0f)clipped.Add(a);
                    if((da<0f&&db>0f)||(da>0f&&db<0f))clipped.Add(Vector2.Lerp(a,b,da/(da-db)));
                }
                polygon=clipped;
            }
            result[i]=polygon.ToArray();
        }
        return result;
    }

    public static int SpeciesRow(string id)
    {switch((id??"").ToLowerInvariant()){case "seolmyo":return 1;case "sundongi":return 2;case "hongryeon":return 3;default:return 0;}}
    public static int PreparationPose(float t)
    {
        if(t<SealEndTime)
        {
            int beat=Mathf.Max(0,Mathf.FloorToInt((t-SealStartTime)*SealFrameRate))%2;
            return beat==0?2:3;
        }
        return t<TensionEndTime?3:t<CondenseEndTime?4:5;
    }
    private static float SnapProgress(float start,float end,float t)
    {float u=Mathf.Clamp01(Mathf.InverseLerp(start,end,t));return 1f-(1f-u)*(1f-u)*(1f-u);}
    private static float Irregular(float t,int seed,float rate)
    {
        float clock=t*rate;int sample=Mathf.FloorToInt(clock);
        return Mathf.Lerp(Noise(seed+(sample-1)*17),Noise(seed+sample*17),Ease(0f,.38f,clock-sample))*2f-1f;
    }
    private float Side=>Context.enemy?-1f:1f;
    private Vector3 Screen(float x,float top,float depth=6f)
    {
        // Keep projection local: inverse projection at the distant stage origin loses precision.
        float height=ScreenHeight(1f,depth);
        return View.transform.localPosition+CameraFacing*new Vector3((x-.5f)*height*View.aspect,(.5f-top)*height,depth);
    }
    private float ScreenHeight(float height,float depth=6f)
        =>View.orthographic?height*View.orthographicSize*2f:height*2f*depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
    private Vector3 MapPoint(Vector2 uv)=>new Vector3((uv.x-.5f)*22f*Side,0f,(uv.y-.5f)*13f+5.5f);
    private Vector2 FloorTexcoord(Vector2 uv)
        =>new Vector2(FloorUV.x+FloorUV.width*(Context.enemy?1f-uv.x:uv.x),FloorUV.y+FloorUV.height*uv.y);
    private Vector3 WarpPoint(Vector2 uv,int face)
        =>pivots[face]+offsets[face]+rotations[face]*Vector3.Scale(MapPoint(uv)-pivots[face],scales[face]);
    private Vector3 CellPoint(SpriteRenderer actor,Vector2 marker)
    {
        Bounds bounds=actor.sprite.bounds;
        Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,marker.x),Mathf.Lerp(bounds.max.y,bounds.min.y,marker.y),0f);
        if(actor.flipX)local.x=-local.x;
        return Root.InverseTransformPoint(actor.transform.TransformPoint(local));
    }
    private void Segment(Vector3 a,Vector3 b,float width,Color color)
    {
        if(width<=0f||edgeCount+4>edgeVertices.Length||(b-a).sqrMagnitude<.000001f)return;
        Vector3 normal=Vector3.Cross(b-a,View.transform.forward).normalized*width*.5f;
        edgeVertices[edgeCount]=a-normal;edgeVertices[edgeCount+1]=b-normal;
        edgeVertices[edgeCount+2]=b+normal;edgeVertices[edgeCount+3]=a+normal;
        for(int i=0;i<4;i++)edgeColors[edgeCount+i]=color;edgeCount+=4;
    }
    private void Fracture(Vector3 a,Vector3 b,float width)
    {
        Vector3 toward=-View.transform.forward;
        Segment(a,b,width*3.2f,new Color32(53,24,89,255));
        Segment(a+toward*.008f,b+toward*.008f,width*1.7f,Violet);
        Segment(a+toward*.016f,b+toward*.016f,width*.55f,Core);
    }
    private static bool Inside(Vector2 point,Vector2[] polygon)
    {
        bool plus=false,minus=false;
        for(int i=0;i<polygon.Length;i++)
        {
            Vector2 a=polygon[i],b=polygon[(i+1)%polygon.Length];
            float cross=(b.x-a.x)*(point.y-a.y)-(b.y-a.y)*(point.x-a.x);
            plus|=cross>.0001f;minus|=cross<-.0001f;
        }
        return !(plus&&minus);
    }
    private static int FaceFor(Vector2 uv)
    {for(int i=0;i<Faces.Length;i++)if(Inside(uv,Faces[i]))return i;return 4;}

    private static Vector2 InsetTarget(Vector2 uv,int face)
    {
        float pull=0f;Vector2 center=Cells[face];
        for(int i=0;i<Faces[face].Length;i++)
        {
            Vector2 a=Faces[face][i],edge=Faces[face][(i+1)%Faces[face].Length]-a;
            Vector2 inward=new Vector2(-edge.y,edge.x).normalized;
            float distance=Vector2.Dot(inward,uv-a),inside=Vector2.Dot(inward,center-a);
            if(distance<.018f&&inside>distance)pull=Mathf.Max(pull,(.018f-distance)/(inside-distance));
        }
        return Vector2.Lerp(uv,center,Mathf.Clamp01(pull));
    }

    private void Eye(float t)
    {
        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
        int pose=t<.58f?0:t<.78f?1:2;
        PaintWorld(leadEye,eyes[SpeciesRow(Context.source.species)*4+pose],new Vector3(0f,-4.5f,0f),9f,
            Context.source.color,100,Quaternion.identity,Context.enemy);
        if(t>=.65f)
        {
            float glow=Ease(.65f,.88f,t);
            for(int i=0;i<12;i++)
            {
                float angle=Noise(i*13+3)*Mathf.PI*2f,radius=.15f+Noise(i*31+7)*.8f;
                Vector3 p=new Vector3(Side*(.35f+Mathf.Cos(angle)*radius),.05f+Mathf.Sin(angle)*radius*1.4f,-.05f);
                Pixel(p,.035f*glow,new Color(.76f,.42f,1f,.6f));
            }
        }
    }

    private void LayerCamera(float t)
    {
        float u=Ease(1.20f,FilmImpactTime,t),v=1f-u;
        Vector3 a=new Vector3(-1.8f,13f,-14.5f),b=new Vector3(-1.45f,13.2f,-14.2f);
        Vector3 c=new Vector3(.15f,13.8f,-13.4f),d=new Vector3(1.4f,14.2f,-12.5f);
        Vector3 position=a*(v*v*v)+b*(3f*v*v*u)+c*(3f*v*u*u)+d*(u*u*u);position.x*=Side;
        Vector3 target=new Vector3(Mathf.Lerp(.1f,.6f,u)*Side,.25f,5.5f);
        Vector3 up=Quaternion.AngleAxis(-3f*Side*Ease(2.15f,3.18f,t),Vector3.forward)*Vector3.up;
        CameraPose(position,target,Mathf.Lerp(40f,38f,u),false,up);
    }

    private void Layers(float t)
    {
        Texture2D backdrop=Context.battlefieldBackdrop==null?fallback:Context.battlefieldBackdrop;
        mapTexture.SetTexture("_MainTex",backdrop==null?Texture2D.blackTexture:backdrop);space.SetPropertyBlock(mapTexture);
        scenery.SetPropertyBlock(mapTexture);
        for(int k=0;k<4;k++)
        {
            Vector2 uv=k==0?Vector2.zero:k==1?Vector2.right:k==2?Vector2.one:Vector2.up;
            sceneryUV[k]=new Vector2(SceneryUV.x+SceneryUV.width*uv.x,SceneryUV.y+SceneryUV.height*uv.y);
            underlayUV[k]=new Vector2(FloorUV.x+FloorUV.width*uv.x,FloorUV.y+FloorUV.height*uv.y);
        }
        sceneryMesh.uv=sceneryUV;underlayMesh.uv=underlayUV;
        space.enabled=true;sides.enabled=true;scenery.enabled=true;underlay.enabled=true;
        int vertex=0,section=0;
        for(int i=0;i<Faces.Length;i++)
        {
            float refract=SnapProgress(LayerStart[i],Mathf.Min(TerrainSnapEndTime,LayerStart[i]+.075f+Noise(i*19+7)*.025f),t);
            float compress=.82f*Ease(CompressionStart[i],TensionEndTime,t)+.18f*SnapProgress(CondenseTime,CondenseEndTime,t);
            layerProgress[i]=refract;compressionProgress[i]=compress;
            Vector2 center=Vector2.zero;for(int k=0;k<Faces[i].Length;k++)center+=Faces[i][k];center/=Faces[i].Length;
            pivots[i]=MapPoint(center);
            float twist=Noise(i*29+7)*2f-1f,depth=Noise(i*41+13)*2f-1f;
            float tension=Ease(TerrainSnapEndTime,TensionEndTime,t);
            float tremor=(.018f+.060f*tension)*Ease(TerrainSnapEndTime,2.20f,t)*(1f-Ease(TensionEndTime,CondenseTime,t));
            tremor*=CombatFlashPresentation.Reduced?.5f:1f;
            float rate=22f+Noise(i*13+11)*9f;
            layerTremor[i]=new Vector3(Irregular(t,i*191+7,rate),Irregular(t,i*193+31,rate+3f)*.65f,
                Irregular(t,i*197+73,rate-2f))*tremor;
            offsets[i]=new Vector3((center.x-.53f)*22f*Side*(.045f*refract-.205f*compress),
                depth*(.95f*refract+.52f*compress)+(.10f+Noise(i*7+3)*.18f)*refract,
                (center.y-.47f)*13f*(.055f*refract-.15f*compress)+twist*.18f*compress)+layerTremor[i];
            rotations[i]=Quaternion.Euler((Noise(i*17+11)-.5f)*(28f*refract+9f*compress),
                Side*(Noise(i*23+19)-.5f)*(21f*refract+6f*compress),Side*twist*(14f*refract+4f*compress))
                *Quaternion.Euler(layerTremor[i]*12f);
            scales[i]=new Vector3(1f-(.16f+Noise(i*31+3)*.24f)*compress,1f,
                1f-(.08f+Noise(i*47+5)*.16f)*compress);
            thickness[i]=(.16f+Noise(i*43+17)*.30f)*refract;
            for(int k=0;k<Faces[i].Length;k++)
            {
                int index=vertex+k;Vector2 uv=Faces[i][k];spaceVertices[index]=WarpPoint(uv,i);
                spaceUV[index]=FloorTexcoord(uv);
                float light=1f-(.025f+Noise(i*13+7)*.13f)*refract-.07f*compress;
                spaceColors[index]=new Color(light,light-.025f*compress,light,1f);
            }
            Vector3 back=-(rotations[i]*Vector3.up)*thickness[i];
            for(int k=0;k<Faces[i].Length;k++)
            {
                Vector3 p=spaceVertices[vertex+k],q=spaceVertices[vertex+(k+1)%Faces[i].Length];
                sideVertices[section]=p;sideVertices[section+1]=q;sideVertices[section+2]=q+back;sideVertices[section+3]=p+back;
                sideColors[section]=sideColors[section+1]=new Color(.16f,.045f,.25f,refract);
                sideColors[section+2]=sideColors[section+3]=new Color(.022f,.006f,.045f,refract);section+=4;
                if(refract<=0f)continue;
                Vector2 a=Faces[i][k],b=Faces[i][(k+1)%Faces[i].Length];
                bool outside=(a.x<.001f&&b.x<.001f)||(a.x>.999f&&b.x>.999f)||(a.y<.001f&&b.y<.001f)||(a.y>.999f&&b.y>.999f);
                if(outside||Noise(i*53+k*7+2)<.26f)continue;
                p-=View.transform.forward*.025f;q-=View.transform.forward*.025f;
                float width=(.018f+.025f*compress)*refract;
                for(int part=0;part<3;part++)
                {
                    float begin=part*.33f+.035f+Noise(i*17+k*11+part)*.045f;
                    float end=Mathf.Min(1f,begin+.14f+Noise(i*31+k*3+part)*.09f);
                    Fracture(Vector3.Lerp(p,q,begin),Vector3.Lerp(p,q,end),width);
                }
            }
            vertex+=Faces[i].Length;
        }
        spaceMesh.vertices=spaceVertices;spaceMesh.uv=spaceUV;spaceMesh.colors=spaceColors;spaceMesh.RecalculateBounds();
        sideMesh.vertices=sideVertices;sideMesh.colors=sideColors;sideMesh.RecalculateBounds();
        Targets();
    }

    private void Targets()
    {
        int count=Mathf.Min(victims.Length,Context.targets.Length);if(count==0)return;
        Vector2 min=Context.targets[0].ground,max=min;
        for(int i=1;i<count;i++){min=Vector2.Min(min,Context.targets[i].ground);max=Vector2.Max(max,Context.targets[i].ground);}
        Vector2 span=max-min;
        for(int i=0;i<count;i++)
        {
            var actor=Context.targets[i];Sprite sprite=actor.sprite;if(sprite==null)continue;
            float x=span.x>.00001f?(actor.ground.x-min.x)/span.x:.5f;
            float y=span.y>.00001f?(actor.ground.y-min.y)/span.y:.5f;
            if(span.sqrMagnitude<.000001f&&count>1){x=(i%4+.5f)/Mathf.Min(4,count);y=(i/4+.5f)/Mathf.Ceil(count/4f);}
            Vector2 uv=new Vector2(Mathf.Lerp(.32f,.90f,x),Mathf.Lerp(.18f,.82f,y));
            int face=FaceFor(uv);targetUV[i]=InsetTarget(uv,face);targetFace[i]=face;
            Vector3 feet=WarpPoint(targetUV[i],face)+(rotations[face]*Vector3.up)*.065f;targetFeet[i]=feet;
            float height=Mathf.Clamp(actor.size*1.8f,1.4f,2.5f)*(1f-.06f*compressionProgress[face]);targetHeights[i]=height;
            float roll=Mathf.Clamp(Mathf.DeltaAngle(0f,rotations[face].eulerAngles.z),-5f,5f);
            Quaternion billboard=CameraFacing*Quaternion.AngleAxis(roll,Vector3.forward);
            float screenY=View.WorldToViewportPoint(Root.TransformPoint(feet)).y;
            PaintWorld(victims[i],sprite,feet,height,actor.color,25+Mathf.RoundToInt((1f-screenY)*12f),billboard,actor.flipX);
            Vector3 scale=victims[i].transform.localScale;scale.x*=scales[face].x;
            Bounds body=PixelMantisReaper.VisibleBody(sprite);
            Vector3 anchor=new Vector3(actor.flipX?-body.center.x:body.center.x,body.min.y,0f);
            victims[i].transform.localScale=scale;
            victims[i].transform.localPosition=feet-billboard*Vector3.Scale(anchor,scale);
        }
    }

    private void Caster(float t)
    {
        float compress=Ease(2.15f,3.20f,t);
        float x=Mathf.Lerp(.18f,.20f,compress);if(Context.enemy)x=1f-x;
        float height=Mathf.Lerp(.38f,.40f,compress);
        int pose=PreparationPose(t),row=SpeciesRow(Context.source.species);
        Bounds reference=PixelMantisReaper.VisibleBody(motion[row*8+2]),standing=PixelMantisReaper.VisibleBody(motion[row*8]);
        float scale=ScreenHeight(height,6.2f)/Mathf.Max(.001f,reference.size.y);
        float charge=Ease(1.30f,1.65f,t)*(1f-Ease(3.10f,3.30f,t));
        float tick=t*30f;int sample=Mathf.FloorToInt(tick),seed=row*197+(Context.enemy?771:113);
        float jump=Ease(0f,.32f,tick-sample);
        Vector2 tremor=new Vector2(
            Mathf.Lerp(Noise(seed+(sample-1)*17),Noise(seed+sample*17),jump)*2f-1f,
            Mathf.Lerp(Noise(seed+(sample-1)*31+89),Noise(seed+sample*31+89),jump)*2f-1f);
        tremor=Vector2.Scale(tremor,new Vector2(4.5f/1920f,3f/1080f))*charge*(CombatFlashPresentation.Reduced?.5f:1f);
        Vector3 feet=Screen(x+tremor.x,.76f+height*.5f+tremor.y,6.2f);
        Vector3 anchor=new Vector3(Context.enemy?-standing.center.x:standing.center.x,(8f-64f)/64f,0f);
        Vector2 faceOffset=(Face[row][pose-1]-Face[row][1])/motion[row*8+pose].pixelsPerUnit;
        anchor+=new Vector3(faceOffset.x*Side,-faceOffset.y,0f);
        lead.enabled=true;lead.sprite=motion[row*8+pose];lead.flipX=Context.enemy;lead.color=Context.source.color;lead.sortingOrder=70;
        lead.transform.localRotation=CameraFacing;lead.transform.localScale=Vector3.one*scale;
        lead.transform.localPosition=feet-CameraFacing*(anchor*scale);
        releaseHand=CellPoint(lead,Paw[row][pose-1]/128f);
        if(t>=SealEndTime&&t<FilmImpactTime)
        {
            float reach=SnapProgress(SealEndTime,TerrainSnapTime,t),tight=Ease(TensionEndTime,CondenseEndTime,t);
            float size=ScreenHeight(Mathf.Lerp(.13f,.065f,tight)*reach,6.1f);
            PaintWorld(handRift,rifts[tight>.6f?1:0],releaseHand-View.transform.up*size*.5f,size,Color.white,90,CameraFacing);
        }
    }

    private void Scene(float t)
    {
        LayerCamera(t);Layers(t);Caster(t);
        float reveal=SnapProgress(TerrainSnapTime,TerrainSnapEndTime,t),compress=Ease(TerrainSnapEndTime,CondenseEndTime,t);
        Vector2 fault=new Vector2(.615f,.55f);float height=7.4f*reveal;
        Vector3 center=WarpPoint(fault,FaceFor(fault))+View.transform.up*height*.5f;
        int frame=compress<.3f?0:compress<.8f?1:2;
        // A narrow opening exposes the void without masking the eight real target silhouettes.
        PaintArtWorld(centralRift,rifts[frame],center,new Vector2(.55f+.12f*compress,height),new Color(1f,1f,1f,.82f),20,CameraFacing);
        for(int i=0;i<fragments.Length;i++)
        {
            float age=t-(TerrainSnapTime+.03f+Noise(i*37+5)*.19f);if(age<=0f)continue;
            int face=i%Faces.Length,edge=(i*3+1)%Faces[face].Length;
            Vector2 uv=Vector2.Lerp(Faces[face][edge],Faces[face][(edge+1)%Faces[face].Length],.20f+Noise(i*17+7)*.55f);
            Vector3 direction=new Vector3((Noise(i*19+3)-.5f)*.40f,.08f+Noise(i*23+9)*.18f,-.12f-Noise(i*29+11)*.25f);
            Vector3 position=WarpPoint(uv,face)+direction*age-View.transform.forward*.12f;
            float size=(.13f+Noise(i*13+2)*.28f)*Ease(0f,.10f,age);
            PaintWorld(fragments[i],rifts[6+i%2],position,size,new Color(.75f,.48f,1f,.66f),35,
                CameraFacing*Quaternion.Euler(Noise(i*11+4)*32f,Noise(i*31+8)*28f,Noise(i*43+6)*180f+age*(Noise(i*41+7)-.5f)*22f));
            if(i%2==0)Pixel(position+direction*.22f,.025f+Noise(i+27)*.025f,new Color(.69f,.37f,1f,.5f));
        }
    }

    private void MatchCut(float t)
    {
        float p=Ease(1.12f,1.32f,t);if(p<=0f||p>=1f)return;
        float thickness=ScreenHeight(Mathf.Sin(p*Mathf.PI)*1.10f,3f);
        if(p>.30f&&p<.72f)
            Segment(Screen(-.1f,.5f,3.1f),Screen(1.1f,.5f,3.1f),ScreenHeight(1.25f,3.1f),new Color32(50,18,77,255));
        Segment(Screen(-.12f,.88f,3f),Screen(1.12f,.12f,3f),thickness,new Color32(100,43,150,255));
        Fracture(Screen(.61f,-.12f,2.95f),Screen(.50f,1.12f,2.95f),ScreenHeight(.055f*Mathf.Sin(p*Mathf.PI),2.95f));
    }

    private void Return(float t)
    {
        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);PaintReturn(field,70);
        float age=t-FilmImpactTime,fade=1f-Ease(.06f,.50f,age);
        bool reduced=CombatFlashPresentation.Reduced;
        float x=Context.enemy ? .40f : .60f;
        Vector3 center=Screen(x,.51f,5f);
        if(age<.18f)
        {
            float height=ScreenHeight(Mathf.Lerp(.38f,reduced?.68f:.85f,SnapProgress(0f,.075f,age)),5f);
            PaintWorld(impactRift,rifts[4],center-Vector3.up*height*.5f,height,new Color(1f,1f,1f,1f-Ease(.04f,.18f,age)),120,Quaternion.identity);
        }
        if(age<.30f)
        {
            float height=ScreenHeight(Mathf.Lerp(.20f,.92f,SnapProgress(0f,.10f,age)),5.1f);
            PaintWorld(waveRift,rifts[5],center-Vector3.up*height*.5f,height,new Color(1f,1f,1f,(1f-Ease(.04f,.30f,age))*(reduced?.35f:.55f)),110,Quaternion.identity);
        }
        for(int i=0;i<fragments.Length;i++)
        {
            if(fade<=0f)break;
            float angle=Noise(i*43+3)*Mathf.PI*2f;
            float travel=(1f-Mathf.Exp(-age*7f))*(.8f+Noise(i*17+19)*3.2f)+age*(1.2f+Noise(i*23+7)*1.8f);
            Vector3 position=center+new Vector3(Mathf.Cos(angle)*travel,Mathf.Sin(angle)*travel*.75f-age*age*.6f,0f);
            float height=.32f+Noise(i*11+2)*.48f;
            Paint(fragments[i],rifts[6+i%2],new Vector2(position.x,position.y),height,new Color(1f,1f,1f,fade),100,false,
                Noise(i+71)*180f+age*(Noise(i+13)-.5f)*180f);
        }
        if(age<.22f)for(int i=0;i<(reduced?24:72);i++)
        {
            float angle=Noise(i*11+3)*Mathf.PI*2f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f);
            float travel=(1f-Mathf.Exp(-age*15f))*(.5f+Noise(i+31)*3.8f);
            Pixel(center+direction*travel,(reduced?.035f:.055f)*(1f-age/.22f),new Color(.85f,.59f,1f,fade));
        }
    }

    protected override void Animate(float t,float dt)
    {
        edgeCount=0;space.enabled=false;sides.enabled=false;scenery.enabled=false;underlay.enabled=false;edges.enabled=false;
        ActionTime=t<FilmImpactTime?Mathf.Lerp(0f,NinjaSpacetimeRules.Windup-.001f,Ease(0f,FilmImpactTime,t)):
            Mathf.Lerp(NinjaSpacetimeRules.Windup,NinjaSpacetimeRules.Duration,Ease(FilmImpactTime,Duration,t));
        Shot=t<.35f?1:t<1.20f?2:t<2.15f?3:t<FieldReturnTime?4:t<3.65f?5:6;
        SoundBeat=t<.35f?-1:t<TerrainSnapTime?0:t<FilmImpactTime?1:2;
        Opacity=1f-Ease(3.65f,Duration,t);
        View.backgroundColor=t>=.35f&&t<FieldReturnTime?new Color(.018f,.009f,.035f,1f):Color.clear;
        if(t<.35f){CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);PaintReturn(field,70);}
        else if(t<1.20f)Eye(t);
        else if(t<FieldReturnTime)Scene(t);
        else Return(t);
        MatchCut(t);
        for(int i=edgeCount;i<edgeVertices.Length;i++){edgeVertices[i]=Vector3.zero;edgeColors[i]=Color.clear;}
        edgeMesh.vertices=edgeVertices;edgeMesh.colors=edgeColors;edgeMesh.RecalculateBounds();edges.enabled=edgeCount>0;
    }
}
