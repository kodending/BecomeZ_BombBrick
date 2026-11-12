using UnityEngine;

public readonly struct BombBrickUiLayoutSnapshot
{
    public readonly Rect safeArea;
    public readonly Rect topBar;
    public readonly Rect runHud;
    public readonly Rect monsterDetails;
    public readonly Rect itemBar;
    public readonly Rect captureStatus;
    public readonly Rect developerToggle;
    public readonly Rect developerPanel;
    public readonly Rect traitPanel;

    public BombBrickUiLayoutSnapshot(
        Rect safeArea,Rect topBar,Rect runHud,Rect monsterDetails,Rect itemBar,
        Rect captureStatus,Rect developerToggle,Rect developerPanel,
        Rect traitPanel)
    {
        this.safeArea=safeArea;
        this.topBar=topBar;
        this.runHud=runHud;
        this.monsterDetails=monsterDetails;
        this.itemBar=itemBar;
        this.captureStatus=captureStatus;
        this.developerToggle=developerToggle;
        this.developerPanel=developerPanel;
        this.traitPanel=traitPanel;
    }
}

public static class BombBrickUiLayout
{
    public const float ReferenceWidth=1920f;
    public const float ReferenceHeight=1080f;
    public static float Scale=>ScaleForScreen(Screen.width,Screen.height);
    public static float Width=>Screen.width/Scale;
    public static float Height=>Screen.height/Scale;
    public static bool ReferenceGuiActive { get; private set; }

    public static float ScaleForScreen(int width,int height)=>
        Mathf.Max(0.01f,Mathf.Min(width/ReferenceWidth,height/ReferenceHeight));

    public static Vector2 ScreenToGuiPoint(Vector2 point,int width,int height)=>
        new Vector2(point.x,height-point.y)/ScaleForScreen(width,height);

    public static Vector2 ScreenToGuiPoint(Vector2 point)=>
        ScreenToGuiPoint(point,Screen.width,Screen.height);

    public static Vector2 GuiToScreenPoint(Vector2 point,int width,int height)
    {
        point*=ScaleForScreen(width,height);
        return new Vector2(point.x,height-point.y);
    }

    public readonly struct GuiScope : System.IDisposable
    {
        private readonly Matrix4x4 previous;
        private readonly bool wasActive;
        public GuiScope(bool enabled)
        {
            previous=GUI.matrix;
            wasActive=ReferenceGuiActive;
            if(!enabled)return;
            ReferenceGuiActive=true;
            GUI.matrix=previous*Matrix4x4.Scale(new Vector3(Scale,Scale,1f));
        }
        public void Dispose()
        {
            GUI.matrix=previous;
            ReferenceGuiActive=wasActive;
        }
    }

    public const float Margin=12f;
    public const float Gap=8f;
    public const float TopBarHeight=80f;
    public const float RunHudWidth=360f;
    public const float MonsterDetailsWidth=480f;
    public const float MonsterDetailsHeight=470f;
    public const float ItemBarMaximumWidth=920f;
    public const float ItemBarHeight=78f;
    public const float DeveloperPanelWidth=330f;
    public const float DeveloperPanelHeight=306f;
    public const float MinimumPointerTarget=36f;

    public static BombBrickUiLayoutSnapshot ForScreen(
        int screenWidth,int screenHeight,Rect systemSafeArea)
    {
        Rect safe=ToGuiSafeArea(screenWidth,screenHeight,systemSafeArea);
        float scale=ScaleForScreen(screenWidth,screenHeight);
        safe=new Rect(safe.position/scale,safe.size/scale);
        Rect topBar=new Rect(
            safe.x+Margin,safe.y+Margin,
            Mathf.Max(320f,safe.width-Margin*2f),TopBarHeight);
        float contentTop=topBar.yMax+Gap;
        float runWidth=Mathf.Min(RunHudWidth,Mathf.Max(280f,safe.width-Margin*2f));
        Rect run=new Rect(safe.x+Margin,contentTop,runWidth,154f);

        float detailsWidth=Mathf.Min(
            MonsterDetailsWidth,Mathf.Max(360f,safe.width-Margin*2f));
        Rect details=new Rect(
            safe.xMax-Margin-detailsWidth,contentTop,
            detailsWidth,MonsterDetailsHeight);

        float itemWidth=Mathf.Min(
            ItemBarMaximumWidth,Mathf.Max(320f,safe.width-Margin*2f));
        Rect item=new Rect(
            safe.center.x-itemWidth*0.5f,
            safe.yMax-Margin-ItemBarHeight,
            itemWidth,ItemBarHeight);

        float captureWidth=Mathf.Min(500f,safe.width-Margin*2f);
        Rect capture=new Rect(
            safe.center.x-captureWidth*0.5f,
            item.y-50f,captureWidth,42f);

        Rect developerToggle=new Rect(
            safe.xMax-Margin-54f,item.y-32f,54f,24f);
        float developerX=details.x-DeveloperPanelWidth-Gap;
        if(developerX<safe.x+Margin)
            developerX=safe.xMax-Margin-DeveloperPanelWidth;
        Rect developerPanel=new Rect(
            developerX,contentTop,
            Mathf.Min(DeveloperPanelWidth,safe.width-Margin*2f),
            DeveloperPanelHeight);

        float traitTop=Mathf.Max(run.yMax+Gap,safe.y+312f);
        float traitHeight=Mathf.Max(
            220f,item.y-traitTop-Gap);
        Rect traitPanel=new Rect(
            safe.x+Margin,traitTop,
            Mathf.Min(500f,safe.width-Margin*2f),traitHeight);

        return new BombBrickUiLayoutSnapshot(
            safe,topBar,run,details,item,capture,
            developerToggle,developerPanel,traitPanel);
    }

    public static Rect CurrentSafeArea()
    {
        Rect safe=ToGuiSafeArea(Screen.width,Screen.height,Screen.safeArea);
        return ReferenceGuiActive?new Rect(safe.position/Scale,safe.size/Scale):safe;
    }

    public static Rect AugmentTargetPanel(
        Rect safeArea,Rect details,int rosterCount,out int columns)
    {
        float right=Mathf.Min(safeArea.xMax-12f,details.x-12f);
        float availableWidth=Mathf.Max(420f,right-safeArea.x-24f);
        float width=Mathf.Min(780f,availableWidth);
        columns=width>=650f?4:3;
        int rows=Mathf.Max(1,Mathf.CeilToInt(rosterCount/(float)columns));
        float height=98f+rows*122f+(rows-1)*8f+58f;
        return ClampRect(new Rect(
            Mathf.Min(safeArea.center.x-width*0.5f,right-width),
            safeArea.center.y-height*0.5f,width,height),safeArea,12f);
    }

    public static Rect PlacePopup(
        Vector2 pointer,Vector2 size,Rect safeArea,float offset=16f)
    {
        float x=pointer.x+offset;
        if(x+size.x>safeArea.xMax-Margin)
            x=pointer.x-size.x-offset;
        float y=pointer.y+offset;
        if(y+size.y>safeArea.yMax-Margin)
            y=pointer.y-size.y-offset;
        return ClampRect(new Rect(x,y,size.x,size.y),safeArea,Margin);
    }

    public static Rect ClampRect(Rect rect,Rect safeArea,float padding=Margin)
    {
        float width=Mathf.Min(rect.width,Mathf.Max(1f,safeArea.width-padding*2f));
        float height=Mathf.Min(rect.height,Mathf.Max(1f,safeArea.height-padding*2f));
        float x=Mathf.Clamp(
            rect.x,safeArea.x+padding,
            Mathf.Max(safeArea.x+padding,safeArea.xMax-padding-width));
        float y=Mathf.Clamp(
            rect.y,safeArea.y+padding,
            Mathf.Max(safeArea.y+padding,safeArea.yMax-padding-height));
        return new Rect(x,y,width,height);
    }

    public static float InteractionDistance(
        float basePixels,int screenWidth,int screenHeight)
    {
        float scale=Mathf.Clamp(
            Mathf.Min(screenWidth/1400f,screenHeight/788f),1f,1.35f);
        return Mathf.Max(basePixels,basePixels*scale);
    }

    private static Rect ToGuiSafeArea(
        int screenWidth,int screenHeight,Rect systemSafeArea)
    {
        Rect source=systemSafeArea.width>0f&&systemSafeArea.height>0f
            ?systemSafeArea
            :new Rect(0f,0f,screenWidth,screenHeight);
        return new Rect(
            source.x,
            screenHeight-source.y-source.height,
            source.width,source.height);
    }
}
