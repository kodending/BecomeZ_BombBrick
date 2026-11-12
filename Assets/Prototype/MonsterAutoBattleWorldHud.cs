using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private void DrawFearIndicator(Rect healthBar,Unit unit)
    {
        if(battleEnding||!IsCombatActive(unit)||!unit.combatStatuses.Has(CombatStatusKind.Fear))return;
        Texture2D icon=CombatHudIconArt.Get(CombatStatusKind.Fear);
        if(icon==null)return;
        float clock=Time.time;
        Rect rect=FearIndicatorRect(healthBar,combatHudIcons.Count,RankUpPresentationStarCount(unit,out _)>0,clock,unit.id);
        Matrix4x4 previousMatrix=GUI.matrix;
        Color previousColor=GUI.color;
        try
        {
            UnityEngine.GUIUtility.RotateAroundPivot(Mathf.Sin(clock*18f+unit.id)*7f,rect.center);
            GUI.color=new Color(.87f,.77f,1f,1f);
            GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit,true);
        }
        finally { GUI.matrix=previousMatrix;GUI.color=previousColor; }
    }

    private static Rect FearIndicatorRect(Rect healthBar,int iconCount,bool hovered,float clock,int id)
    {
        float top=iconCount>0?CombatHudIconRect(healthBar,iconCount-1,iconCount).yMin:healthBar.yMin;
        if(hovered)top=(iconCount>0?top-22f:RankStarsRect(healthBar).yMin);
        return new Rect(healthBar.center.x-12f+Mathf.Sin(clock*18f+id)*2f,
            top-32f+Mathf.Sin(clock*9f+id)*2f,24f,24f);
    }

    private bool TryGetUnitWorldHudRect(Unit unit,out Rect bounds)
    {
        bounds=new Rect();
        if(unit==null||unit.spriteBillboard!=null&&unit.spriteBillboard.IsFullySubmerged)return false;
        SpriteRenderer renderer=RankUpHudRenderer(unit);
        bool evolving=unit.alive&&renderer!=null&&renderer!=Rank3Renderer(unit)&&renderer.enabled;
        if(!ShouldShowWorldHealthBar(IsCombatActive(unit),unit.inStorage,unit==hoveredUnit,
               unit.alive&&unit.model!=null&&unit.model.activeInHierarchy)&&!evolving)return false;
        if(renderer!=null)
            return TryGetSpriteBodyGuiRect(mainCamera,renderer,out bounds);
        return TryGetUnitGuiRect(unit,out bounds);
    }

    public static bool TryGetSpriteBodyGuiRect(Camera camera,SpriteRenderer sprite,out Rect bounds)
    {
        bounds=new Rect();
        if(camera==null||sprite==null||!sprite.enabled||!sprite.gameObject.activeInHierarchy||
           sprite.sprite==null)return false;
        // Reuse the visible-body cache warmed during loading; sprite canvases include large transparent margins.
        Bounds body=PixelMantisReaper.VisibleBody(sprite.sprite);
        Vector3 center=body.center;
        if(sprite.flipX)center.x=-center.x;
        if(sprite.flipY)center.y=-center.y;
        Vector3 min=center-body.extents,max=center+body.extents;
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity;
        float maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        {
            Vector3 screen=camera.WorldToScreenPoint(sprite.transform.TransformPoint(
                new Vector3(x==0?min.x:max.x,y==0?min.y:max.y,center.z)));
            if(screen.z<=0f)return false;
            Vector2 point=BombBrickUiLayout.ScreenToGuiPoint(new Vector2(screen.x,screen.y));
            minX=Mathf.Min(minX,point.x);minY=Mathf.Min(minY,point.y);
            maxX=Mathf.Max(maxX,point.x);maxY=Mathf.Max(maxY,point.y);
        }
        bounds=Rect.MinMaxRect(minX,minY,maxX,maxY);
        return true;
    }
}
