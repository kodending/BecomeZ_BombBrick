using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MammothRicochetBody
    {
        public Unit unit;
        public MammothExpulsionFlight flight;
        public Vector3 velocity;
        public float bornAt,endsAt,initialSpeed,nextWallAt;
        public int revision,generation;
        public bool stopped;
        public readonly Dictionary<Unit,float> nextBodyHit=new Dictionary<Unit,float>();
    }
    private readonly List<MammothRicochetBody> mammothRicochets=new List<MammothRicochetBody>();
    private readonly Dictionary<Unit,MammothRicochetBody> mammothRicochetByUnit=new Dictionary<Unit,MammothRicochetBody>();
    private readonly List<Unit> mammothRicochetParticipants=new List<Unit>(16);
    private float mammothRicochetClock;

    private static bool MammothRicochetControlsMovement(Unit unit)=>unit!=null&&unit.mammothRicochetActive;
    private bool CanMammothRicochet(Unit unit)=>IsCombatTargetable(unit)&&unit.hp>0f&&!PreyDisplacementImmune(unit)&&
        unit.augmentReturnInvulnerabilityTimer<=0f&&!IsSpiritSanctuaryProtected(unit)&&
        unit.spiritConcertOwner==null&&unit.sabertoothDevouredBy==null&&unit.crocodileAbyssHeldBy==null;

    private void BeginMammothRicochet(Unit unit,MammothExpulsionFlight flight,Vector3 velocity)
    {
        if(!CanMammothRicochet(unit))return;
        if(mammothRicochetByUnit.TryGetValue(unit,out var existing))
        {
            if(velocity.sqrMagnitude>=existing.velocity.sqrMagnitude)
            {existing.velocity=velocity;existing.initialSpeed=velocity.magnitude;existing.flight=flight;existing.bornAt=mammothRicochetClock;}
            existing.endsAt=Mathf.Max(existing.endsAt,flight.startedAt+MammothExpulsionRules.Duration);return;
        }
        if(unit.mammothExpulsion!=null)EndMammothExpulsion(unit,unit.mammothExpulsion);
        InterruptPositionalAction(unit);ClearRoute(unit);ReleaseAttackSlot(unit);ReleaseUnitOccupancy(unit);
        unit.displacing=false;unit.hexStepping=false;ClearUnitInertia(unit);unit.hitSlideVelocity=Vector3.zero;
        var body=new MammothRicochetBody{unit=unit,flight=flight,velocity=Flat(velocity),initialSpeed=velocity.magnitude,
            bornAt=mammothRicochetClock,endsAt=flight.startedAt+MammothExpulsionRules.Duration,
            revision=unit.positionRevision,generation=combatGeneration};
        mammothRicochets.Add(body);mammothRicochetByUnit[unit]=body;unit.mammothRicochetActive=true;
        MaintainMammothRicochetUnit(unit);
    }

    private void MaintainMammothRicochetUnit(Unit unit)
    {
        if(!mammothRicochetByUnit.TryGetValue(unit,out var body)){unit.mammothRicochetActive=false;return;}
        unit.velocity=body.velocity;unit.hitSlideVelocity=Vector3.zero;
        unit.inertiaVelocity=Vector3.zero;unit.hexStepping=false;unit.currentAction="mammoth_chain_slide";
        SetMoving(unit,true);
    }

    private void SetMammothGround(Unit unit,Vector3 ground)
    {
        ground=Flat(ground);unit.lastGroundPosition=ground;
        unit.model.transform.position=ground+Vector3.up*unit.visualHeightOffset;
        if(unit.ring!=null)unit.ring.transform.position=ground+Vector3.up*.025f;
        unit.currentTile=NearestGridCoordinate(ground);
    }

    private void ReclaimMammothOccupancy(Unit unit)
    {
        if(battleEnding||!IsCombatActive(unit)||unit.hp<=0f)return;
        if(tiles.TryGetValue(unit.currentTile,out var tile)&&tile.walkable&&!tile.bench&&
           !IsCombatHexOccupiedByOther(unit,unit.currentTile))OccupyUnit(unit,unit.currentTile);
    }

    private void ReleaseMammothRicochet(Unit unit)
    {
        if(unit==null)return;
        if(mammothRicochetByUnit.TryGetValue(unit,out var body))
        {body.stopped=true;mammothRicochetByUnit.Remove(unit);}
        if(!unit.mammothRicochetActive)return;
        unit.mammothRicochetActive=false;unit.velocity=Vector3.zero;unit.hitSlideVelocity=Vector3.zero;
        ClearUnitInertia(unit);unit.hexStepping=false;unit.decisionTimer=0f;
        if(IsCombatActive(unit)&&unit.hp>0f)
        {SetMoving(unit,false);unit.currentAction="idle";ReclaimMammothOccupancy(unit);}
    }

    private bool MammothRicochetValid(MammothRicochetBody body)=>!body.stopped&&body.generation==combatGeneration&&
        body.revision==body.unit.positionRevision&&IsCombatActive(body.unit)&&body.unit.hp>0f&&
        body.unit.mantisMolt==null&&body.unit.augmentReturnTimer<=0f&&body.unit.crocodileAbyssHeldBy==null;

    private void AdvanceMammothRicochets(float dt)
    {
        float frameEnd=mammothRicochetClock+dt;
        float remaining=Mathf.Min(dt,MammothExpulsionRules.Duration);
        // Damage callbacks can register a death-flower. Keep a stable, reusable frame snapshot.
        mammothRicochetParticipants.Clear();mammothRicochetParticipants.AddRange(units);
        while(remaining>.000001f)
        {
            float speed=1f,radius=.3f;
            foreach(var body in mammothRicochets)
                if(MammothRicochetValid(body))
                {speed=Mathf.Max(speed,body.velocity.magnitude);radius=Mathf.Min(radius,Mathf.Max(.06f,CombatBodyRadius(body.unit)));}
            float step=Mathf.Min(remaining,Mathf.Min(MammothExpulsionRules.MaximumStep,radius*.45f/(speed*2f)));
            mammothRicochetClock+=step;remaining-=step;
            foreach(var flight in mammothExpulsionFlights)
                if(flight.generation==combatGeneration)
                {
                    flight.time=Mathf.Min(MammothExpulsionRules.Duration,mammothRicochetClock-flight.startedAt);
                    AdvanceMammothChargeBody(flight,step);
                }
            for(int i=0;i<mammothRicochets.Count;i++)
            {
                var body=mammothRicochets[i];
                if(!MammothRicochetValid(body)||mammothRicochetClock>=body.endsAt-.00001f)
                {ReleaseMammothRicochet(body.unit);continue;}
                if(mammothRicochetClock-body.bornAt>.35f)
                    body.velocity=Vector3.MoveTowards(body.velocity,Vector3.zero,
                        body.initialSpeed/(MammothExpulsionRules.Duration-.35f)*step);
                if(body.velocity.sqrMagnitude<TileSize*TileSize*.0625f)
                {ReleaseMammothRicochet(body.unit);continue;}
                Vector3 previous=Flat(body.unit.model.transform.position);
                bool wall=MammothExpulsionRules.MoveInside(combatBounds,previous,ref body.velocity,
                    CombatBodyRadius(body.unit),step,out Vector3 ground,out Vector3 normal);
                SetMammothGround(body.unit,ground);
                if(wall&&mammothRicochetClock>=body.nextWallAt)
                {
                    body.nextWallAt=mammothRicochetClock+MammothExpulsionRules.CollisionCooldown;
                    MammothRicochetCollisionDamage(body.flight,body.unit,ground,true);
                }
            }
            for(int i=0;i<mammothRicochetParticipants.Count;i++)for(int j=i+1;j<mammothRicochetParticipants.Count;j++)
                ResolveMammothBodyCollision(mammothRicochetParticipants[i],mammothRicochetParticipants[j]);
        }
        mammothRicochetClock=frameEnd;
        for(int i=mammothRicochets.Count-1;i>=0;i--)
        {
            var body=mammothRicochets[i];
            if(!MammothRicochetValid(body)||mammothRicochetClock>=body.endsAt-.00001f)
                ReleaseMammothRicochet(body.unit);
            if(body.stopped)mammothRicochets.RemoveAt(i);
        }
    }

    private void ResolveMammothBodyCollision(Unit a,Unit b)
    {
        if(a.team!=b.team||!IsCombatTargetable(a)||!IsCombatTargetable(b))return;
        mammothRicochetByUnit.TryGetValue(a,out var first);mammothRicochetByUnit.TryGetValue(b,out var second);
        if(first==null&&second==null)return;
        Vector3 pa=Flat(a.model.transform.position),pb=Flat(b.model.transform.position),delta=pb-pa;
        float distance=delta.magnitude,clearance=CombatBodyRadius(a)+CombatBodyRadius(b);
        if(distance>clearance+.002f)return;
        Vector3 va=first?.velocity??Vector3.zero,vb=second?.velocity??Vector3.zero;
        Vector3 normal=distance>.0001f?delta/distance:(va-vb).sqrMagnitude>.0001f?(va-vb).normalized:Vector3.right;
        float closing=Vector3.Dot(vb-va,normal);
        if(distance<clearance)
        {
            float overlap=clearance-distance+.001f;
            float moveA=first==null?0f:second==null?1f:.5f,moveB=second==null?0f:first==null?1f:.5f;
            SetMammothGround(a,MammothExpulsionRules.ClampGround(combatBounds,pa-normal*overlap*moveA,CombatBodyRadius(a)));
            SetMammothGround(b,MammothExpulsionRules.ClampGround(combatBounds,pb+normal*overlap*moveB,CombatBodyRadius(b)));
        }
        if(closing>=-.025f)return;
        var owner=first?.flight??second.flight;
        if(first==null&&CanMammothRicochet(a))
        {BeginMammothRicochet(a,owner,normal*Vector3.Dot(vb,normal));mammothRicochetByUnit.TryGetValue(a,out first);}
        if(second==null&&CanMammothRicochet(b))
        {BeginMammothRicochet(b,owner,normal*Vector3.Dot(va,normal));mammothRicochetByUnit.TryGetValue(b,out second);}
        if(first!=null&&second!=null)
        {
            first.velocity=(va+normal*closing)*MammothExpulsionRules.BodyRetention;
            second.velocity=(vb-normal*closing)*MammothExpulsionRules.BodyRetention;
        }
        else if(first!=null)first.velocity=Vector3.Reflect(va,normal)*MammothExpulsionRules.BodyRetention;
        else if(second!=null)second.velocity=Vector3.Reflect(vb,normal)*MammothExpulsionRules.BodyRetention;
        bool cooling=first!=null&&first.nextBodyHit.TryGetValue(b,out float nextA)&&mammothRicochetClock<nextA||
            second!=null&&second.nextBodyHit.TryGetValue(a,out float nextB)&&mammothRicochetClock<nextB;
        if(cooling)return;
        float next=mammothRicochetClock+MammothExpulsionRules.CollisionCooldown;
        if(first!=null)first.nextBodyHit[b]=next;if(second!=null)second.nextBodyHit[a]=next;
        Vector3 point=(pa+pb)*.5f;
        // Both participants take one event's damage, even when the first participant dies.
        var firstOwner=first?.flight??owner;var secondOwner=second?.flight??owner;
        MammothRicochetCollisionDamage(firstOwner,a,point,false);
        MammothRicochetCollisionDamage(secondOwner,b,point,false);
    }

    private void MammothRicochetCollisionDamage(MammothExpulsionFlight flight,Unit target,Vector3 point,bool wall)
    {
        if(flight.generation!=combatGeneration||!IsCombatTargetable(target))return;
        int hits=flight.collisionHits.TryGetValue(target,out int count)?count:0;
        if(hits>=MammothExpulsionRules.MaximumCollisionHits)return;
        flight.collisionHits[target]=hits+1;
        ApplyMammothChainDamage(flight,target,MammothExpulsionRules.CollisionDamageRatio);
        if(!flight.collisionFeedbackPending||wall&&!flight.collisionFeedbackWall)
        {flight.collisionFeedbackPoint=point;flight.collisionFeedbackWall=wall;}
        flight.collisionFeedbackPending=true;
        if(!mlEpisodeActive)PixelMammothExpulsion.Contact(point,Rank3Renderer(target),
            PixelMammothWallRush.HitColor(flight.source.definition.monsterId),wall,false);
    }

    private void FlushMammothCollisionFeedback(MammothExpulsionFlight flight)
    {
        if(!flight.collisionFeedbackPending)return;
        flight.collisionFeedbackPending=false;
        bool wall=flight.collisionFeedbackWall;flight.collisionFeedbackWall=false;
        if(mammothRicochetClock<flight.nextCollisionFeedbackAt)return;
        flight.nextCollisionFeedbackAt=mammothRicochetClock+.18f;
        PlayMammothSound(flight.source,wall?MammothSound.WallBreak:MammothSound.Collision,
            flight.collisionFeedbackPoint,wall?.9f:.75f);
        if(wall||!flight.collisionShakePlayed)
        {
            if(!mlEpisodeActive)combatCameraDirector?.ReportGroundSlam(false);
            flight.collisionShakePlayed=true;
        }
    }
}
