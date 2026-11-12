using UnityEngine;
using BombBrick.Game.Core;
using System.Collections.Generic;

// The same local evolution accents follow the real monster and its existing card portrait.
[DefaultExecutionOrder(11000)] // Follow SpriteMonsterAnimationDriver's final LateUpdate pose.
public sealed class MonsterRankUpFeedback:MonoBehaviour
{
    public const float Duration=3.8f;
    private const int Count=40;
    private static Sprite[] lightningFrames,pulseFrames,sparkFrames;
    private static readonly Dictionary<int,MonsterRankUpFeedback> BodyOwners=new Dictionary<int,MonsterRankUpFeedback>();
    private readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[Count];
    private ParticleSystem system;
    private SpriteRenderer reference;
    private SpriteRenderer[] sprites;
    private SpriteRenderer bodyProxy,silhouetteProxy;
    private Material silhouetteMaterial;
    private MaterialPropertyBlock bodyProperties;
    private bool sourceWasRenderingOff,ownsBodyVisibility;
    private int bodyOwnerId;
    private float startingScale=1f;
    private Vector3 center;
    private float span,bodyHeight,elapsed;
    private int rank,soundMask;

    public float Elapsed=>elapsed;
    public SpriteRenderer BodyRenderer=>bodyProxy;
    public float StartingScale
    {
        get=>startingScale;
        set
        {
            startingScale=Mathf.Max(.01f,value);
            if(reference!=null&&reference.sprite!=null&&bodyProxy!=null&&enabled&&gameObject.activeInHierarchy)Pose();
        }
    }
    public static float DurationFor(int rank)=>MonsterEvolutionCelebration.DurationFor(rank);
    public static Color RankColor(int rank)=>rank>=4?new Color(1f,.74f,.24f):
        rank>=3?new Color(.8f,.45f,1f):new Color(.25f,.9f,1f);

    private static void PrepareFrames()
    {
        pulseFrames??=PixelCombatEffect.LoadFrames(PixelCombatEffect.KingKongEarthPulseResourcePath);
        sparkFrames??=PixelCombatEffect.LoadFrames(PixelCombatEffect.CriticalResourcePath);
        if(lightningFrames!=null)return;
        var texture=BombBrick.Game.Core.GameResourceCache.GetTexture(MonsterEvolutionCelebration.LightningResource);
        if(texture==null)return;
        lightningFrames=new Sprite[8];
        int width=texture.width/4,height=texture.height/2;
        for(int i=0;i<8;i++)
        {
            lightningFrames[i]=Sprite.Create(texture,new Rect(i%4*width,(1-i/4)*height,width,height),
                new Vector2(.5f,0f),height);
            lightningFrames[i].name="Evolution Lightning "+i;
            lightningFrames[i].hideFlags=HideFlags.HideAndDontSave;
        }
    }

    public static MonsterRankUpFeedback Play(SpriteRenderer source,int rank)
    {
        if(rank<2||rank>4||source==null||source.sprite==null||!source.enabled||!source.gameObject.activeInHierarchy)return null;
        if(BodyOwners.TryGetValue(source.GetInstanceID(),out var previous)&&previous!=null)previous.Finish();
        PrepareFrames();
        var ps=PixelMushroomGas.CreateParticles("Monster Evolution Burst",null,Count,
            PixelMushroomGas.Material(true),source,25,true);
        if(source.gameObject.scene.IsValid()&&ps.gameObject.scene!=source.gameObject.scene)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ps.gameObject,source.gameObject.scene);
        var effect=ps.gameObject.AddComponent<MonsterRankUpFeedback>();
        effect.system=ps;effect.reference=source;effect.rank=rank;
        effect.sprites=new SpriteRenderer[7];
        for(int i=0;i<effect.sprites.Length;i++)
        {
            var visual=new GameObject(i==0?"Evolution Shockwave":i<4?"Evolution Lightning":"Evolution Star Burst");
            visual.transform.SetParent(effect.transform,false);
            var renderer=visual.AddComponent<SpriteRenderer>();
            renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder+26+i;
            renderer.enabled=false;effect.sprites[i]=renderer;
        }
        effect.CreateBodyProxy();
        var main=ps.main;main.useUnscaledTime=true;
        effect.Pose();PlaySounds(rank,0f,ref effect.soundMask);return effect;
    }

    private void LateUpdate()=>Advance(Time.unscaledDeltaTime);
    private void Advance(float deltaTime)
    {
        elapsed+=Mathf.Max(0f,deltaTime);
        if(elapsed>=DurationFor(rank)||reference==null||reference.sprite==null||
            !reference.enabled||!reference.gameObject.activeInHierarchy)
        {
            Finish();
            return;
        }
        Pose();PlaySounds(rank,elapsed,ref soundMask);
    }

    private void Pose()
    {
        Bounds body=PixelMantisReaper.VisibleBody(reference.sprite);
        center=CombatReadabilityPresentation.VisibleBodyCenter(reference);
        float width=reference.transform.TransformVector(Vector3.right*body.size.x).magnitude;
        bodyHeight=reference.transform.TransformVector(Vector3.up*body.size.y).magnitude;
        span=Mathf.Max(width,bodyHeight)*.65f;
        float bodyScale=SyncBodyProxy(body);
        center=CombatReadabilityPresentation.VisibleBodyCenter(bodyProxy);
        bodyHeight*=bodyScale;span*=bodyScale;
        Camera camera=Camera.main;
        Vector3 right=camera==null?Vector3.right:camera.transform.right;
        Vector3 up=camera==null?Vector3.up:camera.transform.up;
        Quaternion rotation=camera==null?Quaternion.identity:camera.transform.rotation;
        for(int i=0;i<sprites.Length;i++)
        {sprites[i].sortingLayerID=reference.sortingLayerID;sprites[i].sortingOrder=reference.sortingOrder+26+i;}
        var particleRenderer=system.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sortingLayerID=reference.sortingLayerID;particleRenderer.sortingOrder=reference.sortingOrder+25;
        int count=0;
        for(int i=0;i<Count;i++)
        {
            if(!Sample(i,elapsed,rank,out Vector2 offset,out float size,out Color color))continue;
            particles[count++]=new ParticleSystem.Particle
            {
                position=center+(right*offset.x+up*offset.y)*span,
                startSize3D=Vector3.one*size*span,startColor=CombatFlashPresentation.Flash(color),
                startLifetime=10f,remainingLifetime=10f
            };
        }
        system.SetParticles(particles,count);
        Vector3 ground=center-up*bodyHeight*.34f;
        float age=elapsed-MonsterEvolutionCelebration.ImpactAt(rank);
        float power=rank>=4?1.8f:rank==3?1.4f:1.15f;
        SetSprite(sprites[0],Frame(pulseFrames,age,24f),ground,
            span*2.6f*power,span*1.35f*power,rotation,Color.white);
        float boltHeight=Mathf.Max(bodyHeight,span)*(rank>=4?4.3f:3.1f);
        for(int i=0;i<3;i++)
        {
            float delay=i==0?0f:i==1?.045f:.075f;
            int frame=MonsterEvolutionCelebration.LightningFrame(rank,elapsed-delay);
            Sprite sprite=frame<0||lightningFrames==null||i>0&&rank<4?null:lightningFrames[frame];
            Vector3 foot=ground+right*(i==0?0f:i==1?-span*.65f:span*.65f);
            SetSprite(sprites[i+1],sprite,foot+up*boltHeight*.5f,
                boltHeight*.5f,boltHeight,rotation,RankColor(rank));
        }
        for(int i=0;i<3;i++)
        {
            float t=age-i*.035f;
            Vector3 point=center+right*((i-1)*span*.55f)+up*(i%2==0?.25f:-.15f)*span;
            SetSprite(sprites[4+i],Frame(sparkFrames,t,22f),point,
                span*.6f*power,span*.6f*power,rotation,Color.white);
        }
    }

    private void CreateBodyProxy()
    {
        bodyProperties=new MaterialPropertyBlock();
        bodyProxy=new GameObject("Evolution Body Proxy").AddComponent<SpriteRenderer>();
        bodyProxy.transform.SetParent(transform,false);bodyProxy.gameObject.layer=reference.gameObject.layer;
        var shader=Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette");
        if(shader!=null)
        {
            silhouetteMaterial=new Material(shader){name="Evolution Energy Silhouette",hideFlags=HideFlags.HideAndDontSave};
            silhouetteProxy=new GameObject("Evolution Energy Silhouette").AddComponent<SpriteRenderer>();
            silhouetteProxy.transform.SetParent(transform,false);silhouetteProxy.gameObject.layer=reference.gameObject.layer;
            silhouetteProxy.sharedMaterial=silhouetteMaterial;
        }
        ClaimBodyVisibility();
    }

    private void ClaimBodyVisibility()
    {
        if(reference==null||ownsBodyVisibility)return;
        bodyOwnerId=reference.GetInstanceID();
        if(BodyOwners.TryGetValue(bodyOwnerId,out var owner)&&owner!=null&&owner!=this)return;
        sourceWasRenderingOff=reference.forceRenderingOff;ownsBodyVisibility=true;BodyOwners[bodyOwnerId]=this;
    }

    private float SyncBodyProxy(Bounds body)
    {
        float scale=Mathf.Lerp(startingScale,1f,MonsterEvolutionCelebration.GrowthProgress(rank,elapsed))*
            MonsterEvolutionCelebration.BodyScale(rank,elapsed);
        // Grow from the visible feet; transparent canvas margins and sprite pivots cannot lift the body.
        Vector3 foot=new Vector3(reference.flipX?-body.center.x:body.center.x,
            reference.flipY?-body.max.y:body.min.y,0f);
        Vector3 fixedFoot=reference.transform.TransformPoint(foot);
        Vector3 position=fixedFoot-reference.transform.TransformVector(foot)*scale;
        bool visible=ownsBodyVisibility&&!sourceWasRenderingOff;
        bodyProxy.sprite=reference.sprite;bodyProxy.flipX=reference.flipX;bodyProxy.flipY=reference.flipY;
        bodyProxy.sharedMaterial=reference.sharedMaterial;bodyProxy.color=reference.color;
        reference.GetPropertyBlock(bodyProperties);bodyProxy.SetPropertyBlock(bodyProperties);
        bodyProxy.sortingLayerID=reference.sortingLayerID;bodyProxy.sortingOrder=reference.sortingOrder+23;
        bodyProxy.transform.SetPositionAndRotation(position,reference.transform.rotation);
        bodyProxy.transform.localScale=reference.transform.lossyScale*scale;bodyProxy.enabled=visible;
        if(visible)reference.forceRenderingOff=true;
        if(silhouetteProxy!=null)
        {
            silhouetteProxy.sprite=reference.sprite;silhouetteProxy.flipX=reference.flipX;silhouetteProxy.flipY=reference.flipY;
            silhouetteProxy.sortingLayerID=reference.sortingLayerID;silhouetteProxy.sortingOrder=reference.sortingOrder+24;
            silhouetteProxy.transform.SetPositionAndRotation(position,reference.transform.rotation);
            silhouetteProxy.transform.localScale=reference.transform.lossyScale*scale;
            float alpha=MonsterEvolutionCelebration.SilhouetteAlpha(rank,elapsed);
            Color energy=RankColor(rank);
            float hit=elapsed-MonsterEvolutionCelebration.ImpactAt(rank);
            if(hit>=0f)energy=Color.Lerp(Color.white,energy,Mathf.Clamp01(hit/.12f));
            energy=CombatFlashPresentation.Peak(energy);energy.a=alpha;
            silhouetteProxy.color=energy;silhouetteProxy.enabled=visible&&alpha>.001f;
        }
        return scale;
    }

    public void Cancel()
    {
        if(this==null)return;
        Finish();
    }

    private void Finish()
    {
        OnDisable();ReleaseOwnedMaterial();
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }

    private void OnEnable()
    {if(reference!=null&&bodyProxy!=null)ClaimBodyVisibility();}

    private void OnDisable()
    {
        if(ownsBodyVisibility)
        {
            if(BodyOwners.TryGetValue(bodyOwnerId,out var owner)&&owner==this)
            {
                if(reference!=null)reference.forceRenderingOff=sourceWasRenderingOff;
                BodyOwners.Remove(bodyOwnerId);
            }
            ownsBodyVisibility=false;
        }
        if(bodyProxy!=null)bodyProxy.enabled=false;
        if(silhouetteProxy!=null)silhouetteProxy.enabled=false;
        if(sprites!=null)foreach(var renderer in sprites)if(renderer!=null)renderer.enabled=false;
        if(system!=null)system.Clear();
    }

    private void OnDestroy()
    {
        OnDisable();ReleaseOwnedMaterial();
    }

    private void ReleaseOwnedMaterial()
    {
        if(silhouetteMaterial==null)return;
        var owned=silhouetteMaterial;silhouetteMaterial=null;
        if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);
    }

    private static Sprite Frame(Sprite[] frames,float age,float fps)
    {
        if(frames==null||frames.Length<4||age<0f)return null;
        int frame=Mathf.FloorToInt(age*fps);
        return frame<frames.Length?frames[frame]:null;
    }

    private static void SetSprite(SpriteRenderer renderer,Sprite sprite,Vector3 point,
        float width,float height,Quaternion rotation,Color tint)
    {
        renderer.enabled=sprite!=null;
        if(sprite==null)return;
        renderer.sprite=sprite;renderer.color=CombatFlashPresentation.Flash(tint);
        renderer.transform.rotation=rotation;
        var scale=new Vector3(width/sprite.bounds.size.x,height/sprite.bounds.size.y,1f);
        renderer.transform.localScale=scale;
        // Position the image's visible rectangle, including the bolt's bottom pivot, at this center.
        renderer.transform.position=point-rotation*Vector3.Scale(sprite.bounds.center,scale);
    }

    public static bool Sample(int index,float age,int rank,out Vector2 offset,out float size,out Color color)
    {
        offset=Vector2.zero;size=0f;color=RankColor(rank);
        if(index<0||index>=Count||rank<2||rank>4||age<0f||age>=DurationFor(rank))return false;
        float strength=rank==2?1.1f:rank==3?1.4f:1.85f;
        float impact=MonsterEvolutionCelebration.ImpactAt(rank),hitAge=age-impact;
        float angle=index*2.399963f,radius,alpha;
        float seed=Mathf.Repeat(index*.618034f,1f);
        if(hitAge<0f)
        {
            float t=MonsterEvolutionCelebration.ChargeProgress(rank,age);
            radius=Mathf.Lerp(1.15f+seed*.5f,.10f,t*t);
            size=.035f+seed*.03f+t*.025f;
            alpha=(.2f+t*.55f)*(.7f+.3f*Mathf.Cos(t*Mathf.PI*6f+index));
            color=Color.Lerp(color,Color.white,t*.6f);
        }
        else if(index<8)
        {
            float t=Mathf.Clamp01(hitAge/.12f);
            angle=index*Mathf.PI*.25f;radius=.04f+t*.42f;
            size=(index%2==0?.30f:.20f)*(1f-t);
            color=Color.Lerp(Color.white,color,t);alpha=1f-t;
        }
        else if(index<24)
        {
            float t=Mathf.Clamp01(hitAge/.38f);
            angle=(index-8)*Mathf.PI*.125f;
            radius=.15f+1.3f*(1f-Mathf.Pow(1f-t,3f));size=Mathf.Lerp(.075f,.03f,t);
            alpha=.9f*(1f-Mathf.SmoothStep(0f,1f,t));
        }
        else
        {
            float t=hitAge/(DurationFor(rank)-impact);
            radius=.18f+(1f-Mathf.Exp(-5f*t))*(.8f+seed*.45f);
            size=(.035f+seed*.03f)*(1f-t*.5f);
            color=Color.Lerp(color,Color.white,seed*.5f);
            alpha=1f-Mathf.SmoothStep(0f,1f,(t-.35f)/.65f);
        }
        offset=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius*strength;
        if(index>=24&&hitAge>=0f)offset.y+=hitAge*.18f;
        size*=strength;color.a=alpha;
        return size>0f&&alpha>.001f;
    }

    public static void Draw(Rect portrait,int rank,float age)
    {
        if(Event.current==null||Event.current.type!=EventType.Repaint||rank<2||rank>4||
            age<0f||age>=DurationFor(rank)||portrait.width<=0f||portrait.height<=0f)return;
        PrepareFrames();
        Color previous=GUI.color;
        Matrix4x4 matrix=GUI.matrix;
        float span=Mathf.Max(portrait.width,portrait.height)*.65f;
        try
        {
            float power=rank>=4?1.8f:rank==3?1.4f:1.15f;
            float hitAge=age-MonsterEvolutionCelebration.ImpactAt(rank);
            var ground=new Vector2(portrait.center.x,portrait.center.y+portrait.height*.34f);
            DrawFrame(Frame(pulseFrames,hitAge,24f),new Rect(ground.x-span*1.3f*power,
                ground.y-span*.675f*power,span*2.6f*power,span*1.35f*power),Color.white);
            float height=Mathf.Max(portrait.height,span)*(rank>=4?4.3f:3.1f);
            for(int i=0;i<3;i++)
            {
                if(i>0&&rank<4)break;
                int frame=MonsterEvolutionCelebration.LightningFrame(rank,age-(i==0?0f:i==1?.045f:.075f));
                if(frame<0||lightningFrames==null)continue;
                float x=ground.x+(i==0?0f:i==1?-span*.65f:span*.65f);
                DrawFrame(lightningFrames[frame],new Rect(x-height*.25f,
                    ground.y-height,height*.5f,height),RankColor(rank));
            }
            for(int i=0;i<3;i++)
            {
                Vector2 point=portrait.center+new Vector2((i-1)*span*.55f,(i%2==0?-.25f:.15f)*span);
                DrawFrame(Frame(sparkFrames,hitAge-i*.035f,22f),new Rect(point.x-span*.3f*power,
                    point.y-span*.3f*power,span*.6f*power,span*.6f*power),Color.white);
            }
            for(int i=0;i<Count;i++)
            {
                if(!Sample(i,age,rank,out Vector2 offset,out float size,out Color color))continue;
                Vector2 center=portrait.center+new Vector2(offset.x,-offset.y)*span;
                float pixels=Mathf.Max(2f,Mathf.Round(size*span));GUI.color=CombatFlashPresentation.Flash(color);
                GUI.DrawTexture(new Rect(Mathf.Round(center.x-pixels*.5f),Mathf.Round(center.y-pixels*.5f),pixels,pixels),Texture2D.whiteTexture);
            }
        }
        finally {GUI.color=previous;GUI.matrix=matrix;}
    }

    private static void DrawFrame(Sprite sprite,Rect rect,Color tint)
    {
        if(sprite==null)return;
        Rect uv=sprite.textureRect;
        GUI.color=CombatFlashPresentation.Flash(tint);
        GUI.DrawTextureWithTexCoords(rect,sprite.texture,new Rect(uv.x/sprite.texture.width,
            uv.y/sprite.texture.height,uv.width/sprite.texture.width,uv.height/sprite.texture.height),true);
    }

    public static void PlaySounds(int rank,float age,ref int soundMask)
    {
        if(rank<2||rank>4||age<0f||age>=DurationFor(rank))return;
        if((soundMask&1)==0)
        {soundMask|=1;GameAudio.PlayEvolution(GameAudio.EvolutionCue.Preparation,rank);}
        if(age>=MonsterEvolutionCelebration.ImpactAt(rank)&&(soundMask&2)==0)
        {soundMask|=2;GameAudio.PlayEvolution(GameAudio.EvolutionCue.Impact,rank);}
        if(age>=MonsterEvolutionCelebration.ImpactAt(rank)&&(soundMask&4)==0)
        {soundMask|=4;GameAudio.PlayEvolution(GameAudio.EvolutionCue.RankAccent,rank);}
        if(age>=MonsterEvolutionCelebration.StampAt(rank)&&(soundMask&8)==0)
        {soundMask|=8;GameAudio.PlayEvolution(GameAudio.EvolutionCue.Stamp,rank);}
        if(age>=MonsterEvolutionCelebration.StampAt(rank)+.20f&&(soundMask&16)==0)
        {soundMask|=16;GameAudio.PlayEvolution(GameAudio.EvolutionCue.Afterglow,rank);}
    }
}
