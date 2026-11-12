using UnityEngine;

// The charge establishes weight; the remaining storyboard beats are real arena collisions.
public sealed class MammothExpulsionRank4Stage:MoltColonyV3Stage
{
    public const float FieldReturnTime=1.85f;
    public static readonly float[] BoardTimes={.27f,.95f,1.86f,2.20f,2.70f,3.70f};
    private readonly Sprite[] motion,forest,wall,boulder,dustFrames;
    private readonly SpriteRenderer lead;
    private readonly SpriteRenderer[] opponents=new SpriteRenderer[8];
    private readonly SpriteRenderer[] trees=new SpriteRenderer[18],walls=new SpriteRenderer[4],rocks=new SpriteRenderer[6],dust=new SpriteRenderer[8];
    private readonly MeshRenderer ground;
    public override string Family=>"mammoth";
    public override float Duration=>5.10f;

    public MammothExpulsionRank4Stage(Transform parent):base(parent,"Mammoth chain collision: weight to arena",new Color(.47f,.66f,.65f,1f),1280,720)
    {
        const string path="CombatCinematics/MammothChainV1/";
        motion=LoadAtlas(path+"ActorMotion",12,5);forest=LoadAtlas(path+"Forest",1);
        wall=LoadAtlas(path+"Wall",1);boulder=LoadAtlas(path+"Boulder",1);
        dustFrames=LoadAtlas(path+"ChainDust8",4,2);
        var floor=LoadAtlas(path+"Floor",1)[0].texture;
        lead=MakeSprite("LeadMammoth new frontal charge poses",80);
        for(int i=0;i<opponents.Length;i++)opponents[i]=MakeSprite("Actual approaching opponent "+i,60+i);
        for(int i=0;i<trees.Length;i++)trees[i]=MakeSprite("Organic forest depth "+i,-100+i);
        for(int i=0;i<walls.Length;i++)walls[i]=MakeSprite("Arena boundary stone "+i,-35+i);
        for(int i=0;i<rocks.Length;i++)rocks[i]=MakeSprite("Foreground weight reference "+i,30+i);
        for(int i=0;i<dust.Length;i++)dust[i]=MakeSprite("Eight-frame hoof dust "+i,100+i);
        ground=Geometry("Perspective moss meadow",new[]{new Vector3(-60f,-.035f,-25f),new Vector3(60f,-.035f,-25f),
            new Vector3(60f,-.035f,70f),new Vector3(-60f,-.035f,70f)},new[]{0,1,2,0,2,3},
            new[]{Color.white,Color.white,Color.white,Color.white},
            new[]{new Vector2(0,0),new Vector2(14,0),new Vector2(14,12),new Vector2(0,12)},floor);
    }

    protected override void Animate(float t,float dt)
    {
        bool returned=t>=FieldReturnTime;
        ActionTime=returned?MammothExpulsionRules.Contact+.001f+t-FieldReturnTime:Mathf.Min(.39f,t*.22f);
        SoundBeat=t>=.56f?1:-1;Shot=t<.60f?0:t<FieldReturnTime?1:2;
        Opacity=returned?0f:1f-Ease(1.48f,FieldReturnTime,t);
        ground.enabled=!returned;
        if(returned){CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);View.backgroundColor=Color.clear;return;}
        float tracking=Ease(.52f,1.35f,t),lift=Ease(1.32f,1.70f,t);
        Vector3 camera=Vector3.Lerp(new Vector3(-3.2f,1.05f,-5f),new Vector3(-.8f,1.4f,-3.7f),tracking);
        camera=Vector3.Lerp(camera,new Vector3(-.3f,6.2f,-8.8f),lift);
        Vector3 look=Vector3.Lerp(new Vector3(.6f,.9f,2.4f),new Vector3(2.8f,.5f,4.2f),tracking);
        look=Vector3.Lerp(look,new Vector3(1f,0f,4f),lift);
        CameraPose(camera,look,42f);
        View.backgroundColor=new Color(.47f,.66f,.65f,1f);
        for(int i=0;i<trees.Length;i++)
        {
            int row=i/6;float x=(i%6-2.5f)*8f+(row%2==0?1.8f:-1.5f)+4f;
            PaintWorld(trees[i],forest[0],new Vector3(x,-.3f-row*.2f,16f+row*7f),12f+row*1.7f,
                Color.Lerp(Color.white,new Color(.61f,.75f,.72f,1f),row*.19f),-70-row*10,Quaternion.identity,i%2==1);
        }
        for(int i=0;i<walls.Length;i++)PaintWorld(walls[i],wall[0],new Vector3((i-1.5f)*5.4f,0f,12f),1.4f,
            Color.white,-35+i,Quaternion.identity,i%2==0);
        for(int i=0;i<rocks.Length;i++)
        {
            float side=i%2==0?-1f:1f;int row=i/2;
            PaintWorld(rocks[i],boulder[0],new Vector3(side*(5.8f+row*.5f),0f,-.8f+row*3.7f),1.4f+row*.22f,
                Color.white,30+i,Quaternion.identity,i%2==0);
        }
        int species=SpeciesRow(Context.source.species,"seolgason","irontusk","gaiason");
        if(Context.source.shiny&&species==0)species=4;
        int pose=t<.60f?Mathf.Min(3,Mathf.FloorToInt(t/.15f)):
            t<1.48f?4+Mathf.FloorToInt((t-.60f)*15f)%4:8+Mathf.Min(3,Mathf.FloorToInt((t-1.48f)/.09f));
        float returning=Ease(1.48f,FieldReturnTime,t);
        Vector3 anchor=Vector3.Lerp(new Vector3(.34f,.54f,.64f),new Vector3(.53f,.57f,.53f),tracking);
        if(returning>0f)
        {
            Vector3 native=new Vector3(.5f+Context.returnPosition.x/16f,
                .5f-(Context.returnPosition.y+Context.returnHeight*.5f)/9f,Context.returnHeight/9f);
            anchor=Vector3.Lerp(anchor,native,returning);
        }
        GroundBodyAt(lead,motion[species*12+pose],anchor,80,Color.white);
        if(t>=.60f)
            for(int i=0;i<Mathf.Min(3,Context.targets.Length);i++)
            {
                var target=Context.targets[i];
                float enter=Ease(.60f,1.40f,t);
                Vector3 enemyAnchor=new Vector3(.91f+i*.12f-enter*.10f,.70f-i*.04f,.16f-i*.01f);
                GroundBodyAt(opponents[i],target.sprite,enemyAnchor,60+i,target.color,true);
            }
        for(int i=0;i<dust.Length;i++)
        {
            float born=.50f+i*.12f,age=t-born;if(age<0f||age>.60f)continue;
            int frame=Mathf.Min(7,Mathf.FloorToInt(age/.075f));float drift=age*.22f;
            BodyAt(dust[i],dustFrames[frame],new Vector3(anchor.x-.12f-drift,
                anchor.y+anchor.z*.5f-.04f+age*.02f,.15f+age*.10f),5.8f,100+i,
                new Color(1f,1f,1f,(1f-age/.60f)*.72f));
        }
    }
}
