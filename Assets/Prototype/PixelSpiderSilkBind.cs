using System;
using UnityEngine;

public sealed class PixelSpiderSilkBind:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite frontSprite,backSprite,leftCocoon,rightCocoon,leftFuneral,rightFuneral,shellFragment;
    private static Material silkParticleMaterial;
    private SpriteRenderer source;
    private readonly SpriteRenderer[] strands=new SpriteRenderer[6];
    private readonly SpriteRenderer[] cocoon=new SpriteRenderer[2],anchors=new SpriteRenderer[4];
    private Func<bool> valid;
    private Color tint;
    private float progress,elapsed,release;
    private bool bound,stopping;
    private bool funeral,burst;
    private float pulseAge=1f,burstRadius;
    private ParticleSystem[] burstParticles;
    private ParticleSystem.Particle[] particleBuffer;
    private Bounds burstBounds;
    private Quaternion burstFacing;
    private Vector3 burstOrigin;
    private int burstLayer,burstOrder;
    public bool Bound=>bound;
    public bool Bursting=>burst;

    public static PixelSpiderSilkBind Play(SpriteRenderer source,Color color,Func<bool> valid)
    {
        if(source==null)return null;
        var fx=new GameObject("Spider Silk Cocoon").AddComponent<PixelSpiderSilkBind>();
        fx.source=source;fx.tint=color;fx.valid=valid;
        for(int i=0;i<6;i++)
        {
            var go=new GameObject(i%2==0?"Silk Front":"Silk Back");go.transform.SetParent(fx.transform,false);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=WrapSprite(i%2==0);fx.strands[i]=sr;
        }
        for(int i=0;i<2;i++)
        {
            var go=new GameObject(i==0?"Cocoon Left":"Cocoon Right");go.transform.SetParent(fx.transform,false);
            fx.cocoon[i]=go.AddComponent<SpriteRenderer>();fx.cocoon[i].sprite=CocoonSprite(i==0);
        }
        for(int i=0;i<4;i++)
        {
            var go=new GameObject("Taut Anchor Silk");go.transform.SetParent(fx.transform,false);
            fx.anchors[i]=go.AddComponent<SpriteRenderer>();fx.anchors[i].sprite=PixelSpiderCaptureNet.ThreadSprite;
        }
        fx.Animate(0f);return fx;
    }
    public void SetProgress(float value){if(this!=null&&!bound)progress=Mathf.Clamp01(value);}
    public static PixelSpiderSilkBind PlayFuneral(SpriteRenderer source,Color color,Func<bool> valid)
    {
        var fx=Play(source,color,valid);if(fx==null)return null;
        fx.funeral=true;fx.cocoon[0].sprite=CocoonSprite(true,true);fx.cocoon[1].sprite=CocoonSprite(false,true);
        fx.Tighten();fx.Animate(0f);return fx;
    }
    public void Compress(){if(this!=null)pulseAge=0f;}
    public void Burst(float radius=0f,Vector3? groundCenter=null)
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null||burst||source==null)return;
            burstBounds=source.bounds;burstFacing=Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
            burstOrigin=groundCenter??new Vector3(burstBounds.center.x,0f,burstBounds.center.z);
            burstLayer=source.sortingLayerID;burstOrder=source.sortingOrder;
            burst=true;burstRadius=radius;Stop();
            float body=Mathf.Clamp(burstBounds.size.y,.7f,2.8f);
            float reach=radius>0f?radius:body*.8f;
            if(silkParticleMaterial==null)silkParticleMaterial=new Material(Shader.Find("Sprites/Default"))
                {name="Pixel Silk Burst Particles",mainTexture=ShellFragment.texture,hideFlags=HideFlags.HideAndDontSave};
            burstParticles=new ParticleSystem[2];particleBuffer=new ParticleSystem.Particle[72];
            for(int half=0;half<2;half++)
            {
                var go=new GameObject(half==0?"Silk Burst Particles Back":"Silk Burst Particles Front");
                go.transform.SetParent(transform,false);
                var particles=go.AddComponent<ParticleSystem>();burstParticles[half]=particles;
                particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.loop=false;main.playOnAwake=false;main.duration=.32f;
                main.startLifetime=.32f;main.startSpeed=0f;main.maxParticles=72;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                var emission=particles.emission;emission.enabled=false;
                var shape=particles.shape;shape.enabled=false;
                var colors=particles.colorOverLifetime;colors.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f)},
                    new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(1f,.50f),new GradientAlphaKey(0f,1f)});
                colors.color=gradient;
                var size=particles.sizeOverLifetime;size.enabled=true;
                size.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0f,1.3f),
                    new Keyframe(.18f,1f),new Keyframe(.5f,1f),new Keyframe(1f,.2f)));
                var renderer=particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.sharedMaterial=silkParticleMaterial;
                renderer.sortingLayerID=burstLayer;renderer.sortingOrder=Mathf.Max(2,burstOrder+(half==0?-2:4));
                particles.Play(false);particles.Pause(false);CombatSkillClock.TrackParticle(particles);
                for(int i=0;i<24;i++)for(int tail=0;tail<3;tail++)
                {
                    float variation=.5f+.5f*Mathf.Sin((half*24+i+7)*91.173f);
                    float a=(half+Mathf.Repeat((i+.5f)*.618034f,1f))*Mathf.PI;
                    Vector3 ray=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a));
                    float flight=.07f+.025f*variation,speed=reach*.96f/flight;
                    float trail=tail==0?1f:(tail==1?.82f:.64f)+variation*.04f;
                    Color color=Color.Lerp(tint,Color.white,i%3==0?.65f:.15f);color.a=tail==0?1f:tail==1?.8f:.55f;
                    particles.Emit(new ParticleSystem.EmitParams
                    {
                        position=burstOrigin+ray*reach*.04f+Vector3.up*(.06f+body*.24f),
                        velocity=ray*speed*trail-Vector3.up*(body*.24f/flight),
                        startLifetime=.32f,startSize=(.19f+.075f*variation)*(tail==0?1f:tail==1?.65f:.4f),
                        startColor=color,rotation=i*137f+tail*41f,angularVelocity=(i%2==0?1f:-1f)*620f
                    },1);
                }
            }
        }
    }
    public void Tighten(){if(this==null)return;bound=true;progress=1f;elapsed=0f;}
    public void RevealClosed(){if(this==null)return;elapsed=Mathf.Max(elapsed,.24f);Animate(0f);}
    public void Stop(){if(this==null||stopping)return;stopping=true;release=0f;}
    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        gameObject.SetActive(false);
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Animate((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    private void Animate(float dt)
    {
        // A lethal burst must finish at the hit position even after the victim disappears.
        if(source==null&&!burst){Cancel();return;}
        if(!burst&&valid!=null&&!valid())Stop();
        elapsed+=Mathf.Max(0f,dt);
        pulseAge+=Mathf.Max(0f,dt);
        float releaseDuration=burst?.32f:.24f;
        if(stopping){release+=dt;if(release>=releaseDuration){Cancel();return;}}
        Bounds bounds=burst?burstBounds:source.bounds;
        int layer=burst?burstLayer:source.sortingLayerID,order=burst?burstOrder:source.sortingOrder;
        float body=Mathf.Clamp(bounds.size.y,.7f,2.8f);
        Quaternion camera=burst?burstFacing:Camera.main==null?source.transform.rotation:Camera.main.transform.rotation;
        Vector3 center=bounds.center;
        float pressure=0f;
        if(funeral&&!stopping)
        {
            // Fast crush, a held strain, then a slow recovery; the final crush stays loaded.
            pressure=pulseAge<.04f?Mathf.SmoothStep(0f,1f,pulseAge/.04f):
                pulseAge<.18f?1f:1f-Mathf.SmoothStep(0f,1f,(pulseAge-.18f)/.22f);
            pressure=Mathf.Max(pressure,Mathf.SmoothStep(0f,1f,(elapsed-1.30f)/.11f));
            float tremble=Mathf.Round(Mathf.Sin(elapsed*113f))*body*.018f*pressure;
            center+=camera*new Vector3(tremble,-body*.055f*pressure,0f);
        }
        Vector3 feet=center-camera*Vector3.up*body*.20f;
        float fade=stopping?1f-release/releaseDuration:1f;
        float shellFade=burst?1f-Mathf.Clamp01(release/.09f):fade;
        float wrap=bound?Mathf.SmoothStep(0f,1f,elapsed/(funeral?.24f:.14f)):0f;
        float pinch=bound?1f-Mathf.Sin(Mathf.Clamp01(elapsed/.14f)*Mathf.PI)*.18f:1.18f-.18f*progress;
        if(funeral)pinch*=1f-.40f*pressure;
        for(int i=0;i<6;i++)
        {
            int loop=i/2;var sr=strands[i];float appear=Mathf.Clamp01((funeral?Mathf.Clamp01(elapsed/.24f):progress)*3f-loop);
            sr.sortingLayerID=layer;sr.sortingOrder=order+(i%2==0?2:-2);
            sr.transform.rotation=camera*Quaternion.Euler(0,0,funeral?(i%2==0?12f:-12f):0f);
            sr.transform.position=feet+camera*Vector3.up*body*(loop*Mathf.Lerp(.035f,.15f,wrap))+
                camera*new Vector3(stopping?(i%2==0?1f:-1f)*release*.9f:0f,stopping?release*.25f:0f,0);
            sr.transform.localScale=new Vector3(body*Mathf.Lerp(.55f,funeral?1.05f:.85f,wrap)*pinch,body*.30f*(1f+pressure*.45f),1);
            Color color=bound?Color.Lerp(Color.white,tint,Mathf.Clamp01(elapsed/.13f)):tint;
            color.a=appear*(bound?.95f:.75f)*shellFade;sr.color=color;
        }
        for(int i=0;i<2;i++)
        {
            SpriteRenderer sr=cocoon[i];sr.enabled=bound;
            sr.sortingLayerID=layer;sr.sortingOrder=order+3;
            sr.transform.rotation=camera*Quaternion.Euler(0,0,burst?(i==0?1f:-1f)*release*360f:0f);
            sr.transform.position=center+camera*new Vector3(
                stopping?(i==0?-1f:1f)*release*body*(burst?7f:1.4f):0f,-body*.02f-release*body*.3f,0f);
            sr.transform.localScale=new Vector3(body*(funeral?1.05f:.90f)*pinch/.825f,
                body*(funeral?1.04f:.80f)*(1f-.12f*pressure)*Mathf.Lerp(.35f,1f,wrap)/1.025f,1f);
            Color color=Color.Lerp(Color.white,tint,Mathf.Clamp01(elapsed/.16f));color.a=wrap*shellFade*(1f+pressure*.65f);sr.color=color;
        }
        for(int i=0;i<4;i++)
        {
            SpriteRenderer sr=anchors[i];sr.enabled=bound;
            float side=i%2==0?-1f:1f;float y=i<2?-.04f:.18f;
            Vector3 from=center+camera*new Vector3(side*body*.24f*pinch,body*y,0);
            Vector3 to=bounds.center+camera*new Vector3(side*body*(i<2?.72f:.55f),-body*.48f,0);
            Vector3 local=Quaternion.Inverse(camera)*(to-from);
            sr.sortingLayerID=layer;sr.sortingOrder=order+(i<2?2:-1);
            sr.transform.position=(from+to)*.5f;
            sr.transform.rotation=camera*Quaternion.Euler(0,0,Mathf.Atan2(local.y,local.x)*Mathf.Rad2Deg);
            sr.transform.localScale=new Vector3(local.magnitude/.9f,body*.15f,1f);
            sr.color=new Color(tint.r,tint.g,tint.b,wrap*shellFade*.92f);
        }
        float radius=burstRadius>0f?burstRadius:body*.8f;
        if(burstParticles!=null)foreach(ParticleSystem particles in burstParticles)
        {
            // One clock for the cocoon and its native particles, including pause and tests.
            particles.Simulate(Mathf.Max(0f,dt),false,false,false);
            int count=particles.GetParticles(particleBuffer);
            for(int i=0;i<count;i++)
            {
                var particle=particleBuffer[i];Vector3 offset=particle.position-burstOrigin;
                Vector2 flat=new Vector2(offset.x,offset.z);
                if(flat.sqrMagnitude>=radius*radius)
                {
                    flat=flat.normalized*radius;offset.x=flat.x;offset.z=flat.y;
                    particle.velocity=new Vector3(0f,particle.velocity.y,0f);
                }
                if(offset.y<=.06f){offset.y=.06f;var velocity=particle.velocity;velocity.y=0f;particle.velocity=velocity;}
                particle.position=burstOrigin+offset;particleBuffer[i]=particle;
            }
            particles.SetParticles(particleBuffer,count);
        }
    }
    private static Sprite CocoonSprite(bool left,bool funeral=false)
    {
        Sprite cached=funeral?(left?leftFuneral:rightFuneral):(left?leftCocoon:rightCocoon);
        if(cached!=null)return cached;
        Sprite sprite=PixelNinjaSubstitution.MakeSprite(funeral?"Dense Funeral Cocoon":left?"Silk Cocoon Left":"Silk Cocoon Right",33,41,40f,(x,y)=>
        {
            if(left?x>16:x<=16)return Color.clear;
            float dx=(x-16f)/15f,dy=(y-20f)/19f;
            float edge=dx*dx+dy*dy;if(edge>1f)return Color.clear;
            bool band=(x+y*2)%13<(funeral?3:2)||(x-y*2+100)%15<2;
            bool rim=edge>.87f;
            return new Color(1f,1f,1f,band?.88f:rim?.70f:funeral?.24f:.13f);
        });
        if(funeral){if(left)leftFuneral=sprite;else rightFuneral=sprite;}
        else{if(left)leftCocoon=sprite;else rightCocoon=sprite;}
        return sprite;
    }
    private static Sprite ShellFragment=>shellFragment!=null?shellFragment:
        shellFragment=PixelNinjaSubstitution.MakeSprite("Torn Pixel Cocoon",11,9,10f,(x,y)=>
            x>1+y/3&&x<10-y/2&&y>0&&y<8?new Color(1f,1f,1f,(x+y)%3==0?.55f:1f):Color.clear);
    private static Sprite WrapSprite(bool front)
    {
        if(front?frontSprite!=null:backSprite!=null)return front?frontSprite:backSprite;
        Sprite sprite=PixelNinjaSubstitution.MakeSprite(front?"Silk Loop Front":"Silk Loop Back",33,17,32f,(x,y)=>
        {
            float dx=(x-16f)/15f;if(Mathf.Abs(dx)>1f)return Color.clear;
            float arc=Mathf.Sqrt(Mathf.Max(0f,1f-dx*dx))*5f;
            float line=8f+(front?-arc:arc);
            return Mathf.Abs(y-line)<.6f?Color.white:Color.clear;
        });
        if(front)frontSprite=sprite;else backSprite=sprite;return sprite;
    }
}
