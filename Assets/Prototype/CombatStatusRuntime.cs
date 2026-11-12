using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatEffectIconKind
{
    Shield,AttackUp,Ice,DefenseDown,ResistanceDown,AttackDown,HealingDown,
    Haste,Royal,ToughScales,DeathMark,Hunted,ArmorBroken,DeepFang,Capture,
    DamageReduction,CombinedCast,ReturnMark,TranscendenceGene,Transcended,ApexPredator,
    AttackSpeedDown,
    MoveSpeedDown,AttackSpeedUp,MoveSpeedUp,DefenseUp,ResistanceUp,SkillPowerUp,
    DebuffImmunity,ControlImmunity,Stun,Untargetable,Invulnerable,ChorusMark,
    SporeMark,Taunted,HealthUp,DamageAmplified,Reflection,DamageLink
}

public sealed class CombatEffectIconState
{
    private readonly Dictionary<CombatEffectIconKind,float> remaining=
        new Dictionary<CombatEffectIconKind,float>();
    private readonly List<CombatEffectIconKind> iteration=
        new List<CombatEffectIconKind>();

    public bool Has(CombatEffectIconKind kind)=>remaining.ContainsKey(kind);
    public Dictionary<CombatEffectIconKind,float>.KeyCollection ActiveKinds=>remaining.Keys;
    public Func<CombatEffectIconKind,bool> rejectShow;

    public void Show(
        CombatEffectIconKind kind,float duration=float.PositiveInfinity)
    {
        if(duration<=0f){remaining.Remove(kind);return;}
        if(rejectShow?.Invoke(kind)??false){remaining.Remove(kind);return;}
        if(remaining.TryGetValue(kind,out float current))
            remaining[kind]=Mathf.Max(current,duration);
        else remaining.Add(kind,duration);
    }

    public void Hide(CombatEffectIconKind kind)=>remaining.Remove(kind);

    public void Tick(float deltaTime)
    {
        iteration.Clear();
        foreach(CombatEffectIconKind kind in remaining.Keys)iteration.Add(kind);
        float dt=Mathf.Max(0f,deltaTime);
        for(int i=0;i<iteration.Count;i++)
        {
            CombatEffectIconKind kind=iteration[i];
            float timer=remaining[kind];
            if(float.IsPositiveInfinity(timer))continue;
            timer-=dt;
            if(timer<=0f)remaining.Remove(kind);
            else remaining[kind]=timer;
        }
    }

    public void Clear()
    {
        remaining.Clear();
        iteration.Clear();
    }
}

public enum CombatStatusKind
{
    StickySilk,
    Poison,
    Burning,
    Root,
    Freeze,
    Shield,
    Airborne,
    ComboWeakened,
    ComboBurning,
    ComboChilled,
    ComboShielded,
    ComboRally,
    Knockdown,
    VenomFruit,
    Bleed,
    HallucinatoryPollen,
    Displaced,
    Marked,
    ArmorBroken,
    Silence,
    AttackSpeedDown,
    Fear
}

public enum CombatBenefitKind { Enhancement,Healing,Shield }

// Track still-active contributors without changing stacked damage attribution.
public sealed class CombatEffectSources
{
    private readonly Dictionary<object,float> timers=new Dictionary<object,float>();
    private readonly List<object> iteration=new List<object>();
    public void Apply(object source,float duration)
    {
        if(source==null||duration<=0f)return;
        timers.TryGetValue(source,out float remaining);
        timers[source]=Mathf.Max(remaining,duration);
    }
    public bool Any(Func<object,bool> predicate)
    {
        foreach(var pair in timers)if(pair.Value>0f&&predicate(pair.Key))return true;
        return false;
    }
    public void Tick(float dt)
    {
        iteration.Clear();iteration.AddRange(timers.Keys);
        foreach(object source in iteration)
        {
            float remaining=timers[source]-Mathf.Max(0f,dt);
            if(remaining<=0f)timers.Remove(source);else timers[source]=remaining;
        }
    }
    public void Clear()=>timers.Clear();
}

public sealed class CombatStatusEffect
{
    public CombatStatusKind kind;
    public float remaining;
    public int stacks;
    public float magnitude;
    public object source;
    public bool regionDamageScaled;
    public float tickInterval;
    public float tickRemaining;
    // A held status starts counting down only after its last caster releases it.
    public int activeHolds;
    public readonly CombatEffectSources contributors=new CombatEffectSources();
}

public sealed class CombatStatusState
{
    public const int MaximumPoisonStacks=5;

    private readonly Dictionary<CombatStatusKind,CombatStatusEffect> effects=
        new Dictionary<CombatStatusKind,CombatStatusEffect>();
    private readonly List<CombatStatusKind> expired=new List<CombatStatusKind>();
    private readonly List<CombatStatusEffect> iteration=new List<CombatStatusEffect>();
    private bool debuffImmune;
    private float timedDebuffImmunity;
    private bool controlImmune;
    public float shieldGainMultiplier=1f;
    public float harmfulDurationMultiplier=1f;
    public Func<object,float> sourceDurationMultiplier;
    public Func<object,float> sourceDamageMultiplier;
    public Func<object,float> sourceShieldMultiplier;
    public Func<object,float> sourceBuffMultiplier;
    public event Action<CombatStatusEffect> Applied;
    public Func<bool> externalDebuffImmunity;
    public Action harmfulBlocked;
    public Func<bool> externalBenefitBlock;
    public Action<CombatBenefitKind> benefitBlocked;
    public bool BenefitsBlocked=>externalBenefitBlock?.Invoke()??false;
    public bool RejectBenefit(CombatBenefitKind kind=CombatBenefitKind.Enhancement)
    {
        if(!BenefitsBlocked)return false;
        benefitBlocked?.Invoke(kind);return true;
    }
    public bool PersonalDebuffImmune=>debuffImmune;

    public bool RejectHarmfulEffect()
    {
        if(!DebuffImmune)return false;
        harmfulBlocked?.Invoke();return true;
    }

    public bool DebuffImmune
    {
        get=>debuffImmune||timedDebuffImmunity>0f||(externalDebuffImmunity?.Invoke()??false);
        set
        {
            debuffImmune=value;
            if(!value)return;
            expired.Clear();
            foreach(CombatStatusKind kind in effects.Keys)
                if(IsHarmful(kind))expired.Add(kind);
            for(int i=0;i<expired.Count;i++)effects.Remove(expired[i]);
        }
    }

    public bool ControlImmune
    {
        get=>controlImmune;
        set=>controlImmune=value;
    }

    public IEnumerable<CombatStatusEffect> ActiveEffects=>effects.Values;
    public int Count=>effects.Count;
    public int HarmfulCount
    {
        get
        {
            int count=0;
            foreach(CombatStatusEffect effect in effects.Values)
                if(effect.remaining>0f&&effect.stacks>0&&IsHarmful(effect.kind))count++;
            return count;
        }
    }

    public bool Has(CombatStatusKind kind)
    {
        CombatStatusEffect effect;
        return effects.TryGetValue(kind,out effect)&&effect.remaining>0f&&
               effect.stacks>0&&(kind!=CombatStatusKind.Shield||effect.magnitude>0f);
    }

    public CombatStatusEffect Get(CombatStatusKind kind)
    {
        CombatStatusEffect effect;
        return effects.TryGetValue(kind,out effect)&&effect.remaining>0f?effect:null;
    }

    public CombatStatusEffect ApplyStacks(
        CombatStatusKind kind,float duration,int stacks,int maximumStacks,
        object source=null,float magnitude=0f,float tickInterval=0f,bool add=false,
        bool environmental=false)
    {
        if(!IsHarmful(kind)&&RejectBenefit(kind==CombatStatusKind.ComboRally?CombatBenefitKind.Enhancement:CombatBenefitKind.Shield))
            return new CombatStatusEffect{kind=kind};
        if(IsHarmful(kind)&&RejectHarmfulEffect()||controlImmune&&IsControl(kind))
            return new CombatStatusEffect{kind=kind};
        if(IsHarmful(kind))duration*=Mathf.Clamp01(harmfulDurationMultiplier);
        if(kind==CombatStatusKind.ComboRally)magnitude*=sourceBuffMultiplier?.Invoke(source)??1f;
        bool periodicDamage=kind==CombatStatusKind.Poison||
            kind==CombatStatusKind.Bleed||kind==CombatStatusKind.Burning;
        bool regionScaled=periodicDamage&&sourceDamageMultiplier!=null;
        // Scale each contribution before poison merges different casters and environmental stacks.
        if(regionScaled&&!environmental)magnitude*=sourceDamageMultiplier(source);
        CombatStatusEffect effect=GetOrCreate(kind);
        int previousStacks=effect.stacks;
        int requested=add?effect.stacks+stacks:stacks;
        effect.stacks=Mathf.Clamp(requested,0,Mathf.Max(1,maximumStacks));
        effect.remaining=Mathf.Max(effect.remaining,Mathf.Max(0f,duration));
        if(kind==CombatStatusKind.Poison&&add&&effect.stacks>previousStacks)
            effect.magnitude=(effect.magnitude*previousStacks+
                magnitude*(effect.stacks-previousStacks))/effect.stacks;
        else if(kind!=CombatStatusKind.Poison||!add||previousStacks==0)
            effect.magnitude=magnitude;
        if(source!=null)effect.source=source;
        if(periodicDamage)effect.regionDamageScaled=regionScaled;
        if(kind==CombatStatusKind.Poison||kind==CombatStatusKind.ComboChilled)
            effect.contributors.Apply(source,duration);
        if(tickInterval>0f)
        {
            effect.tickInterval=tickInterval;
            if(effect.tickRemaining<=0f)
                effect.tickRemaining=tickInterval;
            else effect.tickRemaining=Mathf.Min(effect.tickRemaining,tickInterval);
        }
        Applied?.Invoke(effect);
        return effect;
    }

    public CombatStatusEffect ApplyTimed(
        CombatStatusKind kind,float duration,object source=null,float magnitude=0f)
    {
        if(!IsHarmful(kind)&&RejectBenefit(kind==CombatStatusKind.ComboRally?CombatBenefitKind.Enhancement:CombatBenefitKind.Shield))
            return new CombatStatusEffect{kind=kind};
        if(IsHarmful(kind)&&RejectHarmfulEffect()||controlImmune&&IsControl(kind))
            return new CombatStatusEffect{kind=kind};
        if(IsHarmful(kind))duration*=Mathf.Clamp01(harmfulDurationMultiplier);
        if(kind==CombatStatusKind.ComboRally)magnitude*=sourceBuffMultiplier?.Invoke(source)??1f;
        float sourceScale=sourceDurationMultiplier==null?1f:sourceDurationMultiplier(source);
        duration*=sourceScale;
        if(kind==CombatStatusKind.Airborne)magnitude*=sourceScale;
        CombatStatusEffect effect=GetOrCreate(kind);
        effect.stacks=1;
        effect.remaining=Mathf.Max(effect.remaining,Mathf.Max(0f,duration));
        effect.magnitude=Mathf.Max(effect.magnitude,magnitude);
        if(source!=null)effect.source=source;
        if(kind==CombatStatusKind.Poison||kind==CombatStatusKind.ComboChilled)
            effect.contributors.Apply(source,duration);
        Applied?.Invoke(effect);
        return effect;
    }

    public CombatStatusEffect ApplyShield(float duration,float amount,object source=null)
    {
        if(amount>0f&&RejectBenefit(CombatBenefitKind.Shield))return new CombatStatusEffect{kind=CombatStatusKind.Shield};
        duration*=sourceDurationMultiplier==null?1f:sourceDurationMultiplier(source);
        CombatStatusEffect effect=GetOrCreate(CombatStatusKind.Shield);
        effect.stacks=1;
        effect.remaining=Mathf.Max(effect.remaining,Mathf.Max(0f,duration));
        float granted=Mathf.Max(0f,amount)*Mathf.Max(0f,shieldGainMultiplier)*
            (sourceShieldMultiplier?.Invoke(source)??1f);
        if(granted>0f&&granted>=effect.magnitude)
        {
            effect.magnitude=granted;
            effect.source=source;
        }
        return effect;
    }

    public int ConsumeStacks(CombatStatusKind kind,int maximum)
    {
        CombatStatusEffect effect=Get(kind);
        if(effect==null)return 0;
        int consumed=Mathf.Min(Mathf.Max(0,maximum),effect.stacks);
        effect.stacks-=consumed;
        if(effect.stacks<=0)Remove(kind);
        return consumed;
    }

    public float AbsorbShield(float incomingDamage)
    {
        CombatStatusEffect effect=Get(CombatStatusKind.Shield);
        if(effect==null||effect.magnitude<=0f)return 0f;
        float absorbed=Mathf.Min(effect.magnitude,Mathf.Max(0f,incomingDamage));
        effect.magnitude-=absorbed;
        if(effect.magnitude<=0f)Remove(CombatStatusKind.Shield);
        return absorbed;
    }

    public void Tick(
        float deltaTime,Action<CombatStatusEffect> periodicTick=null,
        bool pauseNonControlHarmful=false,bool pauseShield=false)
    {
        float dt=Mathf.Max(0f,deltaTime);
        timedDebuffImmunity=Mathf.Max(0f,timedDebuffImmunity-dt);
        expired.Clear();
        iteration.Clear();
        foreach(CombatStatusEffect current in effects.Values)iteration.Add(current);
        for(int index=0;index<iteration.Count;index++)
        {
            CombatStatusEffect effect=iteration[index];
            bool paused=effect.activeHolds>0||
                        pauseNonControlHarmful&&IsHarmful(effect.kind)&&
                        !IsControl(effect.kind)||
                        pauseShield&&effect.kind==CombatStatusKind.Shield;
            float activeTime=paused?dt:Mathf.Min(dt,effect.remaining);
            if(effect.tickInterval>0f&&activeTime>0f)
            {
                effect.tickRemaining-=activeTime;
                while(effect.tickRemaining<=0f)
                {
                    effect.tickRemaining+=effect.tickInterval;
                    if(periodicTick!=null)periodicTick(effect);
                }
            }
            if(!paused)
            {
                effect.remaining=Mathf.Max(0f,effect.remaining-dt);
                effect.contributors.Tick(dt);
            }
            if(effect.remaining<=0f||effect.stacks<=0||
               effect.kind==CombatStatusKind.Shield&&effect.magnitude<=0f)
                expired.Add(effect.kind);
        }
        for(int i=0;i<expired.Count;i++)effects.Remove(expired[i]);
    }

    public void ApplyDebuffImmunity(float duration)
    {
        if(duration>0f&&RejectBenefit())return;
        timedDebuffImmunity=Mathf.Max(timedDebuffImmunity,Mathf.Max(0f,duration));
        if(timedDebuffImmunity<=0f)return;
        expired.Clear();
        foreach(CombatStatusKind kind in effects.Keys)
            if(IsHarmful(kind))expired.Add(kind);
        for(int i=0;i<expired.Count;i++)effects.Remove(expired[i]);
    }

    public void Remove(CombatStatusKind kind)
    {
        effects.Remove(kind);
    }

    public int RemoveAllHarmful()
    {
        expired.Clear();
        foreach(CombatStatusKind kind in effects.Keys)
            if(IsHarmful(kind))expired.Add(kind);
        int removed=expired.Count;
        for(int i=0;i<expired.Count;i++)effects.Remove(expired[i]);
        expired.Clear();
        return removed;
    }

    public bool RemoveOneHarmful()
    {
        CombatStatusKind? harmful=null;
        foreach(CombatStatusKind kind in effects.Keys)
        {
            if(IsHarmful(kind)){harmful=kind;break;}
        }
        return harmful.HasValue&&effects.Remove(harmful.Value);
    }

    public void Clear()
    {
        effects.Clear();
        expired.Clear();
        iteration.Clear();
        shieldGainMultiplier=1f;
        harmfulDurationMultiplier=1f;
        debuffImmune=false;
        timedDebuffImmunity=0f;
        controlImmune=false;
    }

    public static bool IsHarmful(CombatStatusKind kind)
    {
        return kind!=CombatStatusKind.Shield&&
               kind!=CombatStatusKind.ComboShielded&&
               kind!=CombatStatusKind.ComboRally;
    }

    private static bool IsControl(CombatStatusKind kind)
    {
        return kind==CombatStatusKind.Root||kind==CombatStatusKind.Freeze||
               kind==CombatStatusKind.Airborne||
               kind==CombatStatusKind.Knockdown||
               kind==CombatStatusKind.Displaced||kind==CombatStatusKind.Silence||
               kind==CombatStatusKind.Fear;
    }

    private CombatStatusEffect GetOrCreate(CombatStatusKind kind)
    {
        CombatStatusEffect effect;
        if(effects.TryGetValue(kind,out effect))return effect;
        effect=new CombatStatusEffect{kind=kind};
        effects.Add(kind,effect);
        return effect;
    }
}
