using UnityEngine;

// The camera returns before the existing battlefield shadow and breath begin.
public sealed class WyvernHeavenRank4Stage:Rank4CinematicStage
{
    private const string ArtRoot="CombatCinematics/Rank4V3/wyvern/NativeLinkV03/";
    public const float SkyCoverTime=.60f,FieldReturnTime=3.65f,FieldRevealTime=4.20f;
    public static readonly float[] BoardTimes={.38f,1.05f,1.88f,2.72f,3.40f,4.17f};
    private static readonly float[] PhaseTimes={0f,.60f,1.45f,2.25f,3.10f,3.65f,4.40f};
    private static readonly float[] MotionTimes={0f,.38f,1.05f,1.88f,2.72f,3.40f,4.17f,4.40f};
    private readonly Vector4[] motion={Vector4.zero,new Vector4(.43f,.57f,.36f,-7f),
        new Vector4(.52f,.40f,.56f,-11f),new Vector4(.30f,.44f,.67f,-8f),
        new Vector4(.38f,.48f,1.32f,-4f),new Vector4(.47f,.48f,1.45f,3f),
        new Vector4(.98f,-.20f,1.00f,15f),new Vector4(1.65f,-.75f,1.50f,20f)};
    public override string Family=>"wyvern";
    public override float Duration=>4.40f;
    private readonly Sprite[][] bodies;
    private readonly float[] bodyHeights=new float[5];
    private readonly Vector3[,] bodyCenters=new Vector3[6,5];
    private readonly Sprite[] sky,clouds;
    private readonly SpriteRenderer lead,poseBlend,field,backdrop,backdropBlend;
    private readonly SpriteRenderer[] cloud=new SpriteRenderer[9],cloudBlend=new SpriteRenderer[9];
    private readonly Material fieldMaterial;
    private readonly Vector3[] cloudAnchors=new Vector3[9];
    private readonly Vector2[] cloudSizes=new Vector2[9];
    private readonly Vector3 skyAnchor;
    private readonly Vector2 skySize;
    private Sprite fallbackField;
    private Texture2D fallbackTexture;

    public WyvernHeavenRank4Stage(Transform parent):base(parent,"wyvern native-link V03",Color.clear,1280,720)
    {
        Root.position=new Vector3(960f,320f,960f);View.farClipPlane=100f;
        bodies=new[]{LoadAtlas(ArtRoot+"Takeoff40",8,5),LoadAtlas(ArtRoot+"Hunt40",8,5),LoadAtlas(ArtRoot+"Pass40",8,5)};
        sky=LoadAtlas(ArtRoot+"Sky4",2,2);clouds=LoadAtlas(ArtRoot+"Cloud4",2,2);
        for(int sheet=0;sheet<3;sheet++)for(int row=0;row<5;row++)for(int frame=0;frame<8;frame++)
        {
            Bounds body=PixelMantisReaper.VisibleBody(bodies[sheet][row*8+frame]);
            bodyHeights[row]=Mathf.Max(bodyHeights[row],body.size.y);
            if(frame%4==2)bodyCenters[sheet*2+frame/4,row]=body.center;
        }
        lead=MakeSprite("Lead wyvern",40);poseBlend=MakeSprite("Adjacent authored wyvern pose",41);
        field=MakeSprite("Actual captured battlefield",-70);
        fieldMaterial=OwnMaterial(Shader.Find("Hidden/WyvernFieldAperture"));field.sharedMaterial=fieldMaterial;
        backdrop=MakeSprite("Far cloud sea",-100);backdropBlend=MakeSprite("Far cloud sea adjacent frame",-99);
        CameraPose(Vector3.back*20f,Vector3.zero,25.36077f);
        skyAnchor=Point(.50f,.50f,60f);skySize=new Vector2(Span(2.8f,60f)*View.aspect,Span(2.8f,60f));
        for(int i=0;i<cloud.Length;i++)
        {
            int layer=i/3;float depth=40f-layer*15f;
            int order=layer==0?-40:layer==1?10:28;
            cloud[i]=MakeSprite((layer==0?"Rear":layer==1?"Middle":"Front")+" passing cloud "+i,order);
            cloudBlend[i]=MakeSprite("Adjacent cloud pose "+i,order+1);
            float x=i%3==0?-.13f:i%3==1?1.13f:.62f,y=i%3==0?.38f:i%3==1?.12f:1.12f;
            cloudAnchors[i]=Point(x,y,depth);
            cloudSizes[i]=new Vector2(Span(1.25f,depth)*View.aspect,Span(.80f,depth));
        }
    }

    private int SpeciesRow()
    {
        if(Context.source.shiny&&Context.source.species=="vivre")return 4;
        switch(Context.source.species)
        {
            case "igvire":return 1;
            case "frovire":return 2;
            case "temvire":return 3;
            default:return 0;
        }
    }

    private Vector2 EntryFeet=>Context.entryHeight>0f?Context.entryPosition:Context.returnPosition;
    private float EntryHeight=>Context.entryHeight>0f?Context.entryHeight:Mathf.Max(.3f,Context.returnHeight);
    private float Mirror(float x)=>Context.enemy?1f-x:x;
    private Vector3 Point(float x,float y,float depth=12f)=>Root.InverseTransformPoint(
        View.ViewportToWorldPoint(new Vector3(x,1f-y,depth)));
    private float Span(float fraction,float depth=12f)=>depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f)*2f*fraction;

    private Vector4 FlightPose(float time)
    {
        Vector2 feet=EntryFeet;
        motion[0]=new Vector4(Mirror(.5f+feet.x/16f),.5f-(feet.y+EntryHeight*.5f)/9f,EntryHeight/9f,0f);
        int segment=0;while(segment<MotionTimes.Length-2&&time>MotionTimes[segment+1])segment++;
        float duration=MotionTimes[segment+1]-MotionTimes[segment],p=Mathf.Clamp01((time-MotionTimes[segment])/duration);
        Vector4 before=segment==0?Vector4.zero:(motion[segment+1]-motion[segment-1])/(MotionTimes[segment+1]-MotionTimes[segment-1]);
        Vector4 after=segment+2==motion.Length?Vector4.zero:(motion[segment+2]-motion[segment])/(MotionTimes[segment+2]-MotionTimes[segment]);
        float p2=p*p,p3=p2*p;
        Vector4 pose=(2f*p3-3f*p2+1f)*motion[segment]+(p3-2f*p2+p)*duration*before+
            (-2f*p3+3f*p2)*motion[segment+1]+(p3-p2)*duration*after;
        pose.x=Mirror(pose.x);if(Context.enemy)pose.w=-pose.w;
        return pose;
    }

    private void FlightCamera(float time)
    {
        float rise=Ease(.08f,1.45f,time),bank=Ease(1.15f,2.65f,time),home=1f-Ease(3.65f,Duration,time);
        float side=(Context.enemy?-1f:1f)*bank*home;
        CameraPose(new Vector3(.90f*side,1.70f*rise*home,-20f+.80f*rise*home),
            new Vector3(.60f*side,.70f*rise*home,0f),25.36077f+(19f*rise-4f*bank)*home,
            false,Quaternion.Euler(0f,0f,-4f*side)*Vector3.up);
    }

    private Sprite BattlefieldSprite()
    {
        if(Context.wyvernBattlefieldSprite!=null)return Context.wyvernBattlefieldSprite;
        if(Context.battlefieldBackdrop==null)return null;
        if(fallbackTexture!=Context.battlefieldBackdrop)
        {
            fallbackTexture=Context.battlefieldBackdrop;
            fallbackField=OwnSprite(Sprite.Create(fallbackTexture,new Rect(0f,0f,fallbackTexture.width,fallbackTexture.height),
                Vector2.one*.5f,64f,0,SpriteMeshType.FullRect));
            fallbackField.name="Direct fixture battlefield fallback";
        }
        return fallbackField;
    }

    private void Battlefield(float time)
    {
        float ascent=Ease(.12f,1.45f,time),bank=Ease(1.45f,2.65f,time),home=Ease(FieldReturnTime,FieldRevealTime,time);
        float aperture=Ease(.28f,.85f,time)*(1f-home);
        Rect arena=Context.wyvernBattlefieldViewportRect;
        if(arena.width<=.001f||arena.height<=.001f)arena=new Rect(0f,0f,1f,1f);
        fieldMaterial.SetVector("_ArenaRect",new Vector4(arena.x,arena.y,arena.width,arena.height));
        fieldMaterial.SetFloat("_Aperture",aperture);
        Vector2 center=Vector2.Lerp(new Vector2(Mathf.Lerp(.50f,.73f,ascent),Mathf.Lerp(.50f,.75f,ascent)),Vector2.one*.5f,home);
        float nativeHeight=Mathf.Lerp(Mathf.Lerp(1f,.39f,ascent)-.055f*bank,1f,home);
        float height=Mathf.Lerp(nativeHeight,(.38f-.045f*bank)/Mathf.Max(.15f,arena.height),aperture);
        float alpha=Ease(0f,.14f,time)*(1f-Ease(FieldRevealTime,Duration,time));
        float direction=Context.enemy?-1f:1f;
        Quaternion tilt=CameraFacing*Quaternion.Euler((40f*ascent+8f*bank)*(1f-home),
            -12f*direction*bank*(1f-home),-9f*direction*ascent*(1f-home));
        float h=Span(height,45f);
        Vector3 focus=new Vector3((arena.center.x-.5f)*h*View.aspect,(arena.center.y-.5f)*h,0f);
        Vector3 position=Point(Mirror(center.x),center.y,45f)-tilt*focus*aperture;
        PaintArtWorld(field,BattlefieldSprite(),position,new Vector2(h*View.aspect,h),
            new Color(1f,1f,1f,alpha),-70,tilt);
    }

    private void CloudSea(float time)
    {
        float alpha=Ease(.15f,SkyCoverTime,time)*(1f-Ease(FieldReturnTime,Duration,time));
        float frame=Mathf.Clamp(time/.95f,0f,3f);int index=Mathf.Min(3,Mathf.FloorToInt(frame));
        PaintArtWorld(backdrop,sky[index],skyAnchor,skySize,new Color(1f,1f,1f,alpha),-100,Quaternion.identity);
        if(index<3)PaintArtWorld(backdropBlend,sky[index+1],skyAnchor,skySize,
            new Color(1f,1f,1f,alpha*(frame-index)),-99,Quaternion.identity);
        float travel=.2f*time+1.5f*Ease(.10f,1.45f,time)+2.8f*Ease(3.10f,3.85f,time);
        for(int i=0;i<cloud.Length;i++)
        {
            int layer=i/3;float speed=.3f+layer*.75f;
            Vector3 anchor=cloudAnchors[i];if(Context.enemy)anchor.x=-anchor.x;
            Vector3 position=anchor+new Vector3((Context.enemy?1f:-1f)*travel*speed,-travel*(.45f+layer*.18f),0f);
            float phase=time*2.5f+i*.57f;int current=Mathf.FloorToInt(phase)%4,next=(current+1)%4;
            Color tint=new Color(1f,1f,1f,alpha*(layer==2?.68f:.88f));
            int order=layer==0?-40:layer==1?10:28;
            PaintArtWorld(cloud[i],clouds[current],position,cloudSizes[i],tint,order,Quaternion.identity);
            tint.a*=Ease(.82f,1f,Mathf.Repeat(phase,1f));
            PaintArtWorld(cloudBlend[i],clouds[next],position,cloudSizes[i],tint,order+1,Quaternion.identity);
        }
    }

    private Vector3 BodyCenter(float time,int phase,int row)
    {
        for(int boundary=1;boundary<6;boundary++)
            if(time>=PhaseTimes[boundary]-.06f&&time<=PhaseTimes[boundary]+.06f)
                return Vector3.Lerp(bodyCenters[boundary-1,row],bodyCenters[boundary,row],
                    Ease(PhaseTimes[boundary]-.06f,PhaseTimes[boundary]+.06f,time));
        return bodyCenters[phase,row];
    }

    private void AuthoredBody(SpriteRenderer renderer,int phase,int frame,Vector4 pose,float alpha,float time)
    {
        int sheet=phase/2,row=SpeciesRow();Sprite sprite=bodies[sheet][row*8+(phase%2)*4+frame];
        // A row keeps its cell scale: a folded wing must not enlarge the whole animal.
        float h=Span(pose.z)*sprite.bounds.size.y/Mathf.Max(.001f,bodyHeights[row]);
        Color tint=Context.source.color;tint.a*=alpha;
        Quaternion rotation=CameraFacing*Quaternion.Euler(0f,0f,pose.w);
        Vector3 anchor=BodyCenter(time,Shot,row);if(Context.enemy)anchor.x=-anchor.x;
        Vector3 center=Point(pose.x,pose.y)-rotation*anchor*(h/sprite.bounds.size.y);
        PaintArtWorld(renderer,sprite,center,new Vector2(h*sprite.bounds.size.x/sprite.bounds.size.y,h),
            tint,renderer==lead?40:41,rotation);
        renderer.flipX=Context.enemy;
    }

    private void Body(float time)
    {
        if(time==0f)
        {
            if(Context.returnBodyVisible)PaintWorld(lead,Context.returnSprite??Context.source.sprite,EntryFeet,EntryHeight,
                Context.source.color,40,CameraFacing,Context.source.flipX);
            return;
        }
        Vector4 pose=FlightPose(time);
        int phase=Shot;float length=PhaseTimes[phase+1]-PhaseTimes[phase],elapsed=time-PhaseTimes[phase];
        int frame=Mathf.Min(3,Mathf.FloorToInt(Mathf.Clamp01(elapsed/length)*4f));
        float changed=PhaseTimes[phase]+frame*length*.25f;
        float blend=frame==0?Ease(changed,changed+.065f,time):1f;
        float alpha=1f-Ease(4.23f,4.38f,time);
        if(time<.10f)
        {
            blend=Ease(.025f,.10f,time);
            if(Context.returnBodyVisible)
            {
                Color tint=Context.source.color;tint.a*=1f-blend;
                PaintWorld(poseBlend,Context.returnSprite??Context.source.sprite,EntryFeet,EntryHeight,tint,41,CameraFacing,Context.source.flipX);
            }
        }
        else if(blend<1f)
        {
            int previousPhase=frame==0?Mathf.Max(0,phase-1):phase;
            AuthoredBody(poseBlend,previousPhase,frame==0?3:frame-1,pose,alpha*(1f-blend),time);
        }
        AuthoredBody(lead,phase,frame,pose,alpha*blend,time);
    }

    private void Air(float time)
    {
        float alpha=Ease(.10f,.55f,time)*(1f-Ease(4.10f,Duration,time));
        float travel=time*.12f+1.1f*Ease(.08f,1.45f,time)+.38f*Ease(1.45f,2.25f,time)+2.3f*Ease(3.10f,3.90f,time);
        float rush=Ease(3.10f,3.35f,time)*(1f-Ease(4.10f,Duration,time));
        for(int i=0;i<88;i++)
        {
            float phase=Mathf.Repeat(travel*(.45f+Noise(i*19)*.45f)+Noise(i*31),1f);
            float x=Mirror(1.18f-phase*1.40f),y=Mathf.Repeat(Noise(i*43)+phase*.33f,1.15f)-.075f;
            float depth=6f+Noise(i*71)*28f;
            Color tint=i%5==0?new Color(.69f,.85f,.50f):new Color(.85f,.94f,1f);
            tint.a=alpha*Mathf.Sin(phase*Mathf.PI)*(.13f+Noise(i*53)*.14f);
            float size=Span(.0012f+Noise(i*13)*.0016f,depth);
            Pixel(Point(x,y,depth),size,tint,Noise(i*7)*180f);
            if(rush>0f)for(int j=1;j<4;j++)
            {
                tint.a*=.72f;
                Pixel(Point(x+(Context.enemy?-1f:1f)*j*.0045f*rush,y-j*.0018f*rush,depth),size*.7f,tint,0f);
            }
        }
    }

    private void LaunchGust(float time)
    {
        if(time<=.025f||time>=.67f||field.sprite==null)return;
        Vector2 feet=EntryFeet;Bounds bounds=field.sprite.bounds;
        Vector3 local=new Vector3(bounds.min.x+(.5f+feet.x/16f)*bounds.size.x,
            bounds.min.y+(.5f+feet.y/9f)*bounds.size.y,0f);
        Vector3 origin=Root.InverseTransformPoint(field.transform.TransformPoint(local));
        float age=time-.025f,alpha=1f-Ease(.22f,.67f,time);
        for(int i=0;i<42;i++)
        {
            float angle=Noise(i*31+17)*Mathf.PI*2f,speed=.5f+Noise(i*19)*2.3f;
            Vector3 displacement=new Vector3(Mathf.Cos(angle)*age*speed,Mathf.Sin(angle)*age*speed*.25f+.6f*age-age*age,0f);
            Color tint=i%3==0?new Color(.43f,.63f,.24f,alpha):new Color(.72f,.62f,.43f,alpha*.75f);
            Pixel(origin+CameraFacing*displacement,.018f+Noise(i*13)*.033f,tint,angle*Mathf.Rad2Deg+age*180f);
        }
    }

    protected override void Animate(float time,float dt)
    {
        View.ResetProjectionMatrix();ActionTime=-1f;
        Shot=time<.60f?0:time<1.45f?1:time<2.25f?2:time<3.10f?3:time<3.65f?4:5;
        SoundBeat=time<.60f?0:time<2.25f?1:time<3.10f?2:3;
        Opacity=1f-Ease(FieldRevealTime,Duration,time);
        View.backgroundColor=new Color(.25f,.49f,.82f,Ease(.15f,SkyCoverTime,time)*(1f-Ease(FieldRevealTime,Duration,time)));
        FlightCamera(time);CloudSea(time);Battlefield(time);LaunchGust(time);Air(time);Body(time);
    }
}
