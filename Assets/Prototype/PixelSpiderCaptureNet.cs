using System;
using UnityEngine;

// One compact bundle opens overhead; contact, not launch, resolves the skill.
public sealed class PixelSpiderCaptureNet:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private static Sprite netSprite,bundleSprite,threadSprite;
    private SpriteRenderer main,shade,source;
    private SpriteRenderer[] scraps;
    private Vector3[] scrapOrigins;
    private Func<bool> valid;
    private Action landed,spreadStarted;
    private Vector3 start,destination;
    private Color tint;
    private float elapsed,radius,travel,body;
    private bool heavenly,strongHit;
    private Mesh foldedNet;
    private Material foldedMaterial;
    private Vector3[] wovenVertices;
    private Color[] wovenColors;
    private float pulseAge=1f;
    private int phase; // 0: gather, 1: flight, 2: spread/drop, 3: ground, 4: break
    private CombatProjectileSource.Flight flight;
    private float previousClock;
    public const float SpreadDuration=.11f,DropDuration=.13f;

    public static PixelSpiderCaptureNet Charge(SpriteRenderer source,Color color,Func<bool> valid)
    {
        if(source==null)return null;
        var fx=Create(source,color,valid,"Spider Gathered Silk");
        fx.main.sprite=BundleSprite;fx.Animate(0f);return fx;
    }

    public static PixelSpiderCaptureNet Throw(SpriteRenderer source,Vector3 from,Vector3 target,
        float radius,Color color,Func<bool> valid,Action landed,bool heavenly=false,Action spread=null)
    {
        var fx=Create(source,color,valid,"Spider Capture Net Flight");
        fx.phase=1;fx.start=from;fx.destination=target;fx.radius=radius;fx.landed=landed;
        fx.spreadStarted=spread;
        fx.heavenly=heavenly;
        fx.travel=Mathf.Clamp(Vector3.Distance(from,target)/16f,.14f,.34f);
        fx.main.sprite=BundleSprite;fx.main.transform.position=from;fx.Animate(0f);return fx;
    }

    public static GameObject Ground(SpriteRenderer source,Vector3 center,float radius,Color color)
    {
        var fx=Create(source,color,null,"Spider Landed Capture Net");
        fx.phase=3;fx.destination=center;fx.radius=radius;fx.main.sprite=NetSprite;
        fx.main.transform.rotation=Quaternion.Euler(90,0,0);
        // The layered battlefield itself is transparent at order 0; stay above it and below units.
        fx.main.sortingOrder=Mathf.Max(2,(source==null?10:source.sortingOrder)-6);
        fx.shade=fx.Child("Silk Shadow",NetSprite,new Color(.12f,.18f,.22f,.25f),0);
        fx.shade.sortingOrder=fx.main.sortingOrder-1;
        fx.shade.transform.rotation=Quaternion.Euler(90,0,0);
        fx.Animate(0f);return fx.gameObject;
    }

    private static PixelSpiderCaptureNet Create(SpriteRenderer source,Color color,Func<bool> valid,string label)
    {
        var fx=new GameObject(label).AddComponent<PixelSpiderCaptureNet>();
        fx.source=source;fx.tint=PixelSpiritConcert.Tint(source,color);fx.valid=valid;
        fx.flight=CombatProjectileSource.Capture(source==null?null:source.transform);
        fx.previousClock=fx.flight.clock==null?0:fx.flight.clock();
        fx.body=source==null?1f:Mathf.Clamp(source.bounds.size.y,.7f,2.8f);
        fx.main=fx.Child("Silk",BundleSprite,color,3);return fx;
    }

    private SpriteRenderer Child(string label,Sprite sprite,Color color,int order)
    {
        var go=new GameObject(label);go.transform.SetParent(transform,false);
        var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=color;
        sr.sortingLayerID=source==null?0:source.sortingLayerID;
        sr.sortingOrder=(source==null?0:source.sortingOrder)+order;return sr;
    }

    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        landed=null;spreadStarted=null;Remove();
    }

    public static void Hit(SpriteRenderer target,Color color,bool strong=false)
    {
        if(target==null)return;
        var fx=Create(target,color,null,"Spider Silk Contact");
        fx.strongHit=strong;
        fx.main.enabled=false;fx.destination=target.bounds.center;fx.radius=fx.body*.28f;
        fx.Break();
        Quaternion camera=Camera.main==null?target.transform.rotation:Camera.main.transform.rotation;
        for(int i=0;i<fx.scraps.Length;i++)
        {
            float a=i*Mathf.PI*2f/fx.scraps.Length;
            fx.scrapOrigins[i]=fx.destination+camera*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*fx.radius*.5f;
            fx.scraps[i].transform.localScale=Vector3.one*fx.body*(strong?.23f:.16f);
        }
        fx.Animate(0f);
    }

    public void Break()
    {
        if(this==null||phase==4)return;
        phase=4;elapsed=0f;
        scraps=new SpriteRenderer[heavenly?24:strongHit?18:12];scrapOrigins=new Vector3[scraps.Length];
        for(int i=0;i<scraps.Length;i++)
        {
            float a=i*Mathf.PI*2f/scraps.Length;
            scraps[i]=Child("Loose Silk",ThreadSprite,tint,2);
            scrapOrigins[i]=destination+new Vector3(Mathf.Cos(a),.04f,Mathf.Sin(a))*radius*(i%2==0?.8f:.45f);
            scraps[i].transform.localScale=Vector3.one*.20f;
        }
    }

    // Keep the outer boundary fixed while the inner weave folds toward captured enemies.
    public void GatherHeavenly()
    {
        if(phase!=3||foldedNet!=null)return;
        heavenly=true;elapsed=0f;main.enabled=false;
        var go=new GameObject("Folding Heavenly Weave");go.transform.SetParent(transform,false);
        go.transform.position=destination+Vector3.up*.027f;
        foldedNet=new Mesh{name="Heavenly Silk Folds"};
        wovenVertices=new Vector3[65];wovenColors=new Color[65];
        var uv=new Vector2[65];uv[0]=Vector2.one*.5f;
        var triangles=new int[16*7*3];int index=0;
        for(int ring=1;ring<=4;ring++)for(int sector=0;sector<16;sector++)
        {
            int v=1+(ring-1)*16+sector,next=1+(ring-1)*16+(sector+1)%16;
            float a=sector*Mathf.PI/8f;
            uv[v]=Vector2.one*.5f+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*ring/8f;
            if(ring==1){triangles[index++]=0;triangles[index++]=next;triangles[index++]=v;}
            else
            {
                triangles[index++]=v-16;triangles[index++]=next;triangles[index++]=v;
                triangles[index++]=v-16;triangles[index++]=next-16;triangles[index++]=next;
            }
        }
        foldedNet.vertices=wovenVertices;foldedNet.uv=uv;foldedNet.triangles=triangles;
        go.AddComponent<MeshFilter>().sharedMesh=foldedNet;
        var renderer=go.AddComponent<MeshRenderer>();
        foldedMaterial=new Material(main.sharedMaterial){mainTexture=NetSprite.texture};
        renderer.sharedMaterial=foldedMaterial;
        renderer.sortingLayerID=main.sortingLayerID;renderer.sortingOrder=main.sortingOrder;
        UpdateWeave();
    }

    public void Pulse(){pulseAge=0f;}

    private void UpdateWeave()
    {
        float gather=phase==4?1f:Mathf.SmoothStep(0f,1f,elapsed/ArachneRank3Rules.HeavenlyPullDuration);
        float pulse=Mathf.Sin(Mathf.Clamp01(pulseAge/.22f)*Mathf.PI);
        for(int i=0;i<wovenVertices.Length;i++)
        {
            int ring=i==0?0:1+(i-1)/16,sector=i==0?0:(i-1)%16;
            float fraction=ring/4f,a=sector*Mathf.PI/8f;
            float fold=ring==4?0f:Mathf.Sin(fraction*Mathf.PI);
            float r=radius*(fraction-fold*(.10f*gather+.035f*pulse));
            wovenVertices[i]=new Vector3(Mathf.Cos(a)*r,fold*gather*(sector%2==0?.12f:.035f),Mathf.Sin(a)*r);
            float alpha=phase==4?1f-Mathf.Clamp01((elapsed-sector/2*.018f)/.12f):1f;
            wovenColors[i]=new Color(tint.r,tint.g,tint.b,alpha*(.73f+.22f*pulse));
        }
        foldedNet.vertices=wovenVertices;foldedNet.colors=wovenColors;foldedNet.RecalculateBounds();
    }

    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(flight.valid!=null&&!flight.valid()){Cancel();return;}
            float dt=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(flight.clock!=null){float now=flight.clock();dt=Mathf.Max(0,now-previousClock);previousClock=now;}
            Animate(dt);
        }
    }
    private void Animate(float dt)
    {
        if(valid!=null&&!valid()){Cancel();return;}
        elapsed+=Mathf.Max(0f,dt);
        pulseAge+=Mathf.Max(0f,dt);
        Quaternion camera=Camera.main==null?Quaternion.identity:Camera.main.transform.rotation;
        if(phase==0)
        {
            if(source==null){Cancel();return;}
            float gather=Mathf.Clamp01(elapsed/.22f);
            Vector3 side=(source.flipX?-1f:1f)*(camera*Vector3.right);
            main.transform.position=source.bounds.center+side*body*.18f;
            main.transform.rotation=camera*Quaternion.Euler(0,0,elapsed*170f);
            main.transform.localScale=Vector3.one*body*Mathf.Lerp(.30f,.19f,gather);
            main.color=new Color(tint.r,tint.g,tint.b,Mathf.Clamp01(elapsed/.04f));return;
        }
        if(phase==1)
        {
            float t=Mathf.Clamp01(elapsed/travel);
            Vector3 top=destination+Vector3.up*1.5f;
            Vector3 next=Vector3.Lerp(start,top,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.38f);
            if(flight.deflect!=null&&flight.deflect(main.transform.position,next,main)){Cancel();return;}
            main.transform.position=next;
            main.transform.rotation=camera*Quaternion.Euler(0,0,elapsed*240f);
            main.transform.localScale=Vector3.one*body*(heavenly?.30f:.22f);
            if(t<1f)return;
            elapsed-=travel;phase=2;main.sprite=NetSprite;
            Action spread=spreadStarted;spreadStarted=null;spread?.Invoke();
        }
        if(phase==2)
        {
            float spread=Mathf.SmoothStep(0f,1f,elapsed/SpreadDuration);
            float drop=Mathf.Clamp01((elapsed-SpreadDuration)/DropDuration);
            main.transform.rotation=Quaternion.Euler(90,0,0);
            Vector3 next=destination+Vector3.up*Mathf.Lerp(1.5f,.025f,drop*drop);
            if(flight.deflect!=null&&flight.deflect(main.transform.position,next,main)){Cancel();return;}
            main.transform.position=next;
            main.transform.localScale=Vector3.one*(radius*2f/NetSprite.bounds.size.x)*Mathf.Lerp(.10f,1f,spread);
            main.color=new Color(tint.r,tint.g,tint.b,Mathf.Lerp(.65f,.95f,spread));
            if(drop<1f)return;
            Action hit=landed;landed=null;hit?.Invoke();Remove();return;
        }
        float size=radius*2f/NetSprite.bounds.size.x;
        if(phase==3)
        {
            main.transform.position=destination+Vector3.up*.025f;
            main.transform.localScale=Vector3.one*size;
            main.color=new Color(tint.r,tint.g,tint.b,Mathf.Lerp(.9f,.55f,Mathf.Clamp01(elapsed/.25f)));
            shade.transform.position=destination+new Vector3(.025f,.018f,-.025f);
            shade.transform.localScale=Vector3.one*size;
            if(foldedNet!=null)UpdateWeave();
            return;
        }
        float alpha=1f-Mathf.Clamp01(elapsed/(heavenly?.32f:.24f));
        if(foldedNet!=null)UpdateWeave();
        main.color=new Color(tint.r,tint.g,tint.b,alpha*.55f);
        if(shade!=null)shade.color=new Color(.12f,.18f,.22f,alpha*.25f);
        for(int i=0;i<scraps.Length;i++)
        {
            Vector3 ray=(scrapOrigins[i]-destination).normalized;
            scraps[i].transform.position=scrapOrigins[i]+ray*elapsed*.7f+Vector3.up*(elapsed*.5f-elapsed*elapsed*2f);
            scraps[i].transform.rotation=camera*Quaternion.Euler(0,0,i*43f+elapsed*110f);
            scraps[i].color=new Color(tint.r,tint.g,tint.b,alpha);
        }
        if(alpha<=0f)Remove();
    }

    private void OnDestroy()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            flight.finished?.Invoke();ReleaseWeave();
        }
    }

    private void ReleaseWeave()
    {
        if(Application.isPlaying){Destroy(foldedNet);Destroy(foldedMaterial);}
        else{DestroyImmediate(foldedNet);DestroyImmediate(foldedMaterial);}
        foldedNet=null;foldedMaterial=null;
    }

    private void Remove()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            flight.finished?.Invoke();
            ReleaseWeave();
            gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
        }
    }

    internal static Sprite NetSprite=>netSprite!=null?netSprite:
        netSprite=PixelNinjaSubstitution.MakeSprite("Pixel Woven Capture Net",65,65,64f,(x,y)=>
        {
            float dx=x-32f,dy=y-32f,r=Mathf.Sqrt(dx*dx+dy*dy);
            if(r>32.5f)return Color.clear;
            float a=Mathf.Atan2(dy,dx),sector=Mathf.PI/4f;
            float nearest=Mathf.Round(a/sector)*sector;
            bool spoke=Mathf.Abs(Mathf.Sin(a-nearest)*r)<.55f;
            float sag=.88f+.12f*Mathf.Cos(a*8f);
            float ring=r/(32f*sag)*4f;
            bool thread=ring>.6f&&ring<3.5f&&Mathf.Abs(ring-Mathf.Round(ring))*32f*sag/4f<.6f;
            // A woven outer thread follows the actual circular gameplay boundary.
            return spoke||thread||Mathf.Abs(r-32f)<.6f?new Color(.95f,.99f,1f,.94f):Color.clear;
        });
    internal static Sprite ThreadSprite=>threadSprite!=null?threadSprite:
        threadSprite=PixelNinjaSubstitution.MakeSprite("Broken Pixel Silk",9,5,10f,(x,y)=>
            y==2+(x<3?1:x>6?-1:0)?Color.white:Color.clear);
    private static Sprite BundleSprite=>bundleSprite!=null?bundleSprite:
        bundleSprite=PixelNinjaSubstitution.MakeSprite("Dense Silk Knot",15,15,16f,(x,y)=>
        {
            float dx=x-7f,dy=y-7f;if(dx*dx+dy*dy>38f)return Color.clear;
            bool seam=(x+y)%5==0||(x-y+15)%7==0;
            return seam?new Color(.56f,.70f,.75f):new Color(.95f,.99f,1f);
        });
}
