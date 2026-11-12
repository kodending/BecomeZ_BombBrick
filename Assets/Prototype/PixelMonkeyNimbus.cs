using System.Collections.Generic;
using UnityEngine;

// Persistent puffs form the cloud; only its short wake and landing disperse.
[DefaultExecutionOrder(10020)] // Snapshot the rider after its animation and billboard pose.
public sealed class PixelMonkeyNimbus:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private ParticleSystem core,wake;
    private readonly ParticleSystem.Particle[] puffs=new ParticleSystem.Particle[11];
    private static Material material;
    private SpriteRenderer sorting;
    private readonly List<SpriteRenderer> afterimages=new List<SpriteRenderer>();
    private float trailClock,ghostClock,flightAge,speed;
    private bool stopped;

    public static PixelMonkeyNimbus Play(SpriteRenderer sorting)
    {
        var fx=new GameObject("Monkey Nimbus Cloud").AddComponent<PixelMonkeyNimbus>();
        fx.sorting=sorting;
        fx.core=fx.Particles("Nimbus Body",16);
        fx.wake=fx.Particles("Nimbus Wake",48);
        var overLife=fx.wake.sizeOverLifetime;overLife.enabled=true;
        overLife.size=new ParticleSystem.MinMaxCurve(1f,AnimationCurve.Linear(0f,1f,1f,0f));
        return fx;
    }

    private ParticleSystem Particles(string name,int count)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var particles=go.AddComponent<ParticleSystem>();
        particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.loop=false;main.playOnAwake=false;
        main.maxParticles=count;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startSpeed=0f;main.startLifetime=.42f;
        var emission=particles.emission;emission.enabled=false;
        var shape=particles.shape;shape.enabled=false;
        var renderer=particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=CloudMaterial();renderer.renderMode=ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID=sorting==null?0:sorting.sortingLayerID;
        renderer.sortingOrder=(sorting==null?0:sorting.sortingOrder)-1;
        particles.Play();CombatSkillClock.TrackParticle(particles);return particles;
    }

    public void Pose(Vector3 ground,float height,Vector3 velocity,float age)
    {
        if(stopped)return;
        flightAge=age;speed=velocity.magnitude;
        var camera=Camera.main;
        Vector3 right=camera==null?Vector3.right:camera.transform.right;
        Vector3 up=camera==null?Vector3.up:camera.transform.up;
        Vector3 feet=ground+Vector3.up*(height+.05f);
        transform.position=feet;
        float appear=Mathf.SmoothStep(0f,1f,age/.2f);
        float stretch=1f+Mathf.Min(.3f,speed*.015f);
        for(int i=0;i<puffs.Length;i++)
        {
            bool underside=i<4;
            int index=underside?i:i-4,count=underside?4:7;
            float x=(index/(float)(count-1)-.5f)*1.3f;
            float mound=1f-Mathf.Abs(index/(float)(count-1)*2f-1f);
            float bob=Mathf.Sin(age*3.4f+i*1.9f)*.025f;
            puffs[i]=new ParticleSystem.Particle
            {
                position=feet+right*x*stretch+up*((underside?-.2f:-.12f)+bob+mound*.045f),
                startSize=(underside?.46f:.4f+mound*.25f+Mathf.Sin(i*2.3f)*.045f)*appear,
                startColor=underside?new Color(.77f,.57f,.3f,.95f):new Color(1f,.94f,.74f,1f),
                startLifetime=10f,remainingLifetime=10f,randomSeed=(uint)(i+1)
            };
        }
        core.SetParticles(puffs,puffs.Length);
        if(sorting!=null)
        {
            core.GetComponent<ParticleSystemRenderer>().sortingOrder=sorting.sortingOrder-1;
            wake.GetComponent<ParticleSystemRenderer>().sortingOrder=sorting.sortingOrder-2;
        }
        if(speed>3f&&age-trailClock>=.035f)
        {
            trailClock=age;
            Emit(feet-velocity.normalized*.35f,-velocity*.08f+Vector3.up*.08f,.3f,.26f);
        }
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(stopped||speed<5f||flightAge<.12f||flightAge>4.8f||sorting==null||!sorting.enabled)return;
            if(flightAge-ghostClock<.055f)return;
            ghostClock=flightAge;
            afterimages.RemoveAll(ghost=>ghost==null);
            var echo=Rank3CombatFx.Ghost("Nimbus Rider Afterimage",sorting,sorting.transform.position,
                new Color(1f,.91f,.7f,.26f),1f,.18f);
            if(echo==null)return;
            echo.transform.rotation=sorting.transform.rotation;
            echo.flipY=sorting.flipY;echo.sortingOrder=sorting.sortingOrder-3;
            afterimages.Add(echo);
        }
    }

    private void Emit(Vector3 position,Vector3 velocity,float size,float duration)
    {
        wake.Emit(new ParticleSystem.EmitParams
        {
            position=position,velocity=velocity,startSize=size,startLifetime=duration,
            startColor=new Color(1f,.92f,.7f,.8f)
        },1);
    }

    public void Stop(bool landing)
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        if(stopped)return;stopped=true;
        foreach(var echo in afterimages)
            if(echo!=null){if(Application.isPlaying)Destroy(echo.gameObject);else DestroyImmediate(echo.gameObject);}
        afterimages.Clear();
        core.Clear();
        if(landing)
        {
            var camera=Camera.main;Vector3 right=camera==null?Vector3.right:camera.transform.right;
            for(int i=0;i<10;i++)
                Emit(transform.position+right*((i-4.5f)*.1f),right*((i-4.5f)*.65f)+Vector3.up*.12f,.3f,.45f);
        }
        if(Application.isPlaying)Destroy(gameObject,landing?.5f:0f);else DestroyImmediate(gameObject);
    }

    private static Material CloudMaterial()
    {
        if(material!=null)return material;
        string[] rows={"....HH....","..HHHHHH..",".HHHHHHHH.","HHHHHHHHHH","HHHHHHHHHH",".HSSSSSSH.","..SSSSSS..","....SS...."};
        var pixels=new Color32[80];
        for(int y=0;y<8;y++)for(int x=0;x<10;x++)
            pixels[(7-y)*10+x]=rows[y][x]=='H'?new Color32(255,255,255,255):
                rows[y][x]=='S'?new Color32(218,206,179,255):new Color32(0,0,0,0);
        var texture=new Texture2D(10,8,TextureFormat.RGBA32,false)
            {name="Pixel Nimbus Puff",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels32(pixels);texture.Apply(false,true);
        material=new Material(Shader.Find("Sprites/Default"))
            {mainTexture=texture,hideFlags=HideFlags.HideAndDontSave};
        return material;
    }
}
