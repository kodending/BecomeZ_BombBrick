using System.Collections.Generic;
using UnityEngine;

// Shared artwork for every combat HUD entry; enum names match the imported PNGs.
public static class CombatHudIconArt
{
    public static readonly Color BuffAccent=new Color(0.30f,0.77f,0.70f,1f);
    public static readonly Color DebuffAccent=new Color(0.88f,0.44f,0.30f,1f);

    private static readonly Dictionary<string,Texture2D> textures=
        new Dictionary<string,Texture2D>();

    public static Texture2D Get(CombatStatusKind kind)=>Load(kind.ToString());
    public static Texture2D Get(CombatEffectIconKind kind)=>Load(kind.ToString());

    public static void DrawRegionAffinity(Rect rect,float multiplier)
    {
        if(Mathf.Approximately(multiplier,1f))return;
        bool strong=multiplier>1f;
        Color accent=strong?BombBrickUiTheme.Lime:BombBrickUiTheme.Red;
        rect=BombBrickUiTheme.PixelRect(rect);
        BombBrickUiTheme.Fill(rect,BombBrickUiTheme.Surface);
        BombBrickUiTheme.Frame(rect,accent,1f);
        float pixel=Mathf.Max(1f,Mathf.Floor(rect.width/7f));
        for(int row=0;row<3;row++)
        {
            float width=(row*2+1)*pixel;
            float y=rect.center.y+(strong?row-3:2-row)*pixel;
            BombBrickUiTheme.Fill(new Rect(
                rect.center.x-width*.5f,y,width,pixel),accent);
        }
        BombBrickUiTheme.Fill(new Rect(rect.center.x-pixel*.5f,
            rect.center.y+(strong?0f:-3f)*pixel,pixel,3f*pixel),accent);
    }

    public static Color Accent(CombatStatusKind kind)=>
        CombatStatusState.IsHarmful(kind)?DebuffAccent:BuffAccent;

    public static Color Accent(CombatEffectIconKind kind)
    {
        // Polarity is relative to the affected unit, never its team or the caster.
        switch(kind)
        {
            case CombatEffectIconKind.Ice:
            case CombatEffectIconKind.DefenseDown:
            case CombatEffectIconKind.ResistanceDown:
            case CombatEffectIconKind.AttackDown:
            case CombatEffectIconKind.HealingDown:
            case CombatEffectIconKind.DeathMark:
            case CombatEffectIconKind.Hunted:
            case CombatEffectIconKind.ArmorBroken:
            case CombatEffectIconKind.DeepFang:
            case CombatEffectIconKind.Capture:
            case CombatEffectIconKind.AttackSpeedDown:
            case CombatEffectIconKind.MoveSpeedDown:
            case CombatEffectIconKind.Stun:
            case CombatEffectIconKind.ChorusMark:
            case CombatEffectIconKind.SporeMark:
            case CombatEffectIconKind.Taunted:
            case CombatEffectIconKind.DamageAmplified:
                return DebuffAccent;
            default:return BuffAccent;
        }
    }

    private static Texture2D Load(string name)
    {
        if(!textures.TryGetValue(name,out Texture2D texture)||texture==null)
        {
            texture=BombBrick.Game.Core.GameResourceCache.GetTexture("UI/CombatStatusIcons/"+name);
            textures[name]=texture;
        }
        return texture;
    }
}
