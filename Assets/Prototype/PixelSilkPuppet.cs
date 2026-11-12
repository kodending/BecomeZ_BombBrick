using System;
using UnityEngine;

// Reuses the monster's animated sprite and the existing luminance-preserving spirit shader.
[DefaultExecutionOrder(10020)]
public sealed class PixelSilkPuppet:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    public const float RiseDuration=.55f;
    public static readonly Color GhostColor=new Color(.69f,.68f,.98f,.7f);
    private SpriteRenderer body;
    private SpriteRenderer outline;
    private LineRenderer thread;
    private Material bodyMaterial,outlineMaterial,threadMaterial;
    private Func<bool> valid;
    private Vector3 restPosition,restScale;
    private float age;
    private bool departed;

    public static PixelSilkPuppet Create(SpriteRenderer body,Func<bool> valid)
    {
        if(body==null)return null;
        var fx=body.gameObject.AddComponent<PixelSilkPuppet>();fx.body=body;fx.valid=valid;
        fx.restPosition=body.transform.localPosition;fx.restScale=body.transform.localScale;
        var shader=Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette");
        fx.bodyMaterial=new Material(shader);fx.bodyMaterial.SetFloat("_KeepSpriteDetail",1f);
        body.sharedMaterial=fx.bodyMaterial;
        fx.outlineMaterial=new Material(shader);
        fx.outlineMaterial.SetFloat("_OutlineOnly",1f);
        var edge=new GameObject("Puppet Pixel Outline");edge.transform.SetParent(body.transform,false);
        fx.outline=edge.AddComponent<SpriteRenderer>();fx.outline.sharedMaterial=fx.outlineMaterial;
        fx.threadMaterial=new Material(Shader.Find("Sprites/Default"));
        var child=new GameObject("Puppet Single Ivory Thread");child.transform.SetParent(body.transform,false);
        fx.thread=child.AddComponent<LineRenderer>();fx.thread.sharedMaterial=fx.threadMaterial;
        fx.thread.positionCount=2;fx.thread.useWorldSpace=true;fx.thread.widthMultiplier=.018f;
        fx.thread.numCapVertices=0;fx.thread.numCornerVertices=0;
        fx.thread.startColor=new Color(1f,.96f,.8f,.85f);fx.thread.endColor=new Color(1f,.96f,.8f,0f);
        fx.Draw();return fx;
    }

    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(departed)return;
            if(valid!=null&&!valid()){gameObject.SetActive(false);return;}
            age+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);Draw();
        }
    }

    private void Draw()
    {
        float rise=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((age-.12f)/(RiseDuration-.12f)));
        // Crouched body is lifted by the strings, then settles onto its authored baseline.
        if(age<=RiseDuration+.05f)
        {
            body.transform.localScale=Vector3.Scale(restScale,new Vector3(1f,Mathf.Lerp(.38f,1f,rise),1f));
            body.transform.localPosition=restPosition+Vector3.up*Mathf.Sin(rise*Mathf.PI)*.14f;
        }
        body.color=Color.Lerp(Color.white,GhostColor,Mathf.Clamp01(age/.2f));
        outline.sprite=body.sprite;outline.flipX=body.flipX;outline.flipY=body.flipY;
        outline.sortingLayerID=body.sortingLayerID;outline.sortingOrder=body.sortingOrder-1;
        outline.color=new Color(.84f,.84f,1f,.55f*rise);outline.enabled=body.enabled;
        Bounds bounds=body.bounds;
        thread.sortingLayerID=body.sortingLayerID;thread.sortingOrder=body.sortingOrder+1;
        Vector3 from=bounds.center+Vector3.up*bounds.size.y*.36f;
        thread.SetPosition(0,from);
        thread.SetPosition(1,from+Vector3.up*Mathf.Max(.65f,bounds.size.y*.9f));
    }

    public void Depart()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(this==null||departed)return;
            if(CombatEffectEndFade.IsFading(this))return;
            departed=true;
            var soul=PixelBearAvatarDeparture.Create(body,valid,GhostColor);
            if(soul!=null)soul.name="Silk Puppet Unravelling";
            thread.enabled=false;
        }
    }

    private void OnDestroy()
    {
        foreach(var material in new[]{bodyMaterial,outlineMaterial,threadMaterial})
            if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
    }
}
