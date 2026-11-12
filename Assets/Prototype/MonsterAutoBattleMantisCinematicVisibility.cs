using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class Rank4CinematicVisibility
    {
        private readonly HashSet<GameObject> tracked=new HashSet<GameObject>();
        private readonly Dictionary<Renderer,bool> original=new Dictionary<Renderer,bool>();

        public void Track(GameObject model)
        {
            if(model==null||!tracked.Add(model))return;
            foreach(Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                if(!original.ContainsKey(renderer))original.Add(renderer,renderer.forceRenderingOff);
        }

        public void Hide()
        {
            foreach(var entry in original)
                if(entry.Key!=null)entry.Key.forceRenderingOff=true;
        }

        public void Restore()
        {
            foreach(var entry in original)
                if(entry.Key!=null)entry.Key.forceRenderingOff=entry.Value;
            original.Clear();tracked.Clear();
        }
    }

    private void UpdateMantisCinematicVisibility(Rank4Presentation presentation)
    {
        if(presentation.request.context.family!="mantis")return;
        var cast=presentation.request.source.mantisMolt;
        if(cast?.cocoon!=null)presentation.request.context.returnBodyVisible=false;
        if(presentation.elapsed>=MantisDeathMoltRank4Stage.FieldReturnTime)
        {presentation.mantisVisibility?.Restore();return;}
        if(presentation.mantisVisibility==null)presentation.mantisVisibility=new Rank4CinematicVisibility();
        // The native cocoon is a separate root, and newborn models appear during this same handoff.
        if(cast?.cocoon!=null)presentation.mantisVisibility.Track(cast.cocoon.gameObject);
        Unit source=presentation.request.source;
        presentation.mantisVisibility.Track(source.ring);
        if(source.mantisReaperFx!=null)presentation.mantisVisibility.Track(source.mantisReaperFx.gameObject);
        if(cast!=null)foreach(Unit child in cast.children)
        {
            presentation.mantisVisibility.Track(child.model);
            presentation.mantisVisibility.Track(child.ring);
            if(child.mantisReaperFx!=null)presentation.mantisVisibility.Track(child.mantisReaperFx.gameObject);
        }
        presentation.mantisVisibility.Hide();
    }

    private void UpdateMushroomCinematicVisibility(Rank4Presentation presentation)
    {
        if(presentation.request.context.family!="mushroom")return;
        presentation.request.context.returnBodyVisible=false;
        if(presentation.elapsed>=MushroomColonyRank4Stage.FieldRevealTime)
            presentation.mushroomBattleVisibility?.Restore();
        else if(presentation.elapsed>=MushroomColonyRank4Stage.OpaqueStageTime)
        {
            if(presentation.mushroomBattleVisibility==null)presentation.mushroomBattleVisibility=new Rank4CinematicVisibility();
            Unit source=presentation.request.source,flower=MushroomDeathBloomUnit(source);
            foreach(Unit unit in units)
            {
                // The corpse and death flower have their own visibility owners during the handoff.
                if(unit==source||unit==flower)continue;
                presentation.mushroomBattleVisibility.Track(unit.model);
                presentation.mushroomBattleVisibility.Track(unit.ring);
            }
            presentation.mushroomBattleVisibility.Hide();
        }
        if(presentation.elapsed>=MushroomColonyRank4Stage.FieldReturnTime)
        {presentation.mushroomVisibility?.Restore();return;}
        if(presentation.mushroomVisibility==null)presentation.mushroomVisibility=new Rank4CinematicVisibility();
        foreach(GameObject visual in MushroomDeathBloomVisualRoots(presentation.request.source))
            presentation.mushroomVisibility.Track(visual);
        presentation.mushroomVisibility.Hide();
    }
}
