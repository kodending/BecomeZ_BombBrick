using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private void CaptureSpiritGuardianReturn(Rank4StageContext context,Unit source)
    {
        context.guardianReturnVisible=false;
        foreach(PixelSpiritGuardian guardian in spiritGuardians)
        {
            if(guardian==null||guardian.IsAllied!=(source.team==Team.Player))continue;
            guardian.CompleteCinematicEntrance();
            SpriteRenderer body=guardian.BodyRenderer;
            if(body==null||body.sprite==null)return;
            var snapshot=new Rank4StageContext();CaptureRank4ReturnBody(snapshot,body);
            context.guardianReturnPosition=snapshot.returnPosition;
            context.guardianReturnHeight=snapshot.returnHeight;
            context.guardianReturnSprite=body.sprite;
            context.guardianReturnColor=body.color;
            Material material=body.sharedMaterial;
            context.guardianReturnRearEdge=material!=null&&material.HasProperty("_RearEdge")?material.GetFloat("_RearEdge"):-1f;
            context.guardianReturnVisible=body.enabled&&!body.forceRenderingOff&&body.gameObject.activeInHierarchy;
            return;
        }
    }

    private void UpdateSpiritGuardianCinematicVisibility(Rank4Presentation presentation)
    {
        if(presentation.request.context.family!="spirit_deer")return;
        if(presentation.elapsed>=SpiritGuardianRank4Stage.FieldRevealTime)
        {
            presentation.spiritBattleVisibility?.Restore();
            presentation.spiritOppositeGuardianVisibility?.Restore();
        }
        else
        {
            if(presentation.spiritBattleVisibility==null)presentation.spiritBattleVisibility=new Rank4CinematicVisibility();
            if(presentation.spiritOppositeGuardianVisibility==null)presentation.spiritOppositeGuardianVisibility=new Rank4CinematicVisibility();
            foreach(Unit unit in units)
            {
                // Source sprites have their own restoration owner; its independent ring still needs hiding.
                if(unit!=presentation.request.source)presentation.spiritBattleVisibility.Track(unit.model);
                presentation.spiritBattleVisibility.Track(unit.ring);
            }
            foreach(PixelSpiritGuardian guardian in spiritGuardians)
                if(guardian!=null&&guardian.IsAllied!=(presentation.request.source.team==Team.Player))
                    presentation.spiritOppositeGuardianVisibility.Track(guardian.gameObject);
            presentation.spiritBattleVisibility.Hide();
            presentation.spiritOppositeGuardianVisibility.Hide();
        }
        if(presentation.elapsed>=SpiritGuardianRank4Stage.FieldReturnTime)
        {presentation.spiritGuardianVisibility?.Restore();return;}
        if(presentation.spiritGuardianVisibility==null)presentation.spiritGuardianVisibility=new Rank4CinematicVisibility();
        foreach(PixelSpiritGuardian guardian in spiritGuardians)
            if(guardian!=null&&guardian.IsAllied==(presentation.request.source.team==Team.Player))
                presentation.spiritGuardianVisibility.Track(guardian.gameObject);
        presentation.spiritGuardianVisibility.Hide();
    }
}
