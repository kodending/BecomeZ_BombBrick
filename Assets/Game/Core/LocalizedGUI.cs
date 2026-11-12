using UnityEngine;

/// <summary>Text-localizing pass-through for the project's IMGUI screens.</summary>
public static class LocalizedGUI
{
    private static readonly GUIContent measuredContent = new GUIContent();
    public static bool changed { get => GUI.changed; set => GUI.changed = value; }
    public static Color color { get => GUI.color; set => GUI.color = value; }
    public static int depth { get => GUI.depth; set => GUI.depth = value; }
    public static bool enabled { get => GUI.enabled; set => GUI.enabled = value; }
    public static Matrix4x4 matrix { get => GUI.matrix; set => GUI.matrix = value; }
    public static GUISkin skin { get => GUI.skin; set => GUI.skin = value; }

    public static void Label(Rect position, string text) =>
        GUI.Label(position, GameLocalization.Text(text));
    public static void Label(Rect position, string text, GUIStyle style)
    {
        string localized = GameLocalization.Text(text);
        int originalSize = style.fontSize;
        style.fontSize = FittedFontSize(position, localized, style);
        GUI.Label(position, localized, style);
        style.fontSize = originalSize;
    }
    public static void Label(Rect position, GUIContent content) =>
        GUI.Label(position, Content(content));
    public static void Label(Rect position, GUIContent content, GUIStyle style)
    {
        GUIContent localized = Content(content);
        int originalSize = style.fontSize;
        style.fontSize = FittedFontSize(position, localized == null ? "" : localized.text, style);
        GUI.Label(position, localized, style);
        style.fontSize = originalSize;
    }
    public static void Label(Rect position, Texture image) => GUI.Label(position, image);
    public static void Label(Rect position, Texture image, GUIStyle style) =>
        GUI.Label(position, image, style);

    public static bool Button(Rect position, string text) =>
        WithClickSound(GUI.Button(position, GameLocalization.Text(text)));
    public static bool Button(Rect position, string text, GUIStyle style)
    {
        string localized = GameLocalization.Text(text);
        int originalSize = style.fontSize;
        style.fontSize = FittedFontSize(position, localized, style);
        bool clicked = GUI.Button(position, localized, style);
        style.fontSize = originalSize;
        return WithClickSound(clicked);
    }
    public static bool Button(Rect position, GUIContent content) =>
        WithClickSound(GUI.Button(position, Content(content)));
    public static bool Button(Rect position, GUIContent content, GUIStyle style)
    {
        GUIContent localized = Content(content);
        int originalSize = style.fontSize;
        style.fontSize = FittedFontSize(position, localized == null ? "" : localized.text, style);
        bool clicked = GUI.Button(position, localized, style);
        style.fontSize = originalSize;
        return WithClickSound(clicked);
    }
    public static bool Button(Rect position, Texture image) =>
        WithClickSound(GUI.Button(position, image));
    public static bool Button(Rect position, Texture image, GUIStyle style) =>
        WithClickSound(GUI.Button(position, image, style));

    private static bool WithClickSound(bool clicked)
    {
        if (clicked)
            BombBrick.Game.Core.GameAudio.Play(
                BombBrick.Game.Core.GameAudio.Cue.Click);
        return clicked;
    }

    public static void Box(Rect position, string text) =>
        GUI.Box(position, GameLocalization.Text(text));
    public static void Box(Rect position, string text, GUIStyle style)
    {
        string localized = GameLocalization.Text(text);
        int originalSize = style.fontSize;
        style.fontSize = FittedFontSize(position, localized, style);
        GUI.Box(position, localized, style);
        style.fontSize = originalSize;
    }
    public static void Box(Rect position, GUIContent content) => GUI.Box(position, Content(content));
    public static void Box(Rect position, GUIContent content, GUIStyle style)
    {
        GUIContent localized = Content(content);
        int originalSize = style.fontSize;
        style.fontSize = FittedFontSize(position, localized == null ? "" : localized.text, style);
        GUI.Box(position, localized, style);
        style.fontSize = originalSize;
    }
    public static void Box(Rect position, Texture image) => GUI.Box(position, image);
    public static void Box(Rect position, Texture image, GUIStyle style) =>
        GUI.Box(position, image, style);

    public static string TextField(Rect position, string text) => GUI.TextField(position, text);
    public static string TextField(Rect position, string text, GUIStyle style) =>
        GUI.TextField(position, text, style);

    public static void BeginGroup(Rect position) => GUI.BeginGroup(position);
    public static void EndGroup() => GUI.EndGroup();
    public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect) =>
        GUI.BeginScrollView(position, scrollPosition, viewRect);
    public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect,
        bool alwaysShowHorizontal, bool alwaysShowVertical) =>
        GUI.BeginScrollView(position, scrollPosition, viewRect, alwaysShowHorizontal, alwaysShowVertical);
    public static void EndScrollView() => GUI.EndScrollView();

    public static void DrawTexture(Rect position, Texture image) => GUI.DrawTexture(position, image);
    public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode) =>
        GUI.DrawTexture(position, image, scaleMode);
    public static void DrawTexture(Rect position, Texture image, ScaleMode scaleMode,
        bool alphaBlend) => GUI.DrawTexture(position, image, scaleMode, alphaBlend);
    public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords) =>
        GUI.DrawTextureWithTexCoords(position, image, texCoords);
    public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords,
        bool alphaBlend) => GUI.DrawTextureWithTexCoords(position, image, texCoords, alphaBlend);

    public static int FittedFontSize(Rect position, string text, GUIStyle style)
    {
        if ((!GameLocalization.IsEnglish && !BombBrickUiLayout.ReferenceGuiActive) ||
            style == null || style.fontSize <= 8 ||
            string.IsNullOrEmpty(text) || position.width <= 0f || position.height <= 0f)
            return style == null ? 0 : style.fontSize;

        int originalSize = style.fontSize;
        GUIContent content = measuredContent;
        content.text = text;
        for (int size = originalSize; size >= 8; size--)
        {
            style.fontSize = size;
            Vector2 sizeRequired = style.wordWrap ? Vector2.zero : style.CalcSize(content);
            bool fits = style.wordWrap
                ? style.CalcHeight(content, position.width) <= position.height + 0.5f
                : sizeRequired.x <= position.width + 0.5f && sizeRequired.y <= position.height + 0.5f;
            if (fits)
            {
                style.fontSize = originalSize;
                return size;
            }
        }
        style.fontSize = originalSize;
        return 8;
    }

    private static GUIContent Content(GUIContent source)
    {
        if (source == null || string.IsNullOrEmpty(source.text) && string.IsNullOrEmpty(source.tooltip))
            return source;
        string text = GameLocalization.Text(source.text);
        string tooltip = GameLocalization.Text(source.tooltip);
        return text == source.text && tooltip == source.tooltip
            ? source
            : new GUIContent(text, source.image, tooltip);
    }
}
