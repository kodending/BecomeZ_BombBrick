using System.Collections.Generic;
using BombBrick.Combat;
using BombBrick.Game.Core;
using UnityEngine;

public static class MonkeyStormRules
{
    public const int StrikeCount=4;
    public const float Duration=2f,EarliestImpact=.8f,MinimumInterval=.35f,Warning=.25f,Tail=.65f;
    public const float DamageRatio=5f,RadiusTiles=.65f,StunDuration=1f,StunInterval=1.2f;
    public static float[] CreateImpactTimes(System.Random random)
    {
        var times=new float[StrikeCount];
        float spread=Duration-EarliestImpact-MinimumInterval*(StrikeCount-1);
        for(int i=0;i<times.Length;i++)times[i]=(float)random.NextDouble()*spread;
        System.Array.Sort(times);
        for(int i=0;i<times.Length;i++)times[i]+=EarliestImpact+i*MinimumInterval;
        return times;
    }
}

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MonkeyStormCast
    {
        public Unit source;
        public int generation,nextWarning,nextImpact,targetCursor;
        public float time,attack,damageScale;
        public float[] impactTimes;
        public bool awaitingPresentation,released,cancelled;
        public readonly Vector3[] points=new Vector3[MonkeyStormRules.StrikeCount];
        public readonly Dictionary<Unit,int> aimed=new Dictionary<Unit,int>();
        public System.Random random;
        public PixelMonkeyStorm effect;
    }

    private readonly List<MonkeyStormCast> monkeyStorms=new List<MonkeyStormCast>();
    private readonly Dictionary<Unit,float> monkeyStormStunReady=new Dictionary<Unit,float>();
    private float monkeyStormClock;

    private bool IsMonkeyStormActive(Unit source)=>monkeyStorms.Exists(c=>
        c.source==source&&!c.cancelled&&c.generation==combatGeneration);

    private bool IsMonkeyStormPending(Unit source)=>monkeyStorms.Exists(c=>
        c.source==source&&!c.cancelled&&c.generation==combatGeneration&&c.awaitingPresentation);

    private void BeginMonkeyStorm(Unit source,Unit target,SkillMasterRow skill)
    {
        var cast=new MonkeyStormCast{source=source,generation=combatGeneration,attack=source.damage,
            damageScale=source.monkeyBarrageDamageMultiplier,
            random=new System.Random(unchecked(combatGeneration*486187739+source.id*397+source.skillUses*7919))};
        cast.impactTimes=MonkeyStormRules.CreateImpactTimes(cast.random);
        monkeyStorms.Add(cast);
        source.monkeyBarragePending=false;source.monkeyBarrageTarget=null;
        source.monkeyBarrageDamageMultiplier=source.monkeyBarrageRadiusMultiplier=1f;
        source.traitActivations++;
        source.currentAction="monkey_storm_prepare";
        ClearUnitInertia(source);ClearRoute(source);StopUnitMovement(source);
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,.35f);
        var targets=new List<Unit>();
        foreach(Unit enemy in units)
            if(enemy.team!=source.team&&MonkeyStormTarget(enemy))targets.Add(enemy);
        cast.awaitingPresentation=QueueRank4Cinematic(source,targets);
        if(!cast.awaitingPresentation)ReleaseMonkeyStorm(source);
    }

    private void ReleaseMonkeyStorm(Unit source)
    {
        foreach(var cast in monkeyStorms)
        {
            if(cast.source!=source||cast.cancelled||cast.released||cast.generation!=combatGeneration||battleEnding)continue;
            cast.awaitingPresentation=false;cast.released=true;cast.time=0f;
            if(!mlEpisodeActive)
            {
                cast.effect=PixelMonkeyStorm.Play(combatBounds,Rank3Renderer(source),mainCamera);
                cast.effect?.Advance(0f);
            }
            source.attackMovementLockTimer=0f;source.decisionTimer=0f;
            if(IsCombatActive(source))
            {
                source.currentAction="monkey_storm_release";
                source.spriteAnimation?.InterruptCurrentAction(true);
            }
        }
    }

    private void AdvanceMonkeyStormCinematic(Unit source,float absoluteAge)
    {
        var cast=monkeyStorms.FindLast(c=>c.source==source&&c.released&&!c.cancelled&&c.generation==combatGeneration);
        if(cast==null||battleEnding||paused)return;
        float step=Mathf.Max(0f,absoluteAge-cast.time),start=monkeyStormClock;
        monkeyStormClock+=step;
        AdvanceMonkeyStorm(cast,step,start);
    }

    private void UpdateMonkeyStorms(float dt)
    {
        if(paused||dt<=0f)return;
        float start=monkeyStormClock;monkeyStormClock+=dt;
        for(int index=monkeyStorms.Count-1;index>=0;index--)
        {
            if(index>=monkeyStorms.Count)continue;
            var cast=monkeyStorms[index];
            if(cast.cancelled){monkeyStorms.RemoveAt(index);continue;}
            if(cast.generation!=combatGeneration||battleEnding)
            {StopMonkeyStorm(cast,battleEnding);monkeyStorms.RemoveAt(index);continue;}
            if(cast.awaitingPresentation)
            {
                bool queued=rank4Presentation?.request.source==cast.source||
                    rank4PresentationQueue.Exists(request=>request.source==cast.source&&Rank4PresentationValid(request));
                if(queued&&IsCombatActive(cast.source)&&cast.source.hp>0f)
                {cast.source.attackMovementLockTimer=Mathf.Max(cast.source.attackMovementLockTimer,.15f);continue;}
                // A missing or interrupted cutscene cannot consume an already committed storm.
                ReleaseMonkeyStorm(cast.source);
            }
            AdvanceMonkeyStorm(cast,dt,start);
            if(cast.cancelled||cast.time+0.00001f<MonkeyStormRules.Duration+MonkeyStormRules.Tail)continue;
            StopMonkeyStorm(cast,false);monkeyStorms.Remove(cast);
        }
    }

    private void AdvanceMonkeyStorm(MonkeyStormCast cast,float dt,float clockStart)
    {
        float before=cast.time,finish=before+Mathf.Max(0f,dt);
        // Resolve warnings and hits chronologically, including a frame spanning several strikes.
        while(!cast.cancelled&&!battleEnding&&cast.generation==combatGeneration)
        {
            float warning=cast.nextWarning<MonkeyStormRules.StrikeCount?
                cast.impactTimes[cast.nextWarning]-MonkeyStormRules.Warning:float.PositiveInfinity;
            float impact=cast.nextImpact<MonkeyStormRules.StrikeCount?
                cast.impactTimes[cast.nextImpact]:float.PositiveInfinity;
            float at=Mathf.Min(warning,impact);
            if(at>finish+0.00001f)break;
            cast.time=at;
            if(warning<impact)
            {
                int strike=cast.nextWarning++;
                cast.points[strike]=ChooseMonkeyStormPoint(cast);
                cast.effect?.QueueStrike(strike,cast.points[strike],cast.impactTimes[strike]);
            }
            else
            {
                int strike=cast.nextImpact++;
                cast.effect?.Advance(at);
                ApplyMonkeyStormStrike(cast,strike,clockStart+Mathf.Max(0f,at-before));
            }
        }
        cast.time=finish;
        cast.effect?.Advance(finish);
    }

    private static bool MonkeyStormTarget(Unit target)=>IsCombatTargetable(target)&&target.hp>0f&&
        !target.crocodilePreyFlying&&target.augmentReturnTimer<=0f&&target.augmentReturnInvulnerabilityTimer<=0f;

    private Vector3 ChooseMonkeyStormPoint(MonkeyStormCast cast)
    {
        Unit selected=null;int selectedIndex=-1,least=int.MaxValue;
        for(int offset=0;offset<units.Count;offset++)
        {
            int index=(cast.targetCursor+offset)%units.Count;Unit enemy=units[index];
            if(enemy.team==cast.source.team||!MonkeyStormTarget(enemy))continue;
            int count;cast.aimed.TryGetValue(enemy,out count);
            if(count>=least)continue;
            selected=enemy;selectedIndex=index;least=count;
        }
        if(selected!=null)
        {
            cast.aimed[selected]=least+1;cast.targetCursor=(selectedIndex+1)%units.Count;
            return Flat(selected.model.transform.position);
        }
        float margin=TileSize*.4f;
        return new Vector3(Mathf.Lerp(combatBounds.min.x+margin,combatBounds.max.x-margin,(float)cast.random.NextDouble()),
            0f,Mathf.Lerp(combatBounds.min.z+margin,combatBounds.max.z-margin,(float)cast.random.NextDouble()));
    }

    private void ApplyMonkeyStormStrike(MonkeyStormCast cast,int strike,float impactClock)
    {
        Unit source=cast.source;Vector3 point=cast.points[strike];
        if(!mlEpisodeActive)combatCameraDirector?.ReportMonkeyStormThunder();
        for(int index=0;index<units.Count&&!cast.cancelled&&!battleEnding&&cast.generation==combatGeneration;index++)
        {
            Unit enemy=units[index];
            if(enemy.team==source.team||!MonkeyStormTarget(enemy)||
               Vector3.Distance(point,Flat(enemy.model.transform.position))>TileSize*MonkeyStormRules.RadiusTiles+enemy.collisionRadius)continue;
            float dealt=ApplySkillDamage(source,enemy,new SkillDamageFormulaResult(
                cast.attack*MonkeyStormRules.DamageRatio*cast.damageScale,0f,0f),false,
                applyTraitMultiplier:false,allowHitSlide:false,bypassProjectileWalls:true);
            source.skillHits++;source.skillDamage+=dealt;
            RecordSkillEvent(source,"monkey_heavenly_thunderstorm",1,dealt);
            float ready;
            if(!IsCombatActive(enemy)||enemy.combatStatuses.ControlImmune||
               monkeyStormStunReady.TryGetValue(enemy,out ready)&&impactClock+0.00001f<ready)continue;
            float duration=HostileStatusDuration(enemy,MonkeyStormRules.StunDuration);
            if(duration<=0f)continue;
            monkeyStormStunReady[enemy]=impactClock+MonkeyStormRules.StunInterval;
            enemy.giraffeStunTimer=Mathf.Max(enemy.giraffeStunTimer,duration);
            InterruptPositionalAction(enemy);StopUnitMovement(enemy);
            enemy.combatEffectIcons.Show(CombatEffectIconKind.Stun,duration);
        }
        if(!mlEpisodeActive)
            PlayCombatSound(source,CombatSoundCue.Impact,point,.65f,false,false,false,PixelHaetaeThunder.ImpactClip);
    }

    private void StopMonkeyStorm(MonkeyStormCast cast,bool preserveVisualTail)
    {
        if(cast.cancelled)return;
        cast.cancelled=true;cast.awaitingPresentation=false;
        if(cast.effect==null)return;
        if(preserveVisualTail)
        {
            cast.effect.StopEmission();
            CombatEffectEndPlayback.Begin(cast.effect.gameObject,EndingEffectTypes);
            // The ending driver owns Advance; keep OnGUI alive for the final impact flash.
            cast.effect.enabled=true;
        }
        else cast.effect.Cancel();
        cast.effect=null;
    }

    private void ClearMonkeyStorms(bool preserveVisualTail=false)
    {
        foreach(var cast in monkeyStorms)StopMonkeyStorm(cast,preserveVisualTail);
        monkeyStorms.Clear();monkeyStormStunReady.Clear();monkeyStormClock=0f;
    }
}
