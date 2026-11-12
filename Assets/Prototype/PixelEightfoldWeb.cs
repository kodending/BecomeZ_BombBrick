using System;
using System.Collections.Generic;
using UnityEngine;

// Authored silk needles travel through the target and stop at a fixed ground point.
public sealed class PixelEightfoldWeb:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    public const string ResourcePath="CombatEffects/Arachne/EightfoldLance";
    public const float FlightToBody=.06f,BodyToGround=.045f;
    private static Sprite[] frames,frontFrames,contactFrames;
    private sealed class Lance
    {
        public SpriteRenderer rear,front,target;
        public Vector3 body,ground,start,axis;
        public float scale;
    }
    private sealed class Volley
    {
        public readonly List<Lance> lances=new List<Lance>();
        public float age,contactTime;
        public bool final,landed;
    }
    private readonly List<Volley> volleys=new List<Volley>();
    private SpriteRenderer[] anchors,tracks;
    private readonly float[] trackAges={-1f,-1f,-1f,-1f};
    private Func<bool> valid;
    private Action<bool> groundImpact;
    private Color tint;
    private float radius,elapsed,finishAge=-1f;
    private bool desert;
    public bool Finishing=>finishAge>=0f;

    public static Sprite[] Frames
    {
        get
        {
            if(frames!=null&&frames.Length==16&&frames[0]!=null)return frames;
            var texture=Resources.Load<Texture2D>(ResourcePath);
            if(texture==null)return Array.Empty<Sprite>();
            frames=new Sprite[16];frontFrames=new Sprite[8];float cell=texture.width/4f;
            for(int i=0;i<16;i++)
            {
                var rect=new Rect(i%4*cell,(3-i/4)*cell,cell,cell);
                frames[i]=Sprite.Create(texture,rect,Vector2.one*.5f,cell,0,SpriteMeshType.FullRect);
                frames[i].name="EightfoldLance_"+i.ToString("00");
                if(i<8)
                {
                    rect.x+=cell*.55f;rect.width=cell*.45f;
                    frontFrames[i]=Sprite.Create(texture,rect,Vector2.one*.5f,cell,0,SpriteMeshType.FullRect);
                }
            }
            return frames;
        }
    }
    public static Sprite[] ContactFrames
    {
        get
        {
            if(contactFrames==null||contactFrames.Length==0||contactFrames[0]==null)
            {
                if(Frames.Length!=16)return Array.Empty<Sprite>();
                contactFrames=new Sprite[6];Array.Copy(Frames,10,contactFrames,0,6);
            }
            return contactFrames;
        }
    }

    public static PixelEightfoldWeb Play(Vector3 center,float radius,SpriteRenderer reference,
        Color color,bool desert,Func<bool> valid,Action<bool> groundImpact=null)
    {
        if(Frames.Length!=16)return null;
        var fx=new GameObject("Eightfold Silk Lances").AddComponent<PixelEightfoldWeb>();
        fx.transform.position=center+Vector3.up*.025f;
        fx.radius=radius;fx.tint=Color.Lerp(Color.white,color,.35f);fx.desert=desert;
        fx.valid=valid;fx.groundImpact=groundImpact;
        fx.anchors=new SpriteRenderer[8];fx.tracks=new SpriteRenderer[4];
        for(int i=0;i<8;i++)
        {
            var anchor=fx.Child("Silk Anchor "+i,reference,3);
            float angle=(i*45f+22.5f)*Mathf.Deg2Rad;
            anchor.transform.localPosition=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius*.93f;
            anchor.transform.localRotation=Quaternion.Euler(90,0,i*45f+202.5f);
            anchor.sprite=Frames[0];anchor.transform.localScale=new Vector3(radius*.3f,radius*.2f,1);
            fx.anchors[i]=anchor;
        }
        for(int i=0;i<4;i++)
        {
            var track=fx.Child("Desert Silk Track "+i,reference,3);
            track.transform.localRotation=Quaternion.Euler(90,0,i*45f+22.5f);
            track.transform.localScale=new Vector3(radius*2.25f,radius*.3f,1f);
            track.sprite=Frames[0];track.enabled=false;fx.tracks[i]=track;
        }
        fx.Animate(0f);return fx;
    }
    private SpriteRenderer Child(string name,SpriteRenderer reference,int order)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var sr=go.AddComponent<SpriteRenderer>();sr.color=tint;
        sr.sortingLayerID=reference==null?0:reference.sortingLayerID;sr.sortingOrder=order;return sr;
    }
    public void Aim(int pair,SpriteRenderer[] targets,bool final=false)
    {
        if(this==null)return;
        if(final)finishAge=0f;else trackAges[pair]=0f;
        var volley=new Volley{final=final,contactTime=final?ArachneRank3Rules.EightfoldFinalTension:FlightToBody};
        foreach(var target in targets)
        {
            if(target==null||!target.gameObject.activeInHierarchy)continue;
            Vector3 body=target.bounds.center;
            float height=Mathf.Clamp(body.y-transform.position.y,.35f,1.6f);
            for(int n=0;n<(final?8:2);n++)
            {
                float angle=(final?n*45f+22.5f:pair*45f+22.5f+n*180f)*Mathf.Deg2Rad;
                var flat=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 ground=new Vector3(body.x,transform.position.y,body.z)+flat*Mathf.Max(.95f,height+.3f);
                Vector3 axis=(ground-body).normalized;
                var rear=Child("Silk Lance Rear",target,target.sortingOrder-1);
                var front=Child("Silk Lance Emerging Tip",target,target.sortingOrder+1);
                front.transform.SetParent(rear.transform,false);front.transform.localPosition=Vector3.right*.275f;
                // Align the sprite with its real 3D trajectory, keeping its broad face visible.
                Vector3 view=Camera.main==null?Vector3.forward:Camera.main.transform.forward;
                Vector3 normal=Vector3.ProjectOnPlane(view,axis).normalized;
                if(normal.sqrMagnitude<.001f)normal=Vector3.forward;
                rear.transform.rotation=Quaternion.LookRotation(normal,Vector3.Cross(normal,axis));
                float scale=Mathf.Max(3.8f,height*4.5f)*(final?1.2f:1f);
                rear.transform.localScale=new Vector3(scale,scale*.95f,1f);
                volley.lances.Add(new Lance{rear=rear,front=front,target=target,body=body,ground=ground,axis=axis,
                    start=body-axis*Mathf.Max(1.3f,height*1.6f),scale=scale});
            }
        }
        volleys.Add(volley);Animate(0f);
    }
    public void Cancel()
    {
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        groundImpact=null;
        if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void Update()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            Animate((skillClock==null?Time.deltaTime:skillClock.DeltaTime));
        }
    }
    private void Animate(float dt)
    {
        if(valid!=null&&!valid()){Cancel();return;}
        elapsed+=dt;if(finishAge>=0f)finishAge+=dt;
        for(int i=0;i<anchors.Length;i++)
        {
            anchors[i].enabled=finishAge<0f&&trackAges[i%4]<0f;
            anchors[i].sprite=Frames[elapsed<ArachneRank3Rules.EightfoldPreparation*.6f?0:1];
        }
        for(int i=0;i<4;i++)
        {
            if(trackAges[i]>=0f)trackAges[i]+=dt;
            tracks[i].enabled=desert&&trackAges[i]>=FlightToBody&&trackAges[i]<3f+FlightToBody;
            tracks[i].color=new Color(tint.r,tint.g,tint.b,.3f);
        }
        foreach(var volley in volleys)
        {
            volley.age+=dt;
            float landAt=volley.contactTime+BodyToGround,dissolveAt=landAt+(volley.final?.16f:.08f);
            int frame=volley.age<.025f?1:volley.age<dissolveAt?3:
                Mathf.Min(7,5+Mathf.FloorToInt((volley.age-dissolveAt)/.045f));
            foreach(var lance in volley.lances)
            {
                bool visible=volley.age<dissolveAt+.135f;
                lance.rear.enabled=visible;lance.front.enabled=visible&&volley.age>=volley.contactTime;
                lance.rear.sprite=Frames[frame];lance.front.sprite=frontFrames[frame];
                Vector3 tip=volley.age<volley.contactTime
                    ?Vector3.Lerp(lance.start,lance.body,Mathf.Pow(Mathf.Clamp01(volley.age/volley.contactTime),2f))
                    :Vector3.Lerp(lance.body,lance.ground,Mathf.Clamp01((volley.age-volley.contactTime)/BodyToGround));
                // Artwork tip at x=.88 keeps moving past the body into the floor.
                lance.rear.transform.position=tip-lance.axis*(lance.scale*.38f);
            }
            if(!volley.landed&&volley.age>=landAt)
            {
                volley.landed=true;
                foreach(var lance in volley.lances)
                {
                    var chips=PixelCombatEffect.PlaySilkLanceGroundImpact(lance.ground,lance.target,volley.final);
                    chips.transform.SetParent(transform,true);
                }
                if(volley.lances.Count>0)groundImpact?.Invoke(volley.final);
            }
        }
        if(finishAge>=.65f&&(!desert||trackAges[3]>=3.2f))Cancel();
        else if(finishAge<0f&&elapsed>2f)Cancel();
    }
}

