using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    public const float GriffinCrosswindDuration=GriffinStormOrbRules.AbsorbDuration;
    private sealed class GriffinStormShot
    {
        public int id;
        public bool basic,launched,finished;
        public Sprite sprite;
        public Color color=Color.white;
        public Vector3 scale=Vector3.one,start,tangent,position;
        public bool flipX,flipY;
        public Unit target;
    }
    private sealed class GriffinCrosswind
    {
        public Unit owner;
        public int generation;
        public Bounds bounds;
        public Vector3 forward,anchor,up,right;
        public float started,nextDeflectSound,nextHitSound,power;
        public bool waiting,contractSounded,burstSounded;
        public PixelGriffinStormOrb effect;
        public readonly List<GriffinStormShot> shots=new List<GriffinStormShot>();
    }
    private readonly List<GriffinCrosswind> griffinCrosswinds=new List<GriffinCrosswind>();

    private void BeginGriffinStormOrbs()
    {
        if(!battleStarted||battleEnding)return;
        foreach(Unit source in units)BeginGriffinCrosswind(source);
    }

    private void BeginGriffinCrosswind(Unit source)
    {
        if(!HasFamilyTranscendence(source)||source.definition.familyId!="griffin"||
           !IsCombatActive(source)||source.hp<=0f||battleEnding)return;
        if(FamilyTranscendenceStateFor(source).griffinStormOrbUsed)return;
        FamilyTranscendenceStateFor(source).griffinStormOrbUsed=true;
        Bounds arena=combatBounds.size.x>0f?combatBounds:CenteredArenaBounds();
        var wind=new GriffinCrosswind {owner=source,generation=combatGeneration,started=battleElapsed,
            power=SkillPower(source)*Mathf.Max(.01f,source.skillTraitDamageMultiplier),
            forward=source.team==Team.Player?Vector3.right:Vector3.left,
            anchor=arena.center+Vector3.up*(TileSize*3f),
            up=mainCamera==null?Vector3.up:mainCamera.transform.up,
            right=mainCamera==null?Vector3.right:mainCamera.transform.right,
            bounds=new Bounds(new Vector3(arena.center.x,TileSize*1.5f,arena.center.z),
                new Vector3(arena.size.x,TileSize*3f,arena.size.z))};
        griffinCrosswinds.Add(wind);source.traitActivations++;
        if(!mlEpisodeActive)
        {
            wind.effect=PixelGriffinStormOrb.Create(Rank3Renderer(source),arena,source.team==Team.Player);
            wind.effect.transform.SetParent(transform,true);wind.anchor=wind.effect.AnchorPosition;
            PlayGriffinSound(source,GriffinSound.WindStart,Flat(source.model.transform.position),.8f);
        }
        for(int i=0;i<GriffinStormOrbRules.BasicShots;i++)StoreGriffinStormShot(wind,null,wind.anchor,true);
        wind.waiting=QueueRank4Cinematic(source);
        if(wind.waiting)wind.effect?.SetCinematicHidden(true);
        else RevealGriffinStormOrb(source);
    }

    private bool IsGriffinStormOrbActive(Unit source)=>griffinCrosswinds.Any(w=>w.owner==source&&
        w.generation==combatGeneration&&!battleEnding&&(w.waiting||battleElapsed-w.started<GriffinStormOrbRules.End));

    private PixelGriffinStormOrb GriffinStormOrbEffect(Unit source)=>
        griffinCrosswinds.FirstOrDefault(w=>w.owner==source&&w.generation==combatGeneration)?.effect;

    private void RevealGriffinStormOrb(Unit source)
    {
        foreach(var wind in griffinCrosswinds.Where(w=>w.owner==source&&w.generation==combatGeneration))
        {
            if(wind.waiting){wind.started=battleElapsed;wind.waiting=false;}
            wind.effect?.SetCinematicHidden(false);wind.effect?.CompleteCinematicEntrance();
        }
    }

    private void StoreGriffinStormShot(GriffinCrosswind wind,SpriteRenderer sprite,Vector3 contact,bool basic)
    {
        var shot=new GriffinStormShot {id=wind.shots.Count,basic=basic};
        if(sprite!=null)
        {
            shot.sprite=sprite.sprite;shot.color=sprite.color;shot.scale=sprite.transform.lossyScale;
            shot.flipX=sprite.flipX;shot.flipY=sprite.flipY;
        }
        wind.shots.Add(shot);
        wind.effect?.Store(shot.id,sprite,contact,Mathf.Max(0f,battleElapsed-wind.started));
    }

    private void UpdateGriffinCrosswinds()
    {
        for(int i=griffinCrosswinds.Count-1;i>=0;i--)
        {
            var wind=griffinCrosswinds[i];
            if(battleEnding||wind.generation!=combatGeneration)
            {wind.effect?.Cancel();StopGriffinWindSound(wind.owner);griffinCrosswinds.RemoveAt(i);continue;}
            if(wind.waiting)continue;
            float age=Mathf.Max(0f,battleElapsed-wind.started);wind.effect?.Pose(age);
            if(!wind.contractSounded&&age>=GriffinStormOrbRules.AbsorbDuration)
            {
                wind.contractSounded=true;
                if(age<GriffinStormOrbRules.Burst)
                    PlayGriffinSound(wind.owner,GriffinSound.OrbContract,wind.anchor,.75f);
            }
            if(!wind.burstSounded&&age>=GriffinStormOrbRules.Burst)
            {
                wind.burstSounded=true;
                PlayGriffinSound(wind.owner,GriffinSound.OrbRelease,wind.anchor,.9f);
            }
            foreach(var shot in wind.shots)
            {
                if(shot.finished)continue;
                float launch=GriffinStormOrbRules.LaunchTime(shot.id,wind.shots.Count);
                if(age<launch)continue;
                if(!shot.launched)
                {
                    shot.launched=true;shot.start=wind.effect==null?wind.anchor:wind.effect.ShotPosition(shot.id);
                    shot.tangent=wind.effect==null?wind.right*((shot.id&1)==0?1f:-1f):wind.effect.ShotTangent(shot.id);
                    shot.position=shot.start;shot.target=GriffinStormTarget(wind,shot.id);
                }
                if(!IsCombatTargetable(shot.target)||shot.target.team==wind.owner.team)
                    shot.target=GriffinStormTarget(wind,shot.id);
                if(shot.target==null){shot.finished=true;wind.effect?.RemoveShot(shot.id);continue;}
                float progress=Mathf.Clamp01((age-launch)/GriffinStormOrbRules.FlightTime(shot.id));
                Vector3 previous=shot.position;
                shot.position=GriffinStormOrbRules.ReturnPosition(shot.start,Rank3Center(shot.target),progress,
                    shot.id,shot.tangent,wind.up,wind.right,TileSize*2f);
                wind.effect?.PoseReleased(shot.id,shot.position,shot.position-previous,progress);
                if(progress<1f)continue;
                // A returned shot owns a new physical hit; no captured enemy callback is retained.
                shot.finished=true;wind.effect?.RemoveShot(shot.id);
                float dealt=ApplySkillDamage(wind.owner,shot.target,new SkillDamageFormulaResult(
                    wind.power*(shot.basic?GriffinStormOrbRules.BasicDamageRatio:GriffinStormOrbRules.StoredDamageRatio),0f,0f),
                    applyTraitMultiplier:false,allowHitSlide:false,bypassProjectileWalls:true);
                if(dealt>0f){wind.owner.skillHits++;wind.owner.skillDamage+=dealt;}
                bool hitSound=dealt>0f&&battleElapsed>=wind.nextHitSound;
                if(hitSound)wind.nextHitSound=battleElapsed+.09f;
                if(!mlEpisodeActive)
                {
                    PixelStarProjectile.PlayWingGustImpact(Rank3Center(shot.target),Rank3Renderer(shot.target),GriffinDiveTint(wind.owner));
                    if(hitSound)
                        PlayGriffinSound(wind.owner,GriffinSound.Pierce,Rank3Center(shot.target),.7f);
                }
            }
            if(age<GriffinStormOrbRules.Burst||wind.shots.Any(s=>!s.finished))continue;
            wind.effect?.Cancel();StopGriffinWindSound(wind.owner);griffinCrosswinds.RemoveAt(i);
        }
    }

    private Unit GriffinStormTarget(GriffinCrosswind wind,int index)
    {
        var enemies=units.Where(u=>u.team!=wind.owner.team&&IsCombatTargetable(u)).OrderBy(u=>u.id).ToArray();
        return enemies.Length==0?null:enemies[index%enemies.Length];
    }

    private void ClearGriffinCrosswinds()
    {
        foreach(var wind in griffinCrosswinds){StopGriffinWindSound(wind.owner);wind.effect?.Cancel();}
        griffinCrosswinds.Clear();
    }

    private bool DeflectGriffinProjectile(Team team,Vector3 from,Vector3 to,SpriteRenderer sprite)
    {
        GriffinCrosswind found=FindGriffinAbsorber(team,from,to,out Vector3 contact);
        if(found==null)return false;
        StoreGriffinStormShot(found,sprite,contact,false);
        PlayGriffinAbsorptionSound(found,contact);
        return true;
    }

    private bool AbsorbGriffinEnergy(Unit source,Vector3 from,Vector3 to,Color color,out Vector3 anchor)=>
        AbsorbGriffinRangedShot(source,from,to,null,color,out anchor);

    private bool AbsorbGriffinRangedShot(Unit source,Vector3 from,Vector3 to,SpriteRenderer sprite,Color color,out Vector3 anchor)
    {
        anchor=Vector3.zero;if(source==null)return false;
        GriffinCrosswind found=FindGriffinAbsorber(source.team,from,to,out Vector3 contact);
        if(found==null)return false;
        anchor=found.effect==null?found.anchor:found.effect.AnchorPosition;
        int id=found.shots.Count;
        StoreGriffinStormShot(found,sprite,sprite==null?from:sprite.transform.position,false);
        found.shots[id].color=color;found.effect?.TintShot(id,color);
        PlayGriffinAbsorptionSound(found,contact);
        return true;
    }

    private void PlayGriffinAbsorptionSound(GriffinCrosswind found,Vector3 contact)
    {
        if(battleElapsed<found.nextDeflectSound)return;
        found.nextDeflectSound=battleElapsed+.12f;
        PlayGriffinSound(found.owner,GriffinSound.OrbAbsorb,contact,.55f);
    }

    private GriffinCrosswind FindGriffinAbsorber(Team team,Vector3 from,Vector3 to,out Vector3 contact)
    {
        contact=from;if(battleEnding)return null;
        Vector3 delta=to-from;float length=delta.magnitude,nearest=float.PositiveInfinity;
        GriffinCrosswind found=null;
        foreach(var wind in griffinCrosswinds)
        {
            if(wind.waiting||wind.generation!=combatGeneration||wind.owner.team==team||
               battleElapsed-wind.started>=GriffinStormOrbRules.AbsorbDuration)continue;
            float distance=0f;
            if(!wind.bounds.Contains(from)&&(length<.00001f||
                !wind.bounds.IntersectRay(new Ray(from,delta/length),out distance)||distance>length))continue;
            if(distance>=nearest)continue;nearest=distance;found=wind;
        }
        if(found!=null)contact=from+(length>.00001f?delta/length*nearest:Vector3.zero);
        return found;
    }
}
