using System;
using UnityEngine;

// A frozen pose proxy keeps anticipation scale separate from collision, movement and world HUD.
public sealed class Rank4SkillPreludeVisual:IDisposable
{
    private static readonly int HitColor=Shader.PropertyToID("_HitColor");
    private static readonly int HitAmount=Shader.PropertyToID("_HitAmount");
    private readonly GameObject root;
    private readonly SpriteRenderer body,halo;
    private readonly Material material,pixelMaterial;
    private readonly ParticleSystem pixels;
    private readonly ParticleSystemRenderer pixelRenderer;
    private readonly ParticleSystem.Particle[] pixelBuffer=new ParticleSystem.Particle[20];
    private readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
    private Vector3 feet,sourceScale,foot,head,bodyCenter;
    private Quaternion rotation;
    private float progress,sourceAlpha,bodyWidth,bodyHeight;
    private bool disposed;
    private GUIStyle exclamationStyle;
    public SpriteRenderer Body=>body;

    public Rank4SkillPreludeVisual(Transform parent)
    {
        Shader shader=Resources.Load<Shader>("CombatEffects/SpriteHitFlash");
        if(shader==null)throw new InvalidOperationException("Missing rank-4 anticipation highlight shader.");
        material=new Material(shader){name="Rank 4 anticipation highlight",hideFlags=HideFlags.DontSave};
        root=new GameObject("Rank 4 skill anticipation"){hideFlags=HideFlags.DontSave};
        root.transform.SetParent(parent,false);
        halo=MakeRenderer("Anticipation soft gold halo",30020);
        body=MakeRenderer("Anticipation frozen caster",30021);
        var lights=new GameObject("Anticipation inward light"){hideFlags=HideFlags.DontSave};
        lights.transform.SetParent(body.transform,false);pixels=lights.AddComponent<ParticleSystem>();
        var main=pixels.main;main.playOnAwake=false;main.loop=false;main.maxParticles=pixelBuffer.Length;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startSpeed=0f;main.startLifetime=1f;
        var emission=pixels.emission;emission.enabled=false;var shape=pixels.shape;shape.enabled=false;
        pixelMaterial=new Material(Shader.Find("Sprites/Default")){name="Rank 4 anticipation pixels",hideFlags=HideFlags.DontSave,
            mainTexture=Texture2D.whiteTexture};
        pixelRenderer=lights.GetComponent<ParticleSystemRenderer>();pixelRenderer.sharedMaterial=pixelMaterial;
        pixelRenderer.renderMode=ParticleSystemRenderMode.Billboard;pixelRenderer.sortingOrder=30022;
        Clear();
    }

    private SpriteRenderer MakeRenderer(string name,int order)
    {
        var child=new GameObject(name){hideFlags=HideFlags.DontSave};
        child.transform.SetParent(root.transform,false);
        var renderer=child.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial=material;renderer.sortingOrder=order;
        return renderer;
    }

    public void Begin(SpriteRenderer source)
    {
        Clear();
        if(disposed||source==null||source.sprite==null)return;
        if(source.sharedMaterial!=null)material.CopyPropertiesFromMaterial(source.sharedMaterial);
        properties.Clear();source.GetPropertyBlock(properties);
        Bounds bounds=PixelMantisReaper.VisibleBody(source.sprite);
        float x=source.flipX?-bounds.center.x:bounds.center.x;
        foot=new Vector3(x,source.flipY?-bounds.max.y:bounds.min.y,0f);
        head=new Vector3(x,source.flipY?-bounds.min.y:bounds.max.y,0f);
        bodyCenter=(foot+head)*.5f;bodyWidth=bounds.size.x;bodyHeight=bounds.size.y;sourceAlpha=source.color.a;
        feet=source.transform.TransformPoint(foot);
        sourceScale=source.transform.lossyScale;rotation=source.transform.rotation;
        foreach(var renderer in new[]{body,halo})
        {
            renderer.gameObject.layer=source.gameObject.layer;
            renderer.sprite=source.sprite;
            renderer.flipX=source.flipX;renderer.flipY=source.flipY;
            renderer.sortingLayerID=source.sortingLayerID;
            renderer.enabled=true;renderer.forceRenderingOff=false;
        }
        body.color=source.color;
        pixelRenderer.gameObject.layer=source.gameObject.layer;pixelRenderer.sortingLayerID=source.sortingLayerID;
        root.SetActive(true);pixels.Play(false);pixels.Pause(false);Draw(0f);
    }

    public void Draw(float normalizedProgress)
    {
        if(disposed||!root.activeSelf)return;
        progress=Mathf.Clamp01(float.IsNaN(normalizedProgress)||float.IsInfinity(normalizedProgress)?0f:normalizedProgress);
        float scale=progress<.16f?Mathf.Lerp(1f,1.235f,1f-Mathf.Pow(1f-progress/.16f,3f)):
            Mathf.Lerp(1.235f,1.18f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.16f,.38f,progress)));
        float charge=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.12f,.76f,progress));
        float accent=Mathf.Pow(Mathf.Clamp01(1f-Mathf.Abs(progress-.89f)/.11f),2f);
        Place(body,scale);Place(halo,scale*(Mathf.Lerp(1.11f,1.035f,charge)+accent*.018f));
        halo.color=new Color(1f,.87f,.42f,sourceAlpha*(CombatFlashPresentation.Reduced?.07f:.15f)*(.65f+charge*.7f+accent*.8f));
        properties.SetColor(HitColor,Color.Lerp(new Color(1f,.89f,.48f),new Color(1f,.98f,.79f),accent));
        properties.SetFloat(HitAmount,(.12f+charge*.16f+accent*.24f)*(CombatFlashPresentation.Reduced?.55f:1f));
        body.SetPropertyBlock(properties);
        properties.SetFloat(HitAmount,1f);
        halo.SetPropertyBlock(properties);
        DrawChargePixels();
    }

    private void DrawChargePixels()
    {
        int count=0;float reduction=CombatFlashPresentation.Reduced?.5f:1f;
        for(int i=0;i<12;i++)
        {
            float age=Mathf.InverseLerp(.09f+i*.012f,.72f+i*.012f,progress);
            if(age<=0f||age>=1f)continue;
            float side=Mathf.Sin(i*2.39996f),lift=1f-Mathf.Pow(1f-age,2f);
            Vector3 from=bodyCenter+new Vector3(side*bodyWidth*.29f,-bodyHeight*.42f,0f);
            Vector3 to=bodyCenter+new Vector3(side*bodyWidth*.08f,bodyHeight*.25f,0f);
            Color tint=i%3==0?new Color(1f,.96f,.74f):new Color(1f,.78f,.30f);
            tint.a=sourceAlpha*Mathf.Sin(age*Mathf.PI)*.65f*reduction;
            pixelBuffer[count++]=LightPixel(Vector3.Lerp(from,to,lift),bodyHeight*(.014f+i%3*.003f),tint);
        }
        float release=Mathf.InverseLerp(.82f,1f,progress);
        if(release>0f&&release<1f)
            for(int i=0;i<8;i++)
            {
                float angle=(i+.5f)*Mathf.PI*.25f,travel=1f-Mathf.Pow(1f-release,2f);
                Vector3 point=bodyCenter+new Vector3(Mathf.Cos(angle)*bodyWidth*.44f*travel,
                    bodyHeight*.20f+Mathf.Sin(angle)*bodyHeight*.27f*travel,0f);
                Color tint=new Color(1f,.92f,.56f,sourceAlpha*(1f-release)*.65f*reduction);
                pixelBuffer[count++]=LightPixel(point,bodyHeight*.022f*(1f-release*.4f),tint);
            }
        pixels.SetParticles(pixelBuffer,count);
    }

    private static ParticleSystem.Particle LightPixel(Vector3 point,float size,Color color)=>
        new ParticleSystem.Particle{position=point,startSize=size,startColor=color,rotation=45f,startLifetime=1f,remainingLifetime=1f};

    private void Place(SpriteRenderer renderer,float amount)
    {
        Vector3 size=sourceScale*amount;
        renderer.transform.SetPositionAndRotation(feet-rotation*Vector3.Scale(foot,size),rotation);
        Vector3 parent=renderer.transform.parent.lossyScale;
        renderer.transform.localScale=new Vector3(
            Mathf.Abs(parent.x)>.0001f?size.x/parent.x:0f,
            Mathf.Abs(parent.y)>.0001f?size.y/parent.y:0f,
            Mathf.Abs(parent.z)>.0001f?size.z/parent.z:0f);
    }

    public void DrawExclamation(Camera camera)
    {
        if(disposed||camera==null||!root.activeSelf||!body.enabled||body.sprite==null)return;
        Vector3 top=camera.WorldToScreenPoint(body.transform.TransformPoint(head));
        Vector3 bottom=camera.WorldToScreenPoint(feet);
        if(top.z<=camera.nearClipPlane||top.z>=camera.farClipPlane)return;
        float pop=progress<.17f?Mathf.Lerp(.55f,1.28f,1f-Mathf.Pow(1f-progress/.17f,3f)):
            Mathf.Lerp(1.28f,1f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.17f,.42f,progress)));
        float size=Mathf.Clamp(Mathf.Abs(top.y-bottom.y)*.58f,24f,48f);
        size*=pop;
        int fontSize=Mathf.Max(12,Mathf.RoundToInt(size));
        if(exclamationStyle==null)
        {
            exclamationStyle=new GUIStyle(BombBrickUiTheme.BodyStyle(fontSize,TextAnchor.MiddleCenter));
            exclamationStyle.fontStyle=FontStyle.Bold;exclamationStyle.normal.textColor=Color.white;
            exclamationStyle.wordWrap=false;
        }
        exclamationStyle.fontSize=fontSize;
        float width=size*1.2f,height=size*1.35f;
        var rect=new Rect(top.x-width*.5f,Screen.height-top.y-height-Mathf.Max(28f,size),width,height);
        Color previousColor=GUI.color;Matrix4x4 previousMatrix=GUI.matrix;int previousDepth=GUI.depth;
        try
        {
            GUI.matrix=Matrix4x4.identity;GUI.depth=-1000;
            GUI.color=new Color(.04f,.025f,.005f,1f);
            GUI.Label(new Rect(rect.x+2f,rect.y+3f,rect.width,rect.height),"!",exclamationStyle);
            GUI.color=Color.Lerp(new Color(1f,.89f,.34f),new Color(1f,.98f,.78f),Mathf.Max(0f,(progress-.82f)/.18f)*.5f);
            GUI.Label(rect,"!",exclamationStyle);
        }
        finally{GUI.color=previousColor;GUI.matrix=previousMatrix;GUI.depth=previousDepth;}
    }

    public void Clear()
    {
        if(disposed)return;
        body.enabled=false;halo.enabled=false;pixels.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);root.SetActive(false);
    }

    public void Dispose()
    {
        if(disposed)return;
        Clear();disposed=true;
        if(Application.isPlaying){UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(pixelMaterial);}
        else{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(pixelMaterial);}
    }
}
