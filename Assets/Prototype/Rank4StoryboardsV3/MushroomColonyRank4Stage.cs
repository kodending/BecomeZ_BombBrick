using UnityEngine;

// The last spore becomes an isolated living set; its billowing curtain carries the same flower home.
public sealed class MushroomColonyRank4Stage:Rank4CinematicStage
{
    private const string ArtRoot="CombatCinematics/Rank4V3/mushroom/StageBloomV02/";
    public const string HeroResourcePath=ArtRoot+"Hero8";
    public const string ChamberResourcePath=ArtRoot+"Chamber4";
    public const string MicroResourcePath=ArtRoot+"SporeInterior4";
    public const float OpaqueStageTime=.65f,FieldRevealTime=3.90f,FieldReturnTime=4.15f;
    public static readonly float[] BoardTimes={.20f,.95f,1.70f,2.85f,3.65f,4.55f};
    private readonly Sprite[] heroFrames,chamberFrames,microFrames,flowers;
    private readonly SpriteRenderer lead,flower,hero,heroBlend,background,backgroundBlend;
    private readonly PixelMushroomDeathBloom smoke;
    public override string Family=>"mushroom";
    public override float Duration=>4.80f;

    public MushroomColonyRank4Stage(Transform parent):base(parent,"Last Bloom: inside the final spore",Color.clear,1280,720)
    {
        Root.position=new Vector3(740f,320f,740f);
        heroFrames=LoadAtlas(HeroResourcePath,4,2);
        chamberFrames=LoadAtlas(ChamberResourcePath,2,2);
        microFrames=LoadAtlas(MicroResourcePath,2,2);
        flowers=PixelMycoking.DeathBloomFrames;
        lead=MakeSprite("LeadMushroom actual fallen species",30);
        flower=MakeSprite("Same rooted death flower",55);
        hero=MakeSprite("Last Bloom authored unfolding hero",50);
        heroBlend=MakeSprite("Last Bloom hero frame blend",51);
        background=MakeSprite("Last Bloom opaque chamber",0);
        backgroundBlend=MakeSprite("Last Bloom chamber frame blend",1);
        flower.sprite=flowers[4];
        smoke=PixelMushroomDeathBloom.Play(Root.position,8.5f,5f,flower,new Color(.57f,.59f,.65f));
        smoke.transform.SetParent(Root,true);smoke.SetPresentation(View,0f,3.8f);smoke.gameObject.SetActive(false);
    }

    public static float CastTimeAt(float time)=>time<FieldReturnTime?-1f:time-FieldReturnTime;
    public static float StageBackgroundAlpha(float time)=>Ease(.35f,OpaqueStageTime,time)*(1f-Ease(FieldRevealTime,FieldReturnTime,time));

    private static float HeroPoseAt(float time)
    {
        if(time<2.60f)return Mathf.Clamp01((time-1.25f)/1.35f)*5f;
        if(time<3.15f)return 5f;
        if(time<3.40f)return 5f+Ease(3.15f,3.40f,time);
        return 6f+(.5f-.5f*Mathf.Cos((time-3.40f)*Mathf.PI*3f));
    }

    private void DeathPrelude(float time)
    {
        Vector2 feet=Context.entryHeight>0f?Context.entryPosition:Context.returnPosition;
        float height=Context.entryHeight>0f?Context.entryHeight:Mathf.Max(.35f,Context.returnHeight*.72f);
        float alpha=1f-Ease(.35f,OpaqueStageTime,time);
        PaintWorld(lead,Frame(Context.source.frames,Ease(0f,.40f,time))??Context.source.sprite,feet,height,
            new Color(Context.source.color.r,Context.source.color.g,Context.source.color.b,alpha),30,CameraFacing,Context.source.flipX);
        float flight=Ease(.12f,OpaqueStageTime,time);
        Vector2 seed=Vector2.Lerp(feet+Vector2.up*height*.35f,new Vector2(0f,.35f),flight);
        for(int i=0;i<36;i++)
        {
            float a=Noise(i*31)*Mathf.PI*2f,radius=(.04f+Noise(i*47)*.10f)*(1f+flight*3.5f);
            Vector2 point=seed+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
            Color color=i%2==0?new Color(.61f,1f,.56f,alpha):new Color(.82f,.55f,1f,alpha);
            Pixel(point,.025f+Noise(i*13)*.035f,color);
        }
    }

    private void Backdrop(float time,float alpha)
    {
        Vector3 center=new Vector3(0f,1f,8f);Vector2 size=new Vector2(42f,23.625f);
        if(time>=1.18f&&time<1.38f)
        {
            float mix=Ease(1.18f,1.38f,time);
            PaintArtWorld(background,microFrames[3],center,size,new Color(1f,1f,1f,alpha*(1f-mix)),0,CameraFacing);
            PaintArtWorld(backgroundBlend,chamberFrames[0],center,size,new Color(1f,1f,1f,alpha*mix),1,CameraFacing);
            return;
        }
        Sprite[] frames=time<1.18f?microFrames:chamberFrames;
        float pose=time<1.18f?Mathf.Clamp01((time-.35f)/.83f)*3f:Mathf.Clamp01((time-1.38f)/2.40f)*3f;
        int index=Mathf.Min(3,Mathf.FloorToInt(pose));float blend=pose-index;
        PaintArtWorld(background,frames[index],center,size,new Color(1f,1f,1f,alpha),0,CameraFacing);
        if(index<3&&blend>0f)
            PaintArtWorld(backgroundBlend,frames[index+1],center,size,new Color(1f,1f,1f,alpha*blend),1,CameraFacing);
    }

    private void HeroFrame(SpriteRenderer renderer,Sprite sprite,Vector3 feet,float height,float alpha)
    {
        if(alpha<=0f)return;
        Bounds mature=PixelMantisReaper.VisibleBody(heroFrames[5]);
        float scale=height/Mathf.Max(.001f,mature.size.y);
        renderer.enabled=true;renderer.sprite=sprite;renderer.color=new Color(1f,1f,1f,alpha);
        renderer.flipX=false;renderer.transform.localRotation=CameraFacing;
        renderer.transform.localScale=Vector3.one*scale;
        // Fitting each silhouette would erase the growth; all drawings share the physical root pivot.
        renderer.transform.localPosition=feet-CameraFacing*(Vector3.up*mature.min.y*scale);
    }

    private void Bloom(float time,Vector3 feet)
    {
        float pose=HeroPoseAt(time);int index=Mathf.Min(7,Mathf.FloorToInt(pose));float blend=pose-index;
        float alpha=Ease(1.20f,1.38f,time)*(1f-Ease(3.60f,3.90f,time));
        HeroFrame(hero,heroFrames[index],feet,8.2f,alpha);
        if(index<7&&blend>0f)HeroFrame(heroBlend,heroFrames[index+1],feet,8.2f,alpha*blend);
        float flourish=Ease(1.45f,2.75f,time)*(1f-Ease(3.40f,3.90f,time));
        for(int i=0;i<84;i++)
        {
            float phase=Mathf.Repeat(time*.21f+Noise(i*71),1f);
            Vector3 point=new Vector3((Noise(i*13)-.5f)*15f,-3.5f+phase*10f,-2f+Noise(i*41)*5f);
            Color tint=i%3==0?new Color(.70f,1f,.64f):i%3==1?new Color(.84f,.66f,1f):new Color(1f,.86f,.53f);
            tint.a=flourish*Mathf.Sin(phase*Mathf.PI)*(.25f+Noise(i*17)*.45f);
            Pixel(point,.018f+Noise(i*19)*.045f,tint,Noise(i*7)*90f);
        }
    }

    protected override void Animate(float time,float dt)
    {
        View.ResetProjectionMatrix();
        ActionTime=CastTimeAt(time);SoundBeat=time>=3.15f?2:time>=2.15f?1:time>=.55f?0:-1;
        Shot=time<.65f?1:time<1.25f?2:time<2.15f?3:time<3.15f?4:time<FieldReturnTime?5:6;
        Opacity=1f-Ease(FieldReturnTime,Duration,time);smoke.gameObject.SetActive(false);
        float backdrop=StageBackgroundAlpha(time);
        View.backgroundColor=new Color(.005f,.012f,.025f,backdrop);
        Vector3 feet=new Vector3(0f,-4.20f,0f);
        if(time<OpaqueStageTime||time>=3.85f)
            CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
        else
        {
            float rise=Ease(1.25f,2.90f,time);
            CameraPose(Vector3.Lerp(new Vector3(-.3f,-1.9f,-10.8f),new Vector3(.45f,-.8f,-13.8f),rise),
                Vector3.Lerp(new Vector3(0f,-.6f,0f),new Vector3(0f,.4f,0f),rise),44f);
        }
        if(time<OpaqueStageTime)DeathPrelude(time);
        if(backdrop>0f)Backdrop(time,backdrop);
        if(time>=1.20f&&time<3.90f)Bloom(time,feet);
        float native=Ease(3.85f,4.10f,time);
        float nativeHeight=Mathf.Max(.35f,Context.returnHeight);
        // The hidden native renderer also supplies the plume's true mouth height during the curtain.
        flower.sprite=flowers[4];
        flower.transform.localScale=Vector3.one*Mathf.Lerp(8.2f,nativeHeight,native)/
            Mathf.Max(.001f,PixelMantisReaper.VisibleBody(flowers[4]).size.y);
        if(time>=3.85f&&time<FieldReturnTime)
            PaintWorld(flower,flowers[4],Context.returnPosition,nativeHeight,
                new Color(1f,1f,1f,native*(1f-Ease(4.10f,FieldReturnTime,time))),55,CameraFacing);
        if(time>=3.15f&&time<Duration)
        {
            Vector3 origin=Vector3.Lerp(feet,(Vector3)Context.returnPosition,native);
            smoke.gameObject.SetActive(true);smoke.SetGround(Root.TransformPoint(origin));
            smoke.SetPresentation(View,Opacity,Mathf.Lerp(4.2f,3.2f,native));
            smoke.Advance(time-3.15f);
        }
    }
}
