using UnityEngine;

[DefaultExecutionOrder(10030)]
public sealed class PixelParasiticCrown:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite mushroom;
    private SpriteRenderer body,cap;
    private ParticleSystem spores,chips;
    private readonly ParticleSystem.Particle[] mist=new ParticleSystem.Particle[48],debris=new ParticleSystem.Particle[24];
    private Vector3 ground,anchor;
    private float age,bloom=-1f,lastPulse=-10f,height,radius;
    private Color tint;

    public static PixelParasiticCrown Play(SpriteRenderer host,Vector3 ground,float radius,Color tint)
    {
        if(mushroom==null)mushroom=BuildMushroom();
        var fx=new GameObject("Parasitic Crown").AddComponent<PixelParasiticCrown>();
        fx.body=host;fx.ground=ground;fx.radius=radius;fx.tint=tint;
        fx.height=host==null?1.6f:Mathf.Clamp(PixelMantisReaper.VisibleBody(host.sprite).size.y*Mathf.Abs(host.transform.lossyScale.y)*1.4f,1.2f,2.4f);
        var root=new GameObject("Growing Mushroom");root.transform.SetParent(fx.transform,false);
        fx.cap=root.AddComponent<SpriteRenderer>();fx.cap.sprite=mushroom;
        fx.cap.sortingLayerID=host==null?0:host.sortingLayerID;fx.cap.sortingOrder=(host==null?0:host.sortingOrder)+2;
        fx.spores=PixelMushroomGas.CreateParticles("Crown Spores",fx.transform,48,PixelMushroomGas.Material(false),host,3,true);
        fx.chips=PixelMushroomGas.CreateParticles("Crown Fragments",fx.transform,24,PixelMushroomGas.Material(true),host,4,true);
        fx.Pose(ground,0f,-1f);return fx;
    }

    public void Pulse(float at){lastPulse=at;}
    public void Bloom(Vector3 at)
    {
        anchor+=at-ground;ground=at;bloom=0f;Render();
    }
    public void Pose(Vector3 at,float time,float bloomAge)
    {ground=at;age=time;bloom=bloomAge;Render();}
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Render();
        }
    }

    private void Render()
    {
        Quaternion rotation=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        Vector3 right=rotation*Vector3.right,up=rotation*Vector3.up;
        if(bloom<0f)
        {
            if(body!=null)
            {
                Bounds visible=PixelMantisReaper.VisibleBody(body.sprite);
                anchor=body.transform.TransformPoint(visible.center+new Vector3(visible.size.x*.22f,visible.size.y*.32f,0f));
                cap.sortingOrder=body.sortingOrder+2;
            }
            else anchor=ground+up*height*.65f;
        }
        cap.transform.position=anchor;cap.transform.rotation=rotation;
        float grow=Mathf.Lerp(.45f,1f,Mathf.Clamp01(age/MushroomRank3Rules.CrownDuration));
        float pulse=Mathf.Clamp01(1f-(age-lastPulse)/.22f),squeeze=Mathf.Clamp01((age-2.86f)/.14f);
        float appear=1f-Mathf.Pow(1f-Mathf.Clamp01(age/.18f),3f);
        float width=bloom<0f?1f+pulse*.17f-squeeze*.28f:1.6f+bloom*1.6f;
        float tall=bloom<0f?1f-pulse*.1f+squeeze*.08f:.7f;
        cap.transform.localScale=new Vector3(width,tall,1f)*height*(bloom<0f?grow*appear:1f);
        cap.enabled=bloom<.2f;
        cap.color=Color.Lerp(tint,new Color(1f,.98f,.8f),bloom<0f?pulse*.3f:.75f);
        if(bloom>=0f)cap.color=new Color(cap.color.r,cap.color.g,cap.color.b,Mathf.Clamp01(1f-bloom/.2f));
        if(bloom<0f)
        {
            for(int i=0;i<14;i++)
            {
                float seed=Mathf.Repeat(i*.618034f,1f),t=Mathf.Repeat(age*.9f+seed,1f);
                Color c=tint;c.a=Mathf.Sin(t*Mathf.PI)*(.1f+grow*.13f);
                mist[i]=Particle(anchor+right*Mathf.Sin(i*2.4f+t*2f)*height*(.12f+t*.18f)+up*(.18f+t*.3f)*height,
                    height*(.12f+t*.13f),c,i);
            }
            spores.SetParticles(mist,14);chips.SetParticles(debris,0);return;
        }
        float tBloom=Mathf.Clamp01(bloom/.6f),spread=1f-Mathf.Pow(1f-Mathf.Clamp01(bloom/.2f),3f);
        for(int i=0;i<mist.Length;i++)
        {
            float seed=Mathf.Repeat(Mathf.Sin(i*12.9898f+3.31f)*43758.5453f,1f),angle=i*2.399963f;
            Vector3 direction=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
            float distance=radius*Mathf.Sqrt(.04f+seed*.96f)*spread;
            Vector3 point=Vector3.Lerp(anchor,ground+direction*distance+Vector3.up*(.16f+seed*.18f),spread);
            Color c=Color.Lerp(tint,new Color(1f,.98f,.76f),Mathf.Clamp01(1f-bloom/.14f));
            c.a=Mathf.Pow(1f-tBloom,1.4f)*.65f;
            mist[i]=Particle(point,radius*(.085f+seed*.085f)*(1f+tBloom*.5f),c,i);
        }
        for(int i=0;i<debris.Length;i++)
        {
            float seed=Mathf.Repeat(i*.618034f,1f),angle=i*2.399963f;
            Vector3 flight=right*Mathf.Cos(angle)*height*(.6f+seed)*tBloom+up*(height*(.4f+seed)*tBloom-height*1.7f*tBloom*tBloom);
            Color c=Color.Lerp(tint,new Color(1f,.91f,.62f),seed);c.a=1f-tBloom;
            debris[i]=Particle(anchor+flight,height*(.035f+seed*.065f),c,i);
        }
        spores.SetParticles(mist,mist.Length);chips.SetParticles(debris,debris.Length);
    }

    private static ParticleSystem.Particle Particle(Vector3 position,float size,Color color,int index)=>
        new ParticleSystem.Particle{position=position,startSize3D=Vector3.one*size,startColor=color,
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(index+1)};
    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private static Sprite BuildMushroom()
    {
        const int w=24,h=26;var pixels=new Color[w*h];
        Color outline=new Color(.22f,.16f,.25f),shade=new Color(.58f,.46f,.64f),light=new Color(.92f,.84f,.98f),spot=new Color(1f,.96f,.78f);
        for(int y=1;y<h-1;y++)for(int x=1;x<w-1;x++)
        {
            int dx=x-12;
            if(y<12)
            {
                int bend=y/4%2;if(x<10+bend||x>14+bend)continue;
                pixels[y*w+x]=x==10+bend||x==14+bend||y==1?outline:x<13?spot:shade;
            }
            else
            {
                int half=Mathf.RoundToInt(11f*Mathf.Sqrt(Mathf.Clamp01(1f-Mathf.Pow((y-13f)/12f,2f))));
                if(Mathf.Abs(dx)>half)continue;
                Color color=Mathf.Abs(dx)>=half-1||y==12?outline:y<15?shade:dx<-2?light:shade;
                if(y>15&&y<24&&((x-7)*(x-7)+(y-18)*(y-18)<6||(x-15)*(x-15)+(y-22)*(y-22)<5||(x-19)*(x-19)+(y-17)*(y-17)<4))color=spot;
                pixels[y*w+x]=color;
            }
        }
        var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels(pixels);texture.Apply(false,true);
        var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.12f),26f);
        sprite.hideFlags=HideFlags.HideAndDontSave;return sprite;
    }
}
