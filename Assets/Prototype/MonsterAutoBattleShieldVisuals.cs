using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private void UpdateUnitShieldVisual(Unit unit)
    {
        if(mlEpisodeActive||!battleStarted||battleEnding||!IsCombatActive(unit))
        {ClearUnitShieldVisual(unit);return;}
        bool shielded=TotalVisibleShield(unit)>.01f;
        if(shielded)EnsureUnitShieldVisual(unit,false);
        unit.shieldVisual?.Pose(battleElapsed,shielded);
    }

    private void EnsureUnitShieldVisual(Unit unit,bool instant)
    {
        if(unit.shieldVisual!=null||mlEpisodeActive||!IsCombatActive(unit))return;
        var renderer=Rank3Renderer(unit);if(renderer==null)return;
        unit.shieldVisual=PixelUnitShield.Create(unit.model.transform,renderer,battleElapsed,instant);
        unit.shieldVisual.transform.SetParent(transform,true);
    }

    private void ShowUnitShieldHit(Unit source,Unit target)
    {
        EnsureUnitShieldVisual(target,true);
        if(target.shieldVisual==null)return;
        var renderer=Rank3Renderer(source);
        Vector3 from=renderer!=null?renderer.bounds.center:source!=null&&source.model!=null?
            source.model.transform.position:target.shieldVisual.Center+(Camera.main==null?Vector3.back:-Camera.main.transform.forward)*3;
        target.shieldVisual.Impact(from,battleElapsed);
    }

    private static void ClearUnitShieldVisual(Unit unit)
    {
        unit.shieldVisual?.Cancel();unit.shieldVisual=null;
    }
}
