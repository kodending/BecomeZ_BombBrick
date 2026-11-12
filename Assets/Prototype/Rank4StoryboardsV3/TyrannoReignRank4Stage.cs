using UnityEngine;

public sealed class TyrannoReignRank4Stage:Rank4CinematicStage
{
    public const string ResourceRoot="CombatCinematics/TyrannoGodV2/";
    public const string DustResourceRoot="CombatCinematics/TyrannoGodImpactV1/";
    public const float CallEndTime=.35f,FieldPreludeEndTime=TyrannoGodFootstepsRules.FieldPreludeEndTime,
        RoarTime=2.80f,LiftTime=3.40f,SoleDriveTime=4.38f,FieldReturnTime=4.72f,FieldRevealTime=4.80f,FilmImpactTime=4.86f,ImpactTime=FilmImpactTime;
    public static readonly float[] BoardTimes={.18f,.98f,1.94f,2.58f,3.05f,3.94f,4.66f,4.91f};
    private readonly Sprite[] motion,shiny,soles,shinySoles,backdrop,props,groundArt,roarCloseFrames,soleMotionTino,dustMotion;
    private readonly Sprite groundShadow;
    private readonly SpriteRenderer lead,field,sky,shadow,sole,closeJaw;
    private readonly SpriteRenderer[] rocks=new SpriteRenderer[8],dust=new SpriteRenderer[20],witnesses=new SpriteRenderer[16];
    private readonly SpriteRenderer[] entryDust=new SpriteRenderer[8];
    private readonly SpriteRenderer[] rockShadows=new SpriteRenderer[8],witnessShadows=new SpriteRenderer[16],
        rockDust=new SpriteRenderer[8],godContacts=new SpriteRenderer[4],godDust=new SpriteRenderer[2],ridge=new SpriteRenderer[12];
    private readonly MaterialPropertyBlock atmosphere=new MaterialPropertyBlock();
    private static readonly Vector2[] RockPositions={new Vector2(-9.8f,-1.4f),new Vector2(11.7f,2.8f),
        new Vector2(-6.8f,7.6f),new Vector2(8.9f,11.3f),new Vector2(-12.2f,18.4f),new Vector2(14.8f,22.7f),
        new Vector2(-18.2f,34.8f),new Vector2(16.4f,39.6f)};
    private static readonly float[] PoseStarts={2.80f,3.27f,3.55f,3.74f};
    private const float SoleJoinTime=3.74f;
    private readonly MeshRenderer ground;
    private float stageTime;
    public override string Family=>"tyranno";
    public override float Duration=>5.80f;

    public TyrannoReignRank4Stage(Transform parent):base(parent,"God tyrant footsteps and sole match cut",Color.clear,1920,1080)
    {
        Root.position=new Vector3(520f,420f,520f);View.farClipPlane=240f;
        motion=LoadAtlas(ResourceRoot+"GodMotion",8,4);shiny=LoadAtlas(ResourceRoot+"GodMotionShiny",8,4);
        if(Resources.Load<Texture2D>(ResourceRoot+"GodRoarTinoV3")!=null)
            roarCloseFrames=LoadAtlas(ResourceRoot+"GodRoarTinoV3",4);
        if(Resources.Load<Texture2D>(ResourceRoot+"FootSoleTinoV3")!=null)
            soleMotionTino=LoadAtlas(ResourceRoot+"FootSoleTinoV3",4);
        dustMotion=LoadAtlas(DustResourceRoot+"PixelDustBurst",4,2);
        soles=LoadAtlas(ResourceRoot+"FootSole",4);shinySoles=LoadAtlas(ResourceRoot+"FootSoleShiny",4);
        backdrop=LoadAtlas(ResourceRoot+"Sky",1);groundArt=LoadAtlas(ResourceRoot+"Ground",1);props=LoadAtlas(ResourceRoot+"Props",2,2);
        groundShadow=PixelMonkeyMortar.ShadowSprite();
        Material litSurface=OwnMaterial(Shader.Find("BecomeZ/Effects/Pixel Hit Flash"));
        lead=MakeSprite("Lead new worm's eye god tyrant animation",30);
        sole=MakeSprite("Lead same right sole wipes across the lens",140);
        closeJaw=MakeSprite("Lead dedicated low three-quarter roaring jaw and chest",90);
        field=MakeSprite("Actual child on the returned battlefield",70);
        sky=MakeSprite("Cloud depth behind the towering god",-100);shadow=MakeSprite("God weight on the horizontal earth",-50);
        for(int i=0;i<rocks.Length;i++)rocks[i]=MakeSprite("Near and far ground witness rock "+i,-35+i);
        for(int i=0;i<dust.Length;i++)dust[i]=MakeSprite("Footstep and roar pressure dust "+i,60+i);
        for(int i=0;i<entryDust.Length;i++)entryDust[i]=MakeSprite("Animated approaching footstep billow at the lens "+i,1000+i);
        for(int i=0;i<witnesses.Length;i++)witnesses[i]=MakeSprite("Actual battlefield species dwarfed by the god "+i,40+i);
        for(int i=0;i<rocks.Length;i++)
        {
            rockShadows[i]=MakeSprite("Ground contact under rock "+i,-46);
            rockDust[i]=MakeSprite("Low earth at rock base "+i,-20);
            rocks[i].sharedMaterial=litSurface;
        }
        for(int i=0;i<witnesses.Length;i++)
        {
            witnessShadows[i]=MakeSprite("Ground contact under actual witness "+i,35);
            witnesses[i].sharedMaterial=litSurface;
        }
        for(int i=0;i<godContacts.Length;i++)godContacts[i]=MakeSprite("God foot contact pool "+i,28);
        for(int i=0;i<godDust.Length;i++)godDust[i]=MakeSprite("Low earth across god toes "+i,31);
        for(int i=0;i<ridge.Length;i++)ridge[i]=MakeSprite("Uneven distant earth silhouette "+i,-60);
        lead.sharedMaterial=litSurface;sole.sharedMaterial=litSurface;closeJaw.sharedMaterial=litSurface;
        ground=Geometry("Horizontal earth beneath the descending lens",new[]{new Vector3(-65f,0f,-30f),new Vector3(65f,0f,-30f),
            new Vector3(65f,0f,90f),new Vector3(-65f,0f,90f)},new[]{0,1,2,0,2,3},
            new[]{Color.white,Color.white,Color.white,Color.white},new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},groundArt[0].texture);
        ground.sortingOrder=-70;
    }

    private void AirTint(SpriteRenderer renderer,float depth)
    {
        renderer.GetPropertyBlock(atmosphere);
        atmosphere.SetTexture("_MainTex",renderer.sprite.texture);
        atmosphere.SetColor("_HitColor",new Color(.24f,.22f,.21f,1f));
        atmosphere.SetFloat("_HitAmount",Mathf.Lerp(.035f,.16f,Mathf.InverseLerp(0f,40f,depth)));
        renderer.SetPropertyBlock(atmosphere);
    }

    private void Contact(SpriteRenderer renderer,Vector3 feet,Vector2 size,float alpha,int order)
        =>PaintArtWorld(renderer,groundShadow,new Vector3(feet.x,.018f,feet.z),size,
            new Color(.16f,.11f,.09f,alpha),order,Quaternion.Euler(90f,0f,0f));

    private void EarthAt(SpriteRenderer renderer,Vector3 feet,Vector2 size,float alpha,int order)
    {
        float phase=Mathf.Repeat((stageTime-FieldPreludeEndTime)*.42f+Noise(Mathf.RoundToInt(feet.x*31f+feet.z*71f)),1f);
        PaintDust(renderer,feet,size,alpha,order,phase);
    }

    private void PaintDust(SpriteRenderer renderer,Vector3 feet,Vector2 size,float alpha,int order,float phase)
    {
        Sprite image=dustMotion[Mathf.Clamp(Mathf.FloorToInt(phase*8f),0,7)];
        Bounds reference=PixelMantisReaper.VisibleBody(dustMotion[2]);Quaternion rotation=CameraFacing;
        PaintArtWorld(renderer,image,feet,size,new Color(.66f,.56f,.44f,alpha),order,rotation);
        Vector3 scale=new Vector3(size.x/reference.size.x,size.y/reference.size.y,1f);
        renderer.transform.localScale=scale;
        float baseline=(.12f-.5f)*image.rect.height/image.pixelsPerUnit;
        renderer.transform.localPosition=feet-rotation*Vector3.Scale(new Vector3(0f,baseline,0f),scale);
    }

    public static int SpeciesRow(string id)
    {switch((id??"").ToLowerInvariant()){case "magno":return 1;case "cheolno":case "ironrex":return 2;case "bonrex":case "bonerex":return 3;default:return 0;}}

    public static int MotherPoseAt(float t)
        =>t<RoarTime?1:t<3.27f?2:t<3.55f?3:t<SoleJoinTime?4:5;

    public static int RoarFrameAt(float t)=>t<2.89f?0:t<2.98f?1:t<3.20f?2:3;
    public static int SoleFrameAt(float t)=>t<3.94f?0:t<SoleDriveTime?1:t<4.54f?2:3;

    private bool DedicatedRoar(float t)=>roarCloseFrames!=null&&SpeciesRow(Context.source.species)==0&&t>=RoarTime&&t<LiftTime;

    public static float CastTimeAt(float t)
    {
        if(t<CallEndTime)return -1f;
        float contact=FamilyTranscendenceRules.TyrannoStompWarning;
        if(t<FieldReturnTime)return Mathf.Lerp(0f,contact-.14f,Mathf.InverseLerp(CallEndTime,FieldReturnTime,t));
        if(t<FilmImpactTime)return Mathf.Lerp(contact-.14f,contact,Mathf.InverseLerp(FieldReturnTime,FilmImpactTime,t));
        return Mathf.Lerp(contact,FamilyTranscendenceRules.TyrannoStompEnd,Mathf.InverseLerp(FilmImpactTime,5.80f,t));
    }

    private Vector3 Screen(Vector2 uv,float depth)
    {
        float half=View.orthographic?View.orthographicSize:depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
        return View.transform.localPosition+CameraFacing*new Vector3((uv.x-.5f)*half*2f*View.aspect,(.5f-uv.y)*half*2f,depth);
    }

    private float ScreenHeight(float fraction,float depth)
        =>fraction*(View.orthographic?View.orthographicSize*2f:depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f)*2f);

    private Vector3 ArtPoint(SpriteRenderer renderer,Vector2 uv)
    {
        Sprite image=renderer.sprite;
        Vector3 point=new Vector3((uv.x-.5f)*image.rect.width/image.pixelsPerUnit,
            (uv.y-.5f)*image.rect.height/image.pixelsPerUnit,0f);
        if(renderer.flipX)point.x=-point.x;
        return renderer.transform.localPosition+renderer.transform.localRotation*Vector3.Scale(point,renderer.transform.localScale);
    }

    private Rect ProjectArt(SpriteRenderer renderer,Rect area)
    {
        Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 point=View.WorldToViewportPoint(Root.TransformPoint(ArtPoint(renderer,
                new Vector2(x==0?area.xMin:area.xMax,y==0?area.yMin:area.yMax))));
            min=Vector2.Min(min,point);max=Vector2.Max(max,point);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    private static Rect RoarArt=>new Rect(.46f,.49f,.39f,.48f);
    private static Rect LiftedSoleArt=>new Rect(.171875f,.287109f,.433594f,.621094f);

    private void Call(float t)
    {
        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
        float show=1f-Ease(.28f,.38f,t);if(show<=0f)return;
        Vector2 center=Context.returnPosition+Vector2.up*(Mathf.Max(.3f,Context.returnHeight)+.27f+Ease(0f,.08f,t)*.13f);
        Color gold=new Color(1f,.83f,.16f,show),dark=new Color(.12f,.075f,.025f,show);
        for(int y=0;y<5;y++)for(int x=0;x<2;x++)
        {
            Vector2 p=center+new Vector2((x-.5f)*.085f,y*.085f);
            Pixel(p+new Vector2(.035f,-.035f),.11f,dark);Pixel(p,.085f,gold);
        }
        Pixel(center+Vector2.down*.17f+new Vector2(.035f,-.035f),.15f,dark);
        Pixel(center+Vector2.down*.17f,.12f,gold);
    }

    private void Lens(float t)
    {
        float side=Context.enemy?-1f:1f,drop=Ease(FieldPreludeEndTime,1.98f,t),reveal=Ease(2.00f,RoarTime,t);
        Vector3 position=Vector3.Lerp(new Vector3(side*1.2f,9f,-18f),new Vector3(side*1.6f,.20f,-10f),drop);
        Vector3 target=Vector3.Lerp(new Vector3(0f,0f,15f),new Vector3(0f,17f,24f),drop);
        target=Vector3.Lerp(target,new Vector3(0f,22.7f,24f),reveal);
        float close=SpeciesRow(Context.source.species)==0&&roarCloseFrames!=null?
            (DedicatedRoar(t)?1f:0f):Ease(RoarTime,2.98f,t)*(1f-Ease(3.24f,3.55f,t));
        float lift=Ease(3.55f,SoleJoinTime,t);
        position=Vector3.Lerp(position,new Vector3(side*-1.7f,.18f,-5f),lift);
        target=Vector3.Lerp(target,new Vector3(0f,23f,24f),lift);
        float lens=Mathf.Lerp(Mathf.Lerp(62f,72f,reveal),64f,lift);
        if(close>0f)
        {
            Vector3 mouth=ArtPoint(lead,RoarArt.center);
            Vector3 direction=new Vector3(side*-.32f,-.30f,-1f).normalized;
            float distance=Vector3.Distance(ArtPoint(lead,new Vector2(RoarArt.center.x,RoarArt.yMin)),
                ArtPoint(lead,new Vector2(RoarArt.center.x,RoarArt.yMax)))/(2f*Mathf.Tan(24f*Mathf.Deg2Rad)*.78f);
            for(int i=0;i<3;i++)
            {
                CameraPose(mouth+direction*distance,mouth,48f);
                distance*=ProjectArt(lead,RoarArt).height/.78f;
            }
            position=Vector3.Lerp(position,mouth+direction*distance,close);
            target=Vector3.Lerp(target,mouth,close);lens=Mathf.Lerp(lens,48f,close);
        }
        float punch=Ease(RoarTime,RoarTime+.035f,t)*(1f-Ease(3.06f,3.29f,t));
        float reduced=CombatFlashPresentation.Reduced?.45f:1f;
        position+=new Vector3(Mathf.Sin(t*113f),Mathf.Sin(t*137f)*.65f,0f)*(.055f*punch*reduced);
        CameraPose(position,target,lens);
    }

    private void God(float t)
    {
        PoseGod(t);Lens(t);ground.enabled=true;
        float side=Context.enemy?-1f:1f,roar=Ease(RoarTime,RoarTime+.08f,t)*(1f-Ease(3.10f,3.45f,t));
        float weight=Ease(LiftTime,3.96f,t);
        float skyHeight=ScreenHeight(1.3f,110f);
        float skyDrift=-.018f*Ease(FieldPreludeEndTime,RoarTime,t)+.006f*Ease(RoarTime,LiftTime,t);
        PaintArtWorld(sky,backdrop[0],Screen(new Vector2(.5f,.48f+skyDrift),110f),new Vector2(skyHeight*View.aspect,skyHeight),Color.white,-100,CameraFacing);
        for(int i=0;i<ridge.Length;i++)
        {
            float x=-55f+i*10f+(Noise(i*19+7)-.5f)*5f,z=59f+Noise(i*31+11)*9f;
            EarthAt(ridge[i],new Vector3(x,.015f,z),new Vector2(12f,.25f+Noise(i*43+3)*.65f),.62f,-60);
        }
        for(int i=0;i<rocks.Length;i++)
        {
            Vector2 point=RockPositions[i];Vector3 feet=new Vector3(point.x,.015f,point.y);
            float height=.65f+Noise(i*17+13)*.95f;
            PaintWorld(rocks[i],props[i%2],feet,height,new Color(.82f,.78f,.70f),-35+i,Quaternion.identity,i%3==0);
            AirTint(rocks[i],feet.z);
            Contact(rockShadows[i],feet,new Vector2(height*1.1f,height*.72f),.36f,-46);
            EarthAt(rockDust[i],feet+Vector3.back*.03f,new Vector2(height*1.2f,height*.12f),.32f,-20+i);
        }
        PaintArtWorld(shadow,props[3],new Vector3(0f,.025f,24f),new Vector2(27f,15f),new Color(.66f,.56f,.44f,.26f),-50,Quaternion.Euler(90f,0f,0f));
        int rowIndex=SpeciesRow(Context.source.species);
        for(int foot=0;foot<2;foot++)
        {
            Vector3 feet=foot==0?ArtPoint(lead,new Vector2(100f/128f,1f-113f/128f)):
                new Vector3(side*(-7f+weight*.45f),0f,20f);
            float planted=foot==0?1f:1f-Ease(LiftTime,3.96f,t);
            Contact(godContacts[foot*2],feet,new Vector2(foot==0?10f:17f,foot==0?5f:7f),.18f*planted,26);
            Contact(godContacts[foot*2+1],feet,new Vector2(foot==0?5.5f:10f,foot==0?2.8f:4f),.45f*planted,27);
            EarthAt(godDust[foot],feet+Vector3.back*.04f,new Vector2(foot==0?7f:13f,1.25f),.42f*planted,31);
        }
        Witnesses(t);
        for(int i=0;i<dust.Length;i++)
        {
            float cycle=Mathf.Repeat((t-FieldPreludeEndTime)*(.31f+roar*.7f)+Noise(i*17+3),1f),sign=i%2==0?-1f:1f;
            float x=sign*(1.2f+cycle*(4f+roar*14f)+Noise(i+71)*5f),z=4f+Noise(i*43+8)*22f;
            float height=(.7f+Noise(i*11+5)*1.2f)*(1f+roar*1.8f),alpha=Mathf.Sin(cycle*Mathf.PI)*(.12f+roar*.25f);
            PaintDust(dust[i],new Vector3(x,.06f+cycle*(.8f+roar*2f),z),new Vector2(height*2.1f,height),alpha,60+i,cycle);
        }
        for(int i=0;i<80;i++)
        {
            float age=Mathf.Repeat((t-FieldPreludeEndTime)*(.24f+roar*.6f)+Noise(i*29+2),1f),sign=i%2==0?-1f:1f;
            Vector3 p=new Vector3(sign*(2.5f+age*(3f+roar*16f)+Noise(i*7+19)*6f),.06f+age*(.65f+roar*3f),Noise(i*37+5)*26f);
            Pixel(p,.045f+Noise(i+11)*.12f,new Color(.53f,.48f,.40f,Mathf.Sin(age*Mathf.PI)*(.22f+roar*.32f)));
        }
        if(DedicatedRoar(t))
        {
            lead.enabled=false;
            Sprite image=roarCloseFrames[RoarFrameAt(t)];
            float height=ScreenHeight(1.14f,24f);
            float pulse=Ease(RoarTime,2.84f,t)*(1f-Ease(3.18f,LiftTime,t))*(CombatFlashPresentation.Reduced?.45f:1f);
            float pan=Ease(3.10f,3.20f,t)*.20f*(Context.enemy?-1f:1f);
            Vector2 center=new Vector2(.5f+pan+Mathf.Sin(t*113f)*.0025f*pulse,.49f+Mathf.Sin(t*137f)*.003f*pulse);
            PaintWorld(closeJaw,image,Screen(center,24f)-CameraFacing*Vector3.up*(height*.5f),height,
                new Color(.94f,.90f,.84f),90,CameraFacing,Context.enemy);
            AirTint(closeJaw,8f);
        }
        if(roar>0f)
        {
            Vector3 jaw=DedicatedRoar(t)?ArtPoint(closeJaw,new Vector2(.60645f,.44238f)):ArtPoint(lead,new Vector2(.72f,.79f));
            for(int i=0;i<48;i++)
            {
                float angle=i%12*Mathf.PI/6f,travel=Mathf.Repeat((t-RoarTime)*2.5f+i/12f*.22f,1f);
                Vector3 outward=CameraFacing*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f);
                Pixel(jaw+outward*(4f+travel*28f),.10f+travel*.13f,new Color(.89f,.83f,.68f,roar*(1f-travel)*.7f));
            }
        }
        if(t>=SoleJoinTime)Sole(t,rowIndex);
        if(t>=4.64f)lead.enabled=false;
    }

    private void PoseGod(float t)
    {
        float side=Context.enemy?-1f:1f,weight=Ease(LiftTime,3.96f,t);
        int rowIndex=SpeciesRow(Context.source.species),pose=MotherPoseAt(t);Sprite[] atlas=Context.source.shiny?shiny:motion;
        // The foreground sole now owns the raised foot; the bent-leg underlay must not expose a second claw.
        int underlay=t>=3.94f?4:pose;
        Sprite image=atlas[rowIndex*8+underlay];Bounds body=PixelMantisReaper.VisibleBody(atlas[rowIndex*8+1]);
        // Every authored pose shares the planted left-foot baseline; changing bounds must not make the god hop.
        float scale=74f/Mathf.Max(.01f,body.size.y);
        float supportY=(.5f-113f/128f)*image.rect.height/image.pixelsPerUnit;
        float settle=0f;
        for(int i=0;i<PoseStarts.Length;i++)
        {
            float age=(t-PoseStarts[i])/.07f;
            if(age>0f&&age<1f)settle=Mathf.Sin(age*Mathf.PI);
        }
        float breathe=Mathf.Sin((t-FieldPreludeEndTime)*3.5f)*.0025f;
        Vector3 bodyScale=new Vector3(scale*(1f+breathe*.5f+settle*.004f),scale*(1f-breathe-settle*.006f),scale);
        float supportX=(100f/128f-.5f)*image.rect.width/image.pixelsPerUnit;
        Vector3 anchor=new Vector3(side*supportX,supportY,0f);
        Vector3 support=new Vector3(side*((supportX-body.center.x)*scale+weight*.45f),0f,24f);
        lead.enabled=true;lead.sprite=image;lead.flipX=Context.enemy;lead.color=new Color(.94f,.90f,.84f);
        lead.transform.localRotation=Quaternion.identity;lead.transform.localScale=bodyScale;
        lead.transform.localPosition=support-Vector3.Scale(anchor,bodyScale);AirTint(lead,8f);
    }

    private void Witnesses(float t)
    {
        bool layout=false;
        if(Context.targets!=null)for(int i=0;i<Context.targets.Length;i++)
            if((Context.targets[i].ground-Context.source.ground).sqrMagnitude>.00001f){layout=true;break;}
        for(int i=0;i<witnesses.Length;i++)
        {
            int enemies=Context.targets==null?0:Context.targets.Length;
            int allies=Context.allies==null?0:Context.allies.Length;
            if(i>enemies+allies)continue;
            var actor=i==0?Context.source:i<=enemies?Context.targets[i-1]:Context.allies[i-enemies-1];
            Vector2 position=actor.ground;
            if(!layout)position=i==0?new Vector2(-.85f,-.05f):new Vector2(-.52f+((i-1)%4)*.35f,-.65f+((i-1)/4)*.92f);
            float depth=Context.battlefieldBounds.size.x>.01f?
                Mathf.Clamp(7.6f*Context.battlefieldBounds.size.z/Context.battlefieldBounds.size.x,2f,8f):4f;
            Vector3 feet=new Vector3(Mathf.Clamp(position.x,-1f,1f)*7.6f,.02f,8.8f+Mathf.Clamp(position.y,-1f,1f)*depth);
            float height=Mathf.Clamp(actor.size*.64f,.32f,1.10f);int order=53-Mathf.RoundToInt(feet.z);
            Color color=actor.color*new Color(.94f,.90f,.84f,1f);
            PaintWorld(witnesses[i],actor.sprite,feet,height,color,order,CameraFacing,actor.flipX);
            AirTint(witnesses[i],feet.z+6f);
            Contact(witnessShadows[i],feet,new Vector2(height*.85f,height*.42f),.40f,order-1);
        }
    }

    private void Sole(float t,int species)
    {
        float join=Ease(SoleJoinTime,3.94f,t),drive=Mathf.Pow(Mathf.InverseLerp(SoleDriveTime,4.64f,t),2.4f);
        Sprite image=species==0&&soleMotionTino!=null?soleMotionTino[SoleFrameAt(t)]:
            (Context.source.shiny?shinySoles:soles)[species];
        Rect lifted=ProjectArt(lead,LiftedSoleArt);
        Vector2 raised=Vector2.Lerp(lifted.center,new Vector2(Context.enemy?.64f:.36f,.54f),join);
        Vector2 landing=Context.hasTyrannoFootprint?Context.tyrannoFootprintViewport.center:new Vector2(.5f,.48f);
        Vector2 center=Vector2.Lerp(raised,landing,drive);
        float contactWidth=Context.hasTyrannoFootprint?Mathf.Max(.3f,Context.tyrannoFootprintViewport.width):.8f;
        float nearWidth=Mathf.Max(4.6f,contactWidth*7f);
        float width=Mathf.Lerp(Mathf.Lerp(lifted.width*1.08f,.60f,join),nearWidth,drive);
        float height=Mathf.Lerp(Mathf.Lerp(lifted.height*1.08f,.84f,join),nearWidth*1.35f,drive);
        Vector3 ankle=ArtPoint(lead,new Vector2(.5547f,.4023f));
        Vector3 left=View.WorldToViewportPoint(Root.TransformPoint(ArtPoint(lead,new Vector2(.2578f,.5801f))));
        Vector3 right=View.WorldToViewportPoint(Root.TransformPoint(ArtPoint(lead,new Vector2(.5332f,.7207f))));
        Vector3 edge=Context.enemy?left-right:right-left;
        float toeAngle=species==0&&soleMotionTino!=null?Mathf.Atan2(5f,223f):Mathf.Atan2(.5078f-.4785f,.7891f-.2266f);
        float roll=(Mathf.Atan2(edge.y,edge.x)-toeAngle)*Mathf.Rad2Deg*(1f-join)*(1f-drive);
        float sourceDepth=View.WorldToViewportPoint(Root.TransformPoint(ankle)).z;
        float depth=Mathf.Lerp(Mathf.Max(2f,sourceDepth-.12f),.85f,drive);
        Bounds body=PixelMantisReaper.VisibleBody(image);
        Vector3 scale=new Vector3(ScreenHeight(width*View.aspect,depth)/body.size.x,ScreenHeight(height,depth)/body.size.y,1f);
        Quaternion rotation=CameraFacing*Quaternion.Euler(0f,0f,roll);
        sole.enabled=true;sole.sprite=image;sole.flipX=Context.enemy;sole.color=new Color(.94f,.90f,.84f);
        sole.transform.localRotation=rotation;sole.transform.localScale=scale;
        Vector3 imageCenter=body.center;if(Context.enemy)imageCenter.x=-imageCenter.x;
        Vector3 centered=Screen(new Vector2(center.x,1f-center.y),depth)-rotation*Vector3.Scale(imageCenter,scale);
        Vector2 ankleArt=species==0&&soleMotionTino!=null?new Vector2(.5625f,.0918f):new Vector2(.5273f,.0781f);
        Vector3 imageAnkle=new Vector3((ankleArt.x-.5f)*image.rect.width/image.pixelsPerUnit,
            (ankleArt.y-.5f)*image.rect.height/image.pixelsPerUnit,0f);
        if(Context.enemy)imageAnkle.x=-imageAnkle.x;
        Vector3 actualAnkle=View.WorldToViewportPoint(Root.TransformPoint(ankle));
        Vector3 attached=Screen(new Vector2(actualAnkle.x,1f-actualAnkle.y),depth)-rotation*Vector3.Scale(imageAnkle,scale);
        sole.transform.localPosition=Vector3.Lerp(attached,centered,join);
        AirTint(sole,0f);
        for(int i=0;i<40;i++)
        {
            float angle=Noise(i*19+2)*Mathf.PI*2f,spread=.4f+drive*.28f;
            Vector2 uv=new Vector2(.5f+Mathf.Cos(angle)*spread,.5f+Mathf.Sin(angle)*spread);
            Pixel(Screen(uv,1f),ScreenHeight(.003f+Noise(i+5)*.004f,1f),new Color(.81f,.76f,.61f,drive*.8f));
        }
    }

    private void Return(float t)
    {
        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);PaintReturn(field,70);
    }

    private void EntryDust(float t)
    {
        if(t<1.50f||t>=1.82f)return;
        float age=(t-1.50f)/.32f;
        for(int i=0;i<entryDust.Length;i++)
        {
            float height=1.02f+Noise(i*17+3)*.12f;
            float x=.19f+(i%2)*.60f+(Noise(i*31+7)-.5f)*.08f;
            float y=.34f+(i/2)*.42f+(Noise(i*43+5)-.5f)*.08f-age*.04f;
            float worldHeight=ScreenHeight(height,.70f);
            PaintDust(entryDust[i],Screen(new Vector2(x,y),.70f),new Vector2(worldHeight*2.1f,worldHeight),
                1f-Ease(.86f,1f,age),1000+i,Mathf.Min(.999f,age+(i%2)*.025f));
            entryDust[i].color=new Color(.83f,.78f,.68f,1f-Ease(.86f,1f,age));
        }
    }

    protected override void Animate(float t,float dt)
    {
        stageTime=t;ground.enabled=false;ActionTime=CastTimeAt(t);
        Shot=t<.38f?1:t<FieldPreludeEndTime?2:t<2.25f?3:t<RoarTime?4:t<LiftTime?5:t<SoleDriveTime?6:t<FieldReturnTime?7:8;
        SoundBeat=t<.1f?-1:t<RoarTime?0:t<FilmImpactTime?1:2;
        Opacity=1f-Ease(5.25f,Duration,t);
        View.backgroundColor=t>=FieldPreludeEndTime&&t<FieldReturnTime?new Color(.035f,.052f,.075f,1f):Color.clear;
        if(t<FieldPreludeEndTime)Call(t);
        else if(t<FieldReturnTime)God(t);
        else Return(t);
        EntryDust(t);
    }
}
