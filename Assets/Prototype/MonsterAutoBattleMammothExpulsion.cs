using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MammothExpulsionRules
{
    public const float Contact=.45f,HitStop=.05f,CastEnd=1.1f,ChargeDuration=.65f,ChargeTiles=8f;
    public const float Duration=3.2f,InitialDamageRatio=1f,CollisionDamageRatio=.6f;
    public const float InitialSpeedTiles=10f,WallRetention=.92f,BodyRetention=.94f;
    public const float CollisionCooldown=.14f,MaximumStep=1f/120f;
    public const int MaximumCollisionHits=6;

    public static Vector3 ClampGround(Bounds bounds,Vector3 position,float radius)
    {
        float x=Mathf.Min(Mathf.Max(0f,radius),bounds.extents.x);
        float z=Mathf.Min(Mathf.Max(0f,radius),bounds.extents.z);
        return new Vector3(Mathf.Clamp(position.x,bounds.min.x+x,bounds.max.x-x),0f,
            Mathf.Clamp(position.z,bounds.min.z+z,bounds.max.z-z));
    }

    private static bool ReflectAxis(ref float position,ref float velocity,float min,float max)
    {
        float width=max-min;
        if(width<=.025f){position=(min+max)*.5f;velocity=0f;return false;}
        if(position>=min&&position<=max)return false;
        float phase=Mathf.Repeat(position-min,width*2f);
        bool reverse=phase>width;
        position=min+(reverse?width*2f-phase:phase);
        velocity*=reverse?-WallRetention:WallRetention;
        return true;
    }

    public static bool MoveInside(Bounds bounds,Vector3 from,ref Vector3 velocity,float radius,float dt,
        out Vector3 ground,out Vector3 normal)
    {
        from=ClampGround(bounds,from,radius);ground=from+velocity*Mathf.Max(0f,dt);ground.y=0f;
        float x=Mathf.Min(Mathf.Max(0f,radius),bounds.extents.x);
        float z=Mathf.Min(Mathf.Max(0f,radius),bounds.extents.z);
        Vector3 previous=velocity;
        bool wallX=ReflectAxis(ref ground.x,ref velocity.x,bounds.min.x+x,bounds.max.x-x);
        bool wallZ=ReflectAxis(ref ground.z,ref velocity.z,bounds.min.z+z,bounds.max.z-z);
        normal=new Vector3(wallX?-Mathf.Sign(previous.x):0f,0f,wallZ?-Mathf.Sign(previous.z):0f).normalized;
        return wallX||wallZ;
    }
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MammothExpulsionState
    {
        public Unit target;
        public Vector3 direction,start,end;
        public int generation,revision;
        public float time,chargeAge;
        public bool launched;
        public PixelMammothExpulsion effect;
    }
    private sealed class MammothExpulsionFlight
    {
        public Unit source;
        public MammothExpulsionState cast;
        public int generation;
        public float time,power,startedAt,nextCollisionFeedbackAt;
        public bool collisionFeedbackPending,collisionFeedbackWall,collisionShakePlayed;
        public Vector3 collisionFeedbackPoint;
        public PixelMammothExpulsion effect;
        public readonly Dictionary<Unit,int> collisionHits=new Dictionary<Unit,int>();
    }
    private readonly List<MammothExpulsionFlight> mammothExpulsionFlights=new List<MammothExpulsionFlight>();

    private bool MammothExpulsionTarget(Unit source,Unit target)=>source!=null&&source!=target&&
        IsCombatTargetable(target)&&source.team!=target.team;

    private bool MammothExpulsionValid(Unit source,MammothExpulsionState state)=>source!=null&&state!=null&&
        !battleEnding&&source.mammothExpulsion==state&&state.generation==combatGeneration&&
        source.positionRevision==state.revision&&IsCombatActive(source)&&source.hp>0f&&
        !MammothRicochetControlsMovement(source)&&!SpiderSilkBound(source)&&
        !source.combatStatuses.Has(CombatStatusKind.Knockdown)&&!source.combatStatuses.Has(CombatStatusKind.Airborne)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&!source.combatStatuses.Has(CombatStatusKind.Freeze)&&
        !source.displacing&&source.giraffeStunTimer<=0f;

    private void StartMammothExpulsion(Unit source,Unit target)
    {
        if(!IsCombatActive(source)||source.mammothExpulsion!=null||source.mammothChargePending||
           source.mammothCharge!=null||source.mammothToss!=null||source.mammothBrace!=null||
           MammothRicochetControlsMovement(source)||
           !MammothExpulsionTarget(source,target))return;
        Vector3 direction=Flat(target.model.transform.position-source.model.transform.position);
        if(direction.sqrMagnitude<.001f)direction=source.facingDirection;
        if(direction.sqrMagnitude<.001f)direction=source.team==Team.Player?Vector3.right:Vector3.left;
        ClearRoute(source);ReleaseAttackSlot(source);ReleaseUnitOccupancy(source);ClearUnitInertia(source);
        source.hitSlideVelocity=Vector3.zero;source.hexStepping=false;source.velocity=Vector3.zero;
        var state=new MammothExpulsionState{target=target,direction=direction.normalized,
            start=Flat(source.model.transform.position),end=Flat(source.model.transform.position),
            generation=combatGeneration,revision=source.positionRevision};
        source.mammothExpulsion=state;SetMoving(source,false);SetTarget(source,target);RotateUnit(source,direction,1f);
        source.currentAction="mammoth_expulsion_prepare";
        if(!mlEpisodeActive)
        {
            state.effect=PixelMammothExpulsion.Prepare(Rank3Renderer(source),state.start,state.direction,
                PixelMammothWallRush.HitColor(source.definition.monsterId));
            state.effect.transform.SetParent(transform,true);
        }
        if(source.spriteAnimation!=null&&!source.spriteAnimation.PlayMammothCharge(()=>MammothExpulsionValid(source,state),
            t=>AdvanceMammothExpulsion(source,state,t),()=>EndMammothExpulsion(source,state),false,false,false,true))
            EndMammothExpulsion(source,state);
        if(source.mammothExpulsion==state)QueueRank4Cinematic(source,units.Where(u=>MammothExpulsionTarget(source,u)).ToArray());
    }

    private void AdvanceMammothExpulsion(Unit source,MammothExpulsionState state,float time)
    {
        if(!MammothExpulsionValid(source,state)){EndMammothExpulsion(source,state);return;}
        state.time=Mathf.Max(state.time,time);
        if(!state.launched)state.effect?.Pose(time);
        if(state.launched||time<MammothExpulsionRules.Contact)return;
        var targets=units.Where(u=>MammothExpulsionTarget(source,u)).ToArray();
        if(targets.Length==0){EndMammothExpulsion(source,state);return;}
        state.launched=true;source.traitActivations++;
        state.start=Flat(source.model.transform.position);
        Vector3 center=Vector3.zero;foreach(Unit target in targets)center+=Flat(target.model.transform.position);
        center/=targets.Length;
        Vector3 toward=center-state.start;
        if(toward.sqrMagnitude<.01f)toward=state.direction;
        float distance=Mathf.Clamp(toward.magnitude,TileSize*2f,TileSize*MammothExpulsionRules.ChargeTiles);
        state.end=MammothExpulsionRules.ClampGround(combatBounds,state.start+toward.normalized*distance,CombatBodyRadius(source));
        if((state.end-state.start).sqrMagnitude<.0025f)
            state.end=MammothExpulsionRules.ClampGround(combatBounds,
                Vector3.MoveTowards(state.start,Flat(combatBounds.center),TileSize*2f),CombatBodyRadius(source));
        if((state.end-state.start).sqrMagnitude>.001f)state.direction=(state.end-state.start).normalized;
        var flight=new MammothExpulsionFlight{source=source,cast=state,generation=combatGeneration,
            effect=state.effect,power=source.damage,startedAt=mammothRicochetClock};
        mammothExpulsionFlights.Add(flight);source.currentAction="mammoth_expulsion_charge";
        foreach(Unit target in targets)
        {
            ApplyMammothChainDamage(flight,target,MammothExpulsionRules.InitialDamageRatio);
            if(!mlEpisodeActive)PixelMammothExpulsion.Contact(Flat(target.model.transform.position),Rank3Renderer(target),
                PixelMammothWallRush.HitColor(source.definition.monsterId),false,true);
            if(!CanMammothRicochet(target))continue;
            Vector3 away=Flat(target.model.transform.position)-center;
            if(away.sqrMagnitude<.01f)away=Flat(target.model.transform.position)-state.start;
            if(away.sqrMagnitude<.01f)away=state.direction;
            BeginMammothRicochet(target,flight,away.normalized*TileSize*MammothExpulsionRules.InitialSpeedTiles);
        }
        if(!mlEpisodeActive)combatCameraDirector?.ReportGroundSlam(true);
        PlayMammothSound(source,MammothSound.Launch,state.start,1.1f);
        RecordSkillEvent(source,"trait_transcend_mammoth_ark_of_earth",targets.Length,0f);
    }

    private float ApplyMammothChainDamage(MammothExpulsionFlight flight,Unit target,float ratio)
    {
        if(!IsCombatTargetable(target)||target.team==flight.source.team)return 0f;
        float dealt=ApplyDamageComponents(flight.source,target,flight.power*ratio,true,false,false,false,
            0f,0f,1f,false,false,false,0f,skillImpact:true,allowHitSlide:false);
        flight.source.skillDamage+=dealt;if(dealt>0f)flight.source.skillHits++;
        return dealt;
    }

    private void EndMammothExpulsion(Unit source,MammothExpulsionState state)
    {
        if(source==null||source.mammothExpulsion!=state)return;
        source.mammothExpulsion=null;source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        if(!state.launched)state.effect?.Cancel();
        source.velocity=Vector3.zero;SetMoving(source,false);
        source.spriteAnimation?.InterruptCurrentAction(true);
        if(IsCombatActive(source)&&source.hp>0f)
        {source.currentAction="mammoth_expulsion_complete";ReclaimMammothOccupancy(source);}
    }

    private void AdvanceMammothChargeBody(MammothExpulsionFlight flight,float dt)
    {
        var source=flight.source;var state=flight.cast;
        if(!MammothExpulsionValid(source,state)){EndMammothExpulsion(source,state);return;}
        state.chargeAge=Mathf.Min(MammothExpulsionRules.ChargeDuration,state.chargeAge+dt);
        Vector3 previous=Flat(source.model.transform.position);
        float t=state.chargeAge/MammothExpulsionRules.ChargeDuration;
        Vector3 desired=Vector3.Lerp(state.start,state.end,1f-Mathf.Pow(1f-t,2f));
        Vector3 ground=ResolveUnitMovement(source,previous,desired);
        ground=MammothExpulsionRules.ClampGround(combatBounds,ground,CombatBodyRadius(source));
        SetMammothGround(source,ground);RotateUnit(source,state.direction,dt);
        flight.effect?.Charge(ground,state.direction,flight.time);
        if(t>=1f)EndMammothExpulsion(source,state);
    }

    private void UpdateMammothExpulsionFlights(float dt)
    {
        if(paused||battleEnding||dt<=0f||CombatSkillClock.Active)return;
        if(mammothExpulsionFlights.Count==0&&mammothRicochets.Count==0)return;
        AdvanceMammothRicochets(dt);
        for(int i=mammothExpulsionFlights.Count-1;i>=0;i--)
        {
            var flight=mammothExpulsionFlights[i];
            if(flight.generation!=combatGeneration){flight.effect?.Cancel();EndMammothExpulsion(flight.source,flight.cast);mammothExpulsionFlights.RemoveAt(i);continue;}
            flight.time=Mathf.Min(MammothExpulsionRules.Duration,mammothRicochetClock-flight.startedAt);
            foreach(var body in mammothRicochets)
                if(!body.stopped&&body.flight==flight)
                    flight.effect?.Slide(Flat(body.unit.model.transform.position),body.velocity,flight.time,Rank3Renderer(body.unit));
            FlushMammothCollisionFeedback(flight);
            flight.effect?.Pose(flight.time);
            if(flight.time<MammothExpulsionRules.Duration)continue;
            if(flight.effect!=null){flight.effect.StopEmission();CombatEffectEndPlayback.Begin(flight.effect.gameObject,EndingEffectTypes);}
            EndMammothExpulsion(flight.source,flight.cast);mammothExpulsionFlights.RemoveAt(i);
        }
    }

    private void ClearMammothExpulsionFlights()
    {
        foreach(var flight in mammothExpulsionFlights.ToArray())
        {flight.effect?.Cancel();EndMammothExpulsion(flight.source,flight.cast);}
        mammothExpulsionFlights.Clear();
        foreach(var body in mammothRicochets)ReleaseMammothRicochet(body.unit);
        mammothRicochets.Clear();mammothRicochetByUnit.Clear();mammothRicochetParticipants.Clear();mammothRicochetClock=0f;
        foreach(Unit unit in units)
            if(unit.mammothExpulsion!=null)EndMammothExpulsion(unit,unit.mammothExpulsion);
    }
}
