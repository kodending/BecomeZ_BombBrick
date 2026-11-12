using UnityEngine;

// Authored whole-head drawings: emerge, open, bite, recoil and flow back into shadow.
public sealed class PixelTyrannoMaw:MonoBehaviour
{
    private static Sprite[] frames;
    // Relative to the opening stomp. Frame 9 makes contact exactly when damage resolves.
    private static readonly float[] frameTimes={-TyrannoRank3Rules.MawVisualLead,-.29f,-.23f,-.17f,
        -.11f,-.04f,.04f,.12f,.29f,TyrannoRank3Rules.MawPullDuration,.40f,.46f,.52f,.58f,.64f,.70f};
    private SpriteRenderer head;
    private ParticleSystem fragments;
    private Vector3 center;
    private float closeRadius;
    private bool crushed;

    public static int FrameAt(float elapsed)
    {
        if(elapsed<-TyrannoRank3Rules.MawVisualLead||elapsed>=TyrannoRank3Rules.MawEnd-TyrannoRank3Rules.MawWindup)return -1;
        for(int i=frameTimes.Length-1;i>=0;i--)if(elapsed>=frameTimes[i])return i;
        return -1;
    }

    public static PixelTyrannoMaw Open(Vector3 center,float closeRadius,SpriteRenderer source,Vector3 direction)
    {
        var texture=Resources.Load<Texture2D>("CombatEffects/Tyranno/PrimordialSummon16");
        if(texture==null||source==null)return null;
        if(frames==null||frames[0]==null)
        {
            frames=new Sprite[16];float width=texture.width/4f,height=texture.height/4f;
            for(int i=0;i<frames.Length;i++)
            {
                frames[i]=Sprite.Create(texture,new Rect(i%4*width,(3-i/4)*height,width,height),
                    new Vector2(.68f,.42f),width,0,SpriteMeshType.FullRect);
                frames[i].name="Primordial Summon "+i.ToString("D2");
                frames[i].hideFlags=HideFlags.HideAndDontSave;
            }
        }
        var dust=PixelCombatEffect.PlayTyrannoTremorImpact(center,.2f,source,false);
        dust.name="Primordial Animated Summon";dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=dust.main;main.maxParticles=80;main.loop=true;main.gravityModifier=1.4f;
        var emission=dust.emission;emission.enabled=false;
        var shape=dust.shape;shape.enabled=false;
        var velocity=dust.velocityOverLifetime;velocity.enabled=false;
        dust.Play();
        var fx=dust.gameObject.AddComponent<PixelTyrannoMaw>();
        fx.fragments=dust;fx.center=center;fx.closeRadius=closeRadius;
        Quaternion view=Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
        float facing=Vector3.Dot(direction,view*Vector3.right)<0f?-1f:1f;
        fx.head=new GameObject("Primordial summon flipbook").AddComponent<SpriteRenderer>();
        fx.head.transform.SetParent(fx.transform,true);
        fx.head.transform.SetPositionAndRotation(center+view*Vector3.up*.85f+view*Vector3.right*facing*closeRadius*.5f,view);
        float size=closeRadius*2.6f;
        fx.head.transform.localScale=new Vector3(size*facing,size,1f);
        fx.head.sortingLayerID=source.sortingLayerID;fx.head.sortingOrder=source.sortingOrder+1;
        fx.head.color=new Color(1f,1f,1f,.72f);
        fx.Advance(-TyrannoRank3Rules.MawVisualLead);return fx;
    }

    public void Advance(float elapsed)
    {
        if(this==null)return;
        int frame=FrameAt(elapsed);head.enabled=frame>=0;
        if(frame>=0)head.sprite=frames[frame];
        if(!crushed&&elapsed>=TyrannoRank3Rules.MawPullDuration)
        {
            crushed=true;
            for(int i=0;i<32;i++)
            {
                float angle=i*2.399963f;
                Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                fragments.Emit(new ParticleSystem.EmitParams {
                    position=center+direction*closeRadius*(.15f+(i%7)*.09f)+Vector3.up*.6f,
                    velocity=direction*(1.8f+(i%5)*.35f)+Vector3.up*(1.8f+(i%4)*.3f),
                    startLifetime=.2f+(i%3)*.035f,startSize=.12f+(i%4)*.035f,
                    startColor=i%3==0?new Color(1f,.93f,.76f):new Color(.69f,.53f,.32f),rotation=i*47f },1);
            }
        }
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
}
