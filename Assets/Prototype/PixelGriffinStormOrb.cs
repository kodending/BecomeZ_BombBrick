using System.Collections.Generic;
using UnityEngine;

// Rendering only; the battle owns capture, return targets, time and damage.
[DefaultExecutionOrder(10040)]
public sealed class PixelGriffinStormOrb:MonoBehaviour
{
    public const string ResourceRoot="CombatCinematics/GriffinStormOrbV1/";
    public const float ViewportHeight=.11f;
    private static Sprite[] orbFrames,burstFrames;
    private SpriteRenderer core,burst;
    private ParticleSystem motes;
    private readonly ParticleSystem.Particle[] pixels=new ParticleSystem.Particle[4096];
    private readonly Dictionary<int,Ammo> shots=new Dictionary<int,Ammo>();
    private Camera view;
    private Bounds arena;
    private bool allied,hidden,entranceComplete;
    private float age,worldHeight;
    private Vector3 anchor;
    private Quaternion facing;
    private const int SpiralSegments=64;
    private readonly MeshRenderer[] spirals=new MeshRenderer[6];
    private readonly Mesh[] spiralMeshes=new Mesh[6];
    private readonly Vector3[][] spiralVertices=new Vector3[6][];
    private readonly Color[][] spiralColors=new Color[6][];
    private Material spiralMaterial;
    public Vector3 AnchorPosition=>anchor;
    public float WorldHeight=>worldHeight;
    private sealed class Ammo
    {
        public SpriteRenderer body;
        public Vector3 contact,originalScale,orbitScale,position;
        public float captured;
        public bool released,basic;
        public readonly Vector3[] trail=new Vector3[7];
        public int samples;
        public Color color;
    }

    public static Vector2 ViewportAnchor(bool allied)=>new Vector2(allied?.55f:.76f,.85f);
    public static Vector3 WorldAnchor(Camera camera,Bounds area,bool allied)
    {
        if(camera==null)return area.center+Vector3.up*8f;
        float depth=Mathf.Max(camera.nearClipPlane+2f,camera.WorldToViewportPoint(area.center).z);
        Vector2 point=ViewportAnchor(allied);return camera.ViewportToWorldPoint(new Vector3(point.x,point.y,depth));
    }
    private static Sprite[] Load(string name)
    {
        Texture2D texture=Resources.Load<Texture2D>(ResourceRoot+name);
        if(texture==null)return System.Array.Empty<Sprite>();
        var frames=new Sprite[8];float width=texture.width/4f,height=texture.height/2f;
        for(int i=0;i<frames.Length;i++)
        {
            frames[i]=Sprite.Create(texture,new Rect(i%4*width,(1-i/4)*height,width,height),Vector2.one*.5f,height,0,SpriteMeshType.FullRect);
            frames[i].name=ResourceRoot+name+" "+i;frames[i].hideFlags=HideFlags.HideAndDontSave;
        }
        return frames;
    }
    public static PixelGriffinStormOrb Create(SpriteRenderer reference,Bounds arena,bool allied)
    {
        if(orbFrames==null||orbFrames.Length!=8||orbFrames[0]==null)orbFrames=Load("Orb8");
        if(burstFrames==null||burstFrames.Length!=8||burstFrames[0]==null)burstFrames=Load("Burst8");
        var fx=new GameObject("Griffin Storm Orb").AddComponent<PixelGriffinStormOrb>();
        fx.view=Camera.main;fx.arena=arena;fx.allied=allied;
        fx.core=fx.MakeSprite("Eight drawing spherical wind",reference,20000);
        fx.burst=fx.MakeSprite("Eight drawing wind release",reference,20005);
        fx.CreateSpirals(reference);
        fx.motes=PixelMushroomGas.CreateParticles("Orb wind, intake and return pixels",fx.transform,fx.pixels.Length,
            PixelMushroomGas.Material(true),reference,0,true);
        fx.motes.GetComponent<ParticleSystemRenderer>().sortingOrder=20001;
        fx.Pose(0f);return fx;
    }
    private SpriteRenderer MakeSprite(string name,SpriteRenderer reference,int order)
    {
        var child=new GameObject(name);child.transform.SetParent(transform,false);var renderer=child.AddComponent<SpriteRenderer>();
        if(reference!=null)renderer.sortingLayerID=reference.sortingLayerID;
        renderer.sortingOrder=order;return renderer;
    }
    private static float Seed(int index)=>Mathf.Repeat(Mathf.Sin(index*127.1f+311.7f)*43758.5453f,1f);
    private void CreateSpirals(SpriteRenderer reference)
    {
        spiralMaterial=new Material(Shader.Find("Sprites/Default")){hideFlags=HideFlags.DontSave};
        spiralMaterial.mainTexture=Texture2D.whiteTexture;
        var triangles=new int[SpiralSegments*6];
        for(int i=0;i<SpiralSegments;i++)
        {int v=i*2,k=i*6;triangles[k]=v;triangles[k+1]=v+1;triangles[k+2]=v+2;triangles[k+3]=v+1;triangles[k+4]=v+3;triangles[k+5]=v+2;}
        for(int i=0;i<spirals.Length;i++)
        {
            var child=new GameObject("Spiral wind "+i/2+(i%2==0?" behind":" in front"));child.transform.SetParent(transform,false);
            spiralVertices[i]=new Vector3[(SpiralSegments+1)*2];spiralColors[i]=new Color[(SpiralSegments+1)*2];
            var mesh=new Mesh{name=child.name,hideFlags=HideFlags.DontSave};mesh.vertices=spiralVertices[i];mesh.triangles=triangles;
            child.AddComponent<MeshFilter>().sharedMesh=mesh;spiralMeshes[i]=mesh;
            var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=spiralMaterial;
            renderer.sortingLayerID=reference==null?0:reference.sortingLayerID;renderer.sortingOrder=i%2==0?19998:20003;spirals[i]=renderer;
        }
    }
    private void DrawSpirals(float time,float size,bool visible)
    {
        float fade=Mathf.Clamp01(time/.25f);
        for(int i=0;i<spirals.Length;i++)
        {
            var renderer=spirals[i];renderer.enabled=visible;renderer.forceRenderingOff=hidden;if(!visible)continue;
            int band=i/2;Quaternion tilt=Quaternion.Euler(28f+band*47f,band*51f,band*63f+time*12f);
            for(int step=0;step<=SpiralSegments;step++)
            {
                float t=step/(float)SpiralSegments,latitude=Mathf.Lerp(-1.16f,1.16f,t),angle=t*Mathf.PI*3f-time*1.55f+band*2.1f;
                Vector3 direction=tilt*new Vector3(Mathf.Cos(latitude)*Mathf.Cos(angle),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Sin(angle));
                Vector3 point=direction*size*.53f;
                Vector3 edge=Vector3.Cross(direction,tilt*Vector3.up).normalized*size*.022f*Mathf.Sin(t*Mathf.PI);
                float side=i%2==0?Mathf.Clamp01(direction.z*18f):Mathf.Clamp01(-direction.z*18f);
                Color color=Color.Lerp(new Color(.50f,.94f,.89f),new Color(1f,.88f,.49f),band*.45f);
                color*=Mathf.Lerp(1f,.58f,Mathf.Clamp01(direction.z));color.a=side*Mathf.Sin(t*Mathf.PI)*fade*.76f;
                int v=step*2;spiralVertices[i][v]=point-edge;spiralVertices[i][v+1]=point+edge;spiralColors[i][v]=spiralColors[i][v+1]=color;
            }
            renderer.transform.SetPositionAndRotation(anchor,facing);spiralMeshes[i].vertices=spiralVertices[i];spiralMeshes[i].colors=spiralColors[i];spiralMeshes[i].RecalculateBounds();
        }
    }
    private void Locate()
    {
        if(view==null)view=Camera.main;
        anchor=WorldAnchor(view,arena,allied);facing=view==null?Quaternion.identity:view.transform.rotation;
        if(view==null){worldHeight=2f;return;}
        float depth=Mathf.Max(view.nearClipPlane+2f,view.WorldToViewportPoint(arena.center).z);
        worldHeight=Vector3.Distance(view.ViewportToWorldPoint(new Vector3(.5f,0f,depth)),
            view.ViewportToWorldPoint(new Vector3(.5f,1f,depth)))*ViewportHeight;
    }
    private Vector3 Orbit(float time,int id)
    {
        float phase=time*(1.6f+Seed(id)*.35f)+id*2.399963f;
        float contraction=Mathf.Lerp(1f,.52f,Mathf.SmoothStep(0f,1f,
            Mathf.InverseLerp(GriffinStormOrbRules.AbsorbDuration,GriffinStormOrbRules.Burst,time)));
        Quaternion plane=Quaternion.Euler(18f+(id%3)*17f,-35f+(id%4)*22f,(id%3)*55f);
        return plane*new Vector3(Mathf.Cos(phase)*1.08f,Mathf.Sin(phase)*.68f,0f)*contraction;
    }
    public Vector3 ShotPosition(int id)=>shots.TryGetValue(id,out Ammo shot)?shot.position:anchor;
    public Vector3 ShotTangent(int id)=>facing*(Orbit(age+.01f,id)-Orbit(age,id)).normalized;
    public void Store(int id,SpriteRenderer original,Vector3 contact,float time)
    {
        if(shots.ContainsKey(id))return;
        var shot=new Ammo{body=MakeSprite("Captured projectile "+id,core,20002),contact=contact,captured=time,basic=original==null};
        shot.body.sprite=original!=null&&original.sprite!=null?original.sprite:orbFrames.Length==8?orbFrames[0]:null;
        shot.color=original==null?new Color(.83f,1f,1f):original.color;
        shot.body.color=shot.color;shot.body.flipX=original!=null&&original.flipX;shot.body.flipY=original!=null&&original.flipY;
        shot.originalScale=original==null?Vector3.one*worldHeight*.15f:original.transform.lossyScale;
        Bounds bounds=shot.body.sprite==null?new Bounds(Vector3.zero,Vector3.one):PixelMantisReaper.VisibleBody(shot.body.sprite);
        float extent=Mathf.Max(bounds.size.x*Mathf.Abs(shot.originalScale.x),bounds.size.y*Mathf.Abs(shot.originalScale.y));
        shot.orbitScale=shot.originalScale*(Mathf.Clamp(extent,worldHeight*.12f,worldHeight*.24f)/Mathf.Max(.001f,extent));
        shot.position=contact;shots.Add(id,shot);Draw(age,false);
    }
    public void Pose(float time){age=Mathf.Max(0f,time);Draw(age,true);}
    public void PoseReleased(int id,Vector3 position,Vector3 velocity,float progress)
    {
        if(!shots.TryGetValue(id,out Ammo shot))return;
        shot.released=true;shot.position=position;shot.body.transform.position=position;shot.body.transform.localScale=shot.orbitScale;
        Vector3 local=Quaternion.Inverse(facing)*velocity;
        float angle=local.sqrMagnitude>.000001f?Mathf.Atan2(local.y,local.x)*Mathf.Rad2Deg:0f;
        shot.body.transform.rotation=facing*Quaternion.Euler(0f,0f,angle);shot.body.sortingOrder=20004;
        shot.body.color=shot.color;
        shot.body.forceRenderingOff=hidden;Remember(shot,position);
    }
    public void RemoveShot(int id)
    {
        if(!shots.TryGetValue(id,out Ammo shot))return;
        if(Application.isPlaying)Destroy(shot.body.gameObject);else DestroyImmediate(shot.body.gameObject);
        shots.Remove(id);
    }
    public void TintShot(int id,Color color)
    {if(shots.TryGetValue(id,out Ammo shot)){shot.color=color;shot.body.color=color;}}
    public void SetCinematicHidden(bool value){hidden=value;Draw(age,false);}
    public void CompleteCinematicEntrance(){entranceComplete=true;age=0f;Draw(0f,false);}
    private void LateUpdate()=>Draw(age,false);
    private static void Remember(Ammo shot,Vector3 point)
    {
        if(shot.samples>0&&(shot.trail[0]-point).sqrMagnitude<.000001f)return;
        for(int i=shot.trail.Length-1;i>0;i--)shot.trail[i]=shot.trail[i-1];
        shot.trail[0]=point;shot.samples=Mathf.Min(shot.samples+1,shot.trail.Length);
    }
    private void Draw(float time,bool record)
    {
        if(core==null||motes==null)return;Locate();int count=0;
        int frame=Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(time,.6f)/.6f*8f),0,7);
        float contraction=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(GriffinStormOrbRules.AbsorbDuration,GriffinStormOrbRules.Burst,time));
        float entrance=entranceComplete?1f:Mathf.SmoothStep(.45f,1f,Mathf.Clamp01(time/.3f));
        float growth=1f+Mathf.Min(.12f,Mathf.Max(0,shots.Count-GriffinStormOrbRules.BasicShots)*.004f);
        core.enabled=orbFrames.Length==8&&time<GriffinStormOrbRules.Burst;core.forceRenderingOff=hidden;
        if(core.enabled)
        {
            core.sprite=orbFrames[frame];core.transform.rotation=facing*Quaternion.Euler(Mathf.Sin(time*.9f)*8f,Mathf.Sin(time*1.1f)*12f,time*18f);
            core.transform.localScale=Vector3.one*(worldHeight/Mathf.Max(.001f,PixelMantisReaper.VisibleBody(orbFrames[0]).size.y)*
                Mathf.Lerp(1f,.35f,contraction)*entrance*growth);
            core.transform.position=anchor-core.transform.rotation*Vector3.Scale(PixelMantisReaper.VisibleBody(orbFrames[0]).center,core.transform.localScale);
            core.color=allied?Color.white:new Color(1f,.90f,.78f);
        }
        DrawSpirals(time,worldHeight*Mathf.Lerp(1f,.35f,contraction)*entrance*growth,core.enabled);
        float burstAge=time-GriffinStormOrbRules.Burst;
        burst.enabled=burstFrames.Length==8&&burstAge>=0f&&burstAge<.48f;burst.forceRenderingOff=hidden;
        if(burst.enabled)
        {
            burst.sprite=burstFrames[Mathf.Clamp(Mathf.FloorToInt(burstAge/.48f*8f),0,7)];
            burst.transform.rotation=facing;
            burst.transform.localScale=Vector3.one*(worldHeight*1.75f/Mathf.Max(.001f,PixelMantisReaper.VisibleBody(burstFrames[2]).size.y));
            burst.transform.position=anchor-facing*Vector3.Scale(PixelMantisReaper.VisibleBody(burstFrames[2]).center,burst.transform.localScale);
        }
        foreach(var pair in shots)
        {
            int id=pair.Key;Ammo shot=pair.Value;shot.body.forceRenderingOff=hidden;
            if(!shot.released)
            {
                Vector3 local=Orbit(time,id);Vector3 target=anchor+facing*local*worldHeight;
                float progress=shot.basic?1f:Mathf.SmoothStep(0f,1f,Mathf.Clamp01((time-shot.captured)/.30f));
                shot.position=Vector3.Lerp(shot.contact,target,progress)+facing*Vector3.up*(Mathf.Sin(progress*Mathf.PI)*worldHeight*.6f);
                shot.body.transform.position=shot.position;
                shot.body.transform.rotation=facing*Quaternion.Euler(0f,0f,time*100f+id*41f);
                float near=Mathf.Clamp01(.5f-local.z*.65f);
                shot.body.transform.localScale=Vector3.Lerp(shot.originalScale,shot.orbitScale*Mathf.Lerp(.86f,1.14f,near),progress);
                Color shade=shot.color*Mathf.Lerp(.58f,1f,near);shade.a=shot.color.a*Mathf.Lerp(.72f,1f,near);
                shot.body.color=Color.Lerp(shot.color,shade,progress);
                shot.body.sortingOrder=local.z>0f?19999:20002;
                if(shot.basic&&orbFrames.Length==8)shot.body.sprite=orbFrames[(frame+id*2)%8];
                if(record)Remember(shot,shot.position);
            }
            for(int i=1;i<shot.samples&&count<pixels.Length;i++)
            {
                Color color=shot.color;color.a*=.4f*(1f-i/(float)shot.trail.Length);
                pixels[count++]=Pixel(shot.trail[i],worldHeight*(shot.released?.026f:.018f),color,id*7+i);
            }
        }
        if(core.enabled)for(int i=0;i<96&&count<pixels.Length;i++)
        {
            float phase=time*(1.8f+Seed(i)*.5f)+i*2.399963f;
            Quaternion plane=Quaternion.Euler(27f+(i%3)*32f,i*29f,i*17f);
            float radius=(.42f+Seed(i+19)*.17f)*Mathf.Lerp(1f,.35f,contraction)*growth;
            Vector3 point=anchor+facing*(plane*new Vector3(Mathf.Cos(phase),Mathf.Sin(phase),0f))*worldHeight*radius;
            Color color=Color.Lerp(new Color(.46f,.92f,.89f,.36f),new Color(1f,.86f,.47f,.6f),Seed(i+31));
            pixels[count++]=Pixel(point,worldHeight*(.012f+Seed(i+7)*.012f),color,i+3000);
        }
        motes.SetParticles(pixels,count);motes.GetComponent<ParticleSystemRenderer>().forceRenderingOff=hidden;
    }
    private static ParticleSystem.Particle Pixel(Vector3 point,float size,Color color,int seed)=>
        new ParticleSystem.Particle{position=point,startSize=size,startColor=color,startLifetime=1f,remainingLifetime=1f,randomSeed=(uint)(seed+1)};
    public void Cancel()
    {
        if(this==null)return;if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void OnDestroy()
    {
        foreach(var mesh in spiralMeshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        if(spiralMaterial!=null){if(Application.isPlaying)Destroy(spiralMaterial);else DestroyImmediate(spiralMaterial);}
    }
}
