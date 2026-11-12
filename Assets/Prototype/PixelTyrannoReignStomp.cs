using System.Collections.Generic;
using UnityEngine;

// Authored toes, ankle and connected shin deform around one planted world-space contact.
public sealed class PixelTyrannoReignStomp:MonoBehaviour
{
    public const string ResourceRoot="CombatCinematics/TyrannoGodImpactV1/";
    private const int ParticleLimit=8,StoneCount=12;
    private static readonly Dictionary<string,Sprite[]> Motion=new Dictionary<string,Sprite[]>();
    private static readonly Dictionary<Sprite,Rect> ContactFallback=new Dictionary<Sprite,Rect>();
    private static readonly Rect TinoV3ContactRect=new Rect(10f/256f,31f/256f,240f/256f,105f/256f);
    private static Sprite[] shockFrames,dustFrames;
    private static readonly int Reveal=Shader.PropertyToID("_Reveal"),Pulse=Shader.PropertyToID("_Pulse"),
        Opacity=Shader.PropertyToID("_Opacity"),Phase=Shader.PropertyToID("_Phase"),PixelGrid=Shader.PropertyToID("_PixelGrid");
    private Sprite[] frames,shadowFrames,rubbleFrames;
    private SpriteRenderer foot,shadow,shock;
    private MeshRenderer fissures;
    private Mesh groundMesh;
    private Material fissureMaterial;
    private MaterialPropertyBlock groundProperties;
    private readonly Vector2[] groundUV=new Vector2[4];
    private int groundFrame=-1;
    private readonly SpriteRenderer[] stones=new SpriteRenderer[StoneCount];
    private readonly ParticleSystem[] clouds=new ParticleSystem[3];
    private readonly SpriteRenderer[] billows=new SpriteRenderer[8];
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[ParticleLimit];
    private Vector3 center;
    private Vector3 projectionOrigin,projectionForward,footprintCenter;
    private bool perspective;
    private Bounds arena;
    private Color terrain;
    private Quaternion facing;
    private float width;
    public SpriteRenderer FootRenderer=>foot;
    public Vector3 LandingAnchor=>center;
    public Rect ContactBounds {get;private set;}
    public Vector2 FootprintWorldSize {get;private set;}

    public static int FrameAt(float time)
    {
        float age=time-FamilyTranscendenceRules.TyrannoStompWarning;
        if(age<-.14f||age>=.70f)return -1;
        if(age<0f)return Mathf.Clamp(Mathf.FloorToInt((age+.14f)/.14f*5f),0,4);
        if(age<.05f)return 5;
        if(age<.14f)return 6;
        if(age<.23f)return 7;
        if(age<.33f)return 8;
        if(age<.46f)return 9;
        if(age<.60f)return 10;
        return 11;
    }

    public static string MotionPath(string species,bool shiny)
    {
        int row=TyrannoReignRank4Stage.SpeciesRow(species);
        return ResourceRoot+(row==1?"ImpactMotionMagno":row==2?"ImpactMotionCheolno":
            row==3?(shiny?"ImpactMotionBonrexShiny":"ImpactMotionBonrex"):"ImpactMotionTinoV3");
    }

    public static PixelTyrannoReignStomp Create(Bounds arena,SpriteRenderer reference,int order,string species=null,bool shiny=false,bool mirrored=false,Camera viewCamera=null)
    {
        if(reference==null)return null;
        Sprite[] animation=Atlas(MotionPath(species,shiny),4,3,new Vector2(.5f,.21875f));
        if(animation==null&&TyrannoReignRank4Stage.SpeciesRow(species)==0)
            animation=Atlas(ResourceRoot+"ImpactMotionTino",4,3,new Vector2(.5f,.21875f));
        if(animation==null)return null;
        var root=new GameObject("Tyrant Battlefield Stomp");root.AddComponent<CombatEffectEndMarker>();
        root.transform.position=new Vector3(arena.center.x,.065f,arena.center.z);
        var fx=root.AddComponent<PixelTyrannoReignStomp>();fx.groundProperties=new MaterialPropertyBlock();fx.frames=animation;fx.arena=arena;
        fx.center=root.transform.position;fx.facing=viewCamera!=null?viewCamera.transform.rotation:reference.transform.rotation;
        fx.terrain=MeadowAmbientParticles.TerrainColor(new Color(.47f,.38f,.25f));
        fx.foot=fx.MakeSprite("Animated tyrant foot",reference,order,fx.facing);
        fx.foot.flipX=mirrored;
        fx.FitContact(animation[5],viewCamera);
        fx.shadow=fx.MakeSprite("Growing foot shadow",reference,2,Quaternion.Euler(90f,0f,0f));
        fx.BuildShadows();
        if(shockFrames==null||shockFrames[0]==null)
            shockFrames=Atlas("CombatEffects/Tyranno/ExtinctionBurst",4,4,Vector2.one*.5f);
        fx.shock=fx.MakeSprite("Authored pixel earth shock",reference,5,Quaternion.Euler(90f,0f,0f));
        if(shockFrames!=null)
        {
            Bounds visible=PixelMantisReaper.VisibleBody(shockFrames[5]);
            Vector3 scale=new Vector3(fx.width/Mathf.Max(.01f,visible.size.x),fx.FootprintWorldSize.y/Mathf.Max(.01f,visible.size.y),1f);
            fx.shock.transform.localScale=scale;
            fx.shock.transform.localPosition=fx.footprintCenter-fx.center+Vector3.up*.012f-
                fx.shock.transform.localRotation*Vector3.Scale(visible.center,scale);
        }
        fx.BuildGround(reference);fx.BuildStones(reference);fx.BuildDust(reference,order);
        fx.Advance(0f);return fx;
    }

    private void BuildShadows()
    {
        shadowFrames=new Sprite[4];Rect contact=ContactBounds;
        if(foot.flipX)contact=new Rect(-contact.xMax,contact.y,contact.width,contact.height);
        for(int i=0;i<shadowFrames.Length;i++)
        {
            Sprite source=frames[5+i];float ppu=source.pixelsPerUnit;
            float x=Mathf.Clamp(Mathf.Floor(contact.xMin*ppu+source.pivot.x),0f,source.rect.width-1f),
                y=Mathf.Clamp(Mathf.Floor(contact.yMin*ppu+source.pivot.y),0f,source.rect.height-1f);
            float right=Mathf.Clamp(Mathf.Ceil(contact.xMax*ppu+source.pivot.x),x+1f,source.rect.width),
                top=Mathf.Clamp(Mathf.Ceil(contact.yMax*ppu+source.pivot.y),y+1f,source.rect.height);
            shadowFrames[i]=Sprite.Create(source.texture,new Rect(source.rect.x+x,source.rect.y+y,right-x,top-y),Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect);
            shadowFrames[i].name="Authored tyrant contact silhouette "+i;shadowFrames[i].hideFlags=HideFlags.HideAndDontSave;
        }
        shadow.sprite=shadowFrames[0];shadow.flipX=foot.flipX;
    }

    private static Rect AuthoredContact(Sprite frame)
    {
        if(frame.name.StartsWith(ResourceRoot+"ImpactMotionTinoV3 "))
        {
            Vector2 size=frame.rect.size/frame.pixelsPerUnit,pivot=frame.pivot/frame.pixelsPerUnit;
            return new Rect(Vector2.Scale(TinoV3ContactRect.position,size)-pivot,Vector2.Scale(TinoV3ContactRect.size,size));
        }
        if(ContactFallback.TryGetValue(frame,out Rect cached))return cached;
        // Legacy species have no sole metadata; measure their visible forefoot, never their tall shin.
        Sprite crop=Sprite.Create(frame.texture,new Rect(frame.rect.x,frame.rect.y,frame.rect.width,frame.rect.height*.35f),
            Vector2.zero,frame.pixelsPerUnit,0,SpriteMeshType.FullRect);
        crop.hideFlags=HideFlags.HideAndDontSave;
        try
        {
            Bounds visible=PixelMantisReaper.VisibleBody(crop);Vector2 pivot=frame.pivot/frame.pixelsPerUnit;
            cached=new Rect(new Vector2(visible.min.x,visible.min.y)-pivot,new Vector2(visible.size.x,visible.size.y));
            ContactFallback[frame]=cached;return cached;
        }
        finally{PixelMantisReaper.ReleaseVisibleBodies(new[]{crop});Remove(crop);}
    }

    private void FitContact(Sprite frame,Camera viewCamera)
    {
        ContactBounds=AuthoredContact(frame);
        if(foot.flipX)ContactBounds=new Rect(-ContactBounds.xMax,ContactBounds.y,ContactBounds.width,ContactBounds.height);
        projectionForward=facing*Vector3.forward;perspective=viewCamera!=null&&!viewCamera.orthographic;
        projectionOrigin=viewCamera!=null?viewCamera.transform.position:Vector3.zero;
        Quaternion inverse=Quaternion.Inverse(facing);float x=0f,y=0f;
        for(int i=0;i<4;i++)
        {
            Vector3 corner=center+new Vector3((i<2?-1f:1f)*arena.extents.x*1.10f,0f,(i%2==0?-1f:1f)*arena.extents.z*1.05f);
            if(viewCamera!=null)
            {
                Vector3 origin=perspective?projectionOrigin:corner,direction=perspective?corner-origin:projectionForward;
                float divisor=Vector3.Dot(direction,projectionForward);
                if(Mathf.Abs(divisor)>.0001f)corner=origin+direction*(Vector3.Dot(center-origin,projectionForward)/divisor);
            }
            Vector3 local=inverse*(corner-center);x=Mathf.Max(x,Mathf.Abs(local.x));y=Mathf.Max(y,Mathf.Abs(local.y));
        }
        float scale=Mathf.Max(x*2f/Mathf.Max(.001f,ContactBounds.width),y*2f/Mathf.Max(.001f,ContactBounds.height));
        foot.transform.localScale=Vector3.one*scale;
        foot.transform.localPosition=-(facing*new Vector3(ContactBounds.center.x,ContactBounds.center.y,0f))*scale;
        Bounds ground=new Bounds(GroundPoint(0f,0f),Vector3.zero);
        ground.Encapsulate(GroundPoint(0f,1f));ground.Encapsulate(GroundPoint(1f,0f));ground.Encapsulate(GroundPoint(1f,1f));
        FootprintWorldSize=new Vector2(ground.size.x,ground.size.z);footprintCenter=ground.center;width=ground.size.x;
    }

    private Vector3 GroundPoint(float u,float v)
    {
        Vector3 p=foot.transform.TransformPoint(new Vector3(Mathf.Lerp(ContactBounds.xMin,ContactBounds.xMax,u),
            Mathf.Lerp(ContactBounds.yMin,ContactBounds.yMax,v),0f));
        Vector3 origin=perspective?projectionOrigin:p,direction=perspective?p-origin:projectionForward;
        if(Mathf.Abs(direction.y)>.0001f)
        {
            Vector3 ground=origin+direction*((center.y-origin.y)/direction.y);ground.y=center.y;return ground;
        }
        return center+new Vector3((u-.5f)*ContactBounds.width*foot.transform.localScale.x,0f,(v-.5f)*arena.size.z*1.05f);
    }

    public Rect ContactViewportRect(Camera camera)
    {
        if(camera==null||foot==null)return Rect.zero;
        Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
        for(int i=0;i<4;i++)
        {
            Vector3 p=foot.transform.TransformPoint(new Vector3(i<2?ContactBounds.xMin:ContactBounds.xMax,
                i%2==0?ContactBounds.yMin:ContactBounds.yMax,0f));
            Vector2 viewport=camera.WorldToViewportPoint(p);min=Vector2.Min(min,viewport);max=Vector2.Max(max,viewport);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    private static Sprite[] Atlas(string path,int columns,int rows,Vector2 pivot)
    {
        var texture=Resources.Load<Texture2D>(path);if(texture==null)return null;
        if(Motion.TryGetValue(path,out Sprite[] cached)&&cached[0]!=null&&cached[0].texture==texture&&
           cached[0].rect.width==texture.width/(float)columns&&cached[0].rect.height==texture.height/(float)rows)return cached;
        var result=new Sprite[columns*rows];float w=texture.width/(float)columns,h=texture.height/(float)rows;
        for(int i=0;i<result.Length;i++)
        {
            result[i]=UnityEngine.Sprite.Create(texture,new Rect(i%columns*w,(rows-1-i/columns)*h,w,h),pivot,w,0,SpriteMeshType.FullRect);
            result[i].name=path+" "+i;result[i].hideFlags=HideFlags.HideAndDontSave;
        }
        Motion[path]=result;return result;
    }

    private SpriteRenderer MakeSprite(string name,SpriteRenderer reference,int order,Quaternion rotation)
    {
        var child=new GameObject(name);child.transform.SetParent(transform,false);child.transform.localRotation=rotation;
        var renderer=child.AddComponent<SpriteRenderer>();renderer.sortingLayerID=reference.sortingLayerID;renderer.sortingOrder=order;return renderer;
    }

    private void BuildGround(SpriteRenderer reference)
    {
        groundMesh=new Mesh{name="Tyrant textured XZ crater",hideFlags=HideFlags.DontSave};
        groundMesh.vertices=new[]{GroundPoint(0f,0f)-center,GroundPoint(0f,1f)-center,GroundPoint(1f,1f)-center,GroundPoint(1f,0f)-center};
        groundMesh.triangles=new[]{0,1,2,0,2,3};groundMesh.RecalculateBounds();
        var child=new GameObject("Authored asymmetric pixel ground fracture");child.transform.SetParent(transform,false);
        child.transform.localPosition=Vector3.down*.025f;
        child.AddComponent<MeshFilter>().sharedMesh=groundMesh;fissures=child.AddComponent<MeshRenderer>();
        fissures.sortingLayerID=reference.sortingLayerID;fissures.sortingOrder=3;
        var shader=Resources.Load<Shader>(KingKongLeylineCinematicStage.ResourceRoot+"GroundFissureSurface");
        if(shader==null)return;
        fissureMaterial=new Material(shader){name="Tyrant authored pixel crater reveal",hideFlags=HideFlags.DontSave};
        var texture=Resources.Load<Texture2D>(ResourceRoot+"PixelCraterMotionV3");
        fissureMaterial.mainTexture=texture;
        if(texture!=null)groundProperties.SetVector(PixelGrid,new Vector4(texture.width,texture.height,1f/texture.width,1f/texture.height));
        fissures.sharedMaterial=fissureMaterial;
        PoseGroundFrame(0);
    }

    private void PoseGroundFrame(int frame)
    {
        if(frame==groundFrame)return;groundFrame=frame;
        Texture texture=fissureMaterial.mainTexture;
        float inset=texture!=null?.5f/texture.width:0f,yInset=texture!=null?.5f/texture.height:0f;
        float left=frame*.25f+inset,right=(frame+1)*.25f-inset;
        groundUV[0]=new Vector2(left,yInset);groundUV[1]=new Vector2(left,1f-yInset);
        groundUV[2]=new Vector2(right,1f-yInset);groundUV[3]=new Vector2(right,yInset);groundMesh.uv=groundUV;
    }

    private void BuildStones(SpriteRenderer reference)
    {
        rubbleFrames=Atlas(ResourceRoot+"PixelRubbleMotionV3",4,1,Vector2.one*.5f);
        for(int i=0;i<StoneCount;i++)
        {
            stones[i]=MakeSprite("Burst terrain plate "+i,reference,7+i%3,facing);
            if(rubbleFrames!=null)stones[i].sprite=rubbleFrames[0];
            stones[i].flipX=i%2==0;
            float visible=rubbleFrames!=null?PixelMantisReaper.VisibleBody(stones[i].sprite).size.x:1f;
            stones[i].transform.localScale=Vector3.one*(width*(.025f+Seed(i+17)*.015f)/Mathf.Max(.01f,visible));
        }
    }

    private void BuildDust(SpriteRenderer reference,int order)
    {
        if(dustFrames==null||dustFrames[0]==null)dustFrames=Atlas(ResourceRoot+"PixelDustBurst",4,2,new Vector2(.5f,.12f));
        Bounds dustBody=dustFrames!=null?PixelMantisReaper.VisibleBody(dustFrames[2]):new Bounds(Vector3.zero,Vector3.one);
        float dustScale=Mathf.Max(width*.24f/Mathf.Max(.01f,dustBody.size.x),FootprintWorldSize.y*.21f/Mathf.Max(.01f,dustBody.size.y));
        for(int i=0;i<billows.Length;i++)
        {
            billows[i]=MakeSprite((i<4?"Near":"Far")+" authored pixel dust volume "+i,reference,i<4?order+6+i%2:4,facing);
            billows[i].transform.localScale=Vector3.one*dustScale*(1f+Seed(i+47)*.25f);
        }
        for(int layer=0;layer<clouds.Length;layer++)
        {
            var system=PixelCombatEffect.PlayTyrannoTremorImpact(center,.1f,reference,false);
            system.name=layer==0?"Near impact dust and grit":layer==1?"Far dust pressure volume":"Downstroke wind and compression core";
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.transform.SetParent(transform,false);system.transform.localPosition=Vector3.zero;
            var main=system.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleLimit;main.simulationSpeed=0f;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startSpeed=0f;main.startSize3D=true;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var velocity=system.velocityOverLifetime;velocity.enabled=false;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sortingOrder=layer==0?order+4:layer==1?4:order+2;
            renderer.sortMode=ParticleSystemSortMode.Distance;clouds[layer]=system;system.Play(false);system.Pause(false);
        }
    }

    public void Advance(float time)
    {
        if(this==null)return;
        float contact=FamilyTranscendenceRules.TyrannoStompWarning,age=time-contact;
        int frame=FrameAt(time);foot.enabled=frame>=0;
        if(frame>=0){foot.sprite=frames[frame];foot.color=Color.white;}
        float warning=Ease(0f,contact,time),residue=1f-Ease(1.22f,FamilyTranscendenceRules.TyrannoStompEnd,time);
        shadow.enabled=time>0f&&age<.33f;
        shadow.sprite=shadowFrames[Mathf.Clamp(Mathf.FloorToInt((time-.36f)/.10f),0,3)];
        shadow.transform.localPosition=footprintCenter-center+Vector3.down*.035f;
        shadow.transform.localScale=new Vector3(width*(.72f+warning*.28f)/shadowFrames[0].bounds.size.x,
            FootprintWorldSize.y/shadowFrames[0].bounds.size.y,1f);
        shadow.color=new Color(.05f,.025f,.02f,(.08f+warning*.28f)*(1f-Ease(.04f,.33f,age)));
        fissures.enabled=age>=0f&&residue>0f&&fissureMaterial!=null;
        if(fissures.enabled)
        {
            PoseGroundFrame(age<.028f?0:age<.056f?1:age<.10f?2:3);
            groundProperties.SetFloat(Reveal,1f);groundProperties.SetFloat(Pulse,0f);
            groundProperties.SetFloat(Opacity,residue*.84f);groundProperties.SetFloat(Phase,0f);fissures.SetPropertyBlock(groundProperties);
        }
        shock.enabled=shockFrames!=null&&age>=0f&&age<.25f;
        if(shock.enabled)
        {
            shock.sprite=shockFrames[Mathf.Clamp(5+Mathf.FloorToInt(age/.018f),5,15)];
            shock.color=new Color(1f,1f,1f,1f-Ease(.14f,.25f,age));
        }
        for(int i=0;i<billows.Length;i++)
        {
            var billow=billows[i];float a=age-(i%4)*.006f;
            billow.enabled=dustFrames!=null&&a>=0f&&a<.57f;if(!billow.enabled)continue;
            float u=.08f+(i%4)*.28f,v=i<4?.04f:.96f,outward=Ease(0f,.12f,a);
            int dustFrame=a<.02f?0:a<.05f?1:a<.09f?2:a<.15f?3:a<.23f?4:a<.32f?5:a<.43f?6:7;
            billow.sprite=dustFrames[dustFrame];billow.flipX=i%2==0;
            Vector3 edge=GroundPoint(u,v),direction=edge-center;direction.y=0f;
            billow.transform.localPosition=edge-center+direction.normalized*(outward*FootprintWorldSize.y*.08f)+
                Vector3.up*(.03f+Mathf.Sin(a/.57f*Mathf.PI)*.12f);
            billow.color=new Color(1f,1f,1f,(i<4?1f:.94f)*(1f-Ease(.35f,.57f,a)));
        }
        for(int i=0;i<stones.Length;i++)
        {
            var stone=stones[i];stone.enabled=rubbleFrames!=null&&age>=0f&&age<.88f;
            if(!stone.enabled)continue;
            stone.sprite=rubbleFrames[Mathf.Clamp(Mathf.FloorToInt(age/.065f),0,3)];
            float seed=Seed(i+17),flight=Mathf.Clamp01(age/(.35f+seed*.22f));
            float angle=i*2.399963f+seed*.75f;
            Vector3 edge=GroundPoint(.5f+Mathf.Cos(angle)*.48f,.5f+Mathf.Sin(angle)*.48f),direction=edge-center;direction.y=0f;
            stone.transform.localPosition=edge-center+direction.normalized*(age*(3.2f+seed*4.5f))+
                Vector3.up*(.08f+Mathf.Sin(flight*Mathf.PI)*(1.8f+seed*2.4f));
            stone.transform.localRotation=facing*Quaternion.Euler(0f,0f,i*31f+flight*230f);
            stone.color=new Color(1f,1f,1f,1f-Ease(.65f,.88f,age));
        }
        for(int layer=0;layer<clouds.Length;layer++)PosePixels(clouds[layer],layer,age);
    }

    private void PosePixels(ParticleSystem system,int layer,float age)
    {
        int count=0;
        if(layer<2&&age>=0f&&age<.82f)
        {
            for(int i=0;i<8;i++)
            {
                float seed=Seed(i+layer*109),a=age-(i%3)*.004f,life=.30f+seed*.27f;
                if(a<0f||a>=life)continue;
                float u=a/life;
                Vector3 edge=GroundPoint(.04f+seed*.92f,layer==0?.04f:.96f),direction=edge-center;direction.y=0f;
                Vector3 p=edge-center+direction.normalized*(a*(4f+seed*5f))+
                    Vector3.up*(.05f+Mathf.Sin(u*Mathf.PI)*(.65f+seed*1.2f));
                Color color=Color.Lerp(terrain,new Color(.66f,.61f,.51f),seed*.45f);color.a=(1f-u)*.78f;
                Add(ref count,p,new Vector3(.12f+seed*.18f,.08f+seed*.10f,.12f+seed*.18f),color);
            }
        }
        if(layer==2&&age>=-.14f&&age<.13f)
        {
            float fall=Ease(-.14f,0f,age),core=1f-Ease(0f,.13f,age);
            for(int i=0;i<8;i++)
            {
                float seed=Seed(i+223);
                Vector3 p=GroundPoint(.12f+seed*.76f,.36f+Seed(i+229)*.28f)-center;
                p.y=age<0f?(1f-fall)*(3f+seed*6f):.12f+seed*.16f;
                Vector3 size=age<0f?new Vector3(.05f,.35f+fall*.55f,.05f):new Vector3(.25f+seed*.22f,.11f,.15f);
                Color color=Color.Lerp(terrain,new Color(.75f,.69f,.57f),.3f);color.a=age<0f?fall*.24f:core*.75f;
                Add(ref count,p,size,color);
            }
        }
        system.SetParticles(pixels,count);
    }

    private void Add(ref int count,Vector3 point,Vector3 size,Color color)
    {
        if(count>=pixels.Length)return;
        pixels[count++]=new ParticleSystem.Particle{position=point,startSize3D=size,startColor=color,rotation3D=new Vector3(0f,0f,count*37f),
            startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)count};
    }

    public void Cancel()
    {
        if(this==null||CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);ReleaseOwned();Remove(gameObject);
    }

    private void OnDestroy()=>ReleaseOwned();
    private void ReleaseOwned()
    {
        Remove(groundMesh);groundMesh=null;Remove(fissureMaterial);fissureMaterial=null;
        if(shadowFrames!=null)foreach(Sprite sprite in shadowFrames)Remove(sprite);shadowFrames=null;
    }
    private static float Seed(int index)=>Mathf.Repeat(index*.61803399f+.371f,1f);
    private static float Ease(float from,float to,float time){float t=Mathf.InverseLerp(from,to,time);return t*t*(3f-2f*t);}
    private static void Remove(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
}

