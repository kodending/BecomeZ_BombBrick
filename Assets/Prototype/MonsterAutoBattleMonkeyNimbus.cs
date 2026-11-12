using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MonkeyNimbusFlight
    {
        public float age,damageScale,radiusScale,bank;
        public int casts;
        public int soundLeg=-1;
        public readonly Vector3[] route=new Vector3[8],bends=new Vector3[7];
        public SkillMasterRow skill;
        public PixelMonkeyNimbus cloud;
        public readonly HashSet<Unit> aimed=new HashSet<Unit>();
    }

    private void BeginMonkeyNimbus(Unit source,Unit target,SkillMasterRow skill)
    {
        Vector3 start=Flat(source.model.transform.position);
        Vector3 center=Flat(target.model.transform.position);
        Vector3 away=start-center;
        if(away.sqrMagnitude<.01f)away=source.team==Team.Player?Vector3.left:Vector3.right;
        away.Normalize();
        Vector3 side=Vector3.Cross(Vector3.up,away);
        var flight=new MonkeyNimbusFlight
        {
            skill=skill,damageScale=source.monkeyBarrageDamageMultiplier,
            radiusScale=source.monkeyBarrageRadiusMultiplier,
            cloud=PixelMonkeyNimbus.Play(Rank3Renderer(source))
        };
        flight.route[0]=start;
        // Cross the formation in short swoops, easing only at each change of direction.
        for(int i=1;i<flight.route.Length;i++)
        {
            float angle=.8f+(i-1)*2.4f;
            flight.route[i]=i==flight.route.Length-1?
                ClampNimbusPosition(ResolveFreePosition(center+away*TileSize*3f,source.collisionRadius)):
                ClampNimbusPosition(center+side*(Mathf.Sin(angle)*TileSize*3.2f)+
                    away*(Mathf.Cos(angle)*TileSize*2.4f));
            Vector3 chord=flight.route[i]-flight.route[i-1];
            flight.bends[i-1]=ClampNimbusPosition((flight.route[i-1]+flight.route[i])*.5f+
                Vector3.Cross(Vector3.up,chord.normalized)*(TileSize*.8f*(i%2==0?1f:-1f)));
        }
        source.monkeyNimbus=flight;
        PlayMonkeySound(source,MonkeySound.Summon,start);
        source.monkeyBarragePending=false;source.monkeyBarrageTarget=null;
        source.traitActivations++;
        FamilyTranscendenceStateFor(source);
        ClearRoute(source);SetMoving(source,false);
        source.spriteAnimation?.InterruptCurrentAction();
        foreach(var enemy in units)
            if(enemy.target==source){SetTarget(enemy,null);ClearRoute(enemy);}
        TickMonkeyNimbus(source,0f);
        QueueRank4Cinematic(source,new[]{target});
    }

    private Vector3 ClampNimbusPosition(Vector3 position)
    {
        position.x=Mathf.Clamp(position.x,combatBounds.min.x+TileSize*.5f,combatBounds.max.x-TileSize*.5f);
        position.z=Mathf.Clamp(position.z,combatBounds.min.z+TileSize*.5f,combatBounds.max.z-TileSize*.5f);
        return position;
    }

    private void TickMonkeyNimbus(Unit source,float dt)
    {
        var flight=source.monkeyNimbus;
        if(flight==null)return;
        flight.age+=Mathf.Max(0f,dt)*CombatStatFormula.CombatAnimationRate;
        float progress=Mathf.Clamp01(flight.age/5f)*flight.bends.Length;
        int leg=Mathf.Min((int)progress,flight.bends.Length-1);
        float phase=progress-leg,t=Mathf.SmoothStep(0f,1f,phase),u=1f-t;
        Vector3 start=flight.route[leg],bend=flight.bends[leg],finish=flight.route[leg+1];
        Vector3 ground=u*u*start+2f*u*t*bend+t*t*finish;
        Vector3 velocity=(2f*u*(bend-start)+2f*t*(finish-bend))*
            (6f*phase*(1f-phase)*flight.bends.Length/5f);
        float airborne=Mathf.SmoothStep(0f,1f,flight.age/.25f)*
            Mathf.SmoothStep(0f,1f,(5f-flight.age)/.35f);
        float lift=TileSize*(1.25f+Mathf.Sin(phase*Mathf.PI)*.16f)*airborne;
        source.model.transform.position=ground+Vector3.up*(source.visualHeightOffset+lift);
        if(source.ring!=null)source.ring.transform.position=ground+Vector3.up*.025f;
        source.lastGroundPosition=ground;source.velocity=Vector3.zero;
        source.currentAction="monkey_nimbus_flight";
        SetMoving(source,false);
        var camera=Camera.main;
        float desiredBank=-Vector3.Dot(velocity.normalized,camera==null?Vector3.right:camera.transform.right)*20f*airborne;
        flight.bank=Mathf.Lerp(flight.bank,desiredBank,1f-Mathf.Exp(-dt*18f));
        source.spriteBillboard?.SetRollPose(flight.bank,Vector3.zero,Vector3.zero);
        flight.cloud?.Pose(ground,source.visualHeightOffset+lift,velocity,flight.age);
        if(leg>flight.soundLeg&&phase>=.12f&&flight.age<5f)
        {
            flight.soundLeg=leg;
            PlayMonkeySound(source,MonkeySound.Dash,ground,.7f);
        }
        if(flight.casts<3&&flight.age>=.2f+flight.casts*1.45f)
        {
            Unit target=units.Where(enemy=>IsCombatTargetable(enemy)&&enemy.team!=source.team)
                .OrderBy(enemy=>flight.aimed.Contains(enemy)?1:0)
                .ThenBy(enemy=>(Flat(enemy.model.transform.position)-ground).sqrMagnitude).FirstOrDefault();
            float scale=flight.casts==0?1f:.5f;
            flight.casts++;
            if(target!=null)
            {
                flight.aimed.Add(target);
                source.monkeyBarragePending=true;
                source.monkeyBarrageCenter=Flat(target.model.transform.position);
                source.monkeyBarrageDamageMultiplier=flight.damageScale;
                source.monkeyBarrageRadiusMultiplier=flight.radiusScale;
                RotateUnit(source,target.model.transform.position-source.model.transform.position,1f);
                StartReleasedSkill(source,ResolveAcrobaticBarrage(source,target,flight.skill,scale,false,combatGeneration));
            }
        }
        if(flight.age>=5f)EndMonkeyNimbus(source,true);
    }

    private void EndMonkeyNimbus(Unit source,bool landed=false)
    {
        var flight=source.monkeyNimbus;
        if(flight==null)return;
        source.monkeyNimbus=null;
        source.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        if(source.model!=null)
        {
            Vector3 ground=ResolveFreePosition(Flat(source.model.transform.position),source.collisionRadius);
            if(landed)ground=ResolveUnitMovement(source,ground,ground);
            source.model.transform.position=ground+Vector3.up*source.visualHeightOffset;
            if(source.ring!=null)source.ring.transform.position=ground+Vector3.up*.025f;
            source.lastGroundPosition=ground;source.currentTile=NearestGridCoordinate(ground);
        }
        source.skillTimer=source.skillCooldownDuration;
        source.attackMovementLockTimer=0f;source.velocity=Vector3.zero;
        source.currentAction="monkey_nimbus_land";
        ClearRoute(source);
        if(flight.cloud!=null)flight.cloud.Stop(landed);
        if(landed)BeginSkillInertia(source,flight.route[flight.route.Length-1]-
            flight.route[flight.route.Length-2],5f/flight.bends.Length);
    }
}
