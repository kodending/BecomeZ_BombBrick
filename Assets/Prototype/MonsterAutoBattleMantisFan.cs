using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private IEnumerator ResolveMantisFan(Unit source,Unit primary,float scale,float radiusScale,int generation)
    {
        if(battleEnding||!IsCombatActive(source)||!IsCombatActive(primary))yield break;
        Vector3 origin=Flat(source.model.transform.position);
        Vector3 forward=Flat(primary.model.transform.position)-origin;
        if(forward.sqrMagnitude<.001f)forward=Vector3.right;
        forward.Normalize();
        bool extended=HasRank3(source,MantisCombatRules.SantiusTraitId);
        bool precise=HasRank3(source,MantisCombatRules.MantiusTraitId);
        float range=TileSize*(MantisRank3Rules.MyriadRadiusTiles+(extended?1.5f:0f));
        SpriteRenderer renderer=Rank3Renderer(source);
        float visualScale=1f;
        visualScale=CombatReadabilityPresentation.BodyVisualScale;
        float height=renderer==null||renderer.sprite==null?.85f:
            Mathf.Clamp(renderer.sprite.bounds.size.y*Mathf.Abs(renderer.transform.lossyScale.y)/visualScale*.52f,.6f,1.1f);
        height*=radiusScale*(extended?1.5f:1f);
        float halfWidth=height*.35f;
        var waveHits=new[]{new HashSet<Unit>(),new HashSet<Unit>()};
        var shots=new List<PixelStarProjectile>();
        bool killed=false;Vector3 killPoint=origin;
        System.Func<bool> valid=()=>ReleasedSkillValid(source,generation);
        try
        {
            for(int wave=0;wave<2;wave++)
            {
                if(!valid())yield break;
                int volley=wave;
                var volleyHits=waveHits[volley];
                Color color=MantisGuillotineColor(source,wave);
                PlayMantisSound(source,MantisSound.Fan,Rank3Center(source));
                for(int blade=0;blade<MantisRank3Rules.MyriadBladeCount;blade++)
                {
                    float angle=MantisRank3Rules.MyriadAngle(wave,blade);
                    System.Action<float,float> sweep=(from,to)=>
                    {
                        // Bound curved sweep segments even when a slow frame crosses the entire fan.
                        int steps=Mathf.Max(1,Mathf.CeilToInt((to-from)/.025f));
                        Vector3 a=MantisRank3Rules.MyriadPoint(origin,forward,angle,range,from);
                        Unit[] candidates=units.ToArray();
                        for(int step=1;step<=steps;step++)
                        {
                            Vector3 b=MantisRank3Rules.MyriadPoint(origin,forward,angle,range,
                                Mathf.Lerp(from,to,step/(float)steps));
                            foreach(Unit enemy in candidates)
                            {
                                if(!valid())return;
                                if(!IsCombatTargetable(enemy)||enemy.team==source.team||volleyHits.Contains(enemy)||
                                   WolfCombatRules.DistanceToSegment(Flat(enemy.model.transform.position),a,b)>
                                   halfWidth+enemy.collisionRadius)continue;
                                volleyHits.Add(enemy);
                                bool doubled=waveHits[1-volley].Contains(enemy);
                                float ratio=enemy==primary?MantisRank3Rules.MyriadPrimaryRatio:MantisRank3Rules.MyriadOtherRatio;
                                float damage=Rank3Hit(source,enemy,ratio*(volley==1&&precise?1.4f:1f)*scale);
                                if(!IsCombatActive(enemy))
                                {killed=true;killPoint=Flat(enemy.model.transform.position);}
                                if(doubled&&IsCombatActive(enemy))ApplyMantisDoubleCutStatus(source,enemy);
                                if(damage>0f&&!mlEpisodeActive)
                                {
                                    PixelCombatEffect.PlayMantisCutImpact(enemy.model.transform,Rank3Renderer(enemy),color,doubled);
                                    enemy.spriteAnimation?.PlayImpactRecoil(b-a,doubled?.055f:.035f);
                                    PlayMantisSound(source,MantisSound.Hit,Flat(enemy.model.transform.position),doubled?1f:.8f);
                                }
                            }
                            a=b;
                        }
                    };
                    shots.Add(PixelStarProjectile.PlayMyriadScythe(source.model.transform,renderer,
                        origin,forward,angle,range,height,color,wave==1,sweep,valid));
                }
                if(!mlEpisodeActive)
                    PixelCombatEffect.PlayMantisScytheSlash(renderer==null?source.model.transform:renderer.transform,
                        renderer,2.5f,wave==1,.85f,color);
                if(wave==0)
                    yield return SkillWait(source,MantisRank3Rules.MyriadWaveInterval);
            }
            while(shots.Any(shot=>shot!=null))
            {if(!valid())yield break;yield return null;}
            if(!valid())yield break;
            if(killed&&HasRank3(source,MantisCombatRules.NectisTraitId))
            {
                Color color=new Color(.7f,.3f,1f);
                var echoes=Rank3Enemies(source,killPoint,range).Where(IsCombatTargetable)
                    .OrderBy(enemy=>(Flat(enemy.model.transform.position)-killPoint).sqrMagnitude).Take(2).ToArray();
                if(echoes.Length>0)PlayMantisSound(source,MantisSound.Fan,killPoint,.65f);
                foreach(Unit enemy in echoes)
                {
                    Unit victim=enemy;
                    shots.Add(PixelStarProjectile.PlayMyriadEcho(killPoint+Vector3.up*.55f,renderer,
                        victim.model.transform,Rank3Renderer(victim),height*.8f,color,()=>
                        {
                            if(!valid()||!IsCombatTargetable(victim))return;
                            float ratio=victim==primary?MantisRank3Rules.MyriadPrimaryRatio:MantisRank3Rules.MyriadOtherRatio;
                            Rank3Hit(source,victim,ratio*.6f*scale);
                            PlayMantisSound(source,MantisSound.Hit,Rank3Center(victim),.65f);
                            if(!mlEpisodeActive)
                                PixelCombatEffect.PlayMantisCutImpact(victim.model.transform,Rank3Renderer(victim),color,false);
                        },()=>valid()&&IsCombatTargetable(victim)));
                }
                while(shots.Any(shot=>shot!=null))
                {if(!valid())yield break;yield return null;}
            }
            if(!valid())yield break;
            source.traitActivations++;
            RecordSkillEvent(source,MantisRank3Rules.MyriadEyes,source.skillHits,source.skillDamage);
        }
        finally {foreach(var shot in shots)if(shot!=null)shot.Cancel();}
    }
}
