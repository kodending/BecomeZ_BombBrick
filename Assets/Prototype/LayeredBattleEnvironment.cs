using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class LayeredBattleEnvironment : MonoBehaviour
{
    private sealed class Visual
    {
        public Transform root;
        public Transform quad;
        public Material material;
        public float depth;
        public float fitScale=1f;
        public Vector2 viewportAnchor;
        public float viewportHeight;
        public bool hangs;
        public float idlePixels;
        public float idleSpeed;
        public float idlePhase;
        public Vector3 fittedPosition;
        public Vector3 fittedScale=Vector3.one;
        public Vector2 offsetPixels;
        public Vector2 velocityPixels;
        public float rotation;
        public float rotationVelocity;
    }

    private readonly List<Visual> visuals=new List<Visual>();
    private readonly List<Visual> props=new List<Visual>();
    private readonly List<Material> materials=new List<Material>();
    private readonly List<Sprite> sprites=new List<Sprite>();
    private readonly List<Mesh> meshes=new List<Mesh>();
    private Camera view;
    private Transform generatedRoot;
    private Visual midground;
    private Visual arena;
    private Visual foreground;
    private int fittedWidth=-1;
    private int fittedHeight=-1;
    private float nextPulseAt;
    private float lastPulseStrength;
    private bool staticLayout;

    public bool IsReady=>generatedRoot!=null&&view!=null;
    public bool HasSeparateCombatLayer=>midground!=null&&arena!=null;
    public int GuardianBackdropOrder=>HasSeparateCombatLayer
        ?arena.root.GetComponent<Renderer>().sortingOrder-1:1;
    // All region master plates share this rear platform edge (viewport coordinates).
    public float GuardianRearEdgeViewportY=>!HasSeparateCombatLayer&&arena!=null?.7065f:-1f;

    public bool Build(Camera camera,string resourceRoot)
    {
        Texture2D pixelBase=Resources.Load<Texture2D>(
            resourceRoot+"/Layer00SkyMountains");
        if(pixelBase!=null)
            return BuildStaticPixelLayers(camera,resourceRoot,pixelBase);

        Texture2D far=Resources.Load<Texture2D>(resourceRoot+"/LayerFar");
        Texture2D mid=Resources.Load<Texture2D>(resourceRoot+"/LayerMid");
        Texture2D ground=Resources.Load<Texture2D>(resourceRoot+"/LayerArena");
        Texture2D front=Resources.Load<Texture2D>(resourceRoot+"/LayerForeground");
        if(camera==null||far==null||mid==null||ground==null||front==null)
            return false;

        ClearGenerated();
        view=camera;
        generatedRoot=new GameObject(resourceRoot+" Layered Environment").transform;
        generatedRoot.SetParent(view.transform,false);
        Configure(far);Configure(mid);Configure(ground);Configure(front);

        float farDepth=Mathf.Max(view.nearClipPlane+12f,view.farClipPlane-8f);
        CreateFullscreen("Far Landscape",far,farDepth,1.035f,0,0.12f,0.16f);
        midground=CreateFullscreen(
            "Worldroot Midground",mid,farDepth+1f,1.025f,1,0.28f,0.3f);
        arena=CreateFullscreen(
            "Combat Ground",ground,farDepth+2f,1.005f,2,0f,0f);

        Texture2D atlas=Resources.Load<Texture2D>(resourceRoot+"/ReactiveProps");
        if(atlas!=null)
        {
            Configure(atlas);
            float propDepth=farDepth+3f;
            CreateProp("Left Sapling",atlas,0,new Vector2(.075f,.47f),.22f,false,propDepth);
            CreateProp("Right Rune",atlas,1,new Vector2(.94f,.51f),.21f,false,propDepth);
            CreateProp("Left Boulder",atlas,2,new Vector2(.075f,.29f),.16f,false,propDepth);
            CreateProp("Right Ferns",atlas,3,new Vector2(.94f,.3f),.16f,false,propDepth);
            CreateProp("Hanging Vines",atlas,4,new Vector2(.68f,.92f),.22f,true,propDepth);
            CreateProp("Front Marker",atlas,5,new Vector2(.9f,.17f),.14f,false,propDepth);
        }

        foreground=CreateFullscreen(
            "Foreground Foliage",front,view.nearClipPlane+2f,1.02f,20,0.65f,0.42f);
        Fit();
        return true;
    }

    public bool BuildMaster(Camera camera,Texture2D master)
    {
        if(camera==null||master==null)return false;
        ClearGenerated();
        view=camera;
        staticLayout=true;
        generatedRoot=new GameObject("Battlefield Master").transform;
        generatedRoot.SetParent(view.transform,false);
        Configure(master);
        master.wrapMode=TextureWrapMode.Mirror;
        float depth=Mathf.Max(view.nearClipPlane+12f,view.farClipPlane-8f);
        arena=CreateFullscreen("Battlefield Master",master,depth,1f,0,0f,0f);
        ExtendMasterEdges(arena);
        Fit();
        return true;
    }

    private bool BuildStaticPixelLayers(
        Camera camera,string resourceRoot,Texture2D sky)
    {
        Texture2D forest=Resources.Load<Texture2D>(resourceRoot+"/Layer10FarForest");
        Texture2D ruins=Resources.Load<Texture2D>(resourceRoot+"/Layer20MidRuins");
        Texture2D sides=Resources.Load<Texture2D>(resourceRoot+"/Layer30SideTrees");
        Texture2D combat=Resources.Load<Texture2D>(resourceRoot+"/Layer40CombatPlatform");
        Texture2D reserve=Resources.Load<Texture2D>(resourceRoot+"/Layer50ReservePlatform");
        Texture2D front=Resources.Load<Texture2D>(resourceRoot+"/Layer60ForegroundRoots");
        if(camera==null||forest==null||ruins==null||sides==null||combat==null||
           reserve==null||front==null)return false;

        ClearGenerated();
        view=camera;
        staticLayout=true;
        generatedRoot=new GameObject(resourceRoot+" Static Pixel Layers").transform;
        generatedRoot.SetParent(view.transform,false);
        Texture2D[] textures={sky,forest,ruins,sides,combat,reserve,front};
        for(int i=0;i<textures.Length;i++)Configure(textures[i]);

        float farDepth=Mathf.Max(view.nearClipPlane+12f,view.farClipPlane-8f);
        CreateFullscreen("Sky And Mountains",sky,farDepth,1f,0,0f,0f);
        CreateFullscreen("Far Forest",forest,farDepth-.1f,1f,1,0f,0f);
        midground=CreateFullscreen("Ruins And Waterfalls",ruins,farDepth-.2f,1f,2,0f,0f);
        CreateFullscreen("Side Trees",sides,farDepth-.3f,1f,3,0f,0f);
        arena=CreateRegisteredPlatform(
            "Combat Platform",combat,farDepth-.4f,4,
            new[]
            {
                new Vector2(.2625f,.7065f),new Vector2(.736f,.7065f),
                new Vector2(.7734f,.3509f),new Vector2(.226f,.3509f)
            },
            new[]
            {
                new Vector2(.1525f,.7758f),new Vector2(.8451f,.7758f),
                new Vector2(.9528f,.1711f),new Vector2(.0467f,.1711f)
            });
        CreateRegisteredPlatform(
            "Reserve Platform",reserve,farDepth-.5f,5,
            new[]
            {
                new Vector2(.237f,.312f),new Vector2(.763f,.312f),
                new Vector2(.7729f,.2111f),new Vector2(.2266f,.2111f)
            },
            new[]
            {
                new Vector2(.1406f,.4283f),new Vector2(.8828f,.4283f),
                new Vector2(.878f,.1498f),new Vector2(.1519f,.1498f)
            });
        foreground=CreateFullscreen(
            "Foreground Roots",front,view.nearClipPlane+2f,1f,20,0f,0f);
        Fit();
        return true;
    }

    public void Fit()
    {
        if(!IsReady||Screen.width<=0||Screen.height<=0||
           fittedWidth==Screen.width&&fittedHeight==Screen.height)return;
        fittedWidth=Screen.width;
        fittedHeight=Screen.height;
        float aspect=Mathf.Max(.01f,view.aspect);
        for(int i=0;i<visuals.Count;i++)
        {
            Visual visual=visuals[i];
            float height=ViewHeight(visual.depth);
            visual.fittedPosition=new Vector3(0f,0f,visual.depth);
            visual.fittedScale=new Vector3(
                height*aspect*visual.fitScale,height*visual.fitScale,1f);
            visual.root.localPosition=visual.fittedPosition;
            visual.root.localScale=visual.fittedScale;
        }
        for(int i=0;i<props.Count;i++)FitProp(props[i],aspect);
    }

    public void Pulse(Vector3 worldPosition,float strength,Vector3 worldDirection)
    {
        if(!IsReady||staticLayout)return;
        strength=Mathf.Clamp01(strength);
        float now=Time.unscaledTime;
        if(now<nextPulseAt&&strength<=lastPulseStrength)return;
        lastPulseStrength=strength;
        nextPulseAt=now+(strength<.3f?.14f:.07f);

        Vector3 localDirection=view.transform.InverseTransformDirection(worldDirection);
        Vector2 direction=new Vector2(localDirection.x,localDirection.y).normalized;
        if(direction.sqrMagnitude<.01f)direction=Vector2.right;
        Kick(arena,-direction,strength*42f,strength*5f);
        Kick(midground,direction,strength*15f,-strength*2.5f);
        Kick(foreground,-direction,strength*24f,strength*4f);

        float impactX=view.WorldToViewportPoint(worldPosition).x;
        for(int i=0;i<props.Count;i++)
        {
            Visual prop=props[i];
            float weight=ImpactWeight(prop.viewportAnchor.x,impactX,.52f);
            if(weight<=0f)continue;
            float side=prop.viewportAnchor.x<impactX?-1f:1f;
            prop.velocityPixels+=new Vector2(
                side*strength*58f*weight,strength*24f*weight);
            prop.rotationVelocity+=-side*strength*15f*weight;
        }
    }

    public static float ImpactWeight(float propX,float impactX,float radius)
    {
        if(radius<=0f)return 0f;
        float normalized=Mathf.Clamp01(Mathf.Abs(propX-impactX)/radius);
        float remaining=1f-normalized;
        return remaining*remaining;
    }

    public static void StepSpring(
        ref float value,ref float velocity,float stiffness,float damping,float deltaTime)
    {
        if(deltaTime<=0f)return;
        velocity-=value*stiffness*deltaTime;
        velocity*=Mathf.Exp(-damping*deltaTime);
        value+=velocity*deltaTime;
    }

    private void LateUpdate()
    {
        if(!IsReady)return;
        Fit();
        if(staticLayout)return;
        float now=Time.unscaledTime;
        float delta=Mathf.Min(.05f,Mathf.Max(0f,Time.unscaledDeltaTime));
        for(int i=0;i<visuals.Count;i++)UpdateVisual(visuals[i],now,delta);
        for(int i=0;i<props.Count;i++)UpdateVisual(props[i],now,delta);
    }

    private Visual CreateFullscreen(
        string name,Texture2D texture,float depth,float scale,int sortingOrder,
        float idlePixels,float idleSpeed)
    {
        Visual visual=CreateVisual(name,texture,sortingOrder);
        visual.depth=depth;
        visual.fitScale=scale;
        visual.idlePixels=idlePixels;
        visual.idleSpeed=idleSpeed;
        visual.idlePhase=sortingOrder*.83f;
        visuals.Add(visual);
        return visual;
    }

    private Visual CreateRegisteredPlatform(
        string name,Texture2D texture,float depth,int sortingOrder,
        Vector2[] viewportCorners,Vector2[] sourceUv)
    {
        Visual visual=CreateVisual(name,texture,sortingOrder);
        Mesh mesh=new Mesh{name=name+" Registered Mesh"};
        mesh.vertices=new[]
        {
            new Vector3(viewportCorners[0].x-.5f,viewportCorners[0].y-.5f,0f),
            new Vector3(viewportCorners[1].x-.5f,viewportCorners[1].y-.5f,0f),
            new Vector3(viewportCorners[2].x-.5f,viewportCorners[2].y-.5f,0f),
            new Vector3(viewportCorners[3].x-.5f,viewportCorners[3].y-.5f,0f)
        };
        mesh.uv=sourceUv;
        mesh.triangles=new[]{0,1,2,0,2,3};
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        visual.root.GetComponent<MeshFilter>().sharedMesh=mesh;
        meshes.Add(mesh);
        visual.depth=depth;
        visuals.Add(visual);
        return visual;
    }

    private void ExtendMasterEdges(Visual visual)
    {
        var mesh=new Mesh{name="Battlefield Master Extended Edges"};
        mesh.vertices=new[]{new Vector3(-1.5f,1.5f,0f),new Vector3(1.5f,1.5f,0f),
            new Vector3(1.5f,-1.5f,0f),new Vector3(-1.5f,-1.5f,0f)};
        mesh.uv=new[]{new Vector2(-1f,2f),new Vector2(2f,2f),new Vector2(2f,-1f),new Vector2(-1f,-1f)};
        mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};
        mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
        visual.root.GetComponent<MeshFilter>().sharedMesh=mesh;meshes.Add(mesh);
    }

    private void CreateProp(
        string name,Texture2D atlas,int index,Vector2 anchor,float height,
        bool hangs,float depth)
    {
        GameObject rootObject=new GameObject(name+" Pivot");
        rootObject.transform.SetParent(generatedRoot,false);
        GameObject spriteObject=new GameObject(name);
        spriteObject.transform.SetParent(rootObject.transform,false);
        int cellWidth=atlas.width/3;
        int cellHeight=atlas.height/2;
        int column=index%3;
        int row=index<3?1:0;
        Sprite sprite=Sprite.Create(
            atlas,new Rect(column*cellWidth,row*cellHeight,cellWidth,cellHeight),
            new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
        sprite.name=name;
        sprites.Add(sprite);
        SpriteRenderer renderer=spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite=sprite;
        renderer.color=new Color(.72f,.78f,.62f,.94f);
        renderer.sortingOrder=10+index;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        Visual visual=new Visual
        {
            root=rootObject.transform,
            quad=spriteObject.transform
        };
        visual.depth=depth;
        visual.viewportAnchor=anchor;
        visual.viewportHeight=height;
        visual.hangs=hangs;
        visual.idlePixels=hangs?.75f:.35f;
        visual.idleSpeed=.7f+index*.09f;
        visual.idlePhase=index*1.17f;
        props.Add(visual);
    }

    private Visual CreateVisual(
        string name,Texture2D texture,int sortingOrder)
    {
        GameObject quadObject=GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObject.name=name;
        Transform root=quadObject.transform;
        root.SetParent(generatedRoot,false);
        Collider collider=quadObject.GetComponent<Collider>();
        if(collider!=null)ReleaseOwned(collider);
        Renderer renderer=quadObject.GetComponent<Renderer>();
        Material material=CreateMaterial(texture);
        renderer.sharedMaterial=material;
        renderer.sortingOrder=sortingOrder;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        return new Visual
        {
            root=root,
            quad=quadObject.transform,
            material=material
        };
    }

    private Material CreateMaterial(Texture2D texture)
    {
        Shader shader=Shader.Find("Sprites/Default")??Shader.Find("Unlit/Transparent");
        if(shader==null)shader=Shader.Find("Universal Render Pipeline/Unlit");
        Material material=new Material(shader)
        {
            name=texture.name+" Layer Material",
            mainTexture=texture,
            renderQueue=(int)RenderQueue.Transparent
        };
        if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
        if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
        materials.Add(material);
        return material;
    }

    private void FitProp(Visual prop,float aspect)
    {
        float viewHeight=ViewHeight(prop.depth);
        float viewWidth=viewHeight*aspect;
        float propHeight=viewHeight*prop.viewportHeight;
        SpriteRenderer renderer=prop.quad.GetComponent<SpriteRenderer>();
        float spriteHeight=renderer==null||renderer.sprite==null
            ?1f:renderer.sprite.bounds.size.y;
        float scale=propHeight/Mathf.Max(.001f,spriteHeight);
        prop.fittedPosition=new Vector3(
            (prop.viewportAnchor.x-.5f)*viewWidth,
            (prop.viewportAnchor.y-.5f)*viewHeight,
            prop.depth);
        prop.fittedScale=Vector3.one;
        prop.root.localScale=Vector3.one;
        prop.quad.localScale=new Vector3(scale,scale,1f);
        prop.quad.localPosition=new Vector3(
            0f,prop.hangs?-propHeight*.5f:propHeight*.5f,0f);
    }

    private void UpdateVisual(Visual visual,float now,float delta)
    {
        StepSpring(ref visual.offsetPixels.x,ref visual.velocityPixels.x,74f,10f,delta);
        StepSpring(ref visual.offsetPixels.y,ref visual.velocityPixels.y,82f,10.5f,delta);
        StepSpring(ref visual.rotation,ref visual.rotationVelocity,68f,9f,delta);
        float idle=Mathf.Sin(now*visual.idleSpeed+visual.idlePhase)*visual.idlePixels;
        float unitsPerPixel=ViewHeight(visual.depth)/Mathf.Max(1,Screen.height);
        Vector2 pixelOffset=visual.offsetPixels+new Vector2(idle,idle*.18f);
        visual.root.localPosition=visual.fittedPosition+new Vector3(
            Mathf.Round(pixelOffset.x)*unitsPerPixel,
            Mathf.Round(pixelOffset.y)*unitsPerPixel,0f);
        visual.root.localRotation=Quaternion.Euler(
            0f,0f,visual.rotation+idle*.06f);
    }

    private static void Kick(Visual visual,Vector2 direction,float speed,float rotation)
    {
        if(visual==null)return;
        visual.velocityPixels+=direction*speed;
        visual.rotationVelocity+=rotation;
    }

    private float ViewHeight(float depth)=>
        2f*depth*Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad);

    private static void Configure(Texture2D texture)
    {
        texture.filterMode=FilterMode.Point;
        texture.wrapMode=TextureWrapMode.Clamp;
    }

    private void ClearGenerated()
    {
        if(generatedRoot!=null)
        {
            generatedRoot.gameObject.SetActive(false);
            ReleaseOwned(generatedRoot.gameObject);
        }
        for(int i=0;i<materials.Count;i++)
            if(materials[i]!=null)ReleaseOwned(materials[i]);
        materials.Clear();
        for(int i=0;i<sprites.Count;i++)
            if(sprites[i]!=null)ReleaseOwned(sprites[i]);
        sprites.Clear();
        for(int i=0;i<meshes.Count;i++)
            if(meshes[i]!=null)ReleaseOwned(meshes[i]);
        meshes.Clear();
        visuals.Clear();
        props.Clear();
        generatedRoot=null;
        midground=null;
        arena=null;
        foreground=null;
        staticLayout=false;
        fittedWidth=-1;
        fittedHeight=-1;
    }

    private void OnDestroy()=>ClearGenerated();
    private static void ReleaseOwned(UnityEngine.Object value)
    {if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
}
