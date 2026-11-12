using UnityEngine;

public static class CombatMovementInertia
{
    public const float AccelerationSeconds=.20f;
    public const float BrakingSeconds=.14f;
    public const float CoastDeceleration=9.2f;
    public const float MaximumCoastSpeed=7.2f;

    // Movement glides 30% farther; direct hit slides retain their separate default drag.
    public static float CoastDecelerationForSpeed(float speed)=>Mathf.Lerp(
        CoastDeceleration*.5f,CoastDeceleration,Mathf.InverseLerp(2.5f,5.5f,speed))/1.3f;

    public static Vector3 Coast(Vector3 velocity,float deltaTime,out Vector3 nextVelocity,
        float deceleration=CoastDeceleration)
    {
        velocity.y=0f;
        nextVelocity=velocity;
        if(deltaTime<=0f)return Vector3.zero;
        float speed=velocity.magnitude;
        if(speed<=.001f){nextVelocity=Vector3.zero;return Vector3.zero;}
        deceleration=Mathf.Max(.001f,deceleration);
        float elapsed=Mathf.Min(deltaTime,speed/deceleration);
        float remaining=Mathf.Max(0f,speed-deceleration*elapsed);
        nextVelocity=remaining<=.001f?Vector3.zero:velocity*(remaining/speed);
        return velocity*((speed+remaining)*.5f*elapsed/speed);
    }

    public static float ApproachCoastDuration(float seedSpeed,float drag)=>
        Mathf.Max(.55f,Mathf.Max(0f,seedSpeed)/Mathf.Max(.001f,drag));

    public static Vector3 ApproachCoast(Vector3 velocity,float seedSpeed,float elapsed,float deltaTime,
        float drag,out Vector3 nextVelocity)
    {
        velocity.y=0f;nextVelocity=velocity;
        if(deltaTime<=0f)return Vector3.zero;
        float speed=velocity.magnitude;
        if(speed<=.001f||seedSpeed<=.001f){nextVelocity=Vector3.zero;return Vector3.zero;}
        elapsed=Mathf.Max(0f,elapsed);
        ApproachCoastState(seedSpeed,elapsed,drag,out float start,out float expectedSpeed);
        if(expectedSpeed<=.001f){nextVelocity=Vector3.zero;return Vector3.zero;}
        ApproachCoastState(seedSpeed,elapsed+deltaTime,drag,out float end,out float nextSpeed);
        // Collision loss is permanent: the planned tail must not restore absorbed momentum.
        float retained=Mathf.Clamp01(speed/expectedSpeed);
        nextVelocity=nextSpeed*retained<=.001f?Vector3.zero:velocity/speed*(nextSpeed*retained);
        return velocity/speed*(Mathf.Max(0f,end-start)*retained);
    }

    public static float ApproachCoastRemainingDistance(float currentSpeed,float seedSpeed,float elapsed,float drag)
    {
        if(currentSpeed<=.001f||seedSpeed<=.001f)return 0f;
        ApproachCoastState(seedSpeed,Mathf.Max(0f,elapsed),drag,out float travelled,out float expectedSpeed);
        if(expectedSpeed<=.001f)return 0f;
        float distance=seedSpeed*seedSpeed/(2f*Mathf.Max(.001f,drag));
        return Mathf.Max(0f,distance-travelled)*Mathf.Clamp01(currentSpeed/expectedSpeed);
    }

    private static void ApproachCoastState(float seedSpeed,float elapsed,float drag,out float distance,out float speed)
    {
        drag=Mathf.Max(.001f,drag);
        float duration=ApproachCoastDuration(seedSpeed,drag),braking=Mathf.Min(.04f,seedSpeed/(4f*drag));
        float total=seedSpeed*seedSpeed/(2f*drag),tail=(2f*total-seedSpeed*braking)/duration;
        if(elapsed>=duration){distance=total;speed=0f;return;}
        if(elapsed<braking)
        {
            speed=seedSpeed+(tail-seedSpeed)*elapsed/braking;
            distance=seedSpeed*elapsed+(tail-seedSpeed)*elapsed*elapsed/(2f*braking);
            return;
        }
        float time=elapsed-braking,remaining=duration-braking;
        speed=tail*(1f-time/remaining);
        distance=(seedSpeed+tail)*braking*.5f+tail*time-tail*time*time/(2f*remaining);
    }

    public static Vector3 Advance(Vector3 velocity,Vector3 desired,float maximumSpeed,
        float deltaTime,float arrivalDistance=float.PositiveInfinity)
    {
        if(deltaTime<=0f)return velocity;
        if(maximumSpeed<=0f)return Vector3.zero;
        velocity.y=desired.y=0f;
        desired=Vector3.ClampMagnitude(desired,maximumSpeed);
        float braking=maximumSpeed/BrakingSeconds;
        float remaining=Mathf.Max(0f,arrivalDistance);
        float arrivalSpeed=Mathf.Min(Mathf.Sqrt(2f*braking*remaining),remaining/deltaTime);
        desired=Vector3.ClampMagnitude(desired,arrivalSpeed);
        bool slowing=desired.sqrMagnitude<velocity.sqrMagnitude||Vector3.Dot(velocity,desired)<0f;
        Vector3 next=Vector3.MoveTowards(velocity,desired,
            (slowing?braking:maximumSpeed/AccelerationSeconds)*deltaTime);
        return Vector3.ClampMagnitude(next,Mathf.Min(maximumSpeed,remaining/deltaTime));
    }

    public static Vector3 AfterCollision(Vector3 requested,Vector3 displacement,float deltaTime)
    {
        if(deltaTime<=0f||Vector3.Dot(displacement,requested)<=0f)return Vector3.zero;
        // Do not store speed into a blocked body, or turn depenetration into a new impulse.
        return Vector3.ClampMagnitude(displacement/deltaTime,requested.magnitude);
    }
}
