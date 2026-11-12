using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    // Action phases use authored animation seconds; world DoT/aura/status seconds stay unscaled.
    private static float CombatActionSeconds(float seconds)=>seconds/BombBrick.Combat.CombatStatFormula.CombatAnimationRate;
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private HashSet<GameObject> existing;
    private MonsterAutoBattlePrototype battle;
    private Type unitType;
    private object source,target;
    private IEnumerator cast;
    private IEnumerator kingCast;

    [SetUp]
    public void SetUp()
    {
        existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        var host=new GameObject("Monkey Arc Test");host.SetActive(false);
        battle=host.AddComponent<MonsterAutoBattlePrototype>();
        unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",BindingFlags.NonPublic);
        var run=typeof(MonsterAutoBattlePrototype).GetField("expeditionRun",Private);
        run.SetValue(battle,Activator.CreateInstance(run.FieldType));
        source=Unit(0,Vector3.zero);target=Unit(1,Vector3.right*4f);
        Set(source,"target",target);
    }

    [TearDown]
    public void TearDown()
    {
        (cast as IDisposable)?.Dispose();
        (kingCast as IDisposable)?.Dispose();
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if(go!=null&&!existing.Contains(go)&&go.scene.IsValid()&&go.transform.parent==null)
                Object.DestroyImmediate(go);
    }

    private object Unit(int team,Vector3 position)
    {
        object unit=Activator.CreateInstance(unitType,true);
        var model=new GameObject("Monkey Test Unit");model.transform.position=position;
        Set(unit,"model",model);Set(unit,"ring",new GameObject("Monkey Ring"));
        Set(unit,"team",Enum.ToObject(unitType.GetField("team").FieldType,team));
        Set(unit,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_chiwoo"));
        Set(unit,"stableId",Guid.NewGuid().ToString());Set(unit,"skillId",MonkeyCombatRules.SkillId);
        Set(unit,"alive",true);Set(unit,"hp",5000f);Set(unit,"maxHp",5000f);
        Set(unit,"damage",100f);Set(unit,"attackInterval",1f);Set(unit,"attackRange",3);
        Set(unit,"collisionRadius",.3f);
        ((IList)typeof(MonsterAutoBattlePrototype).GetField("units",Private).GetValue(battle)).Add(unit);
        return unit;
    }
    private void Set(object unit,string field,object value)=>unitType.GetField(field).SetValue(unit,value);
    private object Get(object unit,string field)=>unitType.GetField(field).GetValue(unit);
    private GameObject Model(object unit)=>(GameObject)Get(unit,"model");
    private float Hp(object unit)=>(float)Get(unit,"hp");
    private void Trait(string id)=>((IList)Get(source,"activeTraitIds")).Add(id);
    private PixelStarProjectile[] Shots()=>Object.FindObjectsOfType<PixelStarProjectile>()
        .Where(p=>p.name=="Pixel Monkey Bean"||p.name=="Pixel Monkey Last Bean")
        .OrderBy(p=>p.name=="Pixel Monkey Last Bean"?1:0).ToArray();
    private void StartCast(float scale=1f)
    {
        cast=(IEnumerator)typeof(MonsterAutoBattlePrototype).GetMethod("ResolveAcrobaticBarrage",Private)
            .Invoke(battle,new[]{source,target,GameMasterData.Skill(MonkeyCombatRules.SkillId),scale,false,
                typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).GetValue(battle)});
    }
    private void LaunchAll()
    {
        StartCast();for(int i=0;i<5;i++)Assert.That(cast.MoveNext(),Is.True);
    }
    private static void Arrive(PixelStarProjectile projectile)
    {
        var callback=typeof(PixelStarProjectile).GetField("onArrive",Private);
        var action=(Action)callback.GetValue(projectile);callback.SetValue(projectile,null);
        action?.Invoke();Object.DestroyImmediate(projectile.gameObject);
    }
    private static Vector3 Pose(PixelStarProjectile projectile,float t)
    {
        typeof(PixelStarProjectile).GetMethod("UpdateVisual",Private).Invoke(projectile,new object[]{t});
        return projectile.transform.GetChild(0).position;
    }

    [Test]
    public void MortarFallCuesFollowVisibleDescentOnceAndStopOnCancellation()
    {
        var falls=new List<int>();var impacts=new List<int>();
        var fx=PixelMonkeyMortar.Play(Vector3.zero,Vector3.right*4f,null,Color.white,1f,2f,
            ()=>true,null,i=>impacts.Add(i),null,i=>falls.Add(i));
        AdvanceMortar(fx,.83f);Assert.That(falls,Is.Empty);
        AdvanceMortar(fx,.03f);Assert.That(falls,Is.EqualTo(new[]{0}));Assert.That(impacts,Is.Empty);
        AdvanceMortar(fx,2f);
        Assert.That(falls,Is.EqualTo(new[]{0,1,2,3,4}));
        Assert.That(impacts,Is.EqualTo(new[]{0,1,2,3,4}));
        falls.Clear();impacts.Clear();bool valid=true;
        fx=PixelMonkeyMortar.Play(Vector3.zero,Vector3.right*4f,null,Color.white,1f,2f,
            ()=>valid,null,i=>impacts.Add(i),null,i=>falls.Add(i));
        AdvanceMortar(fx,.5f);valid=false;AdvanceMortar(fx,2f);
        Assert.That(falls,Is.Empty);Assert.That(impacts,Is.Empty);
    }

    [Test]
    public void BoomerangReturnCuesFollowTurnsOnceAndIgnoreDeflectedFruit()
    {
        var returns=new List<int>();
        var fx=PixelMonkeyReturn.Play(Vector3.zero,Vector3.right,null,Color.white,
            ()=>true,()=>Vector3.zero,null,null,null,float.NaN,(i,p)=>returns.Add(i));
        AdvanceReturn(fx,MonkeyRank3Rules.BoomerangOutDuration-.01f);Assert.That(returns,Is.Empty);
        AdvanceReturn(fx,.03f);Assert.That(returns,Is.EqualTo(new[]{0}));
        AdvanceReturn(fx,3f);Assert.That(returns,Is.EqualTo(new[]{0,1,2,3,4}));
        returns.Clear();
        fx=PixelMonkeyReturn.Play(Vector3.zero,Vector3.right,null,Color.white,
            ()=>true,()=>Vector3.zero,null,null,null,float.NaN,(i,p)=>returns.Add(i));
        var deflected=(bool[])typeof(PixelMonkeyReturn).GetField("deflected",Private).GetValue(fx);
        deflected[0]=true;AdvanceReturn(fx,3f);
        Assert.That(returns,Is.EqualTo(new[]{1,2,3,4}));
    }

    [TestCase(false,false)]
    [TestCase(false,true)]
    [TestCase(true,false)]
    public void ThrownProjectilesTraceVerticalParabolaAndArriveAtMovingTarget(bool heavy,bool basic)
    {
        var shot=basic?PixelStarProjectile.PlayStone(Model(source).transform,null,Model(target).transform,null,Color.white,null):
            PixelStarProjectile.PlayBean(Model(source).transform,null,Model(target).transform,null,Color.white,heavy,2.55f,null);
        Assert.That(shot.transform.GetChild(0).localScale.x,Is.EqualTo(basic?.72f:1.44f).Within(.001f));
        Vector3 a=Pose(shot,0),b=Pose(shot,1),peak=Pose(shot,.5f);
        float height=peak.y-(a.y+b.y)*.5f;
        Assert.That(height,Is.GreaterThan(heavy?1.6f:1f));
        Assert.That(Pose(shot,.25f).y-a.y,Is.EqualTo(height*.75f).Within(.001f));
        Assert.That(Pose(shot,.75f).y-a.y,Is.EqualTo(height*.75f).Within(.001f));
        Assert.That(peak.z,Is.EqualTo(0f).Within(.001f));
        Model(target).transform.position+=Vector3.forward*2f;
        Assert.That(Pose(shot,1),Is.EqualTo(Model(target).transform.position+Vector3.up*.35f));
    }

    [Test]
    public void FiveThrowsDealDamageOnContactWithFinalAreaHitOnly()
    {
        object nearby=Unit(1,Vector3.right*6.4f),outside=Unit(1,Vector3.right*6.7f);
        LaunchAll();var shots=Shots();Assert.That(shots.Length,Is.EqualTo(5));
        Assert.That(Hp(target),Is.EqualTo(5000f));
        for(int i=0;i<4;i++)Arrive(shots[i]);
        Assert.That(Hp(target),Is.EqualTo(4820f).Within(.01f));
        Assert.That(Hp(nearby),Is.EqualTo(5000f));
        Arrive(shots[4]);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(4775f).Within(.01f));
        Assert.That(Hp(nearby),Is.EqualTo(4955f).Within(.01f));
        Assert.That(Hp(outside),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(6));
    }

    [TestCase(MonkeyCombatRules.ChiwooTraitId,270f)]
    [TestCase(MonkeyCombatRules.TotoTraitId,295f)]
    [TestCase(MonkeyCombatRules.MakaTraitId,252f)]
    public void RenewedVisualsPreserveSpeciesIntrinsicDamage(string trait,float damage)
    {
        Trait(trait);LaunchAll();foreach(var shot in Shots())Arrive(shot);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(5000f-Hp(target),Is.EqualTo(damage).Within(.01f));
    }

    [Test]
    public void DeadTargetIsSkippedAndRemainingThrowsRetarget()
    {
        object next=Unit(1,Vector3.right*5f);StartCast();cast.MoveNext();
        var first=Shots()[0];Set(target,"alive",false);Set(target,"hp",0f);Arrive(first);
        Assert.That(Hp(target),Is.Zero);cast.MoveNext();
        Assert.That(typeof(PixelStarProjectile).GetField("target",Private).GetValue(Shots()[0]),
            Is.SameAs(Model(next).transform));
    }

    [Test]
    public void FruitDebrisReachesDamageRadiusOnWorldGroundPlane()
    {
        var burst=PixelStarProjectile.PlayBeanImpact(Vector3.zero,null,Color.yellow,true,2.55f);
        var particles=new ParticleSystem.Particle[32];int count=burst.GetParticles(particles);
        Assert.That(count,Is.EqualTo(28));
        float reach=particles.Take(count).Max(p=>new Vector2(p.velocity.x,p.velocity.z).magnitude*p.startLifetime);
        Assert.That(reach,Is.EqualTo(2.55f).Within(.01f));
        Assert.That(particles.Take(count).Any(p=>p.velocity.z>5f),Is.True);
        Assert.That(PixelStarProjectile.MonkeyFruitSprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(PixelStarProjectile.MonkeyFruitSprite.pixelsPerUnit,Is.EqualTo(PixelStarProjectile.BeanSprite.pixelsPerUnit));
    }

    [TestCase("chiwoo")]
    [TestCase("toto")]
    [TestCase("uno")]
    [TestCase("maka")]
    public void FinalThrowRecoversPromptlyWithoutRedispatchingSkillHit(string monsterId)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+monsterId);
        var prefab=Resources.Load<GameObject>(definition.resourcePath);
        var instance=Object.Instantiate(prefab);
        var driver=instance.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        int hits=0;
        driver.PlaySkillAttack(1f,()=>hits++);
        driver.NotifySkillAttackHitFrameFromAnimation();
        driver.PlayMonkeyThrowFollowThrough(.18f,true,Vector3.right);
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{.2f});
        Assert.That(driver.CurrentMotionName,Is.EqualTo("Idle"));
        Assert.That(driver.IsActionLocked,Is.False);
        Assert.That(hits,Is.EqualTo(1));
    }

    private PixelStarProjectile KingFruit()=>Object.FindObjectsOfType<PixelStarProjectile>()
        .Single(p=>p.name.StartsWith("Monkey King Fruit "));

    private PixelMonkeyReturn LaunchReturn()
    {
        Trait(MonkeyRank3Rules.MyriadReturn);StartCast();
        Assert.That(cast.MoveNext(),Is.True);kingCast=(IEnumerator)cast.Current;
        Assert.That(kingCast.MoveNext(),Is.True);
        return Object.FindObjectOfType<PixelMonkeyReturn>();
    }

    private void AdvanceReturn(PixelMonkeyReturn fx,float seconds)
    {
        var advance=typeof(PixelMonkeyReturn).GetMethod("Advance",Private);
        for(int i=0;i<Mathf.CeilToInt(seconds/.01f)&&fx!=null;i++)advance.Invoke(fx,new object[]{.01f});
    }

    [TestCase("",350f)]
    [TestCase(MonkeyCombatRules.ChiwooTraitId,470f)]
    [TestCase(MonkeyCombatRules.TotoTraitId,560f)]
    [TestCase(MonkeyCombatRules.MakaTraitId,404f)]
    public void BoomerangTenContactsUseNewRatiosAndSpeciesIntrinsics(string intrinsic,float total)
    {
        if(intrinsic!="")Trait(intrinsic);
        Model(target).transform.position=Vector3.right*.9f;
        var fx=LaunchReturn();Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceReturn(fx,2f);Assert.That(fx.Complete,Is.True);
        Assert.That(kingCast.MoveNext(),Is.False);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(5000f-Hp(target),Is.EqualTo(total).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(10));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(total).Within(.01f));
    }

    [TestCase(.01f)]
    [TestCase(.23f)]
    public void BoomerangPiercesAllPathEnemiesOncePerLegEvenAcrossSlowFrames(float step)
    {
        Model(target).transform.position=Vector3.right*.9f;
        object next=Unit(1,Vector3.right*1.3f),farther=Unit(1,Vector3.right*1.7f);
        Set(next,"collisionRadius",1f);Set(farther,"collisionRadius",1f);
        object ally=Unit(0,Vector3.right*2f),outside=Unit(1,new Vector3(2f,0f,8f));
        var fx=LaunchReturn();var advance=typeof(PixelMonkeyReturn).GetMethod("Advance",Private);
        for(int i=0;i<Mathf.CeilToInt(2f/step);i++)advance.Invoke(fx,new object[]{step});
        kingCast.MoveNext();cast.MoveNext();
        Assert.That(Hp(target),Is.EqualTo(4650f).Within(.01f));
        Assert.That(Hp(next),Is.EqualTo(4650f).Within(.01f));
        Assert.That(Hp(farther),Is.EqualTo(4650f).Within(.01f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Hp(outside),Is.EqualTo(5000f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(30));
    }

    [Test]
    public void BoomerangDoesNotTrackEnemiesOrStopAfterOriginalTargetDies()
    {
        Model(target).transform.position=Vector3.right*.9f;
        var fx=LaunchReturn();
        Model(target).transform.position=Vector3.forward*20f;Set(target,"alive",false);
        object inPath=Unit(1,Vector3.right*.9f);
        AdvanceReturn(fx,2f);Assert.That(fx.Complete,Is.True);
        kingCast.MoveNext();cast.MoveNext();
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Hp(inPath),Is.EqualTo(4650f).Within(.01f));
    }

    [Test]
    public void BoomerangCanMissAndStillReturnsWithoutRetargeting()
    {
        var fx=LaunchReturn();Model(target).transform.position=Vector3.forward*20f;
        AdvanceReturn(fx,2f);Assert.That(fx.Complete,Is.True);
        kingCast.MoveNext();cast.MoveNext();
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.Zero);
    }

    [Test]
    public void BoomerangTurnBoundaryDoesNotDoubleHitAndDamageRespectsDefense()
    {
        Model(target).transform.position=Vector3.right*(MonkeyRank3Rules.BoomerangRangeTiles*MonsterAutoBattlePrototype.TileSize);
        Set(target,"collisionRadius",.3f);Set(target,"defense",100f);
        var fx=LaunchReturn();
        var directions=(Vector3[])typeof(PixelMonkeyReturn).GetField("directions",Private).GetValue(fx);
        for(int i=0;i<5;i++)directions[i]=Vector3.right;
        AdvanceReturn(fx,2f);kingCast.MoveNext();cast.MoveNext();
        Assert.That(Get(source,"skillHits"),Is.EqualTo(10));
        Assert.That(5000f-Hp(target),Is.GreaterThan(0f).And.LessThan(350f));
    }

    [Test]
    public void BoomerangCloudVolleyOccursOnlyOnceOnFinalReturnAndOnContact()
    {
        Trait(MonkeyCombatRules.UnoTraitId);
        Model(target).transform.position=Vector3.right*.9f;
        object next=Unit(1,Vector3.right*1.5f),side=Unit(1,new Vector3(1f,0f,3f));
        Set(next,"collisionRadius",1f);
        var fx=LaunchReturn();AdvanceReturn(fx,.9f);
        Assert.That(Object.FindObjectsOfType<PixelStarProjectile>().Length,Is.Zero);
        AdvanceReturn(fx,1f);
        var clouds=Object.FindObjectsOfType<PixelStarProjectile>();Assert.That(clouds.Length,Is.EqualTo(2));
        Assert.That(clouds.All(shot=>Mathf.Abs(shot.transform.GetChild(0).localScale.x-1.872f)<.001f),Is.True);
        float before=Hp(side);Assert.That(before,Is.EqualTo(5000f));
        Assert.That(kingCast.MoveNext(),Is.True);
        foreach(var shot in clouds)Arrive(shot);
        Assert.That(kingCast.MoveNext(),Is.False);cast.MoveNext();
        Assert.That(Hp(side),Is.EqualTo(before-90f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(22));
    }

    [Test]
    public void BoomerangCancelsOnBattleResetWithoutFurtherDamage()
    {
        var fx=LaunchReturn();AdvanceReturn(fx,.3f);float hp=Hp(target);
        var generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private);
        generation.SetValue(battle,(int)generation.GetValue(battle)+1);
        AdvanceReturn(fx,.1f);Assert.That(fx==null,Is.True);
        Assert.That(kingCast.MoveNext(),Is.False);Assert.That(Hp(target),Is.EqualTo(hp));
    }

    [Test]
    public void BoomerangStopsIfCasterDiesDuringASlowFrame()
    {
        bool alive=true;int contacts=0;
        var fx=PixelMonkeyReturn.Play(Vector3.zero,Vector3.right,null,Color.yellow,()=>alive,
            ()=>{Assert.That(alive,Is.True);return Vector3.zero;},null,
            (i,back,a,b)=>{contacts++;alive=false;});
        typeof(PixelMonkeyReturn).GetMethod("Advance",Private).Invoke(fx,new object[]{.5f});
        Assert.That(fx==null,Is.True);Assert.That(contacts,Is.EqualTo(1));
    }

    [Test]
    public void BoomerangUsesRandomAimCurvedGroundPathAndCatchesMovingCaster()
    {
        Vector3 catcher=Vector3.up*.6f;var caught=new Vector3[5];int launches=0;
        var fx=PixelMonkeyReturn.Play(catcher,Vector3.right,null,Color.yellow,()=>true,()=>catcher,
            i=>launches++,(i,back,a,b)=>{if(back)caught[i]=b;});
        var directions=(Vector3[])typeof(PixelMonkeyReturn).GetField("directions",Private).GetValue(fx);
        Assert.That(directions.All(d=>Vector3.Angle(Vector3.right,d)<=30.01f),Is.True);
        Assert.That(directions.Distinct().Count(),Is.GreaterThan(1));
        var point=typeof(PixelMonkeyReturn).GetMethod("Point",Private);
        Vector3 middle=(Vector3)point.Invoke(fx,new object[]{0,.29f,catcher});
        Vector3 side=new Vector3(-directions[0].z,0f,directions[0].x);
        Assert.That(Vector3.Dot(middle-catcher,side),Is.GreaterThan(.3f));
        Assert.That(middle.y,Is.EqualTo(catcher.y));
        AdvanceReturn(fx,.65f);catcher+=new Vector3(1f,0f,2f);AdvanceReturn(fx,1f);
        Assert.That(fx.Complete,Is.True);Assert.That(launches,Is.EqualTo(5));
        Assert.That(caught.All(p=>Vector3.Distance(p,catcher)<.001f),Is.True);
        var renderers=(SpriteRenderer[,])typeof(PixelMonkeyReturn).GetField("fruit",Private).GetValue(fx);
        Assert.That(renderers.Cast<SpriteRenderer>().All(r=>!r.enabled),Is.True);
        Assert.That(renderers[0,0].transform.localScale.x,Is.EqualTo(1.4f).Within(.001f));
        Assert.That(renderers[4,0].transform.localScale.x,Is.EqualTo(1.76f).Within(.001f));
        Assert.That(renderers[0,0].sprite.name,Is.EqualTo("Pixel Banana Boomerang"));
        Assert.That(renderers[0,0].sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
    }

    private PixelMonkeyMortar LaunchMortar()
    {
        Trait(MonkeyRank3Rules.FiveOrbJuggling);StartCast();
        Assert.That(cast.MoveNext(),Is.True);kingCast=(IEnumerator)cast.Current;
        Assert.That(kingCast.MoveNext(),Is.True);
        return Object.FindObjectOfType<PixelMonkeyMortar>();
    }

    private void AdvanceMortar(PixelMonkeyMortar fx,float seconds)=>
        typeof(PixelMonkeyMortar).GetMethod("Advance",Private).Invoke(fx,new object[]{seconds});

    [TestCase("",450f)]
    [TestCase(MonkeyCombatRules.ChiwooTraitId,555f)]
    [TestCase(MonkeyCombatRules.TotoTraitId,520f)]
    [TestCase(MonkeyCombatRules.MakaTraitId,540f)]
    public void MortarDealsDamageOnlyAtFiveContactsWithSpeciesIntrinsics(string intrinsic,float total)
    {
        if(intrinsic!="")Trait(intrinsic);
        var fx=LaunchMortar();Assert.That(Shots().Length,Is.Zero);
        AdvanceMortar(fx,1f);Assert.That(Hp(target),Is.EqualTo(5000f));
        AdvanceMortar(fx,1f);Assert.That(fx.Complete,Is.True);
        Assert.That(kingCast.MoveNext(),Is.False);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(5000f-Hp(target),Is.EqualTo(total).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(5));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(total).Within(.01f));
    }

    [Test]
    public void MortarKeepsCastLocationAfterPrimaryMovesAndDies()
    {
        Vector3 selected=Model(target).transform.position;
        object neighbor=Unit(1,selected+Vector3.forward),ally=Unit(0,selected);
        Set(source,"monkeyBarragePending",true);Set(source,"monkeyBarrageCenter",selected);
        Model(target).transform.position=Vector3.right*40f;Set(target,"alive",false);
        var fx=LaunchMortar();AdvanceMortar(fx,2f);kingCast.MoveNext();cast.MoveNext();
        Assert.That(Hp(neighbor),Is.EqualTo(4550f).Within(.01f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Hp(target),Is.EqualTo(5000f));
    }

    [TestCase(false,150f)]
    [TestCase(true,240f)]
    public void MortarExpandsOnlyFinalBlastAndRespectsDefense(bool powder,float damage)
    {
        if(powder)Trait(MonkeyCombatRules.MakaTraitId);
        object edge=Unit(1,Vector3.right*(powder?8f:7.2f)),outside=Unit(1,Vector3.right*9f);
        Set(target,"defense",100f);
        var fx=LaunchMortar();AdvanceMortar(fx,1.5f);
        Assert.That(Hp(edge),Is.EqualTo(5000f));AdvanceMortar(fx,.3f);
        kingCast.MoveNext();cast.MoveNext();
        Assert.That(5000f-Hp(edge),Is.EqualTo(damage).Within(.01f));
        Assert.That(Hp(outside),Is.EqualTo(5000f));
        Assert.That(5000f-Hp(target),Is.GreaterThan(0f).And.LessThan(powder?540f:450f));
    }

    [Test]
    public void MortarConsecutiveBonusResetsForEnemiesWhoLeaveTheArea()
    {
        Trait(MonkeyCombatRules.ChiwooTraitId);var fx=LaunchMortar();
        AdvanceMortar(fx,1.03f);Model(target).transform.position=Vector3.right*40f;
        AdvanceMortar(fx,.15f);Model(target).transform.position=Vector3.right*4f;
        AdvanceMortar(fx,.2f);AdvanceMortar(fx,.5f);kingCast.MoveNext();cast.MoveNext();
        Assert.That(5000f-Hp(target),Is.EqualTo(75f+75f+82.5f+180f).Within(.01f));
    }

    [Test]
    public void MortarCancellationStopsRemainingContactsAndRemovesVisuals()
    {
        var fx=LaunchMortar();AdvanceMortar(fx,1.03f);float hp=Hp(target);
        var generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private);
        generation.SetValue(battle,(int)generation.GetValue(battle)+1);
        AdvanceMortar(fx,1f);Assert.That(fx==null,Is.True);
        Assert.That(kingCast.MoveNext(),Is.False);Assert.That(Hp(target),Is.EqualTo(hp));
    }

    [Test]
    public void MortarUnoCloudsWaitForFinalBlastAndTheirOwnImpact()
    {
        Trait(MonkeyCombatRules.UnoTraitId);
        object neighbor=Unit(1,Model(target).transform.position+Vector3.forward);
        var fx=LaunchMortar();AdvanceMortar(fx,1.5f);
        Assert.That(Object.FindObjectsOfType<PixelStarProjectile>().Length,Is.Zero);
        AdvanceMortar(fx,.3f);Assert.That(Hp(neighbor),Is.EqualTo(4550f));
        Assert.That(kingCast.MoveNext(),Is.True);
        foreach(var shot in Object.FindObjectsOfType<PixelStarProjectile>())Arrive(shot);
        Assert.That(kingCast.MoveNext(),Is.False);cast.MoveNext();
        Assert.That(Hp(neighbor),Is.EqualTo(4400f));
    }

    [Test]
    public void MortarHidesAboveScreenThenFallsVerticallyOverGrowingPixelShadow()
    {
        int impacts=0;Vector3 ground=new Vector3(4f,0f,2f);
        var fx=PixelMonkeyMortar.Play(Vector3.zero,ground,null,Color.yellow,2.55f,3.4f,
            ()=>true,null,i=>impacts++);
        var renderers=(SpriteRenderer[,])typeof(PixelMonkeyMortar).GetField("fruit",Private).GetValue(fx);
        var shadow=(SpriteRenderer)typeof(PixelMonkeyMortar).GetField("shadow",Private).GetValue(fx);
        AdvanceMortar(fx,.7f);Assert.That(renderers.Cast<SpriteRenderer>().All(r=>!r.enabled),Is.True);
        Assert.That(shadow.enabled,Is.True);float scale=shadow.transform.localScale.x,alpha=shadow.color.a;
        AdvanceMortar(fx,.18f);Vector3 before=renderers[0,0].transform.position;
        Assert.That(renderers[0,0].enabled,Is.True);AdvanceMortar(fx,.08f);
        Assert.That(renderers[0,0].transform.localScale.x,Is.EqualTo(1.28f).Within(.001f));
        Vector3 after=renderers[0,0].transform.position;
        Assert.That(after.x,Is.EqualTo(ground.x));Assert.That(after.z,Is.EqualTo(ground.z));
        Assert.That(after.y,Is.LessThan(before.y));Assert.That(impacts,Is.Zero);
        Assert.That(shadow.transform.localScale.x,Is.GreaterThan(scale));Assert.That(shadow.color.a,Is.GreaterThan(alpha));
        Assert.That(shadow.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        AdvanceMortar(fx,.07f);Assert.That(impacts,Is.EqualTo(1));
    }

    private void LaunchKingFruit()
    {
        Trait(MonkeyRank3Rules.MountainCollapse);StartCast();
        for(int i=0;i<4;i++)Assert.That(cast.MoveNext(),Is.True);
        foreach(var shot in Shots())Arrive(shot);
        Assert.That(cast.MoveNext(),Is.True);
        kingCast=(IEnumerator)cast.Current;
        Assert.That(kingCast.MoveNext(),Is.True);
    }

    [TestCase("",420f)]
    [TestCase(MonkeyCombatRules.ChiwooTraitId,543f)]
    [TestCase(MonkeyCombatRules.TotoTraitId,560f)]
    [TestCase(MonkeyCombatRules.MakaTraitId,492f)]
    public void KingFruitDealsFourSmallHitsThenThreeLandingHits(string intrinsic,float total)
    {
        if(intrinsic!="")Trait(intrinsic);
        LaunchKingFruit();
        for(int stage=0;stage<3;stage++)
        {
            var fruit=KingFruit();Assert.That(fruit.name,Does.EndWith(stage.ToString()));
            Assert.That(fruit.transform.GetChild(0).localScale.x,Is.EqualTo(1.8f).Within(.001f));
            float hp=Hp(target);Arrive(fruit);Assert.That(Hp(target),Is.LessThan(hp));
            Assert.That(kingCast.MoveNext(),Is.EqualTo(stage<2));
        }
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(5000f-Hp(target),Is.EqualTo(total).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(7));
        Assert.That((float)Get(source,"skillDamage"),Is.EqualTo(total).Within(.01f));
    }

    [Test]
    public void KingFruitHopsTowardUnvisitedEnemiesByAtMostOneTile()
    {
        object second=Unit(1,Vector3.right*7f),third=Unit(1,new Vector3(8f,0f,2f));
        LaunchKingFruit();var first=KingFruit();Vector3 a=Pose(first,1f);Arrive(first);
        kingCast.MoveNext();var next=KingFruit();Vector3 b=Pose(next,1f);
        Assert.That(Vector3.Distance(a,b),Is.EqualTo(MonsterAutoBattlePrototype.TileSize).Within(.001f));
        Assert.That(b.x,Is.GreaterThan(a.x));Arrive(next);kingCast.MoveNext();
        Vector3 c=Pose(KingFruit(),1f);
        Assert.That(Vector3.Distance(b,c),Is.LessThanOrEqualTo(MonsterAutoBattlePrototype.TileSize+.001f));
        Assert.That(c.z,Is.GreaterThan(b.z));
    }

    [Test]
    public void KingFruitExpandsOnlyAtFinalLandingAndRespectsDefense()
    {
        object edge=Unit(1,Vector3.right*6.8f);
        Set(edge,"alive",false); // Keep all bounces stationary, then bring the edge target back.
        Set(target,"defense",100f);
        LaunchKingFruit();float afterPellets=Hp(target);
        for(int stage=0;stage<3;stage++)
        {
            var fruit=KingFruit();if(stage==2)Set(edge,"alive",true);
            Arrive(fruit);kingCast.MoveNext();
        }
        cast.MoveNext();
        Assert.That(afterPellets-Hp(target),Is.LessThan(240f));
        Assert.That(5000f-Hp(edge),Is.EqualTo(120f).Within(.01f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.False);
    }

    [Test]
    public void KingFruitCancelsWithoutDamageWhenBattleGenerationChanges()
    {
        LaunchKingFruit();float hp=Hp(target);
        var generation=typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private);
        generation.SetValue(battle,(int)generation.GetValue(battle)+1);
        Arrive(KingFruit());Assert.That(kingCast.MoveNext(),Is.False);
        Assert.That(Hp(target),Is.EqualTo(hp));
    }

    [Test]
    public void UnoCloudShotsOccurOnlyAfterFinalLandingAndWaitForTheirOwnContact()
    {
        Trait(MonkeyCombatRules.UnoTraitId);
        object left=Unit(1,new Vector3(4f,0f,-1f)),right=Unit(1,new Vector3(4f,0f,1f));
        Set(left,"alive",false);Set(right,"alive",false);
        LaunchKingFruit();
        for(int stage=0;stage<2;stage++)
        {
            Arrive(KingFruit());kingCast.MoveNext();
            Assert.That(Object.FindObjectsOfType<PixelStarProjectile>()
                .Count(p=>p.name=="Pixel Monkey Cloud Bounce"),Is.Zero);
        }
        Set(left,"alive",true);Set(right,"alive",true);
        Arrive(KingFruit());Assert.That(kingCast.MoveNext(),Is.True);
        Assert.That(Hp(left),Is.EqualTo(4880f).Within(.01f));
        var clouds=Object.FindObjectsOfType<PixelStarProjectile>()
            .Where(p=>p.name=="Pixel Monkey Cloud Bounce").ToArray();
        Assert.That(clouds.Length,Is.EqualTo(2));foreach(var cloud in clouds)Arrive(cloud);
        Assert.That(kingCast.MoveNext(),Is.False);Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Hp(left),Is.EqualTo(4760f).Within(.01f));
        Assert.That(Hp(right),Is.EqualTo(4760f).Within(.01f));
    }

    [Test]
    public void KingFruitShowsCracksAndSquashWithoutRepeatingContact()
    {
        int hits=0;var fruit=PixelStarProjectile.PlayKingFruit(Vector3.up,
            Vector3.right,null,Color.yellow,0,()=>hits++);
        var update=typeof(PixelStarProjectile).GetMethod("Advance",Private);
        typeof(PixelStarProjectile).GetField("elapsed",Private).SetValue(fruit,fruit.FlightDuration);
        update.Invoke(fruit,new object[]{.01f});
        Assert.That(hits,Is.EqualTo(1));
        var head=fruit.transform.GetChild(0).GetComponent<SpriteRenderer>();
        Assert.That(head.sprite,Is.Not.SameAs(PixelStarProjectile.MonkeyFruitSprite));
        Vector3 scale=head.transform.localScale;update.Invoke(fruit,new object[]{.01f});
        Assert.That(hits,Is.EqualTo(1));
        Assert.That(head.transform.localScale.x,Is.GreaterThanOrEqualTo(scale.x));
        Assert.That(head.transform.localScale.y,Is.LessThanOrEqualTo(scale.y));
        update.Invoke(fruit,new object[]{.08f});
        Assert.That(fruit!=null,Is.True,"Keep the fruit visible until its next arc starts.");
        Assert.That(fruit.FruitBounceReady,Is.True);
        Assert.That(hits,Is.EqualTo(1));
    }
}
