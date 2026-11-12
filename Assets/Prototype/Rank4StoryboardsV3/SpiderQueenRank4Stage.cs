using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpiderQueenRank4Stage:Rank4CinematicStage
{
    public const string ResourceRoot="CombatCinematics/AraneaCeilingV1/";
    public const float DomainRevealTime=3.95f,FieldReturnTime=5.22f;
    public static readonly float[] BoardTimes={.20f,1.12f,2.20f,3.10f,3.70f,5.75f};
    private const float CeilingHeight=10f,CeilingScale=4f/9f,LensDepth=4f,GroundScale=.9f;
    private static readonly Quaternion FloorRotation=Quaternion.Euler(90f,0f,0f);
    private static readonly Color Silk=new Color32(255,230,186,255);
    private static readonly Color SilkShade=new Color32(85,65,89,255);
    private static readonly Color DomainSilk=new Color(.76f,.49f,1f,1f);
    private static readonly Color DomainRim=new Color(.88f,.66f,1f,1f);
    private static readonly Vector2[] AscentPalm={new Vector2(.81f,.63f),new Vector2(.87f,.23f),
        new Vector2(.80f,.21f),new Vector2(.73f,.23f),new Vector2(.76f,.23f),new Vector2(.74f,.23f)};
    private static readonly Vector3 WebCenter=new Vector3(0f,.055f,-.85f);
    private static readonly Vector3[] WebRim={new Vector3(-6.8f,.055f,2.9f),new Vector3(-2.8f,.055f,3.9f),
        new Vector3(2.4f,.055f,3.65f),new Vector3(6.9f,.055f,2.5f),new Vector3(7f,.055f,-2.2f),
        new Vector3(3.4f,.055f,-4.1f),new Vector3(-2.9f,.055f,-4f),new Vector3(-7.15f,.055f,-2f)};
    private readonly Sprite[] motion,ascent,enemyTop,enemyTopShiny,floor,canopy;
    private readonly Transform groundRoot;
    private readonly SpriteRenderer lead,field,setting,ceiling,shadow;
    private readonly SpriteRenderer[] targets=new SpriteRenderer[8],targetShadows=new SpriteRenderer[8];
    private readonly MeshRenderer silk;
    private readonly Mesh silkMesh;
    private readonly PixelSilkQueenDomain floorDomain;
    private readonly MeshRenderer floorDomainRenderer;
    private readonly Vector3[] threadVertices=new Vector3[8192];
    private readonly Color[] threadColors=new Color[8192];
    private readonly Dictionary<string,int> targetCells=new Dictionary<string,int>(StringComparer.Ordinal);
    private readonly Dictionary<string,HashSet<Sprite>> prefabCombatFrames=new Dictionary<string,HashSet<Sprite>>(StringComparer.Ordinal);
    private int vertexCount;
    private Vector3 releaseHand,releaseLeftHand,releaseRightHand;
    public override string Family=>"spider";
    public override float Duration=>6.10f;

    public SpiderQueenRank4Stage(Transform parent):base(parent,"Aranea ceiling ascent and vertical web chamber",Color.clear,1920,1080)
    {
        motion=LoadAtlas(ResourceRoot+"Motion",8,5);
        ascent=LoadAtlas(ResourceRoot+"Ascent",6,5);
        enemyTop=LoadAtlas(ResourceRoot+"EnemyTop",8,10);
        if(Resources.Load<Texture2D>(ResourceRoot+"EnemyTopShiny")!=null)enemyTopShiny=LoadAtlas(ResourceRoot+"EnemyTopShiny",8,10);
        floor=LoadAtlas(ResourceRoot+"ChamberFloor",1);
        if(Resources.Load<Texture2D>(ResourceRoot+"Canopy")!=null)canopy=LoadAtlas(ResourceRoot+"Canopy",1);
        lead=MakeSprite("Lead ceiling true dorsal spider",100);
        field=MakeSprite("Lead actual battlefield ascent and return",100);
        var groundObject=new GameObject("Distant battlefield floor and original domain")
            {layer=Root.gameObject.layer,hideFlags=HideFlags.DontSave};
        groundRoot=groundObject.transform;groundRoot.SetParent(Root,false);groundRoot.localScale=Vector3.one*GroundScale;
        setting=MakeSprite("New vertical chamber floor",-100);
        setting.transform.SetParent(groundRoot,false);
        ceiling=MakeSprite("Close ceiling suspension silk",110);
        shadow=MakeSprite("Ceiling spider distant floor shadow",-5);
        shadow.transform.SetParent(groundRoot,false);
        for(int i=0;i<targets.Length;i++)
        {
            targets[i]=MakeSprite("Actual target dorsal body "+i,10);
            targetShadows[i]=MakeSprite("Actual target floor shadow "+i,-3);
            targets[i].transform.SetParent(groundRoot,false);targetShadows[i].transform.SetParent(groundRoot,false);
        }
        var indices=new int[threadVertices.Length/4*6];
        for(int i=0;i<threadVertices.Length/4;i++)
        {
            int v=i*4,n=i*6;indices[n]=v;indices[n+1]=v+1;indices[n+2]=v+2;
            indices[n+3]=v;indices[n+4]=v+2;indices[n+5]=v+3;
        }
        silk=Geometry("Continuous ascent launch and floor web",threadVertices,indices,threadColors);
        silk.sortingOrder=25;silkMesh=silk.GetComponent<MeshFilter>().sharedMesh;
        floorDomain=PixelSilkQueenDomain.Create(new Bounds(Vector3.zero,new Vector3(15.2f,.1f,8f)),null);
        floorDomain.transform.SetParent(groundRoot,false);floorDomain.transform.localPosition=new Vector3(0f,.027f,0f);
        floorDomain.gameObject.layer=Root.gameObject.layer;floorDomain.enabled=false;
        floorDomainRenderer=floorDomain.GetComponent<MeshRenderer>();floorDomainRenderer.sortingOrder=3;
        floorDomainRenderer.enabled=false;
    }

    public static int SpeciesRow(string id)
    {switch((id??"").ToLowerInvariant()){case "silanti":return 1;case "ragon":case "lagon":return 2;case "icearachne":return 3;default:return 0;}}

    public static int TargetCell(string species)
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database==null?null:database.Get(species);
        int number;
        return definition!=null&&int.TryParse(definition.serialNumber,out number)&&number>=1&&number<=80?number-1:-1;
    }

    private Sprite TargetSprite(FrogCatchCinematicStage.Actor actor)
    {
        if(actor.frames!=null&&actor.frames.Length==1&&!PrefabCombatFrames(actor.species).Contains(actor.sprite))return actor.sprite;
        int cell;
        if(!targetCells.TryGetValue(actor.species??"",out cell))
        {cell=TargetCell(actor.species);targetCells[actor.species??""]=cell;}
        if(cell<0)return actor.sprite;
        // Summons and unprovided shiny drawings keep their exact snapshot instead of another species' art.
        if(actor.shiny&&enemyTopShiny==null)return actor.sprite;
        return (actor.shiny?enemyTopShiny:enemyTop)[cell];
    }

    private HashSet<Sprite> PrefabCombatFrames(string species)
    {
        string key=species??"";HashSet<Sprite> frames;
        if(prefabCombatFrames.TryGetValue(key,out frames))return frames;
        frames=new HashSet<Sprite>();prefabCombatFrames[key]=frames;
        MonsterDatabase database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        MonsterDefinition definition=database==null?null:database.Get(species);
        if(definition==null)return frames;
        foreach(string path in new[]{definition.resourcePath,string.IsNullOrEmpty(definition.shinyResourcePath)?"Enemies/Shiny/"+definition.monsterId:definition.shinyResourcePath})
        {
            if(string.IsNullOrEmpty(path))continue;
            GameObject prefab=Resources.Load<GameObject>(path);
            SpriteMonsterAnimationDriver driver=prefab==null?null:prefab.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
            if(driver!=null)foreach(Sprite sprite in driver.CombatFrames())frames.Add(sprite);
        }
        return frames;
    }

    private Vector3 Screen(float x,float top,float depth=LensDepth)
        =>Root.InverseTransformPoint(View.ViewportToWorldPoint(new Vector3(x,1f-top,depth)));

    private Vector3 SpritePoint(SpriteRenderer actor,Vector2 topLeft)
    {
        Bounds bounds=PixelMantisReaper.VisibleBody(actor.sprite);
        Vector3 point=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,topLeft.x),Mathf.Lerp(bounds.max.y,bounds.min.y,topLeft.y),0f);
        if(actor.flipX)point.x=-point.x;
        return Root.InverseTransformPoint(actor.transform.TransformPoint(point));
    }

    private Vector3 CellPoint(SpriteRenderer actor,Vector2 topLeft)
    {
        Bounds bounds=actor.sprite.bounds;
        Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,topLeft.x),Mathf.Lerp(bounds.max.y,bounds.min.y,topLeft.y),0f);
        if(actor.flipX)local.x=-local.x;
        return Root.InverseTransformPoint(actor.transform.TransformPoint(local));
    }

    private Vector3 GroundPoint(Vector3 point)=>groundRoot.localPosition+Vector3.Scale(point,groundRoot.localScale);

    private void Stroke(Vector3 a,Vector3 b,float width,Color color)
    {
        if(width<=0f||vertexCount+4>threadVertices.Length||(b-a).sqrMagnitude<.000001f)return;
        Vector3 side=Vector3.Cross(b-a,View.transform.forward).normalized*width*.5f;
        threadVertices[vertexCount]=a-side;threadVertices[vertexCount+1]=b-side;
        threadVertices[vertexCount+2]=b+side;threadVertices[vertexCount+3]=a+side;
        for(int i=0;i<4;i++)threadColors[vertexCount+i]=color;
        vertexCount+=4;
    }

    private void Thread(Vector3 a,Vector3 b,float width,Color color)
    {
        Stroke(a+View.transform.forward*.001f,b+View.transform.forward*.001f,width*2.1f,SilkShade);
        Stroke(a,b,width,color);
    }

    private void LensThreads(float t,float start,float end,bool entering)
    {
        float p=Ease(start,end,t);
        if(p<=0f||p>=1f)return;
        float screenScale=View.orthographic?1f:2f*LensDepth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f)/9f;
        float thickness=Mathf.Sin(p*Mathf.PI)*1.65f*screenScale;
        Color curtainSilk=entering?Silk:Color.Lerp(Silk,DomainRim,Ease(5.10f,5.28f,t));
        if(p>.40f&&p<.65f)
            Stroke(Screen(-.1f,.5f,LensDepth+.1f),Screen(1.1f,.5f,LensDepth+.1f),10.4f*screenScale,
                entering?new Color32(185,166,149,255):new Color32(117,89,141,255));
        for(int i=0;i<11;i++)
        {
            float y=-.45f+i*.19f+(entering?p:1f-p)*.8f;
            Thread(Screen(-.1f,y),Screen(1.1f,y+.42f),thickness*(i%3==0?1.4f:.7f),curtainSilk);
        }
        for(int i=0;i<7;i++)
        {
            float x=-.25f+i*.25f;
            Thread(Screen(x,-.1f),Screen(x+.32f,1.1f),thickness*.48f,curtainSilk);
        }
    }

    private void DrawAscent(float t)
    {
        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
        if(!Context.returnBodyVisible)return;
        Vector2 foot=Context.returnPosition;
        float rise=Mathf.Pow(Mathf.InverseLerp(.62f,1.50f,t),2.4f);
        foot.y=Mathf.Lerp(foot.y,6.2f,rise);
        float pixel=9f/1080f;
        foot.x=Mathf.Round(foot.x/pixel)*pixel;foot.y=Mathf.Round(foot.y/pixel)*pixel;
        float height=Mathf.Max(.3f,Context.returnHeight);
        Sprite original=Context.returnSprite??Context.source.sprite;
        int pose=-1;
        if(t<.25f)Paint(field,original,foot,height,Context.source.color,100,Context.source.flipX);
        else
        {
            int row=SpeciesRow(Context.source.species);if(Context.source.shiny&&row==2)row=4;
            pose=Mathf.Clamp(Mathf.FloorToInt((t-.25f)/1.25f*6f),0,5);
            Bounds reference=PixelMantisReaper.VisibleBody(ascent[row*6]);
            float scale=height/Mathf.Max(.001f,reference.size.y);
            Vector3 anchor=new Vector3(Context.source.flipX?-reference.center.x:reference.center.x,reference.min.y,0f);
            field.enabled=true;field.sprite=ascent[row*6+pose];field.flipX=Context.source.flipX;
            field.color=Context.source.color;field.sortingOrder=100;field.transform.localRotation=Quaternion.identity;
            field.transform.localScale=Vector3.one*scale;field.transform.localPosition=(Vector3)foot-anchor*scale;
        }
        if(t<.44f)
        {
            float pop=Ease(0f,.09f,t)*(1f-Ease(.34f,.44f,t));
            Vector3 mark=new Vector3(Context.returnPosition.x,Context.returnPosition.y+height+.34f,-.1f);
            Color gold=new Color32(255,224,103,255);
            Stroke(mark+Vector3.up*.12f,mark+Vector3.up*.47f,.11f*pop,gold);
            Pixel(mark,.11f*pop,gold);
        }
        if(t>=.32f)
        {
            Vector3 hand=pose<0?SpritePoint(field,new Vector2(.74f,.57f)):CellPoint(field,AscentPalm[pose]);
            Vector3 anchor=new Vector3(Context.returnPosition.x+(Context.source.flipX?-.14f:.14f),7.2f,-.05f);
            Vector3 tip=Vector3.Lerp(hand,anchor,Ease(.32f,.55f,t));
            Thread(hand,tip,.038f,Silk);
            if(t<.61f)for(int i=0;i<8;i++)
            {
                float p=Noise(i+41);Vector3 spark=Vector3.Lerp(hand,tip,p);
                Pixel(spark+Vector3.right*(Noise(i+18)-.5f)*.12f,.025f,Silk);
            }
        }
    }

    private void DrawTargets(float alpha)
    {
        int count=Mathf.Min(targets.Length,Context.targets.Length);
        if(count==0)return;
        Vector2 min=Context.targets[0].ground,max=min;
        for(int i=1;i<count;i++){min=Vector2.Min(min,Context.targets[i].ground);max=Vector2.Max(max,Context.targets[i].ground);}
        Vector2 center=(min+max)*.5f,span=max-min;
        bool coincident=span.sqrMagnitude<.000001f;
        for(int i=0;i<count;i++)
        {
            var actor=Context.targets[i];Sprite sprite=TargetSprite(actor);if(sprite==null)continue;
            Vector2 relative=actor.ground-center;
            Vector2 ground=new Vector2(span.x>.00001f?relative.x*11.4f/span.x:0f,
                span.y>.00001f?relative.y*3.2f/span.y:0f);
            if(coincident&&count>1)
            {
                int columns=Mathf.Min(4,count),rows=(count+columns-1)/columns,row=i/columns;
                int rowCount=Mathf.Min(columns,count-row*columns);
                ground=new Vector2((i%columns-(rowCount-1)*.5f)*1.3f,(row-(rows-1)*.5f)*1.1f);
            }
            Vector3 point=new Vector3(ground.x,.09f,ground.y-1.7f);
            float height=Mathf.Clamp(actor.size*.65f,.62f,.94f);
            float angle=Mathf.Atan2(-point.x,3f-point.z)*Mathf.Rad2Deg;
            Quaternion rotation=Quaternion.AngleAxis(angle,Vector3.up)*FloorRotation;
            point-=rotation*Vector3.up*(height*.5f);
            Color color=actor.color;color.a*=alpha;
            PaintWorld(targetShadows[i],sprite,point+new Vector3(.05f,-.04f,-.04f),height,new Color(.04f,.025f,.07f,.34f*alpha),-3,rotation);
            PaintWorld(targets[i],sprite,point,height,color,10,rotation);
        }
    }

    private void DrawHeightSpace(float t)
    {
        for(int i=0;i<4;i++)
        {
            float side=i<2?-1f:1f;
            Vector3 near=new Vector3(side*3.3f,CeilingHeight+.08f,i%2==0?2.05f:1.1f);
            Vector3 far=GroundPoint(new Vector3(side*7.7f,.07f,i%2==0?4f:-4f));
            Thread(near,far,.010f,new Color32(137,128,158,255));
        }
        if(t<1.74f||t>=3.45f)return;
        for(int i=0;i<16;i++)
        {
            float fall=Mathf.Repeat(t-1.74f+Noise(i*13+7)*1.3f,1.3f)/1.3f;
            Vector3 from=new Vector3((Noise(i*19+3)-.5f)*5.6f,CeilingHeight-.2f,1.4f+(Noise(i*7+2)-.5f)*2f);
            Vector3 to=GroundPoint(new Vector3((Noise(i*23+11)-.5f)*14f,.06f,(Noise(i*17+5)-.5f)*7f));
            Color color=new Color(.78f,.73f,.88f,.5f*(1f-Ease(2.85f,3.45f,t)));
            Pixel(Vector3.Lerp(from,to,fall*fall),.022f,color);
        }
    }

    private void DrawFloorWeb(float t)
    {
        float shoot=Ease(3.45f,3.63f,t),grow=Ease(3.60f,4.32f,t);
        if(shoot<=0f)return;
        float floorWidth=1f-Ease(DomainRevealTime,4.35f,t);
        Color floorSilk=Color.Lerp(Silk,DomainSilk,Ease(3.63f,4.15f,t));
        if(t>=DomainRevealTime)
        {
            floorDomainRenderer.enabled=true;
            floorDomain.Reveal(t-DomainRevealTime);
        }
        Vector3 left=releaseLeftHand,right=releaseRightHand;
        Vector3 center=GroundPoint(WebCenter);
        for(int i=0;i<WebRim.Length;i++)
        {
            Vector3 rim=GroundPoint(WebRim[i]);Vector3 launch=(i&1)==0?left:right;
            if(t<3.93f)
            {
                float fade=1f-Ease(3.68f,3.93f,t);
                Thread(launch,Vector3.Lerp(launch,rim,shoot),.060f*CeilingScale*fade,Silk);
            }
            if(grow>0f)
            {
                Vector3 spoke=Vector3.Lerp(center,rim,grow);
                Thread(center,spoke,.028f*GroundScale*floorWidth,floorSilk);
                if(grow<1f)Pixel(spoke+Vector3.up*.04f,.055f*GroundScale*floorWidth,floorSilk);
            }
            float impact=t-3.63f-i*.008f;
            if(impact>=0f&&impact<.30f)for(int k=0;k<12;k++)
            {
                float a=Noise(k*17+i*11)*Mathf.PI*2f;
                Vector3 direction=new Vector3(Mathf.Cos(a),.2f+Noise(k+9),Mathf.Sin(a));
                Pixel(GroundPoint(WebRim[i]+direction*impact*(1f+Noise(k+23)*3f)),
                    .055f*GroundScale*(1f-impact/.30f),floorSilk);
            }
        }
        for(int ring=1;ring<=4;ring++)
        {
            float fill=Ease(3.61f+ring*.08f,3.83f+ring*.10f,t);
            if(fill<=0f)continue;
            float radius=ring*.235f;
            for(int i=0;i<WebRim.Length;i++)
            {
                Vector3 a=Vector3.Lerp(WebCenter,WebRim[i],radius);
                Vector3 b=Vector3.Lerp(WebCenter,WebRim[(i+1)%WebRim.Length],radius);
                Vector3 mid=(a+b)*.5f+new Vector3((Noise(i*13+ring)-.5f)*.15f,0f,(Noise(i*29+ring)-.5f)*.12f);
                Thread(GroundPoint(a),GroundPoint(Vector3.Lerp(a,mid,Mathf.Min(1f,fill*2f))),.021f*GroundScale*floorWidth,floorSilk);
                if(fill>.5f)Thread(GroundPoint(mid),GroundPoint(Vector3.Lerp(mid,b,(fill-.5f)*2f)),.021f*GroundScale*floorWidth,floorSilk);
            }
        }
        if(t>=3.80f)for(int side=-1;side<=1;side+=2)
        {
            Vector3 loop=new Vector3(side*6.7f,.07f,-.75f);
            float appear=Ease(3.80f,DomainRevealTime,t);
            for(int i=0;i<18;i++)
            {
                float a=i*Mathf.PI*2f/18f,b=(i+1)*Mathf.PI*2f/18f;
                Vector3 p=loop+new Vector3(Mathf.Cos(a)*.24f*appear,0f,Mathf.Sin(a)*.24f*appear);
                Vector3 q=loop+new Vector3(Mathf.Cos(b)*.24f*appear,0f,Mathf.Sin(b)*.24f*appear);
                Thread(GroundPoint(p),GroundPoint(q),.024f*GroundScale*floorWidth,floorSilk);
            }
        }
    }

    private void DrawCeiling(float t)
    {
        CameraPose(new Vector3(0f,18f,0f),Vector3.zero,2f*Mathf.Atan(.25f)*Mathf.Rad2Deg,false,Vector3.forward);
        float arrive=Ease(1.50f,1.74f,t),retreat=Ease(1.74f,2.55f,t);
        groundRoot.localPosition=new Vector3(0f,Mathf.Lerp(0f,-15.75f,retreat),Mathf.Lerp(-.36f,-3.375f,retreat));
        Color floorColor=Color.Lerp(new Color(.82f,.84f,.92f),new Color(.55f,.58f,.70f),retreat);floorColor.a=arrive;
        PaintArtWorld(setting,floor[0],Vector3.zero,new Vector2(16f,9f),floorColor,-100,FloorRotation);
        DrawTargets(arrive);
        int row=SpeciesRow(Context.source.species);if(Context.source.shiny&&row==2)row=4;
        int pose=t<1.90f?0:t<2.55f?1:t<2.82f?2:t<3.45f?3:t<3.63f?4:t<DomainRevealTime?5:t<4.35f?6:7;
        float height=4.14f*CeilingScale;
        Vector3 feet=new Vector3(0f,CeilingHeight,.45f*CeilingScale);
        Color sourceColor=Context.source.color;sourceColor.a*=arrive;
        PaintWorld(lead,motion[row*8+pose],feet,height,sourceColor,100,FloorRotation);
        PaintWorld(shadow,motion[row*8+pose],new Vector3(.10f*CeilingScale,.025f,.43f*CeilingScale),height,
            new Color(.03f,.02f,.05f,.14f*arrive),-5,FloorRotation);
        if(canopy!=null)PaintArtWorld(ceiling,canopy[0],new Vector3(0f,CeilingHeight+.12f,0f),
            new Vector2(16f,9f)*CeilingScale*1.12f,new Color(1f,1f,1f,arrive),110,FloorRotation);
        float handSpread=pose==3?.04f:pose==4?.15f:.11f,handY=pose==4?.75f:.78f;
        releaseLeftHand=CellPoint(lead,new Vector2(.5f-handSpread,handY));
        releaseRightHand=CellPoint(lead,new Vector2(.5f+handSpread,handY));
        releaseHand=(releaseLeftHand+releaseRightHand)*.5f;
        DrawHeightSpace(t);
        // The four anchors and each shot keep the same vertical world axis through the cut.
        for(int i=0;i<4;i++)
        {
            float side=i<2?-1f:1f,edge=i%2==0?4.5f:2.7f;
            Vector3 anchor=new Vector3(side*7.8f*CeilingScale,CeilingHeight+.1f,edge*CeilingScale);
            Vector3 grip=new Vector3(side*(i%2==0?1.55f:1.1f)*CeilingScale,CeilingHeight+.04f,(2.8f+i%2*.8f)*CeilingScale);
            Thread(grip,anchor,.032f*CeilingScale,Silk);
        }
        float charge=Ease(2.55f,3.45f,t);
        if(t>=2.55f&&t<3.63f)
        {
            Vector3 bundle=releaseHand+new Vector3(0f,.05f,-.10f*CeilingScale);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2f/12f+t*.7f;
                float radius=Mathf.Lerp(.72f,.22f,charge)*CeilingScale;
                Vector3 end=bundle+new Vector3(Mathf.Cos(a)*radius,0f,Mathf.Sin(a)*radius*.45f);
                Thread(bundle,end,.023f*CeilingScale,Silk);
                Pixel(end,.048f*CeilingScale,Silk);
            }
        }
        DrawFloorWeb(t);
        if(t<1.74f)LensThreads(t,1.50f,1.74f,true);
        LensThreads(t,5.10f,5.35f,false);
    }

    protected override void Animate(float t,float dt)
    {
        vertexCount=0;ActionTime=-1f;floorDomainRenderer.enabled=false;
        Shot=t<.62f?1:t<1.74f?2:t<2.55f?3:t<3.45f?4:t<FieldReturnTime?5:6;
        SoundBeat=t<.32f?-1:t<2.55f?0:t<3.45f?1:2;
        Opacity=1f-Ease(5.62f,Duration,t);
        bool stage=t>=1.50f&&t<FieldReturnTime;
        View.backgroundColor=stage?new Color(.025f,.023f,.045f,Ease(1.50f,1.74f,t)):Color.clear;
        if(t<1.50f)DrawAscent(t);
        else if(t<FieldReturnTime)DrawCeiling(t);
        else
        {
            CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
            PaintReturn(field,100);LensThreads(t,5.10f,5.35f,false);
        }
        for(int i=vertexCount;i<threadVertices.Length;i++){threadVertices[i]=Vector3.zero;threadColors[i]=Color.clear;}
        silkMesh.vertices=threadVertices;silkMesh.colors=threadColors;silkMesh.RecalculateBounds();
        silk.enabled=vertexCount>0;
    }
}
