using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed partial class MonkeyArcTests
{
    [TestCase(1f)] [TestCase(1.4f)]
    public void WallRushParticlesReachBlastRadiusInEveryDirectionWithoutOvershooting(float radiusScale)
    {
        var fx=PixelMammothWallRush.Play(null,"kogason",Vector3.zero,Vector3.right,radiusScale);
        var systems=fx.GetComponentsInChildren<ParticleSystem>();
        var buffer=new ParticleSystem.Particle[512];
        Vector3 center=Vector3.right*MonsterAutoBattlePrototype.TileSize;
        float radius=MonsterAutoBattlePrototype.TileSize*MammothRank3Rules.WallRushBlastTiles*radiusScale;
        fx.Break(0,.61f);fx.Pose(.81f);
        var sectors=new bool[8];int interior=0;
        foreach(var ps in systems)
        {
            Assert.That(ps.main.simulationSpeed,Is.Zero,"Combat pose must also freeze the burst");
            int count=ps.GetParticles(buffer);Assert.That(count,Is.GreaterThan(0));
            for(int i=0;i<count;i++)
            {
                Vector3 offset=buffer[i].position-center;offset.y=0;
                Assert.That(offset.magnitude+buffer[i].startSize3D.x*.5f,Is.LessThanOrEqualTo(radius+.001f));
                if(offset.magnitude<radius*.65f)interior++;
                if(offset.magnitude>radius*.9f&&buffer[i].startColor.a>102)
                {
                    float angle=Mathf.Repeat(Mathf.Atan2(offset.z,offset.x),Mathf.PI*2);
                    sectors[Mathf.Min(7,Mathf.FloorToInt(angle/(Mathf.PI*.25f)))]=true;
                }
            }
        }
        Assert.That(sectors.All(reached=>reached),Is.True,"Visible debris must reach all sides of the damage area");
        Assert.That(interior,Is.GreaterThan(20),"The blast should fill its interior, not only form a ring");
        fx.Pose(.9f);
        foreach(var ps in systems)
        {
            int count=ps.GetParticles(buffer);
            for(int i=0;i<count;i++)
            {
                Vector3 offset=buffer[i].position-center;offset.y=0;
                Assert.That(offset.magnitude,Is.LessThanOrEqualTo(radius));
            }
        }
        fx.Pose(1.04f);
        foreach(var ps in systems)Assert.That(ps.particleCount,Is.Zero,"Finish before charge cleanup");
        fx.Cancel();
    }
    [TestCase("kogason","RockWall16")] [TestCase("seolgason","IceWall16")]
    [TestCase("irontusk","IronWall16")] [TestCase("gaiason","RootWall16")]
    public void WallRushUsesAuthoredMaterialFramesAndBreaksOnContact(string species,string sheet)
    {
        var fx=PixelMammothWallRush.Play(null,species,Vector3.zero,Vector3.right,1);
        Assert.That(fx,Is.Not.Null);fx.Pose(.51f);
        var walls=fx.GetComponentsInChildren<SpriteRenderer>();Assert.That(walls.Length,Is.EqualTo(3));
        foreach(var wall in walls){Assert.That(wall.sprite.name,Is.EqualTo(sheet+"_04"));
            Assert.That(wall.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));}
        fx.Break(0,.61f);Assert.That(walls[0].sprite.name,Is.EqualTo(sheet+"_05"));
        Assert.That(walls[1].sprite.name,Is.EqualTo(sheet+"_04"));
        for(int i=6;i<=15;i++){fx.Pose(.61f+(i-5)*.033f+.0001f);Assert.That(walls[0].sprite.name,Is.EqualTo(sheet+"_"+i.ToString("D2")));}
        fx.Pose(.98f);Assert.That(walls[0].enabled,Is.False);Assert.That(walls[1].enabled,Is.True);
        fx.Cancel();Assert.DoesNotThrow(()=>fx.Cancel());
    }
    [TestCase("kogason",17.5f,0f)] [TestCase("seolgason",100f,0f)]
    [TestCase("irontusk",21f,21f)] [TestCase("gaiason",13.75f,0f)]
    public void WallRushDamagesOnlyAtThreeWallContactsAndRetainsSpeciesPower(string species,float power,float steel)
    {
        Trait(MammothRank3Rules.WallRush);
        if(steel>0)Trait(MammothCombatRules.IrontuskTraitId);
        var charge=StartMammoth(species);Model(target).transform.position=Vector3.right*3.4f;
        var early=Unit(1,Vector3.right*.1f);var late=Unit(1,Vector3.right*6.7f);
        var outside=Unit(1,new Vector3(3.4f,0,3.1f));var ally=Unit(0,Vector3.right*3.4f);
        MammothCall("AdvanceMammothCharge",source,charge,.609f);Assert.That(Hp(target),Is.EqualTo(5000));
        MammothCall("AdvanceMammothCharge",source,charge,.61f);
        Assert.That(Hp(target),Is.EqualTo(5000-power-steel).Within(.02f));
        Assert.That(Model(source).transform.position.x,Is.EqualTo(1.7f*(1f-.45f)).Within(.005f));
        MammothCall("AdvanceMammothCharge",source,charge,.62f);
        Assert.That(Hp(target),Is.EqualTo(5000-power-steel).Within(.02f),"Contact hold must not deal another hit");
        MammothCall("AdvanceMammothCharge",source,charge,.84f);
        Assert.That(Hp(target),Is.EqualTo(5000-power*2-steel).Within(.02f));
        MammothCall("AdvanceMammothCharge",source,charge,1.07f);
        Assert.That(Hp(target),Is.EqualTo(5000-power*3-steel).Within(.02f));
        MammothCall("AdvanceMammothCharge",source,charge,1.55f);
        Assert.That(Hp(target),Is.EqualTo(5000-power*3-steel).Within(.02f),"No extra path or arrival damage");
        Assert.That(Hp(early),Is.EqualTo(5000-power-steel).Within(.02f));
        Assert.That(Hp(late),Is.EqualTo(5000-power-steel).Within(.02f));
        Assert.That(Hp(outside),Is.EqualTo(5000));Assert.That(Hp(ally),Is.EqualTo(5000));
        Assert.That(Model(target).transform.position.x,Is.EqualTo(3.4f));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Knockdown),Is.False);
        Assert.That(Model(source).transform.position.x,Is.EqualTo(5.95f).Within(.005f));
        Assert.That((int)charge.GetType().GetField("walls").GetValue(charge),Is.EqualTo(3));
    }
    [Test] public void WallRushLongFrameBreaksEveryCrossedWallButCountsShieldVictimsOnce()
    {
        Trait(MammothRank3Rules.WallRush);Trait(MammothCombatRules.KogasonTraitId);
        var charge=StartMammoth("kogason");Model(target).transform.position=Vector3.right*3.4f;
        MammothCall("AdvanceMammothCharge",source,charge,1.27f);
        Assert.That(Hp(target),Is.EqualTo(4947.5f).Within(.01f));
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).AbsorbShield(100f),Is.EqualTo(15f).Within(.01f));
        MammothCall("AdvanceMammothCharge",source,charge,1.4f);Assert.That(Hp(target),Is.EqualTo(4947.5f).Within(.01f));
        MammothCall("EndMammothCharge",source,charge);
        Assert.That(((CombatStatusState)Get(source,"combatStatuses")).DebuffImmune,Is.False);
    }
    [TestCase(false)] [TestCase(true)]
    public void WallRushInterruptionCancelsRemainingWallsAndReleasesMotion(bool generationChange)
    {
        Trait(MammothRank3Rules.WallRush);var charge=StartMammoth("seolgason");
        Model(target).transform.position=Vector3.right*3.4f;
        MammothCall("AdvanceMammothCharge",source,charge,.61f);
        if(generationChange)typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else Set(source,"positionRevision",1);
        MammothCall("AdvanceMammothCharge",source,charge,1.27f);
        Assert.That(Hp(target),Is.EqualTo(4900));Assert.That(Get(source,"mammothCharge"),Is.Null);
        Assert.That((float)Get(source,"attackMovementLockTimer"),Is.Zero);
    }
    [Test] public void WallRushNewObstaclePreventsLaterWallExplosions()
    {
        Trait(MammothRank3Rules.WallRush);var charge=StartMammoth("seolgason");
        Model(target).transform.position=Vector3.right*3.4f;
        MammothCall("AdvanceMammothCharge",source,charge,.61f);
        MammothCall("RegisterRank3IceWall",target,Vector3.right*2.2f,Vector3.right,Vector3.forward,5f,4f);
        MammothCall("AdvanceMammothCharge",source,charge,1.27f);
        Assert.That(Hp(target),Is.EqualTo(4900));Assert.That(Model(source).transform.position.x,Is.LessThan(2.2f));
        Assert.That((int)charge.GetType().GetField("walls").GetValue(charge),Is.EqualTo(1));
    }
    [Test] public void WallRushUsesStrikeFrameOnEveryBreakAndStopsWithFreeze()
    {
        var go=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/057_kogason"));
        var driver=go.GetComponentInChildren<SpriteMonsterAnimationDriver>();var advance=driver.GetType().GetMethod("Advance",Private);
        float clock=0;int ended=0;driver.PlayMammothCharge(()=>true,t=>clock=t,()=>ended++,false,true);
        advance.Invoke(driver,new object[]{.3f});driver.SetFrozen(true);advance.Invoke(driver,new object[]{1f});
        Assert.That(clock,Is.EqualTo(.3f).Within(.001f));driver.SetFrozen(false);
        foreach(float contact in new[]{.61f,.84f,1.07f}){
            advance.Invoke(driver,new object[]{contact-clock+.00001f});
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo((int)driver.GetType().GetField("skillAttackHitFrameIndex",Private).GetValue(driver)));}
        advance.Invoke(driver,new object[]{1.56f-clock});Assert.That(ended,Is.EqualTo(1));
        Assert.That(GameMasterData.Database.traits.First(t=>t.id==MammothRank3Rules.WallRush).displayName,Is.EqualTo("쇄벽 돌진"));
    }
}
