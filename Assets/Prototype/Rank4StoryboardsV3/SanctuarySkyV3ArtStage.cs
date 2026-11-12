using UnityEngine;

// Pixel artwork placement only; each family owns its camera, action and edit timeline.
public abstract class SanctuarySkyV3ArtStage:Rank4CinematicStage
{
    protected readonly Sprite[] Motion,Kit;
    protected readonly SpriteRenderer Lead,Backdrop;
    private readonly string[] species;
    protected SanctuarySkyV3ArtStage(Transform parent,string family,Color clear,string[] species,int width=640,int height=360)
        :base(parent,family+" storyboard V3",clear,width,height)
    {
        this.species=species;
        Motion=LoadAtlas("CombatCinematics/Rank4V3/"+family+"/MotionAtlas",8,5);
        Kit=LoadAtlas("CombatCinematics/Rank4V3/"+family+"/SetKit",4,2);
        Lead=MakeSprite("Lead "+family,40);Backdrop=MakeSprite("Backdrop "+family,-100);
    }
    protected Sprite Pose(int column)
    {
        int row=0;
        for(int i=0;i<species.Length;i++)if(Context.source.species==species[i])row=i;
        if(Context.source.shiny)
        {
            string shinySpecies=Family=="spirit_deer"?"forestdeer":Family=="griffin"?"holophonas":Family=="wyvern"?"vivre":Family=="beetle"?"jangpung":"haeta";
            if(Context.source.species==shinySpecies)row=4;
        }
        return Motion[row*8+Mathf.Clamp(column,0,7)];
    }
    protected Vector3 Point(float x,float y,float depth=12f)=>Root.InverseTransformPoint(
        View.ViewportToWorldPoint(new Vector3(x,1f-y,depth)));
    protected float Span(float fraction,float depth=12f)=>Vector3.Distance(Point(.5f,.5f-fraction*.5f,depth),Point(.5f,.5f+fraction*.5f,depth));
    protected void Body(SpriteRenderer renderer,Sprite sprite,Vector2 center,float height,int order,Color color,float angle=0f,float depth=12f,bool flip=false)
    {
        Quaternion rotation=CameraFacing*Quaternion.Euler(0f,0f,angle);
        float h=Span(height,depth);
        PaintWorld(renderer,sprite,Point(center.x,center.y,depth)-rotation*Vector3.up*h*.5f,h,color,order,rotation,flip);
    }
    protected void Art(SpriteRenderer renderer,Sprite sprite,Vector2 center,Vector2 size,int order,Color color,float depth=12f,float angle=0f)
    {
        PaintArtWorld(renderer,sprite,Point(center.x,center.y,depth),new Vector2(Span(size.x,depth)*View.aspect,Span(size.y,depth)),
            color,order,CameraFacing*Quaternion.Euler(0f,0f,angle));
    }
    protected void Background(Color clear)
    {
        clear.a=1f;View.backgroundColor=clear;Opacity=1f;
        Art(Backdrop,Kit[0],new Vector2(.5f,.5f),new Vector2(1.40f,1.40f),-100,Color.white,22f);
    }
    protected void Return(SpriteRenderer renderer,float t,float fadeAt)
    {
        View.backgroundColor=Color.clear;
        CameraPose(Vector3.back*20f,Vector3.zero,4.5f,true);
        PaintReturn(renderer,200);
        Opacity=1f-Ease(fadeAt,Duration,t);
    }
}
