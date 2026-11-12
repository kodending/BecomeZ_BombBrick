using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// A released effect retains its cast's accumulated time after the world resumes.
public sealed class CombatSkillClock
{
    private static CombatSkillClock current,exclusive;
    private static readonly FieldInfo WaitSeconds=typeof(WaitForSeconds).GetField("m_Seconds",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
    private readonly List<ParticleState> particles=new List<ParticleState>();
    private float credit,delta,playbackSpeed=1f;
    private int steppedFrame=-1;
    private bool executing;
    public Transform Owner {get;private set;}
    public float Time=>UnityEngine.Time.time+credit;
    public float DeltaTime=>exclusive==null?UnityEngine.Time.deltaTime:
        ReferenceEquals(exclusive,this)&&executing?delta:0f;
    public static float Delta=>current==null?UnityEngine.Time.deltaTime:current.DeltaTime;
    public static float CurrentTime=>current==null?UnityEngine.Time.time:current.Time;
    public static CombatSkillClock Capture()=>current;
    public static bool Active=>exclusive!=null;
    public bool Executing=>ReferenceEquals(exclusive,this)&&executing;

    public CombatSkillClock(Transform owner){Owner=owner;}
    public struct Scope:IDisposable
    {
        private readonly CombatSkillClock previous;
        private readonly bool entered;
        internal Scope(CombatSkillClock value){previous=current;entered=true;current=value;}
        public void Dispose(){if(entered)current=previous;}
    }
    public static Scope Enter(CombatSkillClock value)=>new Scope(value);
    private static bool Owns(Component actor)=>exclusive!=null&&actor!=null&&exclusive.Owner!=null&&
        (actor.transform==exclusive.Owner||actor.transform.IsChildOf(exclusive.Owner));
    public static bool ActorFrozen(Component actor)=>exclusive!=null&&(!exclusive.executing||!Owns(actor));
    public static float ActorDelta(Component actor)=>Owns(actor)&&exclusive.executing?exclusive.delta:UnityEngine.Time.deltaTime;
    public static float ActorTime(Component actor)=>Owns(actor)&&exclusive.executing?exclusive.Time:UnityEngine.Time.time;
    public static Scope ActorScope(Component actor)=>Enter(Owns(actor)&&exclusive.executing?exclusive:null);

    public void Hold()
    {
        if(exclusive!=null&&!ReferenceEquals(exclusive,this))exclusive.End();
        exclusive=this;executing=false;delta=0f;
    }
    public void BeginExecution(float speed)
    {
        exclusive=this;executing=true;playbackSpeed=Mathf.Max(.01f,speed);
        delta=0f;steppedFrame=UnityEngine.Time.frameCount;
    }
    public void Advance()
    {
        if(!Executing||steppedFrame==UnityEngine.Time.frameCount)return;
        steppedFrame=UnityEngine.Time.frameCount;
        delta=UnityEngine.Time.unscaledDeltaTime*playbackSpeed;
        credit+=delta;
    }
    public void End()
    {
        foreach(var state in particles)
            if(state.system!=null)
            {var main=state.system.main;main.useUnscaledTime=state.unscaled;main.simulationSpeed=state.speed;}
        particles.Clear();executing=false;delta=0f;
        if(ReferenceEquals(exclusive,this))exclusive=null;
    }
    private struct ParticleState
    {public ParticleSystem system;public bool unscaled;public float speed;}
    public static void TrackParticle(ParticleSystem system)
    {
        if(current==null||!current.Executing||system==null)return;
        foreach(var state in current.particles)if(state.system==system)return;
        var main=system.main;
        current.particles.Add(new ParticleState{system=system,unscaled=main.useUnscaledTime,speed=main.simulationSpeed});
        main.useUnscaledTime=true;main.simulationSpeed*=current.playbackSpeed;
    }

    public static object Wait(CombatSkillClock clock,float seconds)
    {
        float until=(clock==null?UnityEngine.Time.time:clock.Time)+Mathf.Max(0f,seconds);
        return new WaitUntil(()=>(clock==null?UnityEngine.Time.time:clock.Time)>=until);
    }
    public static IEnumerator Run(IEnumerator routine,CombatSkillClock clock)
    {
        var stack=new Stack<IEnumerator>();stack.Push(routine);
        bool first=true;
        try
        {
            while(stack.Count>0)
            {
                bool canAdvance=exclusive==null?UnityEngine.Time.deltaTime>0f:
                    ReferenceEquals(clock,exclusive)&&exclusive.executing&&(first||clock.delta>0f);
                if(!canAdvance){yield return null;continue;}
                first=false;
                IEnumerator step=stack.Peek();bool more;object value;
                using(Enter(clock)){more=step.MoveNext();value=more?step.Current:null;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
                var seconds=value as WaitForSeconds;
                if(seconds!=null)
                {
                    if(WaitSeconds==null)throw new InvalidOperationException("Unity WaitForSeconds duration is unavailable.");
                    value=Wait(clock,(float)WaitSeconds.GetValue(seconds));
                }
                var nested=value as IEnumerator;
                if(nested!=null){stack.Push(nested);continue;}
                yield return value;
            }
        }
        finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
    }
}
