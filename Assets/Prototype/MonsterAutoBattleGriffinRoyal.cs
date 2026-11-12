using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class GriffinRoyalState
    {
        public float remaining=GriffinRank3Rules.RoyalDuration,age;
        public int charges=GriffinRank3Rules.RoyalCharges;
        public PixelGriffinRoyal glow;
        public readonly List<GriffinRoyalWave> waves=new List<GriffinRoyalWave>();
    }

    private sealed class GriffinRoyalWave
    {
        public Vector3 origin,direction;
        public float age,damage,length,halfWidth,scale;
        public PixelGriffinRoyal fx;
        public readonly HashSet<Unit> hit=new HashSet<Unit>();
    }

    private void GrantGriffinRoyal(Unit source)
    {
        if(!HasRank3(source,GriffinRank3Rules.RoyalDescent))return;
        if(source.griffinRoyal==null)source.griffinRoyal=new GriffinRoyalState();
        var royal=source.griffinRoyal;
        royal.remaining=GriffinRank3Rules.RoyalDuration;royal.charges=GriffinRank3Rules.RoyalCharges;
        source.combatEffectIcons.Show(CombatEffectIconKind.Royal,royal.remaining);
        source.traitActivations++;
        PlayGriffinSound(source,GriffinSound.Empower,source.model.transform.position,.8f);
        if(!mlEpisodeActive)
        {
            if(royal.glow==null)
            {
                royal.glow=PixelGriffinRoyal.Ready(Rank3Renderer(source),GriffinDiveTint(source));
            }
        }
    }

    private static bool GriffinRoyalReady(Unit source)
    {
        return source.griffinRoyal!=null&&source.griffinRoyal.remaining>0f&&source.griffinRoyal.charges>0;
    }

    private void PrepareGriffinRoyalWing(Unit source,float speed)
    {
        if(!GriffinRoyalReady(source)||source.spriteAnimation==null||!source.spriteAnimation.HasPendingBasicAttackHit)return;
        source.spriteAnimation.UseGriffinWingPose();
        source.griffinRoyal.glow?.Prepare(source.griffinRoyal.age,
            source.spriteAnimation.BasicAttackHitTime/Mathf.Max(.01f,speed*source.spriteAnimation.GlobalSpeedMultiplier));
    }

    private void FireGriffinRoyalWind(Unit source,Unit target)
    {
        var royal=source.griffinRoyal;
        if(royal==null||royal.remaining<=0f||royal.charges<=0||!IsCombatTargetable(target))return;
        Vector3 origin=Flat(source.model.transform.position),direction=Flat(target.model.transform.position)-origin;
        if(direction.sqrMagnitude<.0001f)direction=source.facingDirection;
        if(direction.sqrMagnitude<.0001f)direction=Vector3.right;
        direction.Normalize();
        var wave=new GriffinRoyalWave {origin=origin,direction=direction,
            damage=SkillPower(source)*GriffinRank3Rules.RoyalWindRatio,
            scale=source.skillTraitDamageMultiplier,
            length=TileSize*GriffinRank3Rules.RoyalWindTiles,
            halfWidth=TileSize*GriffinRank3Rules.RoyalWindWidthTiles*.5f};
        royal.charges--;
        if(!mlEpisodeActive)
        {
            royal.glow?.Strike(royal.age);
            wave.fx=PixelGriffinRoyal.Wave(Rank3Renderer(source),origin,direction,wave.length,
                wave.halfWidth,GriffinDiveTint(source));
            wave.fx.transform.SetParent(transform,true);
            PlayGriffinSound(source,GriffinSound.Wing,origin,.85f);
        }
        royal.waves.Add(wave);
        if(royal.charges==0){royal.remaining=0f;source.combatEffectIcons.Hide(CombatEffectIconKind.Royal);}
    }

    private void TickGriffinRoyal(Unit source,float dt)
    {
        var royal=source.griffinRoyal;if(royal==null)return;
        if(!IsCombatActive(source))InterruptGriffinRoyal(source);
        royal.age+=dt;royal.remaining=Mathf.Max(0f,royal.remaining-dt);
        if(royal.remaining<=0f){royal.charges=0;source.combatEffectIcons.Hide(CombatEffectIconKind.Royal);}
        for(int i=royal.waves.Count-1;i>=0;i--)
        {
            var wave=royal.waves[i];float before=wave.age;
            wave.age+=dt;wave.fx?.Pose(wave.age,false);
            float from=GriffinRank3Rules.RoyalWindProgress(before)*wave.length;
            float to=GriffinRank3Rules.RoyalWindProgress(wave.age)*wave.length;
            if(to>from)
            {
                Vector3 side=Vector3.Cross(Vector3.up,wave.direction);
                foreach(Unit enemy in units.ToArray())
                {
                    if(!IsCombatTargetable(enemy)||enemy.team==source.team||wave.hit.Contains(enemy))continue;
                    Vector3 offset=Flat(enemy.model.transform.position)-wave.origin;
                    float along=Vector3.Dot(offset,wave.direction),across=Vector3.Dot(offset,side);
                    if(along<0f||along>wave.length+enemy.collisionRadius||
                       Mathf.Abs(across)>wave.halfWidth+enemy.collisionRadius||
                       along<from-enemy.collisionRadius||along>to+enemy.collisionRadius)continue;
                    wave.hit.Add(enemy);
                    if(wave.hit.Count==1)PlayGriffinSound(source,GriffinSound.Pierce,Rank3Center(enemy),.8f);
                    float previous=source.skillTraitDamageMultiplier;source.skillTraitDamageMultiplier=wave.scale;
                    try {source.skillDamage+=ApplySkillDamage(source,enemy,wave.damage);source.skillHits++;}
                    finally {source.skillTraitDamageMultiplier=previous;}
                    if(!mlEpisodeActive)
                    {
                        PixelStarProjectile.PlayWingGustImpact(Rank3Center(enemy),Rank3Renderer(enemy),GriffinDiveTint(source));
                        enemy.reaction?.PlayTintHit(Color.Lerp(GriffinDiveTint(source),Color.white,.65f));
                        enemy.spriteAnimation?.PlayImpactRecoil(wave.direction,.055f);
                    }
                }
            }
            if(wave.age>=GriffinRank3Rules.RoyalWindDuration+.18f)
            {wave.fx?.Cancel();royal.waves.RemoveAt(i);}
        }
        bool ready=royal.remaining>0f&&royal.charges>0;
        if(source.spriteAnimation==null||!source.spriteAnimation.HasPendingBasicAttackHit)
            royal.glow?.CancelPreparation();
        if(royal.glow!=null&&royal.glow.Pose(royal.age,ready))
        {royal.glow.Cancel();royal.glow=null;}
        if(!ready&&royal.glow==null&&royal.waves.Count==0)source.griffinRoyal=null;
    }

    private static void InterruptGriffinRoyal(Unit source)
    {
        var royal=source.griffinRoyal;if(royal==null)return;
        royal.remaining=0f;royal.charges=0;royal.glow?.Cancel();royal.glow=null;
        source.combatEffectIcons.Hide(CombatEffectIconKind.Royal);
    }

    private static void ClearGriffinRoyal(Unit source)
    {
        var royal=source.griffinRoyal;if(royal==null)return;
        royal.glow?.Cancel();foreach(var wave in royal.waves)wave.fx?.Cancel();
        source.griffinRoyal=null;source.combatEffectIcons.Hide(CombatEffectIconKind.Royal);
    }
}
