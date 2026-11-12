using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed partial class MonkeyArcTests
{
    private void BeetleOverturnBoard(int lane=1,string species="jangpung")
    {
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Type tileType=typeof(MonsterAutoBattlePrototype).GetNestedType("Tile",BindingFlags.NonPublic);
        var tiles=(IDictionary)typeof(MonsterAutoBattlePrototype).GetField("tiles",Private).GetValue(battle);
        for(int y=0;y<4;y++)for(int x=0;x<8;x++)
        {
            var coordinate=new Vector2Int(x,y);object tile=Activator.CreateInstance(tileType,true);
            tileType.GetField("coordinate").SetValue(tile,coordinate);
            tileType.GetField("world").SetValue(tile,MammothCall("GridWorld",coordinate,false));
            tiles.Add(coordinate,tile);
        }
        Set(source,"definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species));
        Set(source,"augmentTranscended",true);Set(source,"skillTimer",7f);Set(source,"skillId",BeetleCombatRules.SkillId);
        Set(source,"skillCooldownDuration",8.5f);Set(source,"defense",250f);
        BeetleOverturnPlace(source,1,lane);BeetleOverturnPlace(target,5,lane);
    }
    private void BeetleOverturnPlace(object unit,int x,int y)
    {
        var coordinate=new Vector2Int(x,y);Set(unit,"currentTile",coordinate);Set(unit,"initialTile",coordinate);
        Model(unit).transform.position=(Vector3)MammothCall("GridWorld",coordinate,false);
    }
    private object BeetleOverturnEnemy(int x,int y)
    {object unit=Unit(1,Vector3.zero);BeetleOverturnPlace(unit,x,y);return unit;}
    private void BeginImperialHorn(object caster=null)
    {Assert.That(MammothCall("BeginBeetleSkyFlip",caster??source,GameMasterData.Skill(BeetleCombatRules.SkillId)),Is.True);}
    private void AdvanceImperialHorn(float seconds)=>MammothCall("UpdateBeetleOverturns",CombatActionSeconds(seconds));

    [TestCase("jangpung")][TestCase("noepung")][TestCase("atlagon")][TestCase("solarbeetle")]
    public void ImperialHornUsesCooldownInsteadOfAnOpeningFlipAndHitsEveryEnemyAtItsOwnGroundPosition(string species)
    {
        BeetleOverturnBoard(1,species);var far=BeetleOverturnEnemy(7,3);var ally=Unit(0,Vector3.left);
        Vector3 first=Model(target).transform.position,last=Model(far).transform.position;
        Assert.That(MammothCall("TryUseSkill",source,false),Is.False);
        Assert.That(Get(source,"beetleOverturn"),Is.Null);Assert.That(Get(source,"skillUses"),Is.EqualTo(0));
        Set(source,"skillTimer",0f);Assert.That(MammothCall("TryUseSkill",source,false),Is.True);
        Assert.That(Get(source,"skillTimer"),Is.EqualTo(8.5f));Assert.That(Get(source,"skillUses"),Is.EqualTo(1));
        AdvanceImperialHorn(BeetleOverturnRules.Warning-.01f);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);
        AdvanceImperialHorn(.02f);Assert.That(Get(target,"beetleOverturnedBy"),Is.Not.Null);
        Assert.That(Get(far,"beetleOverturnedBy"),Is.Not.Null);Assert.That(Get(ally,"beetleOverturnedBy"),Is.Null);
        Assert.That(Model(target).transform.position,Is.EqualTo(first));Assert.That(Model(far).transform.position,Is.EqualTo(last));
        AdvanceImperialHorn(BeetleOverturnRules.Landing-BeetleOverturnRules.Warning);
        Assert.That(Hp(target),Is.EqualTo(4700f).Within(.01f));Assert.That(Hp(far),Is.EqualTo(4700f).Within(.01f));
        Assert.That(Hp(ally),Is.EqualTo(5000f));Assert.That(Get(source,"skillHits"),Is.EqualTo(2));
        Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);Assert.That(Get(far,"beetleOverturnedBy"),Is.Null);
        Assert.That(Model(target).transform.position,Is.EqualTo(first));Assert.That(Model(far).transform.position,Is.EqualTo(last));
        AdvanceImperialHorn(BeetleOverturnRules.End+.01f);Assert.That(Get(source,"beetleOverturn"),Is.Null);
        Assert.That(Get(source,"skillHits"),Is.EqualTo(2));Assert.That(MammothCall("TryUseSkill",source,false),Is.False);
        Set(source,"skillTimer",0f);Assert.That(MammothCall("TryUseSkill",source,false),Is.True);
        AdvanceImperialHorn(BeetleOverturnRules.End+.01f);Assert.That(Get(source,"skillUses"),Is.EqualTo(2));
        Assert.That(Hp(target),Is.EqualTo(4400f).Within(.01f));
    }

    [Test]
    public void ImperialHornAirbornePoseMakesSeveralFullTurnsWithoutMovingTheGroundOrShadow()
    {
        BeetleOverturnBoard();
        var billboard=Model(target).AddComponent<SpriteMonsterBillboard>();Set(target,"spriteBillboard",billboard);
        Vector3 position=Model(target).transform.position,ring=((GameObject)Get(target,"ring")).transform.position;
        float apex=(BeetleOverturnRules.RiseEnd+BeetleOverturnRules.ApexEnd)*.5f;
        BeginImperialHorn();AdvanceImperialHorn(apex);
        float angle=(float)typeof(SpriteMonsterBillboard).GetField("rollAngle",Private).GetValue(billboard);
        var offset=(Vector3)typeof(SpriteMonsterBillboard).GetField("rollOffset",Private).GetValue(billboard);
        Assert.That(Mathf.Abs(angle),Is.GreaterThan(650f));Assert.That(offset.y,Is.GreaterThan(3f));
        Assert.That(Model(target).transform.position,Is.EqualTo(position));Assert.That(((GameObject)Get(target,"ring")).transform.position,Is.EqualTo(ring));
        AdvanceImperialHorn(BeetleOverturnRules.End-apex+.01f);
        Assert.That(typeof(SpriteMonsterBillboard).GetField("rollOffset",Private).GetValue(billboard),Is.EqualTo(Vector3.zero));
        Assert.That(typeof(SpriteMonsterBillboard).GetField("rollAngle",Private).GetValue(billboard),Is.EqualTo(0f));
    }

    [Test]
    public void ImperialHornControlImmuneEnemiesTakeTheLandingHitWithoutBeingMovedOrAirborne()
    {
        BeetleOverturnBoard();((CombatStatusState)Get(target,"combatStatuses")).ControlImmune=true;
        Vector3 position=Model(target).transform.position;BeginImperialHorn();AdvanceImperialHorn(BeetleOverturnRules.Warning+.10f);
        Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Airborne),Is.False);
        AdvanceImperialHorn(BeetleOverturnRules.End);Assert.That(Hp(target),Is.EqualTo(4700f).Within(.01f));
        Assert.That(Model(target).transform.position,Is.EqualTo(position));
    }

    [TestCase("death")][TestCase("freeze")][TestCase("generation")][TestCase("cancel")]
    public void InterruptedImperialHornRestoresAirborneVictimsAndNeverAddsAnUnseenLandingHit(string reason)
    {
        BeetleOverturnBoard();var billboard=Model(target).AddComponent<SpriteMonsterBillboard>();Set(target,"spriteBillboard",billboard);
        Vector3 position=Model(target).transform.position;BeginImperialHorn();AdvanceImperialHorn((BeetleOverturnRules.Warning+BeetleOverturnRules.RiseEnd)*.5f);
        Assert.That(Get(target,"beetleOverturnedBy"),Is.Not.Null);
        if(reason=="death"){Set(source,"alive",false);MammothCall("CleanupRank3Unit",source);}
        else if(reason=="freeze")((CombatStatusState)Get(source,"combatStatuses")).ApplyTimed(CombatStatusKind.Freeze,2f,target);
        else if(reason=="generation")typeof(MonsterAutoBattlePrototype).GetField("combatGeneration",Private).SetValue(battle,1);
        else MammothCall("EndBeetleOverturn",Get(source,"beetleOverturn"));
        AdvanceImperialHorn(BeetleOverturnRules.End+.01f);
        Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);Assert.That(Get(source,"beetleOverturn"),Is.Null);
        Assert.That(Hp(target),Is.EqualTo(5000f));Assert.That(Model(target).transform.position,Is.EqualTo(position));
        Assert.That(typeof(SpriteMonsterBillboard).GetField("rollOffset",Private).GetValue(billboard),Is.EqualTo(Vector3.zero));
        Assert.That(((CombatStatusState)Get(target,"combatStatuses")).Has(CombatStatusKind.Airborne),Is.False);
        Assert.That(Get(source,"attackMovementLockTimer"),Is.EqualTo(0f));
    }

    [Test]
    public void CleansingTheAirbornePoseDoesNotCancelTheCommittedLandingHit()
    {
        BeetleOverturnBoard();BeginImperialHorn();AdvanceImperialHorn(BeetleOverturnRules.RiseEnd);
        ((CombatStatusState)Get(target,"combatStatuses")).Remove(CombatStatusKind.Airborne);AdvanceImperialHorn(.1f);
        Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);AdvanceImperialHorn(BeetleOverturnRules.End);
        Assert.That(Hp(target),Is.EqualTo(4700f).Within(.01f));
    }

    [TestCase(BeetleRank3Rules.ZeroGravity)][TestCase(BeetleRank3Rules.GoldenCoffin)][TestCase(BeetleRank3Rules.MassTheft)]
    public void ImperialHornReplacesEveryRank3PayloadAndItsEcho(string rank3)
    {
        BeetleOverturnBoard();Trait(rank3);BeginImperialHorn();
        Assert.That(Get(source,"beetleFlip"),Is.Null);Assert.That(Get(source,"beetleSkyFlipPending"),Is.False);
        AdvanceImperialHorn(BeetleOverturnRules.End+.01f);
        Assert.That(Get(target,"beetleGravityHeld"),Is.Null);Assert.That(Get(target,"beetleFlippedBy"),Is.Null);
        Assert.That(Get(source,"beetleSweepTimer"),Is.EqualTo(0f));
        float hp=Hp(target);
        Set(source,"skillEchoId",BeetleCombatRules.SkillId);Set(source,"skillEchoTarget",target);MammothCall("ExecuteSkillEcho",source);
        Assert.That(Hp(target),Is.EqualTo(hp));Assert.That(Get(source,"beetleOverturn"),Is.Null);
        Assert.That(MammothCall("TryResolveBeetleRank3",source,target,GameMasterData.Skill(BeetleCombatRules.SkillId),1f,1f,0),Is.False,
            "Rank-3 dispatch also rejects a transcended caster when called directly.");
    }

    [Test]
    public void NonTranscendedBeetleKeepsItsSelectedRank3Skill()
    {
        BeetleOverturnBoard();Set(source,"augmentTranscended",false);Trait(BeetleRank3Rules.ZeroGravity);
        Model(source).transform.position=Model(target).transform.position-Vector3.right*1.5f;
        Assert.That(MammothCall("TryResolveBeetleRank3",source,target,GameMasterData.Skill(BeetleCombatRules.SkillId),1f,1f,0),Is.True);
        Assert.That(Get(target,"beetleGravityHeld"),Is.Not.Null);Assert.That(Get(source,"beetleOverturn"),Is.Null);
    }

    [Test]
    public void MultipleImperialHornsDoNotStealAnotherCastersAirbornePoseAndEachDealsOneHit()
    {
        BeetleOverturnBoard();var other=Unit(0,Vector3.left);Set(other,"definition",Get(source,"definition"));
        Set(other,"augmentTranscended",true);Set(other,"skillCooldownDuration",8.5f);
        BeginImperialHorn();BeginImperialHorn(other);AdvanceImperialHorn(BeetleOverturnRules.Warning+.10f);
        object owner=Get(target,"beetleOverturnedBy");Assert.That(owner,Is.Not.Null);
        AdvanceImperialHorn(BeetleOverturnRules.End);Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);
        Assert.That(Hp(target),Is.EqualTo(4400f).Within(.01f));
        Assert.That(Get(source,"skillHits"),Is.EqualTo(1));Assert.That(Get(other,"skillHits"),Is.EqualTo(1));
    }

    [TestCase("jangpung",false)][TestCase("noepung",false)][TestCase("atlagon",false)]
    [TestCase("solarbeetle",false)][TestCase("jangpung",true)]
    public void ImperialHornBodyPlaysEightDrawingsWithoutAccumulatingItsImpulseAndRestoresItsOriginalTransform(string species,bool shiny)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+species);
        var prefab=Resources.Load<GameObject>(shiny?definition.shinyResourcePath:definition.resourcePath);
        var model=UnityEngine.Object.Instantiate(prefab);
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var renderer=driver.GetComponent<SpriteRenderer>();
        var frames=PixelBeetleOverturn.SourceFrames(species,shiny);
        Assert.That(frames.Length,Is.EqualTo(8));Vector3 originalPosition=driver.transform.localPosition,originalScale=driver.transform.localScale;
        var played=new HashSet<Sprite>();
        driver.PoseImperialHorn(0f,frames);Vector3 neutralPosition=driver.transform.localPosition,neutralScale=driver.transform.localScale;
        for(int step=0;step<=80;step++)
        {
            float time=step*BeetleOverturnRules.End/80f;
            driver.PoseImperialHorn(time,frames);played.Add(renderer.sprite);
            Vector3 position=driver.transform.localPosition,scale=driver.transform.localScale;
            driver.PoseImperialHorn(time,frames);
            Assert.That(driver.transform.localPosition,Is.EqualTo(position));Assert.That(driver.transform.localScale,Is.EqualTo(scale));
        }
        Assert.That(played.Count,Is.EqualTo(8));
        Assert.That(Vector3.Distance(driver.transform.localPosition,neutralPosition),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(driver.transform.localScale,neutralScale),Is.LessThan(.0001f));
        driver.EndBeetleOverturnPose();
        Assert.That(driver.transform.localPosition,Is.EqualTo(originalPosition));
        Assert.That(Vector3.Distance(driver.transform.localScale,originalScale),Is.LessThan(.0001f));
        Assert.That(driver.IsActionLocked,Is.False);
    }

    [TestCase(false)][TestCase(true)]
    public void ImperialHornBodyBracesAtItsFeetThenSnapsUpAndForwardWithMirroredFacing(bool flipped)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_jangpung");
        var model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        var driver=model.GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
        var renderer=driver.GetComponent<SpriteRenderer>();renderer.flipX=flipped;
        var frames=PixelBeetleOverturn.SourceFrames("jangpung",false);
        Bounds anchor=(Bounds)typeof(PixelMantisReaper).GetMethod("VisibleBody",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{frames[7]});
        Vector3 foot=new Vector3((flipped?-1f:1f)*anchor.center.x,anchor.min.y,0f);
        driver.PoseImperialHorn(0f,frames);
        Vector3 position=driver.transform.localPosition,scale=driver.transform.localScale;
        Vector3 planted=position+Vector3.Scale(foot,scale);float height=anchor.size.y*Mathf.Abs(scale.y);
        float facing=flipped?-1f:1f;

        driver.PoseImperialHorn(BeetleOverturnRules.Warning-.01f,frames);
        Vector3 braced=driver.transform.localPosition+Vector3.Scale(foot,driver.transform.localScale);
        Assert.That(driver.transform.localScale.y,Is.LessThan(scale.y*.90f));
        Assert.That(driver.transform.localScale.x,Is.GreaterThan(scale.x*1.05f));
        Assert.That((braced.x-planted.x)*facing,Is.LessThan(-height*.08f));
        Assert.That(braced.y-planted.y,Is.InRange(-height*.08f,-height*.04f),"Compression is anchored at the feet; only the slight authored crouch lowers them.");

        driver.PoseImperialHorn(BeetleOverturnRules.Warning+.10f,frames);
        Vector3 released=driver.transform.localPosition+Vector3.Scale(foot,driver.transform.localScale);
        Assert.That(driver.transform.localScale.y,Is.GreaterThan(scale.y*1.10f));
        Assert.That((released.x-planted.x)*facing,Is.GreaterThan(height*.10f));
        Assert.That(released.y-planted.y,Is.GreaterThan(height*.10f));
        for(int repeat=0;repeat<10;repeat++)driver.PoseImperialHorn(BeetleOverturnRules.Warning+.10f,frames);
        Assert.That(Vector3.Distance(driver.transform.localPosition+Vector3.Scale(foot,driver.transform.localScale),released),Is.LessThan(.0001f));

        driver.PoseImperialHorn(BeetleOverturnRules.End,frames);
        Assert.That(Vector3.Distance(driver.transform.localPosition,position),Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(driver.transform.localScale,scale),Is.LessThan(.0001f));
        driver.EndBeetleOverturnPose();Assert.That(driver.IsActionLocked,Is.False);
    }
}
