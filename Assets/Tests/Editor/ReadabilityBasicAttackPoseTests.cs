using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ReadabilityBasicAttackPoseTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private const BindingFlags Fields=Hidden|BindingFlags.Public;
    private MonkeyArcTests fixture;
    private MonsterAutoBattlePrototype battle;
    private object actor,victim;
    private GameObject root;
    private SpriteMonsterAnimationDriver driver;
    private bool enabled,active;
    private CombatReadabilityPreset preset;
    private UnityEngine.Random.State random;

    [SetUp] public void Setup()
    {
        enabled=CombatReadabilityPreview.Enabled;active=CombatEffectTestConfiguration.Active;
        preset=CombatReadabilityPreview.Preset;random=UnityEngine.Random.state;
        CombatReadabilityPreview.Enabled=CombatEffectTestConfiguration.Active=true;
        CombatReadabilityPreview.Preset=CombatReadabilityPreset.Local;
        fixture=new MonkeyArcTests();fixture.SetUp();battle=Get<MonsterAutoBattlePrototype>(fixture,"battle");
        actor=Get<object>(fixture,"source");victim=Get<object>(fixture,"target");Source("geomchi");Target();
        var camera=new GameObject("Basic attack pose camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=new Vector3(0f,8f,-12f);camera.transform.LookAt(Vector3.zero);Set(battle,"mainCamera",camera);
    }

    [TearDown] public void Cleanup()
    {
        try{fixture?.TearDown();}
        finally
        {
            CombatReadabilityPreview.Enabled=enabled;CombatEffectTestConfiguration.Active=active;
            CombatReadabilityPreview.Preset=preset;UnityEngine.Random.state=random;
        }
    }

    [TestCase("geomchi",0,.16f,1.08f,.95f)]
    [TestCase("woongi",1,.12f,1.12f,.94f)]
    [TestCase("kongjwi",2,-.055f,.96f,1.04f)]
    public void AnticipationContactAndRecoveryPreserveTheHitClockRootReachAndMovementFootprint(
        string species,int style,float thrust,float impactX,float impactY)
    {
        Source(species);Vector3 position=root.transform.position,scale=root.transform.localScale;
        Vector3 idlePosition=driver.transform.localPosition,idleScale=driver.transform.localScale;
        float reach=(float)Call(battle,"AttackDistance",actor,victim),radius=Get<float>(actor,"collisionRadius");
        float interval=Get<float>(actor,"attackInterval"),bodyScale=Get<float>(actor,"combatBodyScale");int range=Get<int>(actor,"attackRange");
        Call(battle,"CacheMeleeReadabilityBody",actor);Rect footprint=Get<Rect>(actor,"meleeBodyShape");
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);string state=JsonUtility.ToJson(UnityEngine.Random.state);
        driver.BeginReadabilityBasicAttackPose(Vector3.right,style);
        Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(state));
        float hit=driver.BasicAttackHitTime;Advance(hit*.35f);
        float anticipation=WorldOffset(idlePosition).x;
        Assert.That(style==2?anticipation:-anticipation,Is.GreaterThan(.001f));
        Assert.That(hits,Is.Zero);Assert.That(driver.HasPendingBasicAttackHit,Is.True);
        Advance(hit*.65f+.001f);Assert.That(hits,Is.EqualTo(1));Assert.That(driver.HasPendingBasicAttackHit,Is.False);
        Assert.That(WorldOffset(idlePosition).x,Is.EqualTo(thrust).Within(.0001f));
        float authored=(float)Call(driver,"ScaleFor","Attack");
        Assert.That(driver.transform.localScale.x/authored,Is.EqualTo(impactX).Within(.0001f));
        Assert.That(driver.transform.localScale.y/authored,Is.EqualTo(impactY).Within(.0001f));
        Call(battle,"CacheMeleeReadabilityBody",actor);Assert.That(Get<Rect>(actor,"meleeBodyShape"),Is.EqualTo(footprint));
        Assert.That(root.transform.position,Is.EqualTo(position));Assert.That(root.transform.localScale,Is.EqualTo(scale));
        Assert.That(Call(battle,"AttackDistance",actor,victim),Is.EqualTo(reach));Assert.That(Get<float>(actor,"collisionRadius"),Is.EqualTo(radius));
        Assert.That(Get<float>(actor,"attackInterval"),Is.EqualTo(interval));Assert.That(Get<float>(actor,"combatBodyScale"),Is.EqualTo(bodyScale));
        Assert.That(Get<int>(actor,"attackRange"),Is.EqualTo(range));
        UnpinContact();Advance(driver.BasicAttackDuration+.25f);
        Assert.That(Vector3.Distance(driver.transform.localPosition,idlePosition),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(driver.transform.localScale,idleScale),Is.LessThan(.0001f));Assert.That(hits,Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void BothTeamsThrustTowardTheProvidedWorldDirectionDespiteFacingAndParentScale(bool enemy)
    {
        root.transform.rotation=Quaternion.Euler(0f,enemy?180f:0f,0f);root.transform.localScale=Vector3.one*1.6f;
        driver.GetComponent<SpriteRenderer>().flipX=enemy;Vector3 before=driver.transform.localPosition;
        Vector3 direction=enemy?Vector3.left:Vector3.right;driver.PlayBasicAttack(1f,()=>{});
        driver.BeginReadabilityBasicAttackPose(direction,0);Advance(driver.BasicAttackHitTime+.001f);
        Vector3 movement=WorldOffset(before);Assert.That(Vector3.Dot(movement,direction),Is.EqualTo(.16f).Within(.0001f));
        Assert.That(root.transform.position,Is.EqualTo(Vector3.zero));
    }

    [TestCase(false)] [TestCase(true)]
    public void TargetLungeFollowsAllEightProjectedDirectionsWithoutMovingTheCombatRoot(bool enemy)
    {
        var directions=new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back,
            new Vector3(1f,0f,1f),new Vector3(-1f,0f,1f),new Vector3(1f,0f,-1f),new Vector3(-1f,0f,-1f)};
        foreach(Vector3 direction in directions)
        {
            Source("geomchi");FaceForTeam(enemy);
            root.transform.position=new Vector3(-.7f,0f,.4f);
            Transform target=Get<GameObject>(victim,"model").transform;
            target.position=root.transform.position+direction.normalized*4f;
            Vector3 rootPosition=root.transform.position,rootScale=root.transform.localScale;
            Vector3 original=driver.transform.localPosition;Quaternion rootRotation=root.transform.rotation;
            float reach=(float)Call(battle,"AttackDistance",actor,victim),radius=Get<float>(actor,"collisionRadius");
            Call(battle,"CacheMeleeReadabilityBody",actor);Rect footprint=Get<Rect>(actor,"meleeBodyShape");
            int hits=0;Vector3 contactOrigin=Vector3.positiveInfinity;
            driver.PlayBasicAttack(1f,()=>{hits++;contactOrigin=root.transform.position;});
            driver.BeginReadabilityTargetAttackPose(root.transform,target,0);
            float hit=driver.BasicAttackHitTime;Advance(hit-.001f);Assert.That(hits,Is.Zero);
            Advance(.002f);Assert.That(hits,Is.EqualTo(1));
            AssertTowardTarget(original,target);
            Vector3 contactPosition=driver.transform.localPosition,contactScale=driver.transform.localScale;
            for(int i=0;i<3;i++)Advance(0f);
            Assert.That(Vector3.Distance(driver.transform.localPosition,contactPosition),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(driver.transform.localScale,contactScale),Is.LessThan(.0001f));
            Assert.That(contactOrigin,Is.EqualTo(rootPosition));Assert.That(root.transform.position,Is.EqualTo(rootPosition));
            Assert.That(root.transform.localScale,Is.EqualTo(rootScale));Assert.That(root.transform.rotation,Is.EqualTo(rootRotation));
            Assert.That(Call(battle,"AttackDistance",actor,victim),Is.EqualTo(reach));
            Assert.That(Get<float>(actor,"collisionRadius"),Is.EqualTo(radius));
            Call(battle,"CacheMeleeReadabilityBody",actor);Assert.That(Get<Rect>(actor,"meleeBodyShape"),Is.EqualTo(footprint));
            UnpinContact();Advance(driver.BasicAttackDuration+.3f);Assert.That(hits,Is.EqualTo(1));
            Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));
        }
    }

    [Test] public void TargetStretchFollowsHorizontalVerticalAndDiagonalAxes()
    {
        Vector3[] ratios=new Vector3[3];
        for(int i=0;i<ratios.Length;i++)
        {
            Source("geomchi");FaceForTeam(false);
            Vector3 direction=i==0?driver.transform.right:i==1?driver.transform.up:
                (driver.transform.right+driver.transform.up).normalized;
            Transform target=Get<GameObject>(victim,"model").transform;
            target.position=root.transform.position+direction*4f;
            Vector3 original=driver.transform.localPosition;
            driver.PlayBasicAttack(1f,()=>{});driver.BeginReadabilityTargetAttackPose(root.transform,target,0);
            Advance(driver.BasicAttackHitTime*.35f);
            float authored=(float)Call(driver,"ScaleFor","Attack");
            if(i==0)Assert.That(driver.transform.localScale.x,Is.LessThan(driver.transform.localScale.y));
            if(i==1)Assert.That(driver.transform.localScale.y,Is.LessThan(driver.transform.localScale.x));
            Advance(driver.BasicAttackHitTime*.65f+.001f);AssertTowardTarget(original,target);
            ratios[i]=driver.transform.localScale/authored;
        }
        Assert.That(ratios[0].x,Is.GreaterThan(1.06f));Assert.That(ratios[0].y,Is.LessThan(.99f));
        Assert.That(ratios[1].y,Is.GreaterThan(1.06f));Assert.That(ratios[1].x,Is.LessThan(.99f));
        Assert.That(ratios[2].x,Is.InRange(ratios[0].y,ratios[0].x));
        Assert.That(ratios[2].y,Is.InRange(ratios[0].y,ratios[0].x));
        Assert.That(ratios[2].x,Is.EqualTo(ratios[2].y).Within(.0001f));
    }

    [TestCase(false)] [TestCase(true)]
    public void RootTurnsBeforeDriverAndBillboardLateUpdateKeepTheRenderedLungeOnTheCameraTargetAxis(bool enemy)
    {
        FaceForTeam(enemy);var camera=Get<Camera>(battle,"mainCamera");
        var billboard=driver.GetComponent<SpriteMonsterBillboard>();
        Transform target=Get<GameObject>(victim,"model").transform;
        target.position=root.transform.position+new Vector3(.35f,0f,4f);
        Vector3 original=driver.transform.localPosition,idleScale=driver.transform.localScale;
        Vector3 rootPosition=root.transform.position,rootScale=root.transform.localScale;
        int hits=0;float hit=0f;
        for(int phase=0;phase<2;phase++)
        {
            Quaternion turn=Quaternion.Euler(0f,(enemy?-1f:1f)*(phase==0?67f:143f),0f);
            root.transform.rotation=turn;
            if(phase==0)
            {
                driver.PlayBasicAttack(1f,()=>hits++);hit=driver.BasicAttackHitTime;
                driver.BeginReadabilityTargetAttackPose(root.transform,target,0);
            }
            // Match the actual rendering order after RotateUnit changes the parent transform.
            Advance(phase==0?hit*.35f:hit*.65f+.001f);Call(billboard,"LateUpdate");
            Vector3 toward=Vector3.ProjectOnPlane(target.position-rootPosition,camera.transform.forward).normalized;
            Vector3 movement=WorldOffset(original);
            Assert.That(movement.magnitude,Is.GreaterThan(.001f));
            Assert.That(Vector3.Dot(movement.normalized,phase==0?-toward:toward),Is.GreaterThan(.9999f));
            Assert.That(Quaternion.Angle(driver.transform.rotation,camera.transform.rotation),Is.LessThan(.001f));
            if(phase==0)Assert.That(driver.transform.localScale.y,Is.LessThan(driver.transform.localScale.x));
            else Assert.That(driver.transform.localScale.y,Is.GreaterThan(driver.transform.localScale.x));
            Assert.That(hits,Is.EqualTo(phase));Assert.That(root.transform.position,Is.EqualTo(rootPosition));
            Assert.That(root.transform.rotation,Is.EqualTo(turn));Assert.That(root.transform.localScale,Is.EqualTo(rootScale));
        }
        UnpinContact();Advance(driver.BasicAttackDuration+.3f);Call(billboard,"LateUpdate");
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(driver.transform.localScale,idleScale),Is.LessThan(.0001f));
    }

    [TestCase(false)] [TestCase(true)]
    public void MovingLockedTargetIsTrackedAcrossTheAttackEvenWhenAiSelectsSomeoneElse(bool enemy)
    {
        FaceForTeam(enemy);Transform target=Get<GameObject>(victim,"model").transform;
        target.position=root.transform.position+Vector3.right*4f;Vector3 original=driver.transform.localPosition;
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);
        driver.BeginReadabilityTargetAttackPose(root.transform,target,0);float hit=driver.BasicAttackHitTime;
        Advance(hit*.35f);Vector3 first=WorldOffset(original);
        Set(actor,"target",actor);target.position=root.transform.position+Vector3.forward*4f;
        Advance(0f);Vector3 second=WorldOffset(original);
        Assert.That(Vector3.Angle(first,second),Is.GreaterThan(60f));Assert.That(hits,Is.Zero);
        root.transform.position+=new Vector3(.4f,0f,-.2f);
        target.position=root.transform.position+new Vector3(-3f,0f,-2f);
        Vector3 rootPosition=root.transform.position;
        Advance(hit*.65f+.001f);Assert.That(hits,Is.EqualTo(1));AssertTowardTarget(original,target);
        bool invert=Get<bool>(driver.GetComponent<SpriteMonsterBillboard>(),"invertHorizontalFacing");
        Assert.That(driver.GetComponent<SpriteRenderer>().flipX,Is.EqualTo(invert));
        Assert.That(root.transform.position,Is.EqualTo(rootPosition));
        UnpinContact();Advance(driver.BasicAttackDuration+.3f);Assert.That(hits,Is.EqualTo(1));
    }

    [Test] public void FrozenTargetLungeHoldsItsPoseThenResumesTowardTheSameMovingTarget()
    {
        FaceForTeam(false);Transform target=Get<GameObject>(victim,"model").transform;
        Vector3 original=driver.transform.localPosition;int hits=0;
        driver.PlayBasicAttack(1f,()=>hits++);driver.BeginReadabilityTargetAttackPose(root.transform,target,0);
        float hit=driver.BasicAttackHitTime;Advance(hit*.35f);driver.SetFrozen(true);
        Vector3 position=driver.transform.localPosition,scale=driver.transform.localScale;
        float time=driver.BasicAttackAnimationTime;
        for(int i=0;i<3;i++)
        {
            target.position=new Vector3(-2f-i,0f,2f+i);Advance(.5f);
            Assert.That(driver.BasicAttackAnimationTime,Is.EqualTo(time));Assert.That(hits,Is.Zero);
            Assert.That(driver.transform.localPosition,Is.EqualTo(position));Assert.That(driver.transform.localScale,Is.EqualTo(scale));
        }
        driver.SetFrozen(false);Advance(hit*.65f+.001f);Assert.That(hits,Is.EqualTo(1));AssertTowardTarget(original,target);
        UnpinContact();Advance(driver.BasicAttackDuration+.3f);Assert.That(hits,Is.EqualTo(1));
    }

    [TestCase("cancel")] [TestCase("reuse")] [TestCase("preparation")]
    public void TargetLungeCancellationRestoresScaleAndReleasesBothTrackingTransforms(string action)
    {
        FaceForTeam(false);Transform target=Get<GameObject>(victim,"model").transform;
        target.position=new Vector3(2f,0f,3f);Vector3 original=driver.transform.localPosition,idleScale=driver.transform.localScale;
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);driver.BeginReadabilityTargetAttackPose(root.transform,target,0);
        Advance(driver.BasicAttackHitTime*.35f);Assert.That(WorldOffset(original).magnitude,Is.GreaterThan(.001f));
        if(action=="cancel")driver.InterruptCurrentAction();
        else if(action=="reuse")driver.ResetForReuse();
        else driver.SetSkillAnticipating(true);
        Advance(0f);Assert.That(Get<Transform>(driver,"readabilityBasicPoseOrigin"),Is.Null);
        Assert.That(Get<Transform>(driver,"readabilityBasicPoseTarget"),Is.Null);
        Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));
        if(action=="preparation")
        {
            Advance(1f);Assert.That(hits,Is.Zero);driver.SetSkillAnticipating(false);
        }
        Advance(driver.BasicAttackDuration+.3f);
        Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(driver.transform.localScale,idleScale),Is.LessThan(.0001f));
        Assert.That(hits,Is.EqualTo(action=="preparation"?1:0));
    }

    [TestCase("chiwoo",false)] [TestCase("kongjwi",false)] [TestCase("versus",false)]
    [TestCase("holydeer",false)] [TestCase("aranea",false)]
    [TestCase("vivre",false)] [TestCase("golgaebi",false)]
    [TestCase("geomchi",true)] [TestCase("woongi",true)] [TestCase("velos",true)] [TestCase("jangpung",true)]
    public void ProductionHookKeepsProjectileBeamAndTonguePosesAndOnlyLungesMeleeAttacks(string species,bool lunge)
    {
        Source(species);FaceForTeam(false);Transform target=Get<GameObject>(victim,"model").transform;
        target.position=Vector3.right*4f;Vector3 original=driver.transform.localPosition;
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);Call(battle,"TrackReadabilityMeleeAttack",actor,victim);
        Assert.That(Get<Transform>(driver,"readabilityBasicPoseOrigin"),Is.EqualTo(lunge?root.transform:null));
        Assert.That(Get<Transform>(driver,"readabilityBasicPoseTarget"),Is.EqualTo(lunge?target:null));
        Advance(driver.BasicAttackHitTime+.001f);Assert.That(hits,Is.EqualTo(1));
        if(lunge)AssertTowardTarget(original,target);
        else if(species=="kongjwi"||species=="holydeer"||species=="vivre")Assert.That(WorldOffset(original).x,Is.LessThan(0f));
        UnpinContact();Advance(driver.BasicAttackDuration+.3f);Assert.That(hits,Is.EqualTo(1));
    }

    [Test] public void FreezeHoldsTheClockAndPoseThenResumesTheSamePendingHitExactlyOnce()
    {
        int hits=0;driver.PlayBasicAttack(1f,()=>hits++);driver.BeginReadabilityBasicAttackPose(Vector3.right,0);
        float hit=driver.BasicAttackHitTime;Advance(hit*.35f);driver.SetFrozen(true);
        Vector3 position=driver.transform.localPosition,scale=driver.transform.localScale;float time=driver.BasicAttackAnimationTime;
        for(int i=0;i<3;i++)
        {
            Advance(.5f);Assert.That(driver.BasicAttackAnimationTime,Is.EqualTo(time));Assert.That(hits,Is.Zero);
            Assert.That(driver.transform.localPosition,Is.EqualTo(position));Assert.That(driver.transform.localScale,Is.EqualTo(scale));
        }
        Assert.That(driver.HasPendingBasicAttackHit,Is.True);driver.SetFrozen(false);Advance(hit*.65f+.001f);
        Assert.That(hits,Is.EqualTo(1));UnpinContact();Advance(driver.BasicAttackDuration+.3f);Assert.That(hits,Is.EqualTo(1));
        Assert.That(Get<bool>(driver,"readabilityBasicPoseActive"),Is.False);
    }

    [TestCase("cancel")] [TestCase("reuse")] [TestCase("preparation")]
    public void CancellationReuseAndSkillPreparationRemoveTheAppliedPoseWithoutLeavingAnOffset(string action)
    {
        Vector3 original=driver.transform.localPosition;int hits=0;
        driver.PlayBasicAttack(1f,()=>hits++);driver.BeginReadabilityBasicAttackPose(Vector3.right,0);Advance(driver.BasicAttackHitTime*.35f);
        Assert.That(WorldOffset(original).magnitude,Is.GreaterThan(.001f));
        if(action=="cancel")driver.InterruptCurrentAction();
        else if(action=="reuse")driver.ResetForReuse();
        else driver.SetSkillAnticipating(true);
        Call(driver,"LateUpdate");Assert.That(Get<bool>(driver,"readabilityBasicPoseActive"),Is.False);
        Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));Assert.That(hits,Is.Zero);
        if(action=="preparation")
        {
            float time=driver.BasicAttackAnimationTime;Advance(1f);Assert.That(driver.BasicAttackAnimationTime,Is.EqualTo(time));
            driver.SetSkillAnticipating(false);Call(driver,"LateUpdate");
            Assert.That(Vector3.Distance(driver.transform.localPosition,original),Is.LessThan(.0001f));
        }
        else{Advance(driver.BasicAttackDuration+.3f);Assert.That(hits,Is.Zero);}
    }

    [Test] public void CurrentCompactAndOptedOutScenesDoNotAddTheBasicAttackPose()
    {
        var modes=new[]{CombatReadabilityPreset.Current,CombatReadabilityPreset.Compact,
            CombatReadabilityPreset.Local,CombatReadabilityPreset.Local};
        for(int i=0;i<modes.Length;i++)
        {
            CombatReadabilityPreview.Preset=modes[i];CombatReadabilityPreview.Enabled=i!=2;CombatEffectTestConfiguration.Active=i!=3;
            Source("geomchi");Vector3 before=driver.transform.localPosition;int hits=0;
            driver.PlayBasicAttack(1f,()=>hits++);driver.BeginReadabilityBasicAttackPose(Vector3.right,0);Advance(driver.BasicAttackHitTime*.35f);
            Assert.That(Get<bool>(driver,"readabilityBasicPoseActive"),Is.False);
            Assert.That(driver.transform.localPosition,Is.EqualTo(before));Assert.That(hits,Is.Zero);
        }
    }

    [Test] public void ARejectedSkillKeepsTheBasicPoseUntilAnExplicitInterruptAllowsTheSkill()
    {
        int basic=0,skill=0;driver.PlayBasicAttack(1f,()=>basic++);driver.BeginReadabilityBasicAttackPose(Vector3.right,0);
        Advance(driver.BasicAttackHitTime*.35f);driver.PlaySkillAttack(1f,()=>skill++);Call(driver,"LateUpdate");
        Assert.That(Get<bool>(driver,"readabilityBasicPoseActive"),Is.True);Assert.That(driver.HasPendingBasicAttackHit,Is.True);
        Assert.That(driver.HasPendingSkillAttackHit,Is.False);Assert.That(basic,Is.Zero);Assert.That(skill,Is.Zero);
        driver.InterruptCurrentAction();driver.PlaySkillAttack(1f,()=>skill++);Call(driver,"LateUpdate");
        Assert.That(Get<bool>(driver,"readabilityBasicPoseActive"),Is.False);Assert.That(driver.HasPendingBasicAttackHit,Is.False);
        Assert.That(driver.HasPendingSkillAttackHit,Is.True);driver.BeginReadabilityBasicAttackPose(Vector3.right,0);
        Assert.That(Get<bool>(driver,"readabilityBasicPoseActive"),Is.False);
        Advance(driver.SkillAttackHitTime+.01f);Assert.That(basic,Is.Zero);Assert.That(skill,Is.EqualTo(1));
        Advance(.1f);Assert.That(skill,Is.EqualTo(1));
    }

    [Test] public void WizardPosePreservesTheActualReleaseFrameLogicalProjectilePathAndDamageArrival()
    {
        float duration=0f,release=0f;Vector3 start=Vector3.zero,end=Vector3.zero;
        for(int sample=0;sample<2;sample++)
        {
            Source("kongjwi");Target();PixelStarProjectile shot=null;int releases=0;float hp=Get<float>(victim,"hp");
            UnityEngine.Random.InitState(197);driver.PlayBasicAttack(1f,()=>
            {
                releases++;var existing=Object.FindObjectsOfType<PixelStarProjectile>();
                Call(battle,"BeginBasicAttackImpact",actor,victim,100f,false,1f);
                shot=Object.FindObjectsOfType<PixelStarProjectile>().Single(x=>!existing.Contains(x));
            });
            if(sample==1)driver.BeginReadabilityBasicAttackPose(Vector3.right,2);
            float hit=driver.BasicAttackHitTime;Advance(hit*.35f);Assert.That(releases,Is.Zero);Assert.That(Get<float>(victim,"hp"),Is.EqualTo(hp));
            Advance(hit*.65f+.001f);Assert.That(releases,Is.EqualTo(1));Assert.That(shot,Is.Not.Null);
            Assert.That(Get<float>(victim,"hp"),Is.EqualTo(hp));
            if(sample==0)
            {duration=shot.FlightDuration;release=driver.BasicAttackAnimationTime;start=Get<Vector3>(shot,"start");end=Get<Vector3>(shot,"lastTarget");}
            else
            {
                Assert.That(driver.BasicAttackAnimationTime,Is.EqualTo(release).Within(.00001f));
                Assert.That(shot.FlightDuration,Is.EqualTo(duration).Within(.00001f));
                Assert.That(Vector3.Distance(Get<Vector3>(shot,"start"),start),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(Get<Vector3>(shot,"lastTarget"),end),Is.LessThan(.0001f));
            }
            Call(shot,"Advance",1f);Assert.That(Get<float>(victim,"hp"),Is.LessThan(hp));Assert.That(releases,Is.EqualTo(1));
        }
    }

    private void FaceForTeam(bool enemy)
    {
        var team=actor.GetType().GetField("team",Fields);
        team.SetValue(actor,Enum.ToObject(team.FieldType,enemy?1:0));
        root.transform.rotation=Quaternion.Euler(0f,enemy?180f:0f,0f);
        root.transform.localScale=new Vector3(1.6f,1.1f,1.35f);
        driver.GetComponent<SpriteRenderer>().flipX=enemy;
        var billboard=driver.GetComponent<SpriteMonsterBillboard>();
        Call(billboard,"Awake");
        Set(billboard,"cachedCamera",Get<Camera>(battle,"mainCamera"));Call(billboard,"LateUpdate");
    }
    private void AssertTowardTarget(Vector3 original,Transform target)
    {
        Vector3 expected=Vector3.ProjectOnPlane(target.position-root.transform.position,driver.transform.forward).normalized;
        Vector3 movement=WorldOffset(original);
        Assert.That(movement.magnitude,Is.GreaterThan(.025f));
        Assert.That(Vector3.Dot(movement.normalized,expected),Is.GreaterThan(.9999f));
    }
    private void Source(string species)
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get(species);
        root=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));root.transform.position=Vector3.zero;
        driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        Set(actor,"model",root);Set(actor,"definition",definition);Set(actor,"spriteAnimation",driver);Set(actor,"skillId",definition.skillId);
        Set(actor,"attackRange",definition.attackRange);Set(actor,"attackInterval",definition.attackInterval);Set(actor,"collisionRadius",definition.collisionRadius);
        Advance(0f);
    }
    private void Target()
    {
        var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase").Get("velos");
        var model=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));model.transform.position=Vector3.right*4f;
        var animation=model.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var reaction=model.GetComponent<VoxelEnemyReaction>()??model.AddComponent<VoxelEnemyReaction>();reaction.Initialize();
        Set(victim,"model",model);Set(victim,"definition",definition);Set(victim,"spriteAnimation",animation);Set(victim,"reaction",reaction);
        Set(victim,"hp",5000f);Set(victim,"maxHp",5000f);Set(victim,"alive",true);
        Call(animation,"Advance",0f);Call(animation,"LateUpdate");
    }
    private Vector3 WorldOffset(Vector3 original)=>driver.transform.parent==null?
        driver.transform.localPosition-original:driver.transform.parent.TransformVector(driver.transform.localPosition-original);
    private void UnpinContact()=>Set(driver,"readabilityBasicPoseContactFrame",Time.frameCount-1);
    private void Advance(float seconds)
    {Call(driver,"ClearAppliedRecoil");Call(driver,"Advance",seconds);Call(driver,"LateUpdate");}
    private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Fields).GetValue(value);
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Fields).SetValue(value,data);
    private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Fields).Invoke(value,args);
}
