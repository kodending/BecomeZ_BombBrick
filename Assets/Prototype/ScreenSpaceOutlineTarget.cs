using UnityEngine;

[DefaultExecutionOrder(10000)]
public sealed class ScreenSpaceOutlineTarget : MonoBehaviour
{
    private static readonly int OutlineStrength=Shader.PropertyToID("_OutlineStrength");
    private static readonly int OutlineColor=Shader.PropertyToID("_OutlineColor");
    private static readonly Color DefaultColor=new Color(0.05f,0.92f,1f,1f);
    private static readonly Color EmphasizedColor=new Color(0.32f,1f,0.52f,1f);
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool outlined;

    private int[] originalLayers;
    private bool initialized;
    private bool emphasized;
    private Color color=DefaultColor;
    private MaterialPropertyBlock propertyBlock;
    private bool countedAsActive;

    public static int ActiveTargetCount { get; private set; }
    public static bool HasActiveTargets => ActiveTargetCount>0;

    public bool IsOutlined => outlined;
    public Renderer[] TargetRenderers
    {
        get
        {
            Initialize();
            return targetRenderers;
        }
    }

    private void Awake()
    {
        Initialize();
        ApplyLayer();
    }

    private void OnEnable()
    {
        Initialize();
        SyncActiveCount();
        ApplyStrength();
        ApplyLayer();
    }

    private void OnDisable()
    {
        SetCountedAsActive(false);
        if(initialized)SetRendererLayers(false);
    }

    private void OnDestroy()
    {
        SetCountedAsActive(false);
    }

    private void LateUpdate()
    {
        if(!outlined)return;
        ApplyStrength();
        ApplyLayer();
    }

    public void SetOutlined(bool value,bool emphasize=false)
    {
        SetOutlined(value,emphasize,emphasize?EmphasizedColor:DefaultColor);
    }

    public void SetOutlined(bool value,bool emphasize,Color outlineColor)
    {
        Initialize();
        if(outlined==value&&emphasized==emphasize&&color==outlineColor)return;
        outlined=value;
        emphasized=emphasize;
        color=outlineColor;
        SyncActiveCount();
        ApplyStrength();
        ApplyLayer();
    }

    private void SyncActiveCount()
    {
        SetCountedAsActive(outlined&&isActiveAndEnabled);
    }

    private void SetCountedAsActive(bool value)
    {
        if(countedAsActive==value)return;
        countedAsActive=value;
        ActiveTargetCount=Mathf.Max(0,ActiveTargetCount+(value?1:-1));
    }

    public void RefreshRenderers()
    {
        if(initialized)SetRendererLayers(false);
        initialized=false;
        Initialize();
        ApplyStrength();
        ApplyLayer();
    }

    private void Initialize()
    {
        if(initialized)return;
        if(targetRenderers==null||targetRenderers.Length==0)
            targetRenderers=GetComponentsInChildren<Renderer>(true);
        originalLayers=new int[targetRenderers.Length];
        for(int i=0;i<targetRenderers.Length;i++)
            originalLayers[i]=targetRenderers[i]!=null?targetRenderers[i].gameObject.layer:0;
        initialized=true;
    }

    private void ApplyLayer()
    {
        SetRendererLayers(outlined&&isActiveAndEnabled);
    }

    private void ApplyStrength()
    {
        if(propertyBlock==null)propertyBlock=new MaterialPropertyBlock();
        for(int i=0;i<targetRenderers.Length;i++)
        {
            Renderer renderer=targetRenderers[i];
            if(renderer==null)continue;
            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OutlineStrength,emphasized?1f:0.5f);
            propertyBlock.SetColor(OutlineColor,color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetRendererLayers(bool useOutlineLayer)
    {
        int outlineLayer=LayerMask.NameToLayer("ScreenSpaceOutline");
        for(int i=0;i<targetRenderers.Length;i++)
        {
            Renderer renderer=targetRenderers[i];
            if(renderer==null)continue;
            renderer.gameObject.layer=useOutlineLayer&&outlineLayer>=0?outlineLayer:originalLayers[i];
        }
    }
}
