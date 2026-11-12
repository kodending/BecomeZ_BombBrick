using System.Collections.Generic;
using UnityEngine;
using GUI = LocalizedGUI;

public static class BombBrickUiTheme
{
    private static readonly Dictionary<int,GUIStyle> DisplayStyles=new Dictionary<int,GUIStyle>();
    private static readonly Dictionary<int,GUIStyle> TitleStyles=new Dictionary<int,GUIStyle>();
    private static readonly Dictionary<int,GUIStyle> BodyStyles=new Dictionary<int,GUIStyle>();
    private static readonly Dictionary<int,GUIStyle> CaptionStyles=new Dictionary<int,GUIStyle>();
    private static readonly Dictionary<int,GUIStyle> ClearButtonStyles=new Dictionary<int,GUIStyle>();
    private static readonly Dictionary<int,GUIStyle> PanelStyles=new Dictionary<int,GUIStyle>();
    private static readonly Dictionary<long,GUIStyle> CardStyles=new Dictionary<long,GUIStyle>();
    private static readonly Dictionary<int,GUIStyle> FramedButtonStyles=new Dictionary<int,GUIStyle>();
    private static readonly Dictionary<(Color,Color,int,bool),Texture2D> FrameTextures=
        new Dictionary<(Color,Color,int,bool),Texture2D>();
    private static GUISkin cachedSkin;
    private static Font cachedPixelFont;
    private static bool pixelFontLoaded;
    public static readonly Color Backdrop=new Color(0.006f,0.010f,0.008f,0.95f);
    public static readonly Color Surface=new Color(0.012f,0.017f,0.012f,0.97f);
    public static readonly Color SurfaceRaised=new Color(0.022f,0.030f,0.018f,0.98f);
    public static readonly Color SurfaceHover=new Color(0.040f,0.055f,0.030f,1f);
    public static readonly Color Border=new Color(0.18f,0.21f,0.12f,0.96f);
    private static readonly Color FrameDark=new Color(0.008f,0.012f,0.009f,1f);
    private static readonly Color FrameLight=new Color(0.46f,0.50f,0.31f,0.72f);
    public static readonly Color TextPrimary=new Color(0.93f,0.95f,0.84f);
    public static readonly Color TextSecondary=new Color(0.66f,0.70f,0.57f);
    public static readonly Color Cyan=new Color(0.22f,0.78f,0.73f);
    public static readonly Color Lime=new Color(0.70f,0.88f,0.28f);
    public static readonly Color Violet=new Color(0.60f,0.45f,0.88f);
    public static readonly Color Orange=new Color(0.96f,0.60f,0.22f);
    public static readonly Color Red=new Color(0.94f,0.32f,0.28f);

    public const float SpaceXs=4f;
    public const float SpaceSm=8f;
    public const float SpaceMd=16f;
    public const float SpaceLg=24f;
    public const float SpaceXl=32f;

    public static GUIStyle DisplayStyle(int size=24)
    {
        EnsureSkinCache();
        size=AccessibleFontSize(size,22);
        if(DisplayStyles.TryGetValue(size,out GUIStyle cached))return cached;
        GUIStyle style=BaseLabel(size,TextPrimary);
        style.alignment=TextAnchor.MiddleCenter;
        style.fontStyle=FontStyle.Bold;
        DisplayStyles[size]=style;
        return style;
    }

    public static GUIStyle TitleStyle(int size=20,TextAnchor alignment=TextAnchor.MiddleLeft)
    {
        EnsureSkinCache();
        size=AccessibleFontSize(size,14);
        int key=StyleKey(size,alignment);
        if(TitleStyles.TryGetValue(key,out GUIStyle cached))return cached;
        GUIStyle style=BaseLabel(size,TextPrimary);
        style.alignment=alignment;
        style.fontStyle=FontStyle.Bold;
        TitleStyles[key]=style;
        return style;
    }

    public static GUIStyle BodyStyle(int size=14,TextAnchor alignment=TextAnchor.UpperLeft)
    {
        EnsureSkinCache();
        size=AccessibleFontSize(size,12);
        int key=StyleKey(size,alignment);
        if(BodyStyles.TryGetValue(key,out GUIStyle cached))return cached;
        GUIStyle style=BaseLabel(
            size,new Color(0.88f,0.91f,0.96f));
        style.alignment=alignment;
        style.wordWrap=true;
        BodyStyles[key]=style;
        return style;
    }

    public static GUIStyle CaptionStyle(int size=12,TextAnchor alignment=TextAnchor.MiddleLeft)
    {
        EnsureSkinCache();
        size=AccessibleFontSize(size,11);
        int key=StyleKey(size,alignment);
        if(CaptionStyles.TryGetValue(key,out GUIStyle cached))return cached;
        GUIStyle style=BaseLabel(size,TextSecondary);
        style.alignment=alignment;
        style.fontStyle=FontStyle.Bold;
        CaptionStyles[key]=style;
        return style;
    }

    public static GUIStyle ClearButtonStyle(int size=14,bool bold=true,bool primary=false)
    {
        EnsureSkinCache();
        size=AccessibleFontSize(size,12);
        int key=size*4+(bold?2:0)+(primary?1:0);
        if(ClearButtonStyles.TryGetValue(key,out GUIStyle cached))return cached;
        GUIStyle style=new GUIStyle(GUIStyle.none)
        {
            alignment=TextAnchor.MiddleCenter,
            fontSize=size,
            fontStyle=bold?FontStyle.Bold:FontStyle.Normal,
            font=PixelFont,
            padding=new RectOffset(8,8,4,4)
        };
        style.normal.background=null;
        style.hover.background=null;
        style.active.background=null;
        style.focused.background=null;
        style.normal.textColor=primary?new Color(0.08f,0.11f,0.08f):TextPrimary;
        style.hover.textColor=Color.white;
        style.active.textColor=Color.white;
        style.focused.textColor=TextPrimary;
        ClearButtonStyles[key]=style;
        return style;
    }

    public static void DrawModalBackdrop()
    {
        Fill(new Rect(0f,0f,
            BombBrickUiLayout.ReferenceGuiActive?BombBrickUiLayout.Width:Screen.width,
            BombBrickUiLayout.ReferenceGuiActive?BombBrickUiLayout.Height:Screen.height),Backdrop);
    }

    public static void DrawPanel(Rect rect,bool raised=false)
    {
        rect=PixelRect(rect);
        Fill(new Rect(rect.x+4f,rect.y+4f,rect.width,rect.height),
            new Color(0f,0f,0f,0.52f));
        GUI.Box(rect,GUIContent.none,PanelStyle(raised));
    }

    public static void DrawCard(Rect rect,Color accent,bool selected,bool hovered)
    {
        rect=PixelRect(rect);
        float lift=selected||hovered?4f:0f;
        rect.y-=lift;
        Fill(new Rect(rect.x+4f,rect.y+4f,rect.width,rect.height),
            new Color(0f,0f,0f,0.48f));
        GUI.Box(rect,GUIContent.none,CardStyle(accent,selected,hovered));
        Fill(new Rect(rect.x,rect.y,4f,rect.height),selected?Lime:accent);
    }

    public static bool Button(
        Rect rect,string label,bool enabled=true,bool primary=false,
        int fontSize=13,float opacity=1f)
    {
        opacity=Mathf.Clamp01(opacity);
        if(opacity<=0f)return false;
        rect=PixelRect(rect);
        Fill(new Rect(rect.x+3f,rect.y+3f,rect.width,rect.height),
            new Color(0f,0f,0f,0.52f*opacity));
        Color previousColor=GUI.color;
        GUI.color=new Color(
            previousColor.r,previousColor.g,previousColor.b,
            previousColor.a*opacity);
        bool previous=GUI.enabled;
        GUI.enabled=previous&&enabled;
        GUIStyle style=FramedButtonStyle(fontSize,true,enabled,primary);
        bool clicked=GUI.Button(rect,label,style);
        GUI.enabled=previous;
        GUI.color=previousColor;
        return clicked;
    }

    public static GUIStyle ButtonStyle(
        int size=12,bool enabled=true,bool primary=false)
    {
        return FramedButtonStyle(size,true,enabled,primary);
    }

    public static int AccessibleFontSize(
        int requested,int minimum,int screenHeight)
    {
        float scale=Mathf.Clamp(screenHeight/900f,0.95f,1.15f);
        return Mathf.Max(minimum,Mathf.RoundToInt(requested*scale));
    }

    private static int AccessibleFontSize(int requested,int minimum)
    {
        return AccessibleFontSize(requested,minimum,
            BombBrickUiLayout.ReferenceGuiActive?1080:Screen.height);
    }

    public static void Badge(Rect rect,string label,Color color)
    {
        rect=PixelRect(rect);
        Fill(rect,new Color(color.r*0.24f,color.g*0.24f,color.b*0.24f,0.96f));
        DrawPixelFrame(rect,new Color(color.r,color.g,color.b,0.72f));
        GUIStyle style=CaptionStyle(11,TextAnchor.MiddleCenter);
        Label(rect,label,style,Color.Lerp(color,Color.white,0.25f),FontStyle.Bold);
    }

    public static void Label(
        Rect rect,string text,GUIStyle style,Color color,
        FontStyle? fontStyle=null)
    {
        Color previousColor=style.normal.textColor;
        Color previousHoverColor=style.hover.textColor;
        FontStyle previousFontStyle=style.fontStyle;
        style.normal.textColor=color;
        // Static labels keep their authored color when the pointer crosses them.
        style.hover.textColor=color;
        if(fontStyle.HasValue)style.fontStyle=fontStyle.Value;
        GUI.Label(rect,text,style);
        style.normal.textColor=previousColor;
        style.hover.textColor=previousHoverColor;
        style.fontStyle=previousFontStyle;
    }

    public static void SectionHeading(Rect rect,string title,string eyebrow="")
    {
        if(!string.IsNullOrEmpty(eyebrow))
            GUI.Label(new Rect(rect.x,rect.y,rect.width,18f),eyebrow.ToUpperInvariant(),
                CaptionStyle(11,TextAnchor.MiddleCenter));
        GUI.Label(new Rect(rect.x,rect.y+14f,rect.width,32f),title,DisplayStyle());
        Fill(new Rect(rect.center.x-28f,rect.y+49f,56f,3f),Lime);
    }

    public static void DrawHoverTooltip(
        Vector2 pointer,string eyebrow,string title,string body,Color accent)
    {
        GUIStyle bodyStyle=BodyStyle(12);
        Rect panel=HoverTooltipRect(pointer,body,bodyStyle,
            BombBrickUiLayout.CurrentSafeArea());
        float bodyHeight=Mathf.Max(0f,panel.height-78f);
        DrawPanel(panel,true);
        Fill(new Rect(panel.x,panel.y,4f,panel.height),accent);
        GUI.Label(new Rect(panel.x+16f,panel.y+10f,panel.width-32f,16f),
            eyebrow,CaptionStyle(10));
        GUI.Label(new Rect(panel.x+16f,panel.y+27f,panel.width-32f,26f),
            title,TitleStyle(17));
        Fill(new Rect(panel.x+16f,panel.y+58f,panel.width-32f,1f),Border);
        GUI.Label(new Rect(panel.x+16f,panel.y+67f,panel.width-32f,bodyHeight),
            body,bodyStyle);
    }

    private static Rect HoverTooltipRect(
        Vector2 pointer,string body,GUIStyle bodyStyle,Rect safeArea)
    {
        float width=Mathf.Min(344f,safeArea.width-BombBrickUiLayout.Margin*2f);
        float bodyHeight=Mathf.Max(38f,Mathf.Ceil(bodyStyle.CalcHeight(
            new GUIContent(GameLocalization.Text(body)),width-32f)));
        return BombBrickUiLayout.PlacePopup(pointer,new Vector2(width,78f+bodyHeight),
            safeArea,18f);
    }

    public static void Fill(Rect rect,Color color)
    {
        rect=PixelRect(rect);
        if(rect.width<=0f||rect.height<=0f)return;
        Color previous=GUI.color;
        GUI.color=color;
        GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=previous;
    }

    public static void Frame(Rect rect,Color color,float thickness)
    {
        rect=PixelRect(rect);
        if(rect.width<=0f||rect.height<=0f)return;
        // Animated border colors must not allocate/cache a texture for each shade.
        float edge=Mathf.Clamp(Mathf.Round(thickness),1f,4f);
        Fill(new Rect(rect.x,rect.y,rect.width,edge),color);
        Fill(new Rect(rect.x,rect.yMax-edge,rect.width,edge),color);
        Fill(new Rect(rect.x,rect.y+edge,edge,rect.height-edge*2f),color);
        Fill(new Rect(rect.xMax-edge,rect.y+edge,edge,rect.height-edge*2f),color);
    }

    public static Rect PixelRect(Rect rect)
    {
        const float grid=2f;
        return new Rect(
            Mathf.Round(rect.x/grid)*grid,Mathf.Round(rect.y/grid)*grid,
            Mathf.Max(grid,Mathf.Round(rect.width/grid)*grid),
            Mathf.Max(grid,Mathf.Round(rect.height/grid)*grid));
    }

    private static void DrawPixelFrame(Rect rect,Color accent)
    {
        Frame(rect,FrameDark,3f);
        Frame(new Rect(rect.x+2f,rect.y+2f,rect.width-4f,rect.height-4f),accent,1f);
        Fill(new Rect(rect.x+6f,rect.y+2f,rect.width-12f,2f),FrameLight);
    }

    public static float EaseOutCubic(float t)
    {
        t=Mathf.Clamp01(t);
        return 1f-Mathf.Pow(1f-t,3f);
    }

    public static float EaseOutBack(float t,float overshoot=1.1f)
    {
        t=Mathf.Clamp01(t);
        float shifted=t-1f;
        return 1f+(overshoot+1f)*shifted*shifted*shifted+
            overshoot*shifted*shifted;
    }

    private static GUIStyle BaseLabel(int size,Color color)
    {
        GUIStyle style=new GUIStyle(GUI.skin.label)
        {
            fontSize=size,
            font=PixelFont,
            // Panel rectangles already include the text insets.
            padding=new RectOffset(0,0,0,0),
            clipping=TextClipping.Clip
        };
        style.normal.textColor=color;
        return style;
    }

    private static int StyleKey(int size,TextAnchor alignment)
    {
        return size*16+(int)alignment;
    }

    private static void EnsureSkinCache()
    {
        if(ReferenceEquals(cachedSkin,GUI.skin))return;
        cachedSkin=GUI.skin;
        DisplayStyles.Clear();
        TitleStyles.Clear();
        BodyStyles.Clear();
        CaptionStyles.Clear();
        ClearButtonStyles.Clear();
    }

    private static Font PixelFont
    {
        get
        {
            if(pixelFontLoaded)return cachedPixelFont;
            pixelFontLoaded=true;
            cachedPixelFont=Resources.Load<Font>(
                "UI/Fonts/Galmuri11-Bold");
            return cachedPixelFont;
        }
    }

    private static GUIStyle PanelStyle(bool raised)
    {
        int key=raised?1:0;
        if(PanelStyles.TryGetValue(key,out GUIStyle cached))return cached;
        GUIStyle style=new GUIStyle(GUIStyle.none)
        {
            border=new RectOffset(3,3,3,3)
        };
        style.normal.background=CreateFramedTexture(
            raised?SurfaceRaised:Surface,Border,3);
        PanelStyles[key]=style;
        return style;
    }

    private static GUIStyle CardStyle(Color accent,bool selected,bool hovered)
    {
        Color border=selected?Lime:
            (hovered?Color.Lerp(accent,Color.white,0.24f):Border);
        long key=(uint)ColorStyleKey(accent)|
                 ((long)(selected?1:0)<<32)|((long)(hovered?1:0)<<33);
        if(CardStyles.TryGetValue(key,out GUIStyle cached))return cached;
        GUIStyle style=new GUIStyle(GUIStyle.none)
        {
            border=new RectOffset(3,3,3,3)
        };
        style.normal.background=CreateFramedTexture(
            hovered?SurfaceHover:Surface,border,3);
        CardStyles[key]=style;
        return style;
    }

    private static GUIStyle FramedButtonStyle(
        int size,bool bold,bool enabled,bool primary)
    {
        size=AccessibleFontSize(size,12);
        int key=size*8+(bold?4:0)+(enabled?2:0)+(primary?1:0);
        if(FramedButtonStyles.TryGetValue(key,out GUIStyle cached))return cached;
        Color normalFill=!enabled?new Color(0.065f,0.073f,0.09f,0.72f):
            (primary?Lime:SurfaceRaised);
        Color hoverFill=primary?Color.Lerp(Lime,Color.white,0.12f):SurfaceHover;
        Color border=!enabled?new Color(0.2f,0.22f,0.27f,0.65f):
            (primary?Color.Lerp(Lime,Color.white,0.12f):Border);
        GUIStyle style=new GUIStyle(GUIStyle.none)
        {
            alignment=TextAnchor.MiddleCenter,
            fontSize=size,
            fontStyle=bold?FontStyle.Bold:FontStyle.Normal,
            font=PixelFont,
            padding=new RectOffset(6,6,3,3),
            border=new RectOffset(3,3,3,3)
        };
        style.normal.background=CreateFramedTexture(normalFill,border,3);
        style.hover.background=CreateFramedTexture(
            enabled?hoverFill:normalFill,border,3);
        style.active.background=CreateFramedTexture(
            enabled?Color.Lerp(hoverFill,FrameDark,0.18f):normalFill,border,3);
        style.focused.background=style.normal.background;
        Texture2D selectedBackground=CreateFramedTexture(
            primary?Lime:SurfaceHover,primary?Lime:Cyan,3);
        style.onNormal.background=selectedBackground;
        style.onHover.background=selectedBackground;
        style.onActive.background=selectedBackground;
        style.onFocused.background=selectedBackground;
        style.normal.textColor=primary&&enabled?new Color(0.08f,0.11f,0.08f):TextPrimary;
        style.hover.textColor=primary&&enabled?new Color(0.06f,0.08f,0.06f):Color.white;
        style.active.textColor=style.hover.textColor;
        style.focused.textColor=style.normal.textColor;
        style.onNormal.textColor=primary?new Color(0.06f,0.08f,0.06f):Color.white;
        style.onHover.textColor=style.onNormal.textColor;
        style.onActive.textColor=style.onNormal.textColor;
        style.onFocused.textColor=style.onNormal.textColor;
        FramedButtonStyles[key]=style;
        return style;
    }

    private static Texture2D CreateFramedTexture(
        Color fill,Color border,int borderPixels,bool darkOuter=true)
    {
        var key=(fill,border,borderPixels,darkOuter);
        if(FrameTextures.TryGetValue(key,out Texture2D cached))return cached;
        const int size=12;
        Texture2D texture=new Texture2D(
            size,size,TextureFormat.RGBA32,false,false)
        {
            name="BombBrick UI Frame",
            filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp,
            hideFlags=HideFlags.HideAndDontSave
        };
        Color32[] pixels=new Color32[size*size];
        Color32 fillPixel=fill;
        Color32 borderPixel=border;
        Color32 outerPixel=darkOuter?FrameDark:border;
        for(int y=0;y<size;y++)
        for(int x=0;x<size;x++)
        {
            int edge=Mathf.Min(Mathf.Min(x,size-1-x),Mathf.Min(y,size-1-y));
            pixels[y*size+x]=edge<borderPixels
                ?(darkOuter&&edge<Mathf.Max(1,borderPixels-1)?outerPixel:borderPixel)
                :fillPixel;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        FrameTextures[key]=texture;
        return texture;
    }

    private static int ColorStyleKey(Color color)
    {
        Color32 value=color;
        return value.r|(value.g<<8)|(value.b<<16)|(value.a<<24);
    }
}

public static class MonsterAbilityTooltipContent
{
    private static readonly Dictionary<string,string> ConciseTraitSummaries=
        new Dictionary<string,string>
        {
            {"trait_raptor_blood_trail","목표를 관통하며 이동 중 기본공격 사거리 안의 모든 적에게 도약 피해 1회; 반대편 착지 후 기존 목표 공격."},
            {"trait_raptor_chain_leap","도약 후 같은 적에게 0.4초간 60%·60%·100% 위력의 3연격. 대상 사망 시 종료; 기본공격 거리로 복귀."},
            {"trait_raptor_pack_target","도약 적중 후 4초간 대상 방어력 50% 감소. 중첩 없이 지속시간 갱신; 기본공격 거리로 복귀 후 같은 적 공격."},
            {"trait_sabertooth_deep_fang_mark","엄니 고정 중과 종료 후 1.5초간 침묵. 새 스킬만 봉쇄하며 기본공격·이미 발동한 스킬은 유지."},
            {"trait_sabertooth_relentless_fang","1초간 적을 아군 진영으로 최대 1.5타일 끌어온 뒤, 3타일 내 아군이 2.5초간 집중 공격. 도발 우선, 진행 중인 공격·스킬은 유지."},
            {"trait_sabertooth_capture_signal","엄니 고정 중 실제로 잃은 체력의 40%를 붙잡은 적에게 고정 피해로 전달. 보호막 흡수량·반사 피해 제외."},
            {"trait_frog_reflex_snatch","혀 작살 착지·회수 타격 시 적이 이미 채운 스킬 쿨타임 게이지의 50% 제거."},
            {"trait_frog_buff_thief","적중 시 버프 1개를 4초 강탈. 없으면 공격속도 15% 강탈; 보스는 50% 복제."},
            {"trait_frog_hunting_chorus","적을 높이 들어 0.15초간 공중에 멈춘 뒤 강하게 내려찍고 0.8초간 쓰러뜨림."},
            {"trait_crocodile_annihilation_spin","가장 가까운 다른 적 방향으로 최대 2타일 투척. 착지 시 마지막 타격, 주변 1타일 120% 피해·0.6초 쓰러짐. 투척 중 아군 추격 해제."},
            {"trait_crocodile_reverse_hell","데스 롤 회전마다 주 대상 주변 2타일 다른 적에게 80% 피해, 최대 2회. 주 대상 기본 피해 유지."},
            {"trait_crocodile_absolute_jaw","데스 롤 마지막 압착이 공격력 320%+잃은 체력 8% 피해를 주며 방어력 50% 무시. 첫 2타 유지, 전체 1초."},
            {"trait_kingkong_triple_earth_pulse","3연속 내려찍기. 기본 피해의 100%/70%/100%, 반경 2.2/2.7/3.2칸. 첫 타만 띄우기."},
            {"trait_kingkong_continental_fissure","고동 후 사방으로 반경 5칸 충격파. 0.35초에 확산하며 닿은 적마다 고동 피해 120%를 1회 적용."},
            {"trait_kingkong_world_overturn","0.45초 힘을 모아 반경 3칸에 고동 피해 200%를 한 번 가하고 1초 쓰러뜨림."},
            {"trait_wizard_mouse_milky_way_barrage","원본 별 적중마다 3타일 내 다른 가까운 적 최대 2명에게 ATK 42% 파편별을 곡선 발사. 최대 10개; 설쥐는 파편도 냉기 중첩."},
            {"trait_wizard_mouse_pentagram_annihilation","첫 적중 위치에 2.2타일 오망성. 완성 후 1초 간격으로 100% 피해 3회. 설쥐는 매회 냉기 1중첩."},
            {"trait_wizard_mouse_supernova_descent","별 5개를 하늘로 쏴 초신성 낙하. 2.5타일 내 250%, 주 대상 460% 피해. 0.75타일 당김·1초 쓰러짐."},
            {"trait_bear_mountain_colossus","투지 3초·최대 체력 15% 보호막·강제이동 면역. 전방 180도·1.8타일 공격. 체력·방어형: 주 대상 평타+위력 40%, 주변 위력 100%. 베르베르: 기존 평타×1.4·주변 ATK 100%. 철갑 추가 피해 유지."},
            {"trait_bear_raging_hundred_strikes","체력·방어형 3연타: 평타 70% / 평타 30%+위력 40% / 위력 150%. 베르베르: 기존 ATK 70%/70%/150%. 투지·고유 보너스는 첫 타에 한 번. 마지막 주변 위력 30%·주 대상 0.2초 경직."},
            {"trait_bear_king_of_beasts_roar","투지 시작: 3타일 내 스킬 위력 180% 피해·공속 20% 감소 4초. 종료: 위력 220% 피해·0.8초 쓰러짐. 투지 중 기본공격·위력 30% 휩쓸기와 고유 효과 유지."},
            {"trait_wolf_annihilation_triangle","잔상과 나란히 쇄도해 전방 3타일 폭을 끝까지 관통. 적마다 240% 피해 1회."},
            {"trait_wolf_celestial_spectral_pack","대상 주위에 분신 8개가 차례로 나타난 뒤 본체와 동시에 공격. 분신마다 공격력 30%, 본체 180% 피해. 본체 타격만 방어력 30% 무시."},
            {"trait_wolf_eclipse_pack","삼면 사냥 후 분신 2개가 4초간 동행. 평타 적중 시 각각 공격력 55%로 협공. 종료·재시전 시 제자리에서 Idle로 깜빡이다 반경 1.5타일 폭발 연출로 소멸(피해 없음)."},
            {"trait_ninja_cat_shadowless_cross_annihilation","통나무 바꿔치기 후 사방에서 70%×4회 베기(경로 50%). 뒤에서 220%+잃은 체력 12%, 방어 50% 무시. 공격 중 대상 지정 불가·강제이동 면역, 대상 사망 시 즉시 종료."},
            {"trait_ninja_cat_black_moon_great_hunt","주 대상 주변 3.5칸 최대 6명을 순회해 50% 피해와 달 표식. 납도 시 표식이 동시에 폭발해 각 100%, 주 대상에 추가 180%. 공격 중 대상 지정 불가·강제이동 면역. 주 대상 사망 후에도 남은 표식 폭발."},
            {"trait_ninja_cat_eight_gate_explosion_formation","기폭찰 쿠나이 8개 설치 후 0.35초 수인. 동시 쇄도로 경로 60% 피해(적당 최대 3회), 중심 2.5칸에 240% 폭발·0.8초 쓰러짐. 설치 위치 고정, 주 대상 사망 후에도 발동. 소실부터 폭발까지 대상 지정 불가·강제이동 면역."},
            {"trait_tyranno_mass_extinction_tremor","1.2초 준비·받는 피해 50% 감소·강제이동 면역. 중심 2타일 위력 420%·1초 쓰러짐, 바깥 3.5타일 위력 300%·0.75초 쓰러짐. 한 번만 적중. 고유 효과 유지."},
            {"trait_tyranno_tyrant_great_march","0.6초 준비 후 1초간 최대 4타일 행진. 경로 충돌 위력 200% 1회, 마지막 반경 2타일 위력 260%·0.8초 쓰러짐. 둘 다 맞으면 위력 460%. 고유 효과 유지."},
            {"trait_tyranno_primordial_great_maw","3타일 내 포착한 적을 0.35초간 최대 0.25타일 당긴 후 닫히는 턱에 위력 120%. 실제 2타일 안은 위력 300%·0.8초 쓰러짐 추가. 물기 방어 30% 무시·잃은 체력 10% 추가(위력 150% 상한)."},
            {"trait_monkey_hwagwasan_collapse","45%×4연투 후 대왕 열매가 두 번 튀며 반경 1타일에 각 60%, 마지막 반경 2타일에 120% 폭발. 적을 향해 최대 1타일씩 이동."},
            {"trait_monkey_celestial_five_orb_juggling","열매 5개를 화면 밖으로 발사한 뒤 고정 지점에 수직 포격. 반경 1.5타일에 75%×4회, 마지막 반경 2타일에 150%. 그림자로 착탄 예고. 주 대상 사망 후에도 낙하."},
            {"trait_monkey_myriad_streams_return","바나나 5개를 대상 방향 ±30° 무작위로 던져 5타일을 왕복 관통. 왕로 20%×5, 귀환 40%×4·마지막 90%. 적마다 왕복 최대 10회·350%. 적을 추적하지 않고 시전자에게 귀환."},
            {"trait_mantis_divine_guillotine","도약 후 길이 6·폭 1.25타일 X자 절단을 내려찍어 각 180% 피해. 두 절단 적중 시 3초간 출혈."},
            {"trait_mantis_myriad_eye_predation","작은 낫 7개씩 두 번 펼쳐 쏘아 7타일 관통. 묶음당 주대상 160%, 나머지 100% 피해. 두 번 적중 시 3초간 출혈."},
            {"trait_mantis_reaper_emergence","첫 스킬 후 성체 변신: 공격 +30%, 공속·이속 +25%. 양팔 60%×2 기본공격과 최대 4타일 관통 비행·120%×2 절단. 두 절단 적중 시 출혈."},
            {"trait_mushroom_subterranean_mycelial_kingdom","2타일·4초 가스 유지. 가스 안 적에게 아군 기본 공격 적중 시 0.08초 후 1타일 내 가스 안 적에게 시전자 공격력×40% 폭발. 시전자별 0.7초 간격·시전당 최대 6회. 처치 타격 발동, 지속 피해·폭발 재발동 없음. 종 고유효과 유지."},
            {"trait_mushroom_parasitic_crown","숙주를 따라가는 기생 버섯이 3초 성장. 1초마다 공격력×50% 침식 3회 후, 숙주 중심 1.5타일에 240% 개화 1회(기본 총 390%). 숙주 사망 시 즉시 개화하고 남은 침식 종료."},
            {"trait_mushroom_mycoking_descent","8초간 뿌리내린 균왕 소환(시전자 최대 체력 40%, 파괴 가능). 등장 시 2.5타일에 공격력×80%, 2초마다 4회 가스 분출. 가스는 2초간 0.5초마다 20% 피해(기본 총 400%). 균왕 파괴 시 가스 종료. 최대 1개, 종 고유효과 유지."},
            {"trait_mammoth_annihilation_plow","상아로 바위를 들어 5타일까지 굴린다. 적마다 스킬 위력 220% 관통 피해 1회. 끝에서 부서져 반경 1.5타일에 140% 추가 피해. 개체별 계수·고유 효과 유지."},
            {"trait_mammoth_ice_age_wall","전방의 벽 3개를 부수며 3.5타일 돌진한다. 벽마다 반경 1.5타일에 스킬 위력 100% 피해(최대 300%). 개체별 계수·고유 효과 유지."},
            {"trait_mammoth_primordial_migration","3초간 주변 2.5타일 아군 피해 30% 분담·분담 피해 50% 감소. 종료 후 전방 2타일·120도에 스킬 위력 200% 후려치기. 중복 분담 불가."},
            {"trait_beetle_mass_theft","스킬 종료 후 6초간 평타가 전방 1.5타일을 휩쓴다. 주 대상은 평타 100%+스킬 위력 80%, 주변 적은 스킬 위력 80% 피해."},
            {"trait_beetle_golden_carapace_coffin","적을 띄우고 빠르게 따라 도약해 정점에서 멈춘 뒤 급강하한다. 주 대상 스킬 위력 60%+300% 피해·0.4초 쓰러짐. 착지 반경 1타일의 다른 적에게 120% 피해. 전체 동작 1.45초."},
            {"trait_beetle_zero_gravity_domain","주 대상 주변 반경 2타일의 적을 함께 띄워 1.5초 붙잡은 뒤 떨어뜨린다. 착지 시 스킬 위력 180% 피해. 추가 쓰러짐 없음. 시전자는 발동 동작 후 정상 전투."},
            {"trait_spirit_deer_life_tithe_decree","5초간 기본 공격 실제 피해의 35% 회복. 초과분은 최대 체력 8% 보호막(3초)."},
            {"trait_spirit_deer_inviolable_sanctuary","5초간 2.5타일 성역. 안에 있는 아군의 1회 체력 피해를 최대 체력 15%로 제한."},
            {"trait_spirit_deer_myriad_spirit_concert","5초간 2.5타일 성역. 아군 영혼이 기본 스킬 1회 복제: 위력·강화·제어 시간 50%. 원본 쿨타임 유지."},
            {"trait_griffin_typhoon_dive","화면 밖으로 상승 후 0.05초 급강하. 반경 2.5타일에 위력 280% 피해, 0.75타일 밀치기·0.6초 쓰러짐. 개체별 고유 효과 유지."},
            {"trait_griffin_raptor_hunt","7타일 내 공격력이 가장 높은 적 급습. 붙잡기 120% → 공중 상승·정점 정지 → 0.05초 내려꽂기 240%·0.6초 쓰러짐. 개체별 고유 효과 유지."},
            {"trait_griffin_royal_descent","기본 착지 유지. 착지 후 6초간 다음 기본 공격 3회에 위력 80% 관통 바람 추가(길이 3타일·폭 1.5타일). 개체별 착지 고유 효과 유지."},
            {"trait_wyvern_cataclysm_breath","0.5초 준비 후 전방 90°·4타일에 300% 피해 1회. 0.2초간 퍼짐. 기본 속성 효과 유지. 쿨타임 8.5초."},
            {"trait_wyvern_wing_sweep","주 대상 주변 4타일에 브레스탄 6발을 0.26초 간격으로 낙하. 그림자 예고 후 반경 1타일에 각 공격력 70% 피해. 속성 효과는 적당 1회."},
            {"trait_wyvern_dragon_core_bombardment","0.4초 준비 후 2초간 4타일 추적 브레스. 30→40→50→60→80→100% 피해, 방어력 20% 무시. 목표 사망 시 다른 적에게 이어 분사."},
            {"trait_haetae_heavenly_judgment_roar","0.45초 준비 → 반경 3.5타일 정화 파동. 적 위치에 0.35초 뒤 천벌(반경 0.6타일): 스킬 위력 240%·공격력 -20%(4초)·스킬 봉쇄 1초. 중복 피해 없음. 고유 효과는 기본 수치 적용. 금강수 피해는 자신의 최대 체력 13.2%."},
            {"trait_haetae_divine_beast_oppression","3타일 이동형 오라 5초. 1초 간격 50% 피해×5회(금강수: 자신의 최대 체력 2.75%×5). 범위 안 적 공속·이속 -25%, 아군 받는 피해 -20%. 생성 시 1회 정화·해타 ATK 60% 회복·금강수 자신의 최대 체력 8% 보호막(5초). 염해 화상·묵령 회복/새 보호막 약화는 적중 시 갱신. 유지 중 이동·기본 공격 가능."},
            {"trait_haetae_judgment_bellow","0.5초 준비 후 전방 4타일·90도 압력 포효. 공격력 280%(금강수: 자신의 최대 체력 15.4%) 피해, 최대 2타일 밀친 뒤 0.6초 쓰러짐. 주변 3타일 아군 정화. 개체별 고유 효과 유지."},
            {"trait_arachne_heavenly_net","반경 2.5칸에 160% 피해. 0.4초간 최대 1.25칸 당긴 뒤 1초 속박(이동·공격·스킬 불가). 4초간 매초 50%, 마지막 220% 파열 피해."},
            {"trait_arachne_myriad_cocoon_funeral","주 대상과 주변 2칸 내 가까운 적 2명에게 100% 피해·1.5초 고치 속박(이동·공격·스킬 불가). 0.5초마다 60%×3 압착 후 220% 파열. 각 고치 주변 1.25칸 내 다른 적에게 100% 피해(적마다 최대 2회)."},
            {"trait_arachne_eightfold_severing_web","주 대상 주변 2.5칸에 0.25초 준비 후 0.12초 간격으로 90%×4 광역 관통. 마지막 여덟 실뿔이 동시에 관통해 240% 피해(방어력 30% 무시). 실뿔은 몸통을 통과해 바닥에 꽂힘. 총 600%. 공통 당김·쓰러짐 없음."}
        };

    public static PersonalityMasterRow Personality(string id)
    {
        return GameMasterData.Personality(id);
    }

    public static string PersonalitySummary(PersonalityMasterRow personality)
    {
        if(personality==null||string.IsNullOrWhiteSpace(personality.behaviorSummary))
            return GameLocalization.TextByKey(
                "tooltip.personality.default","전투 대상 선택 방식.");
        return GameLocalization.TextByKey(
            "personality."+personality.id+".desc",personality.behaviorSummary);
    }

    public static TraitMasterRow Trait(string id)
    {
        GameMasterDatabase database=GameMasterData.Database;
        return database==null||database.traits==null?null:
            database.traits.Find(row=>row!=null&&row.enabled&&row.id==id);
    }

    public static string SkillSummary(SkillMasterRow skill)
    {
        if(skill==null)return GameLocalization.Text("등록된 스킬 설명이 없습니다.");
        string effect=string.IsNullOrWhiteSpace(skill.damageFormulaNote)
            ?GameLocalization.TextByKey(
                "tooltip.skill.default","전투 중 고유 스킬 효과를 발동합니다.")
            :skill.damageFormulaNote;
        effect=string.IsNullOrWhiteSpace(skill.id)
            ?GameLocalization.Text(effect)
            :GameLocalization.TextByKey("skill."+skill.id+".description",effect);
        string timing=GameLocalization.Format(
            "tooltip.skill.cooldown","쿨 {0}초",skill.baseCooldown.ToString("0.#"));
        if(skill.castRangeTiles>0f)
            timing+="  ·  "+GameLocalization.Format(
                "tooltip.skill.range","사거리 {0}칸",
                skill.castRangeTiles.ToString("0.#"));
        if(skill.radiusTiles>0f)
            timing+="  ·  "+GameLocalization.Format(
                "tooltip.skill.area","범위 {0}칸",skill.radiusTiles.ToString("0.#"));
        return effect+"\n"+timing;
    }

    public static string TraitSummary(TraitMasterRow trait)
    {
        if(trait==null)return GameLocalization.Text("등록된 특성 설명이 없습니다.");
        string concise;
        if(ConciseTraitSummaries.TryGetValue(trait.id??string.Empty,out concise))
            return GameLocalization.TextByKey(trait.id+".concise",concise);
        string effect=string.IsNullOrWhiteSpace(trait.effectSummary)
            ?GameLocalization.TextByKey(
                "tooltip.trait.default","전투 중 고유한 효과를 발휘합니다.")
            :trait.effectSummary;
        effect=string.IsNullOrWhiteSpace(trait.id)
            ?GameLocalization.Text(effect)
            :GameLocalization.TextByKey(trait.id+".effect",effect);
        return string.IsNullOrWhiteSpace(trait.triggerRule)
            ?effect
            :effect+"\n"+GameLocalization.Format(
                "tooltip.trait.condition","조건  {0}",
                GameLocalization.Text(trait.triggerRule));
    }

    public static string TraitCategory(TraitMasterRow trait)
    {
        if(trait==null)return "특성";
        switch(trait.category)
        {
            case "Intrinsic": return "고유 특성";
            case "Rank2Enhancement": return "2등급 특성";
            case "Rank4Transcendence": return "4등급 특성";
            default: return "3등급 특성";
        }
    }
}
