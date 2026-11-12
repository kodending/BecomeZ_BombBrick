using NUnit.Framework;
using UnityEngine;

public sealed class CombatMovementInertiaTests
{
    private const float Speed=4f;

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void AccelerationBuildsSpeedAndReachesCruiseAtDifferentFrameRates(int fps)
    {
        float dt=1f/fps,distance=0f;
        Vector3 velocity=Vector3.zero;
        int frames=Mathf.RoundToInt(CombatMovementInertia.AccelerationSeconds*fps);
        for(int frame=0;frame<frames;frame++)
        {
            float previous=velocity.magnitude;
            velocity=CombatMovementInertia.Advance(velocity,Vector3.right*Speed,Speed,dt);
            Assert.That(velocity.x,Is.GreaterThanOrEqualTo(previous-.00001f).And.LessThanOrEqualTo(Speed+.00001f));
            if(frame==0)Assert.That(velocity.x,Is.GreaterThan(0f).And.LessThan(Speed));
            distance+=velocity.x*dt;
        }
        Assert.That(velocity.x,Is.EqualTo(Speed).Within(.0001f));
        Assert.That(distance,Is.InRange(Speed*CombatMovementInertia.AccelerationSeconds*.35f,
            Speed*CombatMovementInertia.AccelerationSeconds*.75f));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void BrakingReducesSpeedAndStopsWithinTheAuthoredTime(int fps)
    {
        float dt=1f/fps,distance=0f;
        Vector3 velocity=Vector3.right*Speed;
        int frames=Mathf.CeilToInt(CombatMovementInertia.BrakingSeconds*fps)+1;
        for(int frame=0;frame<frames;frame++)
        {
            float previous=velocity.magnitude;
            velocity=CombatMovementInertia.Advance(velocity,Vector3.zero,Speed,dt);
            Assert.That(velocity.x,Is.InRange(0f,previous+.00001f));
            if(frame==0)Assert.That(velocity.x,Is.GreaterThan(0f).And.LessThan(Speed));
            distance+=velocity.x*dt;
        }
        Assert.That(velocity.sqrMagnitude,Is.LessThan(.000001f));
        Assert.That(distance,Is.InRange(Speed*CombatMovementInertia.BrakingSeconds*.25f,
            Speed*CombatMovementInertia.BrakingSeconds*.65f));
    }

    [TestCase(30,90)] [TestCase(60,90)] [TestCase(120,90)]
    [TestCase(30,180)] [TestCase(60,180)] [TestCase(120,180)]
    public void TurnsRetainMomentumBeforeSettlingIntoTheNewDirection(int fps,int degrees)
    {
        float dt=1f/fps;
        Vector3 initial=Vector3.right*Speed;
        Vector3 desired=(degrees==90?Vector3.forward:Vector3.left)*Speed;
        Vector3 velocity=CombatMovementInertia.Advance(initial,desired,Speed,dt);
        Assert.That(velocity.x,Is.GreaterThan(0f),"A turn cannot instantly erase the previous travel direction.");
        Assert.That(Vector3.Distance(velocity,desired),Is.GreaterThan(.1f));
        Assert.That(Vector3.Distance(velocity,initial),Is.GreaterThan(0f));
        for(int frame=1;frame<fps;frame++)
        {
            velocity=CombatMovementInertia.Advance(velocity,desired,Speed,dt);
            Assert.That(velocity.magnitude,Is.LessThanOrEqualTo(Speed+.0001f));
        }
        Assert.That(Vector3.Distance(velocity,desired),Is.LessThan(.001f));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void ArrivalBrakesWithoutPassingTheDestinationOrOscillating(int fps)
    {
        float dt=1f/fps,position=0f,destination=.8f;
        Vector3 velocity=Vector3.right*Speed;
        bool slowed=false;
        for(int frame=0;frame<fps*2;frame++)
        {
            float remaining=Mathf.Max(0f,destination-position);
            velocity=CombatMovementInertia.Advance(velocity,Vector3.right*Speed,Speed,dt,remaining);
            Assert.That(velocity.x,Is.GreaterThanOrEqualTo(0f));
            Assert.That(velocity.magnitude*dt,Is.LessThanOrEqualTo(remaining+.00001f));
            if(remaining>.001f&&velocity.x<Speed-.01f)slowed=true;
            position+=velocity.x*dt;
            Assert.That(position,Is.LessThanOrEqualTo(destination+.00001f));
        }
        Assert.That(slowed,Is.True);
        Assert.That(position,Is.EqualTo(destination).Within(.001f));
        Assert.That(velocity.magnitude,Is.LessThan(.001f));
    }

    [TestCase(.05f,.001f)] [TestCase(.2f,.001f)] [TestCase(1f,.001f)]
    [TestCase(.05f,.04f)] [TestCase(.2f,.04f)] [TestCase(1f,.04f)]
    public void OneLongFrameCannotCrossTheRemainingApproachDistance(float dt,float remaining)
    {
        Vector3 velocity=CombatMovementInertia.Advance(Vector3.right*Speed,
            Vector3.right*Speed,Speed,dt,remaining);
        Assert.That(velocity.magnitude*dt,Is.LessThanOrEqualTo(remaining+.000001f));
        Assert.That(velocity.x,Is.GreaterThanOrEqualTo(0f));
    }

    [TestCase(0f)] [TestCase(-.1f)]
    public void PausedOrInvalidElapsedTimeCannotChangeVelocity(float dt)
    {
        Vector3 velocity=new Vector3(1.5f,0f,-2f);
        Assert.That(CombatMovementInertia.Advance(velocity,Vector3.left*Speed,Speed,dt),
            Is.EqualTo(velocity));
    }

    [Test]
    public void ReachingTheDestinationLeavesNoLatentForwardVelocity()
    {
        Assert.That(CombatMovementInertia.Advance(Vector3.right*Speed,
            Vector3.right*Speed,Speed,1f/120f,0f),Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void BlockedOrOppositeCollisionMotionClearsLatentPush()
    {
        Assert.That(CombatMovementInertia.AfterCollision(Vector3.right*Speed,Vector3.zero,.1f),
            Is.EqualTo(Vector3.zero));
        Assert.That(CombatMovementInertia.AfterCollision(Vector3.right*Speed,Vector3.left*.01f,.1f),
            Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void CollisionPreservesOnlyTheAllowedTangentVelocity()
    {
        Vector3 requested=new Vector3(3f,0f,4f);
        Vector3 velocity=CombatMovementInertia.AfterCollision(requested,Vector3.forward*.2f,.1f);
        Assert.That(velocity.x,Is.EqualTo(0f).Within(.00001f));
        Assert.That(velocity.z,Is.EqualTo(2f).Within(.00001f));
    }

    [Test]
    public void CollisionCorrectionCannotManufactureExtraSpeed()
    {
        Vector3 velocity=CombatMovementInertia.AfterCollision(Vector3.right*Speed,
            new Vector3(2f,0f,1f),.1f);
        Assert.That(velocity.magnitude,Is.LessThanOrEqualTo(Speed+.00001f));
        Assert.That(velocity.x,Is.GreaterThan(0f));
        Assert.That(CombatMovementInertia.AfterCollision(Vector3.zero,Vector3.right,.1f),
            Is.EqualTo(Vector3.zero));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void CoastingContinuesAfterStopAndFasterMotionGlidesFartherForLonger(int fps)
    {
        float slowTime,fastTime;
        float slow=CoastDistance(2f,fps,out slowTime);
        float fast=CoastDistance(4f,fps,out fastTime);
        Assert.That(slow,Is.GreaterThan(.1f));
        Assert.That(slow,Is.EqualTo(2f/CombatMovementInertia.CoastDeceleration).Within(.0001f));
        Assert.That(fast,Is.EqualTo(slow*4f).Within(.0001f),
            "Constant drag must retain the stronger momentum of a faster approach.");
        Assert.That(fastTime,Is.EqualTo(slowTime*2f).Within(1f/fps+.0001f));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void CoastingIntegratesTheSameDistanceAsOneFrameThatIncludesTheWholeStop(int fps)
    {
        float ignored;
        float sliced=CoastDistance(4f,fps,out ignored);
        Vector3 next;
        Vector3 whole=CombatMovementInertia.Coast(Vector3.right*4f,2f,out next);
        Assert.That(sliced,Is.EqualTo(whole.x).Within(.0001f));
        Assert.That(whole.x,Is.GreaterThan(.3f));
        Assert.That(next,Is.EqualTo(Vector3.zero));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void RetuningExtendsOrdinaryGlideAndShortensFastSkillGlideWithoutReversingTheirOrder(int fps)
    {
        const float ordinarySpeed=4.4f,skillSpeed=8f,previousDrag=10f;
        float ignored;
        float ordinary=CoastDistance(ordinarySpeed,fps,out ignored);
        float skill=CoastDistance(Mathf.Min(skillSpeed,CombatMovementInertia.MaximumCoastSpeed),fps,out ignored);
        float previousOrdinary=ordinarySpeed*ordinarySpeed/(2f*previousDrag);
        float previousSkill=skillSpeed*skillSpeed/(2f*previousDrag);
        Assert.That(ordinary,Is.GreaterThan(previousOrdinary).And.LessThan(previousOrdinary*1.15f));
        Assert.That(skill,Is.LessThan(previousSkill).And.GreaterThan(previousSkill*.8f));
        Assert.That(skill,Is.GreaterThan(ordinary));
        Assert.That(skill-ordinary,Is.LessThan(previousSkill-previousOrdinary));
        Assert.That(skill/ordinary,Is.LessThan(previousSkill/previousOrdinary));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void MovementGlidesThirtyPercentFartherAtSlowMediumAndFastSpeeds(int fps)
    {
        float ignored;
        float slow=CoastDistance(2f,fps,out ignored,CombatMovementInertia.CoastDecelerationForSpeed(2f));
        float medium=CoastDistance(3.2f,fps,out ignored,CombatMovementInertia.CoastDecelerationForSpeed(3.2f));
        float fastTime;
        float fast=CoastDistance(5.76f,fps,out fastTime,CombatMovementInertia.CoastDecelerationForSpeed(5.76f));
        Assert.That(slow,Is.EqualTo(2f*2f/(2f*4.6f)*1.3f).Within(.0001f));
        Assert.That(medium,Is.EqualTo(3.2f*3.2f/(2f*(4.6f+(9.2f-4.6f)*.7f/3f))*1.3f).Within(.0001f));
        Assert.That(fast,Is.EqualTo(5.76f*5.76f/(2f*9.2f)*1.3f).Within(.0001f));
        Assert.That(fastTime,Is.EqualTo(5.76f/9.2f*1.3f).Within(1f/fps+.0001f));
        float previousSlow=2f*2f/(2f*9.2f);
        Assert.That(fast/slow,Is.LessThan((5.76f*5.76f/(2f*9.2f))/previousSlow));
        Assert.That(medium,Is.GreaterThan(slow).And.LessThan(fast));
    }

    [Test]
    public void StrongerLowSpeedCoastStillHasStrictlyIncreasingDistanceAndDurationWithSpeed()
    {
        float previousDistance=0f,previousTime=0f;
        for(int tenth=1;tenth<=100;tenth++)
        {
            float speed=tenth*.1f,deceleration=CombatMovementInertia.CoastDecelerationForSpeed(speed);
            Vector3 next;
            float distance=CombatMovementInertia.Coast(Vector3.right*speed,10f,out next,deceleration).x;
            Assert.That(distance,Is.GreaterThan(previousDistance));
            Assert.That(speed/deceleration,Is.GreaterThan(previousTime));
            Assert.That(next,Is.EqualTo(Vector3.zero));
            previousDistance=distance;previousTime=speed/deceleration;
        }
    }

    [TestCase(30,2f)] [TestCase(60,2f)] [TestCase(120,2f)]
    [TestCase(30,3.2f)] [TestCase(60,3.2f)] [TestCase(120,3.2f)]
    public void LowSpeedCoastIntegratesTheWholeStopEquallyAcrossFrameRates(int fps,float speed)
    {
        float deceleration=CombatMovementInertia.CoastDecelerationForSpeed(speed),ignored;
        float sliced=CoastDistance(speed,fps,out ignored,deceleration);
        Vector3 next;
        float whole=CombatMovementInertia.Coast(Vector3.right*speed,10f,out next,deceleration).x;
        Assert.That(sliced,Is.EqualTo(whole).Within(.0001f));
        Assert.That(next,Is.EqualTo(Vector3.zero));
    }

    [TestCase(30,1.7f)] [TestCase(60,1.7f)] [TestCase(120,1.7f)]
    [TestCase(30,3.4f)] [TestCase(60,3.4f)] [TestCase(120,3.4f)]
    [TestCase(30,5.76f)] [TestCase(60,5.76f)] [TestCase(120,5.76f)]
    public void ApproachCoastKeepsTheSameTravelWithAVisibleHalfSecondResidualTail(int fps,float seed)
    {
        const float planned=.28f;float drag=seed*seed/(2f*planned),elapsed=0f,distance=0f,tail=0f;
        Vector3 velocity=Vector3.right*seed;
        for(int frame=0;frame<fps*2&&velocity.sqrMagnitude>0f;frame++)
        {
            float previous=velocity.magnitude;
            Vector3 step=CombatMovementInertia.ApproachCoast(velocity,seed,elapsed,1f/fps,drag,out velocity);
            Assert.That(velocity.x,Is.InRange(0f,previous+.00001f));
            Assert.That(step.x,Is.GreaterThanOrEqualTo(0f));
            distance+=step.x;if(elapsed>=.25f)tail+=step.x;
            elapsed+=1f/fps;
            float remaining=CombatMovementInertia.ApproachCoastRemainingDistance(velocity.magnitude,seed,elapsed,drag);
            Assert.That(distance+remaining,Is.EqualTo(planned).Within(.00002f));
        }
        Assert.That(distance,Is.EqualTo(planned).Within(.00002f));
        Assert.That(elapsed,Is.InRange(.55f-.0001f,.55f+1f/fps+.0001f));
        Assert.That(tail,Is.GreaterThan(.04f),"The longer duration must include actual late movement, not an invisible near-zero tail.");
        Assert.That(velocity,Is.EqualTo(Vector3.zero));
    }

    [TestCase(1f)] [TestCase(100f)]
    public void OneLongApproachFrameStillStopsAtItsOriginalDistance(float dt)
    {
        const float seed=3.4f,planned=.28f;
        Vector3 next;
        Vector3 step=CombatMovementInertia.ApproachCoast(Vector3.right*seed,seed,0f,dt,
            seed*seed/(2f*planned),out next);
        Assert.That(step.x,Is.EqualTo(planned).Within(.00001f));
        Assert.That(next,Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void AnApproachCollisionCannotRestoreAbsorbedSpeedOrDistanceOnTheNextTailFrame()
    {
        const float seed=3.4f,drag=seed*seed/(2f*.28f);
        Vector3 velocity;
        CombatMovementInertia.ApproachCoast(Vector3.right*seed,seed,0f,.1f,drag,out velocity);
        Vector3 cleanNext;
        Vector3 cleanStep=CombatMovementInertia.ApproachCoast(velocity,seed,.1f,.1f,drag,out cleanNext);
        float remaining=CombatMovementInertia.ApproachCoastRemainingDistance(velocity.magnitude,seed,.1f,drag);
        velocity=Vector3.forward*(velocity.magnitude*.25f);
        Assert.That(CombatMovementInertia.ApproachCoastRemainingDistance(velocity.magnitude,seed,.1f,drag),
            Is.EqualTo(remaining*.25f).Within(.00001f));
        float speed=velocity.magnitude;
        Vector3 next;
        Vector3 step=CombatMovementInertia.ApproachCoast(velocity,seed,.1f,.1f,drag,out next);
        Assert.That(step.x,Is.Zero);Assert.That(step.z,Is.EqualTo(cleanStep.magnitude*.25f).Within(.00001f));
        Assert.That(next.magnitude,Is.EqualTo(cleanNext.magnitude*.25f).Within(.00001f).And.LessThan(speed));
        Assert.That(CombatMovementInertia.ApproachCoast(Vector3.zero,seed,.2f,.1f,drag,out next),Is.EqualTo(Vector3.zero));
        Assert.That(next,Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void AnAlreadyLongApproachStopIsNeverShortenedToTheMinimumPresentationDuration()
    {
        const float seed=2f,drag=1f;
        Assert.That(CombatMovementInertia.ApproachCoastDuration(seed,drag),Is.EqualTo(2f));
        Vector3 next;
        float distance=CombatMovementInertia.ApproachCoast(Vector3.right*seed,seed,0f,10f,drag,out next).x;
        Assert.That(distance,Is.EqualTo(2f));Assert.That(next,Is.EqualTo(Vector3.zero));
    }

    [TestCase(0f)] [TestCase(-.1f)]
    public void PausingCoastPreservesVelocityWithoutMoving(float dt)
    {
        Vector3 initial=new Vector3(3f,0f,-4f),next;
        Assert.That(CombatMovementInertia.Coast(initial,dt,out next),Is.EqualTo(Vector3.zero));
        Assert.That(next,Is.EqualTo(initial));
    }

    [Test]
    public void CoastingIsPlanarAndCannotReverseOrRestartFromRest()
    {
        Vector3 next;
        Vector3 step=CombatMovementInertia.Coast(new Vector3(3f,80f,4f),.05f,out next);
        Assert.That(step.y,Is.Zero);Assert.That(next.y,Is.Zero);
        Assert.That(Vector3.Dot(step,new Vector3(3f,0f,4f)),Is.GreaterThan(0f));
        Assert.That(next.magnitude,Is.LessThan(5f));
        step=CombatMovementInertia.Coast(Vector3.up*80f,.05f,out next);
        Assert.That(step,Is.EqualTo(Vector3.zero));Assert.That(next,Is.EqualTo(Vector3.zero));
        step=CombatMovementInertia.Coast(Vector3.right,.3f,out next);
        Assert.That(step.x,Is.GreaterThan(0f));Assert.That(next,Is.EqualTo(Vector3.zero));
        Assert.That(CombatMovementInertia.Coast(next,.3f,out next),Is.EqualTo(Vector3.zero));
    }

    private static float CoastDistance(float speed,int fps,out float time,
        float deceleration=CombatMovementInertia.CoastDeceleration)
    {
        Vector3 velocity=Vector3.right*speed;
        float distance=0f;time=0f;
        for(int frame=0;frame<fps*3&&velocity.sqrMagnitude>0f;frame++)
        {
            float previous=velocity.x;
            Vector3 step=CombatMovementInertia.Coast(velocity,1f/fps,out velocity,deceleration);
            Assert.That(velocity.x,Is.InRange(0f,previous));
            Assert.That(step.x,Is.GreaterThanOrEqualTo(0f));
            distance+=step.x;time+=1f/fps;
        }
        Assert.That(velocity,Is.EqualTo(Vector3.zero));
        return distance;
    }
}
