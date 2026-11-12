using UnityEngine;

public sealed class MonkeyCircusRank4Stage:Rank4CinematicStage
{
    public const string SkyResourcePath="CombatCinematics/TyrannoGodV2/Sky";
    public const float PreludeEnd=.45f,ThunderTime=2.75f,FieldReturnTime=3.50f;
    public const float EarliestImpactTime=FieldReturnTime+MonkeyStormRules.EarliestImpact;
    public static readonly float[] BoardTimes={.20f,.92f,1.95f,2.79f,3.82f,4.08f};
    public override string Family=>"monkey";
    public override float Duration=>4.20f;
    private readonly Sprite background;
    private readonly Sprite[] clouds;
    private readonly SpriteRenderer sky,returned;
    private readonly SpriteRenderer[] banks=new SpriteRenderer[12];
    private readonly PixelHaetaeThunder thunder;

    public MonkeyCircusRank4Stage(Transform parent):base(parent,"Caster POV: look up into gathering thunderclouds",Color.clear,1920,1080)
    {
        Root.position=new Vector3(740f,320f,740f);View.farClipPlane=150f;
        background=LoadAtlas(SkyResourcePath,1)[0];clouds=LoadAtlas(PixelMonkeyStorm.CloudResourcePath,4);
        sky=MakeSprite("Existing cloud sky behind the caster's upward view",-100);
        returned=MakeSprite("Actual caster at the returned field",50);
        for(int i=0;i<banks.Length;i++)banks[i]=MakeSprite("Four-frame overhead storm cloud "+i,-40+i);
        thunder=PixelMonkeyStorm.CreateAnimatedBolt(Root.position,null,30);
        thunder.transform.SetParent(Root,true);thunder.gameObject.SetActive(false);
        thunder.transform.localScale=Vector3.one*1.8f;
    }

    public static float CastTimeAt(float t)=>t<FieldReturnTime?-1f:t-FieldReturnTime;
    public static float ThunderFlashAt(float t)
    {
        float age=t-ThunderTime;
        return age<0f||age>.22f?0f:(age<.025f?age/.025f:1f-Mathf.SmoothStep(0f,1f,(age-.025f)/.195f));
    }

    private Vector3 Screen(Vector2 uv,float depth)
    {
        float half=View.orthographic?View.orthographicSize:depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f);
        return View.transform.localPosition+CameraFacing*new Vector3((uv.x-.5f)*half*2f*View.aspect,(.5f-uv.y)*half*2f,depth);
    }
    private float ScreenHeight(float fraction,float depth)=>fraction*(View.orthographic?View.orthographicSize*2f:
        depth*Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f)*2f);

    private void FieldClouds(float t,float alpha)
    {
        for(int i=0;i<12;i++)
        {
            float height=ScreenHeight(.25f,8f+i%3);
            PaintArtWorld(banks[i],clouds[(Mathf.FloorToInt(t*6f)+i)%4],
                Screen(new Vector2(-.04f+i%6*.22f,-.04f+i/6*.065f),8f+i%3),
                new Vector2(height*2.9f,height),new Color(.4f,.43f,.60f,alpha),90+i,CameraFacing);
        }
    }

    private void Prelude(float t)
    {
        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
        FieldClouds(t,Ease(0f,PreludeEnd,t)*.65f);
        Vector2 top=Context.returnPosition+Vector2.up*(Context.returnHeight+.30f);
        float alpha=1f-Ease(.30f,PreludeEnd,t);
        for(int y=0;y<5;y++)Pixel(top+Vector2.up*y*.09f,.10f,new Color(1f,.88f,.25f,alpha));
        Pixel(top+Vector2.down*.18f,.13f,new Color(1f,.88f,.25f,alpha));
    }

    private void SkyView(float t)
    {
        float tilt=Ease(PreludeEnd,1.60f,t),gather=Ease(.90f,2.52f,t),flash=ThunderFlashAt(t);
        CameraPose(new Vector3(0f,.8f,0f),new Vector3(0f,Mathf.Lerp(8f,30f,tilt),Mathf.Lerp(18f,2f,tilt)),64f);
        float jolt=flash*Mathf.Sin((t-ThunderTime)*150f)*.35f;
        View.transform.localRotation*=Quaternion.Euler(jolt*.35f,jolt*.2f,jolt);
        float height=ScreenHeight(1.6f,55f);
        Color shade=Color.Lerp(Color.white,new Color(.27f,.36f,.55f),gather);
        if(CombatFlashPresentation.Reduced)flash*=.35f;
        shade=Color.Lerp(shade,new Color(2f,2f,2f),flash*.85f);
        PaintArtWorld(sky,background,Screen(new Vector2(.5f,Mathf.Lerp(.1f,.53f,tilt)),55f),
            new Vector2(height*2f,height),shade,-100,CameraFacing);
        for(int i=0;i<8;i++)
        {
            int ring=i%2;
            float angle=i*Mathf.PI*.25f+t*.03f;
            float radius=Mathf.Lerp(24f+ring*4f,13f+ring*3f,gather);
            Vector3 position=new Vector3(Mathf.Cos(angle)*radius,12f+ring*2f,Mathf.Sin(angle)*radius+2f);
            float size=11f+gather*4f;
            Color cloud=Color.Lerp(new Color(.35f,.40f,.51f),new Color(.10f,.14f,.24f),gather);
            cloud=Color.Lerp(cloud,new Color(1.6f,1.8f,2f),flash);
            cloud.a=Mathf.Lerp(.08f,.80f,gather);
            PaintArtWorld(banks[i],clouds[(Mathf.FloorToInt(t*6f)+i)%4],position,
                new Vector2(size*1.8f,size),cloud,-40+i,CameraFacing);
            banks[i].flipX=i%2==0;
        }
        if(t>=ThunderTime)
        {
            thunder.transform.localPosition=Screen(new Vector2(.52f,.98f),14f);
            thunder.transform.localRotation=CameraFacing*Quaternion.Euler(0f,0f,-18f);
            thunder.gameObject.SetActive(true);PixelMonkeyStorm.PoseBolt(thunder,t-ThunderTime,View);
        }
        Opacity=Ease(PreludeEnd,.68f,t)*(1f-Ease(3.25f,FieldReturnTime,t));
    }

    protected override void Animate(float t,float dt)
    {
        thunder.gameObject.SetActive(false);ActionTime=CastTimeAt(t);
        SoundBeat=t<.10f?-1:t<ThunderTime?0:1;
        Shot=t<PreludeEnd?1:t<1.30f?2:t<ThunderTime?3:t<FieldReturnTime?4:t<3.98f?5:6;
        View.backgroundColor=t>=PreludeEnd&&t<FieldReturnTime?new Color(.025f,.035f,.06f,1f):Color.clear;
        Opacity=1f;
        if(t<PreludeEnd)Prelude(t);
        else if(t<FieldReturnTime)SkyView(t);
        else
        {
            CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
            PaintReturn(returned,50);FieldClouds(t,.32f*(1f-Ease(FieldReturnTime,Duration,t)));
            Opacity=1f-Ease(3.88f,Duration,t);
        }
    }
}
