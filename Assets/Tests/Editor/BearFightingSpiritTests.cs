using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class BearFightingSpiritTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [TestCase(0)] [TestCase(1)]
    public void MountainShieldRefreshesSeparatelyAndGrowthRestoresWithoutChangingHealth(int team)
    {
        var host=new GameObject("Mountain lifecycle test");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var body=Object.Instantiate(Resources.Load<GameObject>("Enemies/PixelMonsters/026_dami"));
        body.transform.localScale=Vector3.one*2f;
        var type=typeof(MonsterAutoBattlePrototype);
        var unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var unit=System.Activator.CreateInstance(unitType,true);
        System.Action<string,object> set=(name,value)=>unitType.GetField(name).SetValue(unit,value);
        System.Func<string,object> get=name=>unitType.GetField(name).GetValue(unit);
        System.Func<string,object[],object> call=(name,args)=>type.GetMethod(name,
            Private|BindingFlags.Static).Invoke(battle,args);
        set("model",body);set("maxHp",500f);set("hp",400f);set("visualHeightOffset",.4f);
        set("team",System.Enum.ToObject(unitType.GetField("team").FieldType,team));
        set("spriteAnimation",body.GetComponentInChildren<SpriteMonsterAnimationDriver>());
        var statuses=(CombatStatusState)get("combatStatuses");
        try
        {
            statuses.ApplyShield(4f,BearCombatRules.DamiShield(500f),unit);
            call("ActivateBearMountainColossus",new[]{unit,(object)BearCombatRules.MountainDuration});
            var spirit=(SpriteRenderer)get("bearMountainHologram");
            Assert.That(spirit!=null,Is.True,"Mountain keeps the original spirit bear behind the body.");
            Assert.That(get("hp"),Is.EqualTo(400f));Assert.That(get("maxHp"),Is.EqualTo(500f));
            Assert.That(call("TotalVisibleShield",new[]{unit}),Is.EqualTo(125f));
            Assert.That(body.transform.localScale.x,Is.EqualTo(2f),"Activation does not snap the body scale.");
            call("UpdateBearMountainScale",new[]{unit,(object).1f});
            Assert.That(body.transform.localScale.x,Is.EqualTo(2.35f).Within(.001f));
            call("UpdateBearMountainScale",new[]{unit,(object).1f});
            Assert.That(body.transform.localScale.x,Is.EqualTo(2.7f).Within(.001f));
            call("RemoveVisibleShield",new[]{unit,(object)50f});
            Assert.That(get("bearMountainShield"),Is.EqualTo(25f));
            Assert.That(statuses.Get(CombatStatusKind.Shield).magnitude,Is.EqualTo(50f));
            call("ActivateBearMountainColossus",new[]{unit,(object)BearCombatRules.MountainDuration});
            Assert.That(get("bearMountainHologram"),Is.SameAs(spirit),"Refresh reuses one spirit bear.");
            Assert.That(call("TotalVisibleShield",new[]{unit}),Is.EqualTo(125f),"Reuse refreshes only the mountain shield.");
            call("RemoveVisibleShield",new[]{unit,(object)85f});
            Assert.That(get("bearMountainShield"),Is.EqualTo(0f));
            Assert.That(call("TotalVisibleShield",new[]{unit}),Is.EqualTo(40f),"Shield theft drains both pools once.");
            call("EndBearMountainColossus",new[]{unit,(object)false});
            Assert.That(spirit==null,Is.True,"Expiry cleans up the spirit bear.");
            Assert.That(body.transform.localScale.x,Is.EqualTo(2.7f).Within(.001f));
            call("UpdateBearMountainScale",new[]{unit,(object).1f});
            Assert.That(body.transform.localScale.x,Is.EqualTo(2.35f).Within(.001f));
            call("UpdateBearMountainScale",new[]{unit,(object).1f});
            Assert.That(body.transform.localScale,Is.EqualTo(Vector3.one*2f));
            Assert.That(get("visualHeightOffset"),Is.EqualTo(.4f).Within(.001f));
            Assert.That(call("TotalVisibleShield",new[]{unit}),Is.EqualTo(40f),"Ending Mountain preserves Dami's shield.");
            Assert.That(get("hp"),Is.EqualTo(400f));Assert.That(get("maxHp"),Is.EqualTo(500f));
        }
        finally{Object.DestroyImmediate(body);Object.DestroyImmediate(host);}
    }

    [TestCase(0,false)] [TestCase(1,false)] [TestCase(0,true)] [TestCase(1,true)]
    public void MountainActuallyGrantsFifteenPercentAndExpiresAtThreeSecondsWithoutConsumingOtherShields(int team,bool extendedResolve)
    {
        var host=new GameObject("Mountain timed expiry test");host.SetActive(false);
        var body=new GameObject("Mountain timed expiry body");
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var type=typeof(MonsterAutoBattlePrototype);var flags=Private|BindingFlags.Static;
        var unitType=type.GetNestedType("Unit",BindingFlags.NonPublic);
        var unit=System.Activator.CreateInstance(unitType,true);
        System.Action<string,object> set=(name,value)=>unitType.GetField(name).SetValue(unit,value);
        System.Func<string,object> get=name=>unitType.GetField(name).GetValue(unit);
        System.Func<string,object[],object> call=(name,args)=>type.GetMethod(name,flags).Invoke(battle,args);
        type.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*30f));
        type.GetField("expeditionRun",Private).SetValue(battle,new ExpeditionRunSave());
        type.GetField("playerProfile",Private).SetValue(battle,new PlayerProfileSave());
        type.GetField("mlEpisodeActive",Private).SetValue(battle,true);
        type.GetField("battleStarted",Private).SetValue(battle,true);
        set("model",body);set("alive",true);set("maxHp",500f);set("hp",400f);
        set("team",System.Enum.ToObject(unitType.GetField("team").FieldType,team));
        set("definition",Resources.Load<MonsterDefinition>("MonsterData/Monster_woongi"));
        set("skillId",BearCombatRules.SkillId);set("skillTimer",10f);set("skillCooldownDuration",10f);
        ((System.Collections.Generic.List<string>)get("activeTraitIds")).Add(BearCombatRules.MountainColossusTraitId);
        ((System.Collections.IList)type.GetField("units",Private).GetValue(battle)).Add(unit);
        var statuses=(CombatStatusState)get("combatStatuses");
        try
        {
            statuses.ApplyShield(6f,50f,unit);
            int generation=(int)type.GetField("combatGeneration",Private).GetValue(battle);
            Assert.That(call("ApplyBearFightingSpirit",new[]{unit,(object)GameMasterData.Skill(BearCombatRules.SkillId),1f,false,generation}),Is.True);
            Assert.That(get("bearResolveTimer"),Is.EqualTo(3f));
            Assert.That(get("bearMountainShieldTimer"),Is.EqualTo(3f));
            Assert.That(get("bearMountainShield"),Is.EqualTo(75f));
            Assert.That(call("TotalVisibleShield",new[]{unit}),Is.EqualTo(125f));
            if(extendedResolve)set("bearResolveTimer",6f);
            call("UpdateUnit",new[]{unit,(object)2.99f});
            Assert.That(get("bearMountainColossusActive"),Is.EqualTo(true));
            Assert.That((float)get("bearResolveTimer"),Is.GreaterThan(0f));
            Assert.That(get("bearMountainShield"),Is.EqualTo(75f));
            call("UpdateUnit",new[]{unit,(object).02f});
            Assert.That((float)get("bearResolveTimer"),Is.EqualTo(extendedResolve?2.99f:0f).Within(.001f));
            Assert.That(get("bearMountainColossusActive"),Is.EqualTo(false));
            Assert.That(get("bearMountainShield"),Is.EqualTo(0f));
            Assert.That(call("TotalVisibleShield",new[]{unit}),Is.EqualTo(50f));
            Assert.That(statuses.Get(CombatStatusKind.Shield).remaining,Is.GreaterThan(2.9f));
            call("UpdateBearMountainScale",new[]{unit,(object).2f});
            Assert.That(body.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(get("hp"),Is.EqualTo(400f));Assert.That(get("maxHp"),Is.EqualTo(500f));
        }
        finally{Object.DestroyImmediate(body);Object.DestroyImmediate(host);}
    }

    private static void Step(SpriteMonsterAnimationDriver driver,float dt)
    {
        typeof(SpriteMonsterAnimationDriver).GetMethod("Advance",Private).Invoke(driver,new object[]{dt});
        typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",Private).Invoke(driver,null);
    }

    [TestCase("Enemies/PixelMonsters/026_dami")]
    [TestCase("Enemies/Shiny/dami")]
    public void DamiAttackKeepsTheIdleBodyScale(string path)
    {
        var root=Object.Instantiate(Resources.Load<GameObject>(path));
        var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        var renderer=driver.GetComponent<SpriteRenderer>();
        try
        {
            driver.ResetForReuse();Step(driver,0f);
            float idlePixelSize=driver.transform.localScale.x/renderer.sprite.pixelsPerUnit;
            driver.PlayBasicAttack(1f,null);
            var frames=(Sprite[])typeof(SpriteMonsterAnimationDriver).GetField("attackFrames",Private).GetValue(driver);
            foreach(var frame in frames)
                Assert.That(driver.transform.localScale.x/frame.pixelsPerUnit/idlePixelSize,
                    Is.EqualTo(1f).Within(.01f),frame.name+" must keep the neutral body's pixel size.");
            float expected=PixelSpriteIdleScaleNormalizer.CalculatePixelsPerUnit(
                PixelMonsterRosterSetup.FindSourceFolder("026"),"Attack");
            Assert.That(frames[0].pixelsPerUnit,Is.EqualTo(expected).Within(.001f),
                "Rebuilding the frames must preserve the corrected scale.");
            Step(driver,1f);
            Assert.That(driver.transform.localScale.x/renderer.sprite.pixelsPerUnit,
                Is.EqualTo(idlePixelSize).Within(.0001f));
        }
        finally{Object.DestroyImmediate(root);}
    }

    [TestCase("woongi")]
    [TestCase("dami")]
    [TestCase("ironkuma")]
    [TestCase("berber")]
    public void PreparationHoldsPoseAndFrozenFrameUntilOneIgnition(string id)
    {
        var definition=Resources.Load<MonsterDefinition>("MonsterData/Monster_"+id);
        var root=Object.Instantiate(Resources.Load<GameObject>(definition.resourcePath));
        var driver=root.GetComponentInChildren<SpriteMonsterAnimationDriver>();
        try
        {
            int hits=0;driver.PlayBearFightingSpirit(1f,()=>hits++);
            Step(driver,.18f);Assert.That(hits,Is.Zero);
            Assert.That(((Vector3)typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver)).y,Is.EqualTo(.86f));
            driver.SetFrozen(true);var frame=driver.GetComponent<SpriteRenderer>().sprite;var position=driver.transform.localPosition;
            Step(driver,1f);Assert.That(hits,Is.Zero);
            Assert.That(driver.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frame));
            Assert.That(driver.transform.localPosition,Is.EqualTo(position));
            driver.SetFrozen(false);Step(driver,.12f);Assert.That(hits,Is.EqualTo(1));
            Step(driver,1f);Assert.That(hits,Is.EqualTo(1));
            Assert.That((Vector3)typeof(SpriteMonsterAnimationDriver).GetField("skillPoseScale",Private).GetValue(driver),Is.EqualTo(Vector3.one));
            driver.PlayBearFightingSpirit(1f,()=>hits++);Step(driver,.1f);driver.PlayDeath();Step(driver,1f);
            Assert.That(hits,Is.EqualTo(1),"Death cancels pending ignition.");
        }
        finally{Object.DestroyImmediate(root);}
    }

    [Test]
    public void SilhouetteFollowsTheAnimatedBodyAndFadesWithTheActualBuff()
    {
        var root=new GameObject("Bear spirit lifecycle");bool active=true,valid=true;
        var body=root.AddComponent<SpriteRenderer>();
        var sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1f);
        body.sprite=sprite;body.color=Color.green;
        var effect=PixelBearFightingSpirit.Create(root.transform,body,()=>valid,()=>active);
        var type=typeof(PixelBearFightingSpirit);
        var renderers=effect.GetComponentsInChildren<SpriteRenderer>();
        var material=renderers[0].sharedMaterial;
        try
        {
            Assert.That(renderers.Length,Is.EqualTo(7));
            Assert.That(effect.GetComponentsInChildren<ParticleSystem>(),Is.Empty);
            Assert.That(material.shader.isSupported,Is.True);
            effect.Ignite();body.flipX=true;body.sortingOrder=21;
            root.transform.position=new Vector3(3,2,1);
            type.GetField("phaseAge",Private).SetValue(effect,.15f);
            type.GetMethod("Sync",Private).Invoke(effect,null);
            var release=(SpriteRenderer)type.GetField("release",Private).GetValue(effect);
            Assert.That(release.enabled,Is.True);
            Assert.That(release.bounds.size.y,Is.GreaterThan(body.bounds.size.y*1.5f));
            Assert.That(renderers[0].flipX,Is.True);
            Assert.That(renderers[0].sortingOrder,Is.LessThan(body.sortingOrder));
            Assert.That(Vector3.Distance(renderers[0].transform.position,root.transform.position),Is.LessThanOrEqualTo(1f));
            Assert.That(body.color,Is.EqualTo(Color.green),"Spirit does not overwrite hit/freeze tint.");
            type.GetField("phaseAge",Private).SetValue(effect,2f);type.GetMethod("Sync",Private).Invoke(effect,null);
            Assert.That(release.enabled,Is.False);
            effect.Prepare();effect.Ignite();Assert.That(effect.GetComponentsInChildren<SpriteRenderer>(),Is.EqualTo(renderers));
            body.enabled=false;type.GetMethod("Sync",Private).Invoke(effect,null);
            foreach(var renderer in renderers)Assert.That(renderer.enabled,Is.False);
            active=false;effect.Finish();
            // Edit-mode deltaTime can include the editor's idle time; advance this fade deterministically.
            type.GetField("phaseAge",Private).SetValue(effect,-Time.deltaTime);
            type.GetMethod("LateUpdate",Private).Invoke(effect,null);
            Assert.That(type.GetField("phase",Private).GetValue(effect),Is.EqualTo(2));
            type.GetField("phaseAge",Private).SetValue(effect,.31f);type.GetMethod("LateUpdate",Private).Invoke(effect,null);
            Assert.That(effect==null,Is.True);Assert.That(material==null,Is.True);
            Assert.DoesNotThrow(()=>effect?.Cancel(),"Death cleanup can follow automatic buff expiry.");
            Assert.That(body.sprite,Is.SameAs(sprite),"Shared animation sprite survives effect cleanup.");
            effect=PixelBearFightingSpirit.Create(root.transform,body,()=>valid,()=>active);
            valid=false;type.GetMethod("LateUpdate",Private).Invoke(effect,null);
            Assert.That(effect==null,Is.True,"Death/reset cancels immediately.");
            Assert.DoesNotThrow(()=>effect?.Cancel(),"Repeated cleanup also accepts an already destroyed component.");
        }
        finally{if(effect!=null)effect.Cancel();Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);}
    }
}
