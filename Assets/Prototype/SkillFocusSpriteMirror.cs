using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(10020)]
public sealed class SkillFocusSpriteMirror : MonoBehaviour
{
    private SpriteRenderer source;
    private SpriteRenderer foreground;
    private MaterialPropertyBlock properties;
    private SpriteRenderer[] gatherSprites;
    private double gatherStartedAt;
    private float gatherDuration;
    private Vector3 gatherBodyCenter;
    private bool spotlight=true;

    public void SetSpotlight(bool value)
    {
        spotlight=value;
        if(source!=null)LateUpdate();
    }

    public void Follow(SpriteRenderer renderer,float chargeDuration=.5f)
    {
        source=renderer;
        gatherDuration=Mathf.Max(0f,chargeDuration);
        gatherStartedAt=Time.unscaledTimeAsDouble;
        gatherBodyCenter=gatherDuration>0f&&renderer!=null&&renderer.sprite!=null
            ?PixelMantisReaper.VisibleBody(renderer.sprite).center:Vector3.zero;
        gameObject.SetActive(true);
        LateUpdate();
    }

    public void Clear()
    {
        source=null;
        HideVisuals();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        source=null;
        HideVisuals();
    }

    private void HideVisuals()
    {
        if(foreground!=null)foreground.enabled=false;
        if(gatherSprites==null)return;
        for(int i=0;i<gatherSprites.Length;i++)
            if(gatherSprites[i]!=null)gatherSprites[i].enabled=false;
    }

    private void LateUpdate()
    {
        if(foreground==null)foreground=GetComponent<SpriteRenderer>();
        if(source==null||!source.enabled||source.forceRenderingOff||!source.gameObject.activeInHierarchy||source.sprite==null)
        {HideVisuals();return;}
        // Do not raise the real caster: skill effects inherit its sorting order.
        foreground.enabled=true;
        foreground.sprite=source.sprite;
        foreground.color=source.color;
        foreground.flipX=source.flipX;foreground.flipY=source.flipY;
        foreground.sharedMaterial=source.sharedMaterial;
        foreground.sortingLayerID=source.sortingLayerID;
        foreground.sortingOrder=spotlight?30004:29996;
        if(properties==null)properties=new MaterialPropertyBlock();
        properties.Clear();source.GetPropertyBlock(properties);
        foreground.SetPropertyBlock(properties);
        transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        Vector3 size=source.transform.lossyScale;
        Vector3 parent=transform.parent==null?Vector3.one:transform.parent.lossyScale;
        transform.localScale=new Vector3(size.x/parent.x,size.y/parent.y,size.z/parent.z);
        UpdateGatherSprites();
    }

    private void UpdateGatherSprites()
    {
        if(gatherSprites==null)
        {
            gatherSprites=new SpriteRenderer[3];
            for(int i=0;i<gatherSprites.Length;i++)
            {
                var layer=new GameObject("Skill Focus Spread "+(i+1));
                layer.transform.SetParent(transform,false);
                layer.layer=0;
                gatherSprites[i]=layer.AddComponent<SpriteRenderer>();
                gatherSprites[i].sortingOrder=30001+i;
            }
        }
        double elapsed=Time.unscaledTimeAsDouble-gatherStartedAt;
        Vector3 center=gatherBodyCenter;
        if(source.flipX)center.x=-center.x;
        if(source.flipY)center.y=-center.y;
        int visibleCount=3;
        float opacityScale=1f;
        float fadeStart=.68f;
        if(CombatReadabilityPresentation.Active)
        {
            visibleCount=CombatReadabilityPresentation.HologramCount;
            opacityScale=CombatReadabilityPresentation.HologramOpacity;
        }
        if(CombatReadabilityPresentation.RefinedSkills)fadeStart=.9f;
        for(int i=0;i<gatherSprites.Length;i++)
        {
            SpriteRenderer ghost=gatherSprites[i];
            double delay=gatherDuration*i*.08d;
            double travelDuration=gatherDuration-delay;
            double age=elapsed-delay;
            ghost.enabled=i<visibleCount&&gatherDuration>0f&&age>=0d&&age<travelDuration;
            if(!ghost.enabled)continue;
            float t=Mathf.Clamp01((float)(age/travelDuration));
            float spread=Mathf.SmoothStep(0f,1f,t);
            ghost.sprite=source.sprite;
            ghost.flipX=source.flipX;ghost.flipY=source.flipY;
            ghost.sharedMaterial=source.sharedMaterial;
            ghost.sortingLayerID=source.sortingLayerID;
            ghost.sortingOrder=(spotlight?30001:29993)+i;
            ghost.SetPropertyBlock(properties);
            Color tint=new Color(.95f,.78f,.35f);
            float opacity=Mathf.SmoothStep(0f,1f,t/.18f)*
                (1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(fadeStart,1f,t)));
            tint.a=source.color.a*(.32f-i*.07f)*(CombatFlashPresentation.Reduced?.5f:1f)*opacity*opacityScale;
            ghost.color=tint;
            float endScale=1.42f+i*.14f;
            if(CombatReadabilityPresentation.Active)endScale=CombatReadabilityPresentation.HologramScale(i);
            float size=Mathf.Lerp(1.03f,endScale,spread);
            ghost.transform.localScale=Vector3.one*size;
            ghost.transform.localPosition=center*(1f-size);
        }
    }
}
