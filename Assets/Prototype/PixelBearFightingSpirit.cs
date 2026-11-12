using System;
using UnityEngine;

// Reuse the bear's current sprite: four thin edges, one release silhouette and two arm echoes.
[DefaultExecutionOrder(10010)]
public sealed class PixelBearFightingSpirit:MonoBehaviour
{
    private CombatSkillClock skillClock=CombatSkillClock.Capture();
    private readonly SpriteRenderer[] edges=new SpriteRenderer[4],echoes=new SpriteRenderer[2];
    private static readonly Vector2[] Directions={Vector2.left,Vector2.right,Vector2.up,Vector2.down};
    private Transform source;
    private SpriteRenderer reference,release;
    private SpriteMonsterAnimationDriver animationDriver;
    private Func<bool> valid,active;
    private Material material;
    private float phaseAge,attackAge=1f;
    private int phase; // prepare, active, fading
    private bool mountain;
    private int ragingStrike=-1;
    private Vector3 ragingPawPosition,ragingPawPush;

    public static PixelBearFightingSpirit Create(Transform source,SpriteRenderer reference,Func<bool> valid,Func<bool> active)
    {
        if(source==null||reference==null)return null;
        var effect=new GameObject("Bear Fighting Spirit Silhouette").AddComponent<PixelBearFightingSpirit>();
        effect.source=source;effect.reference=reference;effect.valid=valid;effect.active=active;
        effect.animationDriver=reference.GetComponent<SpriteMonsterAnimationDriver>();
        effect.material=new Material(Resources.Load<Shader>("CombatEffects/Bear/FightingSpiritSilhouette"))
            {name="Bear Spirit Silhouette",hideFlags=HideFlags.HideAndDontSave};
        for(int i=0;i<4;i++)effect.edges[i]=effect.Make("Spirit Edge "+i);
        effect.release=effect.Make("Spirit Release Silhouette");
        for(int i=0;i<2;i++)effect.echoes[i]=effect.Make("Spirit Arm Echo "+i);
        effect.Prepare();return effect;
    }

    private SpriteRenderer Make(string name)
    {
        var r=new GameObject(name).AddComponent<SpriteRenderer>();r.transform.SetParent(transform,false);
        r.sharedMaterial=material;r.enabled=false;return r;
    }
    private void Snapshot(SpriteRenderer r,bool arms=false)
    {
        r.sprite=reference.sprite;r.flipX=reference.flipX;r.flipY=reference.flipY;
        r.transform.SetPositionAndRotation(source.position,source.rotation);
        r.transform.localScale=source.lossyScale;
        if(arms&&r.sprite!=null)
        {
            var rect=r.sprite.rect;var texture=r.sprite.texture;
            var properties=new MaterialPropertyBlock();
            // A custom block must retain the per-renderer sprite texture binding.
            properties.SetTexture("_MainTex",texture);
            properties.SetFloat("_ArmsOnly",mountain?2f:ragingStrike>=0?3f:1f);
            properties.SetVector("_SpriteUV",new Vector4(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height));
            r.SetPropertyBlock(properties);
            if(mountain)
            {
                var palm=new Vector3((rect.width*.67f-r.sprite.pivot.x)/r.sprite.pixelsPerUnit,
                    (rect.height*.35f-r.sprite.pivot.y)/r.sprite.pixelsPerUnit,0f);
                if(r.flipX)palm.x=-palm.x;
                if(r.flipY)palm.y=-palm.y;
                r.transform.localScale*=1.8f;
                r.transform.position-=source.TransformVector(palm)*.8f;
            }
            else if(ragingStrike>=0)
            {
                float facing=reference.flipX?-1f:1f;
                float size=ragingStrike==2?2.6f:1.85f;
                var palm=new Vector3((rect.width*.67f-r.sprite.pivot.x)/r.sprite.pixelsPerUnit*facing,
                    (rect.height*.35f-r.sprite.pivot.y)/r.sprite.pixelsPerUnit,0f);
                r.transform.localScale*=size;
                r.transform.position-=source.TransformVector(palm)*(size-1f);
                r.transform.position+=source.right*facing*reference.bounds.size.x*.065f;
            }
            else r.transform.position+=source.right*(reference.flipX?1f:-1f)*reference.bounds.size.x*.09f;
        }
    }
    public void Prepare()
    {
        phase=0;phaseAge=0f;attackAge=1f;release.enabled=false;
        foreach(var echo in echoes)echo.enabled=false;
        Sync();
    }
    public void Ignite(bool giant=false)
    {
        phase=1;phaseAge=0f;mountain=giant;
        Sync();
    }
    public void PlayAttackEcho(int strike=-1,Vector3? targetPoint=null)
    {
        if(phase!=1)return;
        attackAge=0f;ragingStrike=strike;
        if(strike>=0)
        {
            // Keep one actual contact paw, ahead of the body; never rotate whole-body cutouts.
            Snapshot(echoes[0],true);
            ragingPawPosition=echoes[0].transform.position;
            Vector3 direction=targetPoint.HasValue?targetPoint.Value-reference.bounds.center:
                source.right*(reference.flipX?-1f:1f);
            ragingPawPush=direction.normalized*reference.bounds.size.y*(strike==2?.14f:.09f);
            echoes[1].enabled=false;
            Sync();
        }
    }
    public void Finish(){if(CombatEffectEndPlayback.IsPreserving(this)||phase==2)return;phase=2;phaseAge=0f;}
    public void Cancel()
    {
        // Null-conditional calls can still reach a destroyed Unity component.
        if(this==null)return;
        if(CombatEffectEndFade.IsFading(this))return;
        ReleaseMaterial();if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);
    }
    private void LateUpdate()
    {
        using(var scope=skillClock==null?default(CombatSkillClock.Scope):CombatSkillClock.Enter(skillClock))
        {
            if(CombatSkillClock.Active&&(skillClock==null?Time.deltaTime:skillClock.DeltaTime)<=0f)return;
            if(source==null||reference==null||!source.gameObject.activeInHierarchy||(valid!=null&&!valid())){Cancel();return;}
            if(phase==1&&active!=null&&!active())Finish();
            for(int i=0;i<echoes.Length;i++)
                if(ragingStrike<0&&phase==1&&attackAge<=i*.05f&&attackAge+(skillClock==null?Time.deltaTime:skillClock.DeltaTime)>i*.05f)Snapshot(echoes[i],true);
            attackAge+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);phaseAge+=(skillClock==null?Time.deltaTime:skillClock.DeltaTime);
            if(phase==2&&phaseAge>=.3f){Cancel();return;}
            Sync();
        }
    }
    private void Sync()
    {
        if(reference==null||reference.sprite==null)return;
        float fade=phase==2?1f-Mathf.Clamp01(phaseAge/.3f):1f;
        float pulse=.5f+.5f*Mathf.Sin(phaseAge*4f);
        float strength=phase==0?Mathf.Lerp(.15f,.85f,Mathf.Clamp01(phaseAge/BearCombatRules.PreparationDuration)):
            Mathf.Lerp(.35f,.65f,pulse)*fade;
        if(mountain)strength=Mathf.Min(.95f,strength*1.4f);
        bool avatar=reference.sprite.name.StartsWith("OriginalBear_",StringComparison.Ordinal);
        bool charging=animationDriver!=null&&animationDriver.BearAvatarRoarCharging;
        bool roarImpact=animationDriver!=null&&animationDriver.BearAvatarRoarImpact;
        if(charging||roarImpact)strength=roarImpact?1f:.85f;
        float pixel=(mountain?2f:1f)/reference.sprite.pixelsPerUnit;
        for(int i=0;i<edges.Length;i++)
        {
            var edge=edges[i];Snapshot(edge);
            edge.transform.position+=source.TransformVector((Vector3)Directions[i]*pixel);
            Show(edge,roarImpact?new Color(.87f,1f,1f,strength):
                avatar?new Color(.4f,.91f,1f,strength):new Color(1f,.78f,.32f,strength),-1);
        }
        float burst=Mathf.Clamp01(phaseAge/.34f),expansion=1f+.8f*(1f-Mathf.Pow(1f-burst,3f));
        Snapshot(release);
        release.transform.localScale=source.lossyScale*expansion;
        if(charging||roarImpact)release.transform.localScale=source.lossyScale*(roarImpact?1.06f:1.015f);
        Show(release,charging||roarImpact?new Color(.55f,.96f,1f,roarImpact?.38f:.14f):
            new Color(1f,.82f,.47f,phase==1?.65f*Mathf.Pow(1f-burst,1.5f):0f),-4);
        for(int i=0;i<echoes.Length;i++)
        {
            if(ragingStrike>=0)
            {
                float duration=ragingStrike==2?.23f:.14f;
                float progress=Mathf.Clamp01(attackAge/duration);
                if(i==0)echoes[i].transform.position=ragingPawPosition+
                    ragingPawPush*(1f-Mathf.Pow(1f-Mathf.Clamp01(attackAge/.065f),3f));
                float opacity=i==0?Mathf.Pow(1f-progress,1.15f)*.92f*fade:0f;
                Show(echoes[i],new Color(1f,.88f,.51f,opacity),3);
                continue;
            }
            float t=(attackAge-i*.05f)/.2f;
            Show(echoes[i],new Color(1f,.85f,.55f,t>=0f&&t<1f?(mountain?.7f:.36f)*(1f-t)*fade:0f),-2-i);
        }
    }
    private void Show(SpriteRenderer r,Color color,int order)
    {
        r.enabled=reference.enabled&&color.a>.005f&&r.sprite!=null;
        color.a*=reference.color.a;r.color=color;
        r.sortingLayerID=reference.sortingLayerID;r.sortingOrder=reference.sortingOrder+order;
    }
    private void OnDestroy(){ReleaseMaterial();}
    private void ReleaseMaterial()
    {
        if(material==null)return;
        if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);
        material=null;
    }
}
