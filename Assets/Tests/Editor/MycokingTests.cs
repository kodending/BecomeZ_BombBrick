using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private object MycokingReady(string intrinsic=null)
    {
        GasReady(intrinsic);Trait(MushroomRank3Rules.MycokingDescent);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        object root=Unit(0,Model(target).transform.position+Vector3.forward);
        ReaperCall("ConfigureMycoking",source,root);
        return root;
    }
    private void MycokingGas(object root,float scale=1f)=>ReaperCall("CreateMushroomGas",source,
        Model(root).transform.position,GameMasterData.Skill(MushroomCombatRules.SkillId),scale,1f,false,0,root,null,0);

    [Test]
    public void MycokingSurvivingEightSecondsDealsSixteenTicksIncludingLastTick()
    {
        var root=MycokingReady();MycokingGas(root);
        ReaperCall("Rank3AreaDamage",source,Model(root).transform.position,
            MonsterAutoBattlePrototype.TileSize*2.5f,.8f,0f,null);
        GasTick(.49f);Assert.That(Hp(target),Is.EqualTo(4920f));
        GasTick(.01f);Assert.That(Hp(target),Is.EqualTo(4900f));
        GasTick(7f);Assert.That(Hp(target),Is.EqualTo(4620f));Assert.That(Get(root,"alive"),Is.True);
        GasTick(.5f);Assert.That(Hp(target),Is.EqualTo(4600f));Assert.That(Get(root,"alive"),Is.False);
        Assert.That(GasZones.Count,Is.Zero);GasTick(5f);Assert.That(Hp(target),Is.EqualTo(4600f));
        Assert.That(Get(source,"skillDamage"),Is.EqualTo(400f));
    }

    [Test]
    public void MycokingIsDamageableRootedAndCannotPerformBasicAttacks()
    {
        var root=MycokingReady();Assert.That(Hp(root),Is.EqualTo(2000f));
        Assert.That(Get(root,"speedTilesPerSecond"),Is.EqualTo(0f));
        Assert.That(ReaperCall("DisplaceUnit",root,Vector3.right,3f,true,false,false,false),Is.False);
        Assert.That(ReaperCall("ResolveRank3BasicAttack",root,target,999f),Is.True);
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That(MoltDamage(target,root,100f),Is.EqualTo(100f));Assert.That(Hp(root),Is.EqualTo(1900f));
    }

    [Test]
    public void MycokingDeathImmediatelyCancelsCloudAndNoLongFrameCatchupDamage()
    {
        var root=MycokingReady();MycokingGas(root);GasTick(1f);
        MoltDamage(target,root,99999f);GasTick(12f);
        Assert.That(Hp(target),Is.EqualTo(4960f));Assert.That(GasZones.Count,Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MycokingSurvivesOwnerDeathAndNewBattleRemovesSummonAndGas(bool reset)
    {
        var root=MycokingReady();MycokingGas(root);
        if(reset)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else MoltDamage(target,source,99999f);
        GasTick(2f);Assert.That(Get(root,"alive"),Is.EqualTo(!reset));
        Assert.That(GasZones.Count,Is.EqualTo(reset?0:1));Assert.That(Hp(target),Is.EqualTo(reset?5000f:4920f));
    }

    [Test]
    public void MycokingReplacementDiscardsOldDamageAndBattleCleanupRemovesAll()
    {
        var first=MycokingReady();MycokingGas(first);GasTick(1f);
        ReaperCall("DeactivateRank3Special",first);
        var second=Unit(0,Model(target).transform.position);ReaperCall("ConfigureMycoking",source,second);MycokingGas(second);
        GasTick(.5f);Assert.That(Hp(target),Is.EqualTo(4940f));Assert.That(GasZones.Count,Is.EqualTo(1));
        ReaperCall("ClearMushroomGasZones");Assert.That(Get(second,"alive"),Is.False);
        GasTick(9f);Assert.That(Hp(target),Is.EqualTo(4940f));
    }

    [Test]
    public void MycokingZoneStaysFixedAndChecksCurrentRangeAtEachTick()
    {
        var root=MycokingReady();MycokingGas(root);Vector3 at=Model(root).transform.position;
        object inside=Unit(1,at+Vector3.right*4.4f),outside=Unit(1,at+Vector3.right*4.7f),ally=Unit(0,at);
        GasTick(.5f);Model(target).transform.position=Vector3.right*30f;Model(source).transform.position=Vector3.left*30f;
        GasTick(.5f);Assert.That(Hp(target),Is.EqualTo(4980f));Assert.That(Hp(inside),Is.EqualTo(4960f));
        Assert.That(Hp(outside),Is.EqualTo(5000f));Assert.That(Hp(ally),Is.EqualTo(5000f));
        Assert.That(Model(root).transform.position,Is.EqualTo(at));
    }

    [Test]
    public void MycokingSharesDamageGateWithCasterEchoAndPauseDoesNotAdvance()
    {
        var root=MycokingReady();MycokingGas(root);GasCreate(center:Model(root).transform.position,scale:.6f,echo:true);
        GasTick(0f);Assert.That(Hp(target),Is.EqualTo(5000f));GasTick(.5f);
        Assert.That(Hp(target),Is.EqualTo(4980f));GasTick(7.5f);Assert.That(Hp(target),Is.EqualTo(4680f));
    }

    [TestCase(MushroomCombatRules.VersusTraitId,436f)]
    [TestCase(MushroomCombatRules.DoksirumTraitId,380f)]
    public void MycokingKeepsGasSpeciesEffectsWithinEightSecondBudget(string intrinsic,float damage)
    {
        var root=MycokingReady(intrinsic);MycokingGas(root);GasTick(8f);
        Assert.That(Hp(target),Is.EqualTo(5000f-damage).Within(.01f));
    }

    [Test]
    public void MycokingHealingTotals105PercentAcrossAllFourBreaths()
    {
        var root=MycokingReady(MushroomCombatRules.LumirumTraitId);
        object ally=Unit(0,Model(root).transform.position);Set(ally,"hp",1000f);MycokingGas(root);GasTick(8f);
        Assert.That(Hp(ally),Is.EqualTo(1105f).Within(.01f));Assert.That(Get(root,"alive"),Is.False);
    }

    [Test]
    public void MycokingVisualBreathesInThenCompressesAndUsesPointPixels()
    {
        var fx=PixelMycoking.Play(null,Vector3.zero,Color.magenta,()=>false);
        fx.Pose(1.95f);Vector2 inhale=fx.BodyRenderer.sprite.rect.size;
        Assert.That(PixelMycoking.FrameAt(1.95f),Is.EqualTo(7));
        fx.Pose(2.05f);Vector2 exhale=fx.BodyRenderer.sprite.rect.size;
        Assert.That(PixelMycoking.FrameAt(2.05f),Is.EqualTo(8));
        Assert.That(exhale.y,Is.LessThan(inhale.y));Assert.That(exhale.x,Is.GreaterThan(inhale.x));
        foreach(var sr in fx.GetComponentsInChildren<SpriteRenderer>())Assert.That(sr.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(fx.Outline.TargetRenderers.Length,Is.EqualTo(1),"Health bars and outlines use the large body, not gas bounds");
        Assert.That(fx.BodyRenderer.sprite.pivot.y,Is.EqualTo(3f).Within(.01f),"Root baseline stays grounded across drawings");
        fx.Depart();typeof(PixelMycoking).GetField("depart",Private).SetValue(fx,.5f);fx.Pose(2.05f);
        Assert.That(fx.BodyRenderer.sprite.rect.height,Is.LessThan(exhale.y*.5f));
        Assert.That(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),Is.GreaterThan(0));
        fx.Cancel();Assert.That(fx==null,Is.True);
    }

    [Test]
    public void MycokingUsesMonsterLayerAndGroundPivotInsteadOfFrontOverlay()
    {
        var host=new GameObject("Mycoking depth reference").AddComponent<SpriteRenderer>();
        host.sortingOrder=10;
        var fx=PixelMycoking.Play(null,new Vector3(1f,0f,2f),Color.magenta,()=>false);
        try
        {
            typeof(PixelMycoking).GetField("reference",Private).SetValue(fx,host);
            foreach(float age in new[]{.15f,.6f,1.95f,2.05f})
            {
                fx.Pose(age);
                Assert.That(fx.BodyRenderer.sortingOrder,Is.EqualTo(host.sortingOrder));
                Assert.That(fx.BodyRenderer.spriteSortPoint,Is.EqualTo(SpriteSortPoint.Pivot));
                Assert.That(fx.BodyRenderer.transform.position,Is.EqualTo(new Vector3(1f,0f,2f)));
            }
            host.sortingOrder=12;fx.Depart();fx.Pose(2.05f);
            Assert.That(fx.BodyRenderer.sortingOrder,Is.EqualTo(12));
        }
        finally{fx.Cancel();UnityEngine.Object.DestroyImmediate(host.gameObject);}
    }
}
