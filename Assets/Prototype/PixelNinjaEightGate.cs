using System;
using UnityEngine;

// The combat animation samples the armed formation, so freezing the caster freezes its kunai too.
public sealed class PixelNinjaEightGate:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private const float KunaiVisualScale=.7f;
    private static Sprite kunaiSprite,sealSprite,glyphSprite,cloudSprite,pixelSprite;
    private static readonly Color Vermilion=new Color(1f,.25f,.055f);
    private readonly SpriteRenderer[] blades=new SpriteRenderer[8],papers=new SpriteRenderer[8];
    private readonly SpriteRenderer[] glyphs=new SpriteRenderer[8];
    private readonly SpriteRenderer[,] echoes=new SpriteRenderer[8,2],grit=new SpriteRenderer[8,3];
    private SpriteRenderer[] clouds,chips;
    private SpriteRenderer core;
    private Vector3 center;
    private Quaternion billboard;
    private Func<bool> valid;
    private int layer,order;
    private float blastAge;
    private bool detonated;
    private readonly bool[] removed=new bool[8];

    public SpriteRenderer Blade(int index)=>blades[index];
    public void RemoveBlade(int index)
    {
        removed[index]=true;blades[index].enabled=papers[index].enabled=glyphs[index].enabled=false;
        for(int j=0;j<2;j++)echoes[index,j].enabled=false;
        for(int j=0;j<3;j++)grit[index,j].enabled=false;
    }

    public static PixelNinjaEightGate Play(Vector3 center,SpriteRenderer source,Func<bool> valid)
    {
        if(source==null)return null;
        var fx=new GameObject("Eight Gate Explosive Talismans").AddComponent<PixelNinjaEightGate>();
        fx.center=center;fx.valid=valid;
        fx.billboard=Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
        fx.layer=source.sortingLayerID;fx.order=source.sortingOrder;
        for(int i=0;i<8;i++)
        {
            fx.blades[i]=fx.Child("Planted Kunai "+i,Kunai,Color.white);
            fx.papers[i]=fx.Child("Explosive Paper "+i,Seal,Color.white);
            fx.glyphs[i]=fx.Child("Glowing Talisman Writing "+i,Glyph,Vermilion,1);
            for(int j=0;j<2;j++)fx.echoes[i,j]=fx.Child("Kunai Echo",Kunai,new Color(1f,.48f,.18f,.35f-j*.12f));
            for(int j=0;j<3;j++)fx.grit[i,j]=fx.Child("Planting Grit",Pixel,new Color(.57f,.38f,.22f));
        }
        fx.Sample(0f);return fx;
    }

    public void Sample(float time)
    {
        if(this==null||detonated)return;
        float flight=NinjaCatRank3Rules.GateFlightProgress(time);
        float charge=Mathf.InverseLerp(NinjaCatRank3Rules.GateArrivalTime,NinjaCatRank3Rules.GateLaunchTime,time);
        for(int i=0;i<8;i++)
        {
            if(removed[i])continue;
            Vector3 origin=NinjaCatRank3Rules.GatePoint(center,i);
            float age=time-NinjaCatRank3Rules.GateInstallTime-i*NinjaCatRank3Rules.GateInstallInterval;
            bool visible=age>=0f&&time<NinjaCatRank3Rules.GateBlastTime;
            blades[i].enabled=papers[i].enabled=visible;
            Vector3 ground=Vector3.Lerp(origin,center,flight);
            float drop=(1f-Mathf.Clamp01(age/.075f));
            Vector3 position=ground+Vector3.up*(.55f*KunaiVisualScale+drop*drop*1.7f+Mathf.Sin(flight*Mathf.PI)*.3f);
            blades[i].transform.position=position;
            Vector3 screen=Quaternion.Inverse(billboard)*(center-origin);
            float angle=flight>0f?Mathf.Atan2(screen.y,screen.x)*Mathf.Rad2Deg+90f:-12f+(i%3)*10f;
            blades[i].transform.rotation=billboard*Quaternion.Euler(0,0,angle);
            blades[i].transform.localScale=Vector3.one*(1.25f*KunaiVisualScale);
            papers[i].transform.position=position+billboard*new Vector3(.24f+Mathf.Sin(age*21f+i)*.04f,.18f,0)*KunaiVisualScale;
            papers[i].transform.rotation=billboard*Quaternion.Euler(0,0,Mathf.Sin(age*18f+i)*14f);
            papers[i].transform.localScale=new Vector3(.9f+Mathf.Sin(age*20f)*.1f,1f,1f)*KunaiVisualScale;
            papers[i].color=Color.Lerp(Color.white,new Color(1f,.50f,.25f),charge*.75f);
            glyphs[i].enabled=visible&&charge>0f;
            glyphs[i].transform.SetPositionAndRotation(papers[i].transform.position,papers[i].transform.rotation);
            glyphs[i].transform.localScale=papers[i].transform.localScale;
            Color glow=Color.Lerp(Vermilion,new Color(1f,.96f,.6f),charge*(.7f+.3f*Mathf.Sin(time*36f)));
            glow.a=charge;glyphs[i].color=glow;
            for(int j=0;j<2;j++)
            {
                var echo=echoes[i,j];echo.enabled=visible&&flight>0f;
                float behind=Mathf.Max(0f,flight-(j+1)*.075f);
                echo.transform.position=Vector3.Lerp(origin,center,behind)+Vector3.up*(.55f*KunaiVisualScale+Mathf.Sin(behind*Mathf.PI)*.3f);
                echo.transform.rotation=blades[i].transform.rotation;echo.transform.localScale=blades[i].transform.localScale;
            }
            for(int j=0;j<3;j++)
            {
                float p=(age-.05f)/.18f;
                grit[i,j].enabled=p>=0f&&p<1f;
                grit[i,j].transform.position=origin+billboard*new Vector3((j-1)*p*.4f,Mathf.Sin(p*Mathf.PI)*.22f,0);
                grit[i,j].transform.localScale=Vector3.one*(.07f*(1f-p*.5f));
            }
        }
    }

    public void Detonate()
    {
        if(this==null||detonated)return;
        Sample(NinjaCatRank3Rules.GateBlastTime);detonated=true;
        core=Child("White Hot Blast",Cloud,Color.white,4);
        clouds=new SpriteRenderer[12];chips=new SpriteRenderer[28];
        for(int i=0;i<clouds.Length;i++)clouds[i]=Child("Vermilion Fire and Soot",Cloud,Vermilion);
        for(int i=0;i<chips.Length;i++)chips[i]=Child("Flying Ember and Seal Scrap",i%4==0?Seal:Pixel,Vermilion,2);
        AnimateBlast(0f);
    }

    public static void PlayHit(SpriteRenderer target,bool blast)
    {
        var fx=PixelNinjaMoonFx.PlayImpact(target,Vector3.up,blast,Vermilion);
        if(fx!=null)fx.name=blast?"Eight Gate Victim Blast":"Eight Gate Kunai Contact";
    }

    private SpriteRenderer Child(string name,Sprite sprite,Color color,int extra=0)
    {
        var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);
        r.transform.rotation=billboard;r.sprite=sprite;r.color=color;
        r.sortingLayerID=layer;r.sortingOrder=order+extra;return r;
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(detonated)AnimateBlast((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
            else if(valid!=null&&!valid())Cancel();
        }
    }

    private void AnimateBlast(float dt)
    {
        blastAge+=Mathf.Max(0f,dt);
        if(blastAge>=.6f){Cancel();return;}
        float p=blastAge/.6f,radius=MonsterAutoBattlePrototype.TileSize*NinjaCatRank3Rules.KunaiExplosionRadiusTiles;
        float expansion=1f-Mathf.Pow(1f-Mathf.Clamp01(blastAge/.19f),3f);
        core.transform.position=center+Vector3.up*(.5f+p*1.4f);
        core.transform.localScale=Vector3.one*(1.3f+expansion*3.3f);
        Color heart=blastAge<.055f?new Color(1f,.96f,.76f):
            Color.Lerp(new Color(1f,.55f,.08f),new Color(.18f,.12f,.16f),Mathf.InverseLerp(.08f,.23f,blastAge));
        heart.a=1f-Mathf.InverseLerp(.22f,.53f,blastAge);core.color=heart;
        for(int i=0;i<clouds.Length;i++)
        {
            float angle=i*30f*Mathf.Deg2Rad;
            float travel=radius*.76f*expansion*(i%3==0?.66f:1f);
            clouds[i].transform.position=center+new Vector3(Mathf.Cos(angle)*travel,.25f+p*(.9f+(i%3)*.3f),Mathf.Sin(angle)*travel);
            clouds[i].transform.localRotation=billboard*Quaternion.Euler(0,0,(i%4)*90f);
            float size=(1.7f+(i%3)*.28f)*Mathf.Lerp(.6f,1.2f,expansion)*(1f-p*.22f);
            clouds[i].transform.localScale=Vector3.one*size;
            Color c=blastAge<.08f?new Color(1f,.67f,.16f):Color.Lerp(Vermilion,new Color(.16f,.11f,.17f),Mathf.InverseLerp(.09f,.24f,blastAge));
            c.a=1f-Mathf.InverseLerp(.25f,.6f,blastAge);clouds[i].color=c;
        }
        for(int i=0;i<chips.Length;i++)
        {
            float angle=(i*137.5f)*Mathf.Deg2Rad;
            float travel=radius*(.65f+(i%5)*.1f)*Mathf.Sqrt(p);
            chips[i].transform.position=center+new Vector3(Mathf.Cos(angle)*travel,.2f+Mathf.Sin(p*Mathf.PI)*(.7f+i%4*.3f),Mathf.Sin(angle)*travel);
            chips[i].transform.rotation=billboard*Quaternion.Euler(0,0,i*31f+p*320f);
            chips[i].transform.localScale=Vector3.one*(i%4==0?.48f:.10f+(i%3)*.045f)*(1f-p*.4f);
            Color c=i%4==0?Color.white:i%3==0?new Color(.20f,.14f,.17f):new Color(1f,.55f,.1f);
            c.a=1f-Mathf.InverseLerp(.55f,1f,p);chips[i].color=c;
        }
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private static Sprite Pixel=>pixelSprite!=null?pixelSprite:
        pixelSprite=PixelNinjaSubstitution.MakeSprite("Gate Pixel",1,1,1f,(x,y)=>Color.white);
    private static Sprite Kunai=>kunaiSprite!=null?kunaiSprite:
        kunaiSprite=PixelNinjaSubstitution.MakeSprite("Pixel Steel Kunai",16,32,28f,(x,y)=>
        {
            int d=Mathf.Abs(x-7);
            if(y<19)
            {
                int half=y<13?y/2:(19-y)/2+3;
                if(d>half)return Color.clear;
                if(d==half)return new Color(.10f,.14f,.23f);
                return x<7?new Color(.47f,.64f,.73f):new Color(.87f,.96f,1f);
            }
            if(y<26&&d<=1)return y%3==0?new Color(.68f,.40f,.25f):new Color(.21f,.21f,.29f);
            if(y>=26&&y<=30&&d<=3&&(d>=2||y==26||y==30))return new Color(.47f,.64f,.73f);
            return Color.clear;
        });
    private static Sprite Seal=>sealSprite!=null?sealSprite:
        sealSprite=PixelNinjaSubstitution.MakeSprite("Pixel Explosive Paper",12,20,28f,(x,y)=>
        {
            if(x==0||x==11||y==0||y==19)return new Color(.32f,.19f,.17f);
            return IsGlyph(x,y)?new Color(.65f,.075f,.055f):new Color(1f,.86f,.59f);
        });
    private static bool IsGlyph(int x,int y)=>(y==16&&x>2&&x<9)||(x==5&&y>3&&y<17)||
        (y%4==0&&x>=3&&x<=8)||(x==8&&y>7&&y<14)||(x==3&&y>4&&y<10);
    private static Sprite Glyph=>glyphSprite!=null?glyphSprite:
        glyphSprite=PixelNinjaSubstitution.MakeSprite("Glowing Pixel Seal Glyph",12,20,28f,
            (x,y)=>x>0&&x<11&&y>0&&y<19&&IsGlyph(x,y)?Color.white:Color.clear);
    private static Sprite Cloud=>cloudSprite!=null?cloudSprite:
        cloudSprite=PixelNinjaSubstitution.MakeSprite("Chunky Pixel Blast",32,32,32f,(x,y)=>
        {
            int px=x/2*2,py=y/2*2;
            float dx=px-15f,dy=py-15f,angle=Mathf.Atan2(dy,dx);
            float r=12f+2f*Mathf.Sin(angle*5f)+1.5f*Mathf.Cos(angle*9f);
            if(dx*dx+dy*dy>r*r)return Color.clear;
            float shade=py<9?.72f:px>22?.85f:1f;
            return new Color(shade,shade,shade);
        });
}
