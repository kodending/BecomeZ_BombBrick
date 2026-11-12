using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class RaptorBiteEffectTests
{
    [Test]
    public void ArmorBreak_UsesCachedShieldFramesAtImpactAndReturnsToPoolAfterHalfASecond()
    {
        GameObject target=new GameObject("Armor Shatter Test");
        SpriteRenderer reference=target.AddComponent<SpriteRenderer>();
        reference.sprite=PixelStarProjectile.BeanSprite;
        reference.sortingOrder=12;
        PixelCombatEffect first=null,second=null;
        try
        {
            Texture2D icon=PixelCombatEffect.BrokenShieldIcon;
            Assert.That(icon.width,Is.EqualTo(16));
            Assert.That(icon.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(PixelCombatEffect.BrokenShieldIcon,Is.SameAs(icon));
            Assert.That(PixelCombatEffect.PlayArmorBreak(null,null),Is.Null);
            first=PixelCombatEffect.PlayArmorBreak(target.transform,reference);
            second=PixelCombatEffect.PlayArmorBreak(target.transform,reference);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var field=typeof(PixelCombatEffect).GetField("frames",flags);
            var frames=(Sprite[])field.GetValue(first);
            Assert.That(field.GetValue(second),Is.SameAs(frames));
            Assert.That(frames.Length,Is.EqualTo(10));
            Assert.That(frames.All(frame=>frame.texture.width==48&&
                frame.texture.filterMode==FilterMode.Point),Is.True);
            Assert.That(first.GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(52));
            Assert.That((float)typeof(PixelCombatEffect).GetField("framesPerSecond",flags).GetValue(first),
                Is.EqualTo(20f));
            Vector3 impact=first.transform.position;
            Object.DestroyImmediate(target);
            typeof(PixelCombatEffect).GetMethod("LateUpdate",flags).Invoke(first,null);
            Assert.That(first.transform.position,Is.EqualTo(impact));
            typeof(PixelCombatEffect).GetField("elapsed",flags).SetValue(first,0.5f);
            typeof(PixelCombatEffect).GetMethod("Update",flags).Invoke(first,null);
            Assert.That(first.gameObject.activeSelf,Is.False);
        }
        finally
        {
            if(first!=null)Object.DestroyImmediate(first.gameObject);
            if(second!=null)Object.DestroyImmediate(second.gameObject);
            if(target!=null)Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RaptorContactClaws_CachePixelsAndStayAtImpactWhenVictimMoves()
    {
        GameObject target=new GameObject("Raptor Impact Test");
        SpriteRenderer renderer=target.AddComponent<SpriteRenderer>();
        renderer.sprite=PixelStarProjectile.BeanSprite;
        renderer.sortingOrder=12;
        PixelCombatEffect first=null,second=null;
        try
        {
            first=PixelCombatEffect.PlayRaptorClawImpact(
                target.transform,renderer,Vector3.right,Color.cyan);
            second=PixelCombatEffect.PlayRaptorClawImpact(
                target.transform,renderer,Vector3.left,Color.cyan);
            SpriteRenderer output=first.GetComponent<SpriteRenderer>();
            Assert.That(output.sprite.texture.width,Is.EqualTo(32));
            Assert.That(output.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(output.sortingOrder,Is.EqualTo(37));
            Assert.That(output.sprite,Is.SameAs(second.GetComponent<SpriteRenderer>().sprite));
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Sprite[] frames=(Sprite[])typeof(PixelCombatEffect).GetField("frames",flags).GetValue(first);
            float fps=(float)typeof(PixelCombatEffect).GetField("framesPerSecond",flags).GetValue(first);
            Assert.That(frames.Length/fps,Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(frames[0].bounds,Is.EqualTo(frames[1].bounds));
            Vector3 impact=first.transform.position;
            target.transform.position+=Vector3.right*10f;
            typeof(PixelCombatEffect).GetMethod("LateUpdate",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(first,null);
            Assert.That(first.transform.position,Is.EqualTo(impact));
        }
        finally
        {
            if(first!=null)Object.DestroyImmediate(first.gameObject);
            if(second!=null)Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RaptorPoseAndRecoil_RestoreWithoutMovingCollisionRoot()
    {
        GameObject root=new GameObject("Raptor Pose Test");
        GameObject visual=new GameObject("Visual");
        visual.transform.SetParent(root.transform,false);
        SpriteMonsterAnimationDriver driver=visual.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var late=typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",flags);
        try
        {
            driver.PlaySkillAttack(1f,null);
            driver.SetSkillPoseScale(new Vector3(1.12f,0.8f,1f));
            late.Invoke(driver,null);
            Assert.That(visual.transform.localScale.x/visual.transform.localScale.y,
                Is.EqualTo(1.4f).Within(0.001f));
            driver.InterruptCurrentAction();
            late.Invoke(driver,null);
            Assert.That(visual.transform.localScale.x,
                Is.EqualTo(visual.transform.localScale.y).Within(0.001f));
            driver.PlayImpactRecoil(Vector3.right,0.12f);
            typeof(SpriteMonsterAnimationDriver).GetField("impactRecoilRemaining",flags)
                .SetValue(driver,0.112f);
            late.Invoke(driver,null);
            Assert.That(visual.transform.localPosition.x,Is.GreaterThan(0.1f));
            Assert.That(root.transform.position,Is.EqualTo(Vector3.zero));
            driver.ResetForReuse();
            Assert.That(visual.transform.localPosition,Is.EqualTo(Vector3.zero));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void LeapTargetScoreUsesRemainingPhysicalEffectiveHealth()
    {
        Assert.That(RaptorCombatRules.PhysicalEffectiveHealth(100f,50f),
            Is.EqualTo(150f).Within(0.001f));
        Assert.That(RaptorCombatRules.PhysicalEffectiveHealth(120f,0f),
            Is.LessThan(RaptorCombatRules.PhysicalEffectiveHealth(90f,50f)));
    }

    [Test]
    public void LeapImpactTracksALivingTargetAndKeepsTheLockedFallback()
    {
        Vector3 locked=new Vector3(1f,0f,2f);
        Vector3 current=new Vector3(5f,0f,6f);

        Assert.That(RaptorCombatRules.LeapImpactPosition(
            locked,current,true),Is.EqualTo(current));
        Assert.That(RaptorCombatRules.LeapImpactPosition(
            locked,current,false),Is.EqualTo(locked));
    }

    [Test]
    public void LeapTouchesTheTargetThenReturnsToAttackDistance()
    {
        Vector3 source=Vector3.zero;
        Vector3 target=new Vector3(4f,0f,0f);
        Vector3 contact=RaptorCombatRules.LeapContactPosition(
            source,target,0.5f,0.75f);
        Vector3 returned=RaptorCombatRules.LeapReturnPosition(
            contact,target,3f);

        Assert.That(contact,Is.EqualTo(new Vector3(2.75f,0f,0f)));
        Assert.That(returned,Is.EqualTo(new Vector3(1f,0f,0f)));
        Assert.That(RaptorCombatRules.LeapReturnDuration,Is.EqualTo(0.08f));
    }

    [Test]
    public void RaptorLeap_CompressesTheFullActionWithoutRemovingContactHold()
    {
        float travel=RaptorCombatRules.LeapApproachDuration(0.24f);
        Assert.That(travel,Is.EqualTo(0.12f).Within(0.001f));
        Assert.That(RaptorCombatRules.LeapWindupDuration+travel,
            Is.EqualTo(0.16f).Within(0.001f));
        Assert.That(RaptorCombatRules.LeapImpactHoldDuration,Is.InRange(0.02f,0.03f));
        Assert.That(RaptorCombatRules.LeapWindupDuration+travel+
            RaptorCombatRules.LeapImpactHoldDuration+RaptorCombatRules.LeapReturnDuration,
            Is.LessThan(0.28f));
        Assert.That(RaptorCombatRules.LeapApproachDuration(0f),Is.GreaterThan(0f));
    }

    [Test]
    public void PiercingHunt_SweepsSkippedFramesAndLandsBeyondTheTarget()
    {
        Vector3 start=Vector3.zero,end=Vector3.right*8f;
        Assert.That(RaptorCombatRules.IsInPiercingSweep(
            new Vector3(4,20,1.5f),start,end,2f),Is.True);
        Assert.That(RaptorCombatRules.IsInPiercingSweep(
            new Vector3(4,0,2.01f),start,end,2f),Is.False);
        Assert.That(RaptorCombatRules.IsInPiercingSweep(
            Vector3.right,start,start,2f),Is.True);
        Assert.That(RaptorCombatRules.PiercingLandingPosition(start,Vector3.right*4f,2f),
            Is.EqualTo(Vector3.right*6f));
        Assert.That(RaptorCombatRules.PiercingLandingPosition(end,Vector3.right*4f,2f),
            Is.EqualTo(Vector3.right*2f));
        Assert.That(RaptorCombatRules.PiercingHuntTraitId,Is.EqualTo("trait_raptor_blood_trail"));
    }

    [Test]
    public void PiercingHunt_HitsEachEnemyOnceAndDoesNotHitAlliesOrOutsideEnemies()
    {
        var existing=new System.Collections.Generic.HashSet<GameObject>(
            Resources.FindObjectsOfTypeAll<GameObject>());
        try
        {
            GameObject host=new GameObject("Piercing Hunt Test");host.SetActive(false);
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var unitType=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",
                System.Reflection.BindingFlags.NonPublic);
            var units=(System.Collections.IList)typeof(MonsterAutoBattlePrototype)
                .GetField("units",flags).GetValue(battle);
            System.Func<int,Vector3,object> create=(team,position)=>
            {
                object unit=System.Activator.CreateInstance(unitType,true);
                GameObject model=new GameObject("Piercing Test Unit");model.transform.position=position;
                unitType.GetField("model").SetValue(unit,model);
                var teamField=unitType.GetField("team");
                teamField.SetValue(unit,System.Enum.ToObject(teamField.FieldType,team));
                unitType.GetField("alive").SetValue(unit,true);
                unitType.GetField("hp").SetValue(unit,10000f);
                unitType.GetField("maxHp").SetValue(unit,10000f);
                unitType.GetField("damage").SetValue(unit,100f);
                units.Add(unit);return unit;
            };
            object source=create(0,Vector3.zero);
            object first=create(1,new Vector3(2,0,0));
            object second=create(1,new Vector3(5,0,1));
            object outside=create(1,new Vector3(5,0,3));
            object ally=create(0,new Vector3(4,0,0));
            unitType.GetField("leaping").SetValue(source,true);
            unitType.GetField("skillId").SetValue(source,"raptor_hunting_leap");
            unitType.GetField("leapEnd").SetValue(source,Vector3.right*8);
            float meleeReach=(float)typeof(MonsterAutoBattlePrototype).GetMethod("AttackDistance",flags)
                .Invoke(battle,new[]{source,outside});
            ((GameObject)unitType.GetField("model").GetValue(outside)).transform.position=
                new Vector3(5,0,meleeReach-.17f);
            var sweep=typeof(MonsterAutoBattlePrototype).GetMethod("ApplyRaptorPiercingSweep",flags);
            sweep.Invoke(battle,new[]{source,(object)Vector3.zero,Vector3.right*8});
            float firstHp=(float)unitType.GetField("hp").GetValue(first);
            Assert.That(firstHp,Is.LessThan(10000f));
            Assert.That((float)unitType.GetField("hp").GetValue(second),Is.LessThan(10000f));
            Assert.That(unitType.GetField("hp").GetValue(outside),Is.EqualTo(10000f),
                "The additional melee staging space must not widen the piercing skill's damage corridor.");
            Assert.That(unitType.GetField("hp").GetValue(ally),Is.EqualTo(10000f));
            sweep.Invoke(battle,new[]{source,(object)Vector3.zero,Vector3.right*8});
            Assert.That(unitType.GetField("hp").GetValue(first),Is.EqualTo(firstHp));
            Assert.That(unitType.GetField("skillHits").GetValue(source),Is.EqualTo(2));
        }
        finally
        {
            foreach(GameObject created in Resources.FindObjectsOfTypeAll<GameObject>())
                if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
                    created.transform.parent==null)Object.DestroyImmediate(created);
        }
    }

    [Test]
    public void PiercingHunt_ScuffsStayOnGroundAndExpireWithoutRouteMarkers()
    {
        GameObject reference=new GameObject("Piercing Scuff Test");
        SpriteRenderer renderer=reference.AddComponent<SpriteRenderer>();renderer.sortingOrder=12;
        PixelCombatEffect scuff=PixelCombatEffect.PlayRaptorGroundScuff(
            new Vector3(2f,9f,3f),Vector3.right,renderer);
        PixelCombatEffect gouge=GameObject.Find("Raptor Ground Gouge")
            .GetComponent<PixelCombatEffect>();
        try
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Assert.That(gouge.GetComponent<SpriteRenderer>().sortingOrder,
                Is.EqualTo(renderer.sortingOrder-3));
            Assert.That(scuff.GetComponent<SpriteRenderer>().sortingOrder,
                Is.EqualTo(renderer.sortingOrder-2));
            Vector3 position=scuff.transform.position;
            reference.transform.position=Vector3.one*100f;
            typeof(PixelCombatEffect).GetMethod("LateUpdate",flags).Invoke(scuff,null);
            Assert.That(scuff.transform.position,Is.EqualTo(position));
            foreach(PixelCombatEffect effect in new[]{gouge,scuff})
            {
                Assert.That(effect.transform.position.y,Is.InRange(0.03f,0.05f));
                Assert.That(Vector3.Dot(effect.transform.forward,Vector3.up),
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(effect.GetComponent<SpriteRenderer>().sprite.texture.filterMode,
                    Is.EqualTo(FilterMode.Point));
                Assert.That(effect.GetComponentsInChildren<Collider>().Length,Is.Zero);
                Assert.That(effect.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
                Assert.That(effect.GetComponentsInChildren<MeshRenderer>().Length,Is.Zero);
                var frames=(Sprite[])typeof(PixelCombatEffect).GetField("frames",flags).GetValue(effect);
                float fps=(float)typeof(PixelCombatEffect).GetField("framesPerSecond",flags).GetValue(effect);
                Assert.That(frames.Length/fps,Is.EqualTo(0.25f).Within(0.001f));
                typeof(PixelCombatEffect).GetField("elapsed",flags).SetValue(effect,0.3f);
                typeof(PixelCombatEffect).GetMethod("Update",flags).Invoke(effect,null);
                Assert.That(effect.gameObject.activeSelf,Is.False);
            }
            Assert.That(PixelCombatEffect.PlayRaptorGroundScuff(
                Vector3.zero,Vector3.zero,renderer),Is.Null);
            var trait=GameMasterData.Database.traits.First(t=>t.id==RaptorCombatRules.PiercingHuntTraitId);
            Assert.That(trait.displayName,Is.EqualTo("관통 사냥"));
            Assert.That(trait.category,Is.EqualTo("SkillEvolution"));
        }
        finally
        {
            Object.DestroyImmediate(scuff.gameObject);Object.DestroyImmediate(gouge.gameObject);
            Object.DestroyImmediate(reference);
        }
    }

    [Test]
    public void RelentlessHunt_DealsSixtySixtyOneHundredOverFourTenths()
    {
        Assert.That(RaptorCombatRules.RelentlessHuntTraitId,Is.EqualTo("trait_raptor_chain_leap"));
        Assert.That(RaptorCombatRules.RelentlessStrikeCount,Is.EqualTo(3));
        Assert.That(RaptorCombatRules.RelentlessDuration,Is.EqualTo(0.4f));
        Assert.That(Enumerable.Range(0,3).Select(RaptorCombatRules.RelentlessStrikeDamageMultiplier),
            Is.EqualTo(new[]{0.6f,0.6f,1f}));
        Assert.That(Enumerable.Range(0,3).Select(RaptorCombatRules.RelentlessStrikeTime),
            Is.EqualTo(new[]{0f,0.14f,0.32f}));
    }

    [TestCase("complete")]
    [TestCase("target_dies")]
    [TestCase("target_leaves")]
    [TestCase("target_returns")]
    [TestCase("interrupted")]
    [TestCase("skipped_frame")]
    public void RelentlessHunt_UsesOneTargetAndAlwaysExitsTheCombo(string ending)
    {
        var existing=new System.Collections.Generic.HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        try
        {
            GameObject host=new GameObject("Relentless Hunt Test");host.SetActive(false);
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var type=typeof(MonsterAutoBattlePrototype).GetNestedType("Unit",System.Reflection.BindingFlags.NonPublic);
            var units=(System.Collections.IList)typeof(MonsterAutoBattlePrototype).GetField("units",flags).GetValue(battle);
            typeof(MonsterAutoBattlePrototype).GetField("combatBounds",flags).SetValue(battle,
                new Bounds(Vector3.zero,Vector3.one*100f));
            System.Func<int,Vector3,object> create=(team,position)=>
            {
                object unit=System.Activator.CreateInstance(type,true);
                var model=new GameObject("Combo Unit");model.transform.position=position;
                type.GetField("model").SetValue(unit,model);
                type.GetField("ring").SetValue(unit,new GameObject("Combo Ring"));
                var field=type.GetField("team");field.SetValue(unit,System.Enum.ToObject(field.FieldType,team));
                type.GetField("alive").SetValue(unit,true);
                type.GetField("hp").SetValue(unit,10000f);type.GetField("maxHp").SetValue(unit,10000f);
                type.GetField("damage").SetValue(unit,100f);
                units.Add(unit);return unit;
            };
            object source=create(0,Vector3.zero),target=create(1,Vector3.right),other=create(1,Vector3.forward);
            type.GetField("skillId").SetValue(source,"raptor_hunting_leap");
            type.GetField("leapTraitDamageMultiplier").SetValue(source,1.3f);
            typeof(MonsterAutoBattlePrototype).GetMethod("ApplyRaptorLeapHit",flags)
                .Invoke(battle,new object[]{source,target,1f,-1});
            float baseline=10000f-(float)type.GetField("hp").GetValue(target);
            Assert.That(baseline,Is.GreaterThan(0f));
            type.GetField("hp").SetValue(target,10000f);
            type.GetField("leapHitCount").SetValue(source,0);
            type.GetField("leapDamageDealt").SetValue(source,0f);
            type.GetField("skillHits").SetValue(source,0);
            type.GetField("leaping").SetValue(source,true);
            type.GetField("leapRelentless").SetValue(source,true);
            type.GetField("leapTarget").SetValue(source,target);
            type.GetField("target").SetValue(source,target);
            type.GetField("leapImpactPosition").SetValue(source,Vector3.right);
            var resolve=typeof(MonsterAutoBattlePrototype).GetMethod("ResolveAssassinLeapImpact",flags);
            var combo=typeof(MonsterAutoBattlePrototype).GetMethod("UpdateRelentlessHunt",flags);
            resolve.Invoke(battle,new[]{source});resolve.Invoke(battle,new[]{source});
            float afterFirst=(float)type.GetField("hp").GetValue(target);
            Assert.That(10000f-afterFirst,Is.EqualTo(baseline*0.6f).Within(0.005f));
            if(ending=="target_dies")type.GetField("alive").SetValue(target,false);
            if(ending=="target_leaves"||ending=="target_returns")
            {
                var targetModel=(GameObject)type.GetField("model").GetValue(target);
                targetModel.transform.position=Vector3.right*50f;
                combo.Invoke(battle,new object[]{source,0.14f});
                Assert.That(type.GetField("leaping").GetValue(source),Is.True);
                Assert.That(type.GetField("leapReturning").GetValue(source),Is.False);
                Assert.That(type.GetField("leapHitCount").GetValue(source),Is.EqualTo(1));
                Assert.That(type.GetField("hp").GetValue(target),Is.EqualTo(afterFirst),
                    "The combo keeps its timing while an out-of-range strike misses.");
                if(ending=="target_returns")
                {
                    targetModel.transform.position=Vector3.right;
                    combo.Invoke(battle,new object[]{source,0.18f});
                    Assert.That(afterFirst-(float)type.GetField("hp").GetValue(target),
                        Is.EqualTo(baseline).Within(0.005f));
                    Assert.That(type.GetField("leapHitCount").GetValue(source),Is.EqualTo(2),
                        "Returning to reach receives the final strike, without replaying the missed second strike.");
                }
            }
            if(ending=="interrupted")typeof(MonsterAutoBattlePrototype).GetMethod("InterruptPositionalAction",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new[]{source});
            if(ending=="complete")
            {
                combo.Invoke(battle,new object[]{source,0.14f});
                float afterSecond=(float)type.GetField("hp").GetValue(target);
                Assert.That(afterFirst-afterSecond,Is.EqualTo(baseline*0.6f).Within(0.005f));
                combo.Invoke(battle,new object[]{source,0.18f});
                Assert.That(afterSecond-(float)type.GetField("hp").GetValue(target),
                    Is.EqualTo(baseline).Within(0.005f));
            }
            combo.Invoke(battle,new object[]{source,0.5f});combo.Invoke(battle,new object[]{source,0.5f});
            int expected=ending=="complete"||ending=="skipped_frame"?3:ending=="target_returns"?2:1;
            Assert.That(type.GetField("leapHitCount").GetValue(source),Is.EqualTo(expected));
            Assert.That(type.GetField("hp").GetValue(other),Is.EqualTo(10000f));
            Assert.That(type.GetField("skillEchoTimer").GetValue(source),Is.EqualTo(0f));
            if(ending!="interrupted")typeof(MonsterAutoBattlePrototype).GetMethod("UpdateAssassinLeap",flags)
                .Invoke(battle,new object[]{source,0.2f});
            Assert.That(type.GetField("leaping").GetValue(source),Is.False);
            Assert.That(type.GetField("target").GetValue(source),Is.SameAs(target));
        }
        finally
        {
            foreach(GameObject created in Resources.FindObjectsOfTypeAll<GameObject>())
                if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
                    created.transform.parent==null)Object.DestroyImmediate(created);
        }
    }

    [Test]
    public void ApexPredator_StrengthensBasicsAndChargesSkill()
    {
        Assert.That(RaptorCombatRules.ApexPredatorDuration,Is.EqualTo(4f));
        Assert.That(RaptorCombatRules.ApexPredatorBasicDamage(100f,true),
            Is.EqualTo(160f).Within(0.001f));
        Assert.That(RaptorCombatRules.ApexPredatorCooldown(3f,true),
            Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(RaptorCombatRules.ApexPredatorCooldown(0.2f,true),Is.Zero);
    }

    [Test]
    public void ApexBite_SnapsThenHoldsCachedFangsOnTheDamageFrame()
    {
        GameObject target=new GameObject("Apex Bite Test");
        var reference=target.AddComponent<SpriteRenderer>();
        reference.sprite=PixelStarProjectile.BeanSprite;
        reference.sortingOrder=12;
        PixelCombatEffect normal=null,apex=null,second=null;
        PixelSpiritSanctuaryEffect awakening=null;
        try
        {
            normal=PixelCombatEffect.PlayRaptorBite(target.transform,reference,1f);
            apex=PixelCombatEffect.PlayRaptorBite(target.transform,reference,1f,true);
            second=PixelCombatEffect.PlayRaptorBite(target.transform,reference,2f,true);
            awakening=PixelCombatEffect.PlayRaptorApexActivation(target.transform,reference);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var field=typeof(PixelCombatEffect).GetField("frames",flags);
            Sprite[] frames=(Sprite[])field.GetValue(apex);
            Assert.That(frames.Length,Is.EqualTo(8));
            Assert.That(field.GetValue(second),Is.SameAs(frames));
            Assert.That(field.GetValue(normal),Is.Not.SameAs(frames));
            Assert.That(apex.name,Is.EqualTo("Raptor Apex Crushing Bite"));
            Assert.That(normal.name,Is.EqualTo("Raptor Bite Effect"));
            Assert.That(apex.GetComponent<SpriteRenderer>().sortingOrder,Is.EqualTo(42));
            Assert.That(apex.transform.localScale.x,Is.EqualTo(Mathf.Max(1.05f,
                Mathf.Max(reference.bounds.size.x,reference.bounds.size.y)*1.05f)).Within(0.001f));
            Assert.That(frames.All(frame=>frame.texture.width==48&&
                frame.texture.filterMode==FilterMode.Point),Is.True);
            Assert.That(PixelCombatEffect.RaptorApexBiteImpactFrame/
                PixelCombatEffect.RaptorApexBiteFramesPerSecond,Is.InRange(0.03f,0.045f));
            Assert.That(frames[2],Is.SameAs(frames[3]));
            Assert.That(frames[2],Is.SameAs(frames[4]));
            Assert.That(frames[5],Is.Not.SameAs(frames[4]));
            Assert.That(3f/PixelCombatEffect.RaptorApexBiteFramesPerSecond,
                Is.InRange(0.05f,0.07f),"The closed bite must remain readable after the snap.");
            Assert.That(frames.Length/PixelCombatEffect.RaptorApexBiteFramesPerSecond,
                Is.InRange(0.15f,0.18f),"The release must not linger over the next attack.");
            foreach(float speed in new[]{0.75f,1f,1.4f,2f,4f})
            {
                float hitTime=0.25f/speed;
                float effectImpact=PixelCombatEffect.RaptorApexBiteImpactFrame/
                    (PixelCombatEffect.RaptorApexBiteFramesPerSecond*speed);
                Assert.That(PixelCombatEffect.RaptorBiteLeadTime(hitTime,speed,true)+effectImpact,
                    Is.EqualTo(hitTime).Within(0.001f));
            }
            Assert.That(PixelCombatEffect.RaptorApexFangIcon.width,Is.EqualTo(16));
            Assert.That(PixelCombatEffect.RaptorApexFangIcon.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(awakening.Duration,Is.EqualTo(0.18f));
            typeof(PixelCombatEffect).GetField("elapsed",flags).SetValue(apex,
                frames.Length/PixelCombatEffect.RaptorApexBiteFramesPerSecond);
            typeof(PixelCombatEffect).GetMethod("Update",flags).Invoke(apex,null);
            Assert.That(apex.gameObject.activeSelf,Is.False);
        }
        finally
        {
            if(normal!=null)Object.DestroyImmediate(normal.gameObject);
            if(apex!=null)Object.DestroyImmediate(apex.gameObject);
            if(second!=null)Object.DestroyImmediate(second.gameObject);
            if(awakening!=null)Object.DestroyImmediate(awakening.gameObject);
            Object.DestroyImmediate(target);
        }
    }

    [TestCase("trait_raptor_blood_trail")]
    [TestCase("trait_raptor_chain_leap")]
    [TestCase("trait_raptor_pack_target")]
    public void ApexState_ActivatesAfterLeapAndFlashesOnlyWhenCooldownIsReduced(string trait)
    {
        var existing=new System.Collections.Generic.HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        try
        {
            GameObject host=new GameObject("Apex State Test");host.SetActive(false);
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var bt=typeof(MonsterAutoBattlePrototype);
            var type=bt.GetNestedType("Unit",System.Reflection.BindingFlags.NonPublic);
            var units=(System.Collections.IList)bt.GetField("units",flags).GetValue(battle);
            bt.GetField("combatBounds",flags).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
            var definition=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase")
                .monsters.First(row=>row!=null&&row.monsterId=="velos");
            System.Func<int,object> create=side=>
            {
                object unit=System.Activator.CreateInstance(type,true);
                var model=new GameObject("Apex Unit");model.transform.position=Vector3.right*side;
                type.GetField("model").SetValue(unit,model);
                type.GetField("ring").SetValue(unit,new GameObject("Apex Ring"));
                var team=type.GetField("team");team.SetValue(unit,System.Enum.ToObject(team.FieldType,side));
                type.GetField("definition").SetValue(unit,definition);
                type.GetField("skillId").SetValue(unit,"raptor_hunting_leap");
                type.GetField("alive").SetValue(unit,true);
                type.GetField("hp").SetValue(unit,10000f);type.GetField("maxHp").SetValue(unit,10000f);
                type.GetField("damage").SetValue(unit,100f);type.GetField("attackInterval").SetValue(unit,1f);
                units.Add(unit);return unit;
            };
            object source=create(0),target=create(1);
            ((System.Collections.Generic.List<string>)type.GetField("activeTraitIds").GetValue(source)).Add(trait);
            type.GetField("leapTarget").SetValue(source,target);
            var finish=bt.GetMethod("FinishRaptorLeapAttack",flags);
            var active=bt.GetMethod("RaptorApexPredatorActive",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            finish.Invoke(battle,new[]{source});
            Assert.That(active.Invoke(null,new[]{source}),Is.False);
            type.GetField("augmentTranscended").SetValue(source,true);
            finish.Invoke(battle,new[]{source});
            Assert.That(active.Invoke(null,new[]{source}),Is.False,"A missed leap cannot activate it.");
            type.GetField("leapHitCount").SetValue(source,1);
            finish.Invoke(battle,new[]{source});
            Assert.That(active.Invoke(null,new[]{source}),Is.True);
            Assert.That(type.GetField("raptorApexPredatorTimer").GetValue(source),Is.EqualTo(4f));
            Assert.That(bt.GetMethod("CurrentAttackInterval",flags).Invoke(battle,new[]{source}),
                Is.EqualTo(1f/1.4f).Within(0.001f));
            int waves=Resources.FindObjectsOfTypeAll<PixelSpiritSanctuaryEffect>()
                .Count(effect=>effect.name=="Raptor Apex Awakening");
            finish.Invoke(battle,new[]{source});
            Assert.That(Resources.FindObjectsOfTypeAll<PixelSpiritSanctuaryEffect>()
                .Count(effect=>effect.name=="Raptor Apex Awakening"),Is.EqualTo(waves),
                "Refreshing the buff must not replay the activation burst.");
            var targetModel=(GameObject)type.GetField("model").GetValue(target);
            var visual=new GameObject("Apex Recoil Visual");
            visual.transform.SetParent(targetModel.transform,false);
            var victimAnimation=visual.AddComponent<SpriteMonsterAnimationDriver>();
            Sprite[] poses=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
            victimAnimation.Configure(poses,poses,poses,poses,poses,poses,poses);
            victimAnimation.PlayBasicAttack(1f,()=>{});
            type.GetField("spriteAnimation").SetValue(target,victimAnimation);
            var recoil=typeof(SpriteMonsterAnimationDriver).GetField("impactRecoilRemaining",flags);
            type.GetField("skillTimer").SetValue(source,3f);
            var impact=bt.GetMethod("ResolveBasicAttackImpact",flags);
            impact.Invoke(battle,new object[]{source,target,100f});
            Assert.That(type.GetField("skillTimer").GetValue(source),Is.EqualTo(2.5f));
            Assert.That(type.GetField("raptorApexCooldownFlash").GetValue(source),Is.EqualTo(0.18f));
            Assert.That(recoil.GetValue(victimAnimation),Is.EqualTo(0.14f));
            Assert.That(victimAnimation.HasPendingBasicAttackHit,Is.True,
                "Visual recoil must not interrupt the victim's attack.");
            recoil.SetValue(victimAnimation,0.112f);
            typeof(SpriteMonsterAnimationDriver).GetMethod("LateUpdate",flags)
                .Invoke(victimAnimation,null);
            Assert.That(visual.transform.localPosition.x,Is.EqualTo(0.16f).Within(0.001f));
            Assert.That(targetModel.transform.position,Is.EqualTo(Vector3.right),
                "An enhanced bite must not knock back the collision body.");
            type.GetField("skillTimer").SetValue(source,0f);
            type.GetField("raptorApexCooldownFlash").SetValue(source,0f);
            impact.Invoke(battle,new object[]{source,target,100f});
            Assert.That(type.GetField("raptorApexCooldownFlash").GetValue(source),Is.EqualTo(0f));
            type.GetField("raptorApexPredatorTimer").SetValue(source,0.05f);
            type.GetField("raptorApexCooldownFlash").SetValue(source,0.18f);
            type.GetField("augmentReturnTimer").SetValue(source,10f);
            bt.GetMethod("UpdateUnit",flags).Invoke(battle,new object[]{source,0.1f});
            Assert.That(active.Invoke(null,new[]{source}),Is.False);
            Assert.That(type.GetField("raptorApexCooldownFlash").GetValue(source),Is.EqualTo(0f));
            Assert.That(bt.GetMethod("CurrentAttackInterval",flags).Invoke(battle,new[]{source}),Is.EqualTo(1f));
            type.GetField("augmentReturnTimer").SetValue(source,0f);
            type.GetField("skillTimer").SetValue(source,3f);
            victimAnimation.ResetForReuse();
            impact.Invoke(battle,new object[]{source,target,100f});
            Assert.That(type.GetField("skillTimer").GetValue(source),Is.EqualTo(3f));
            Assert.That(recoil.GetValue(victimAnimation),Is.EqualTo(0f),
                "Normal bites must not inherit the enhanced recoil.");
        }
        finally
        {
            foreach(GameObject created in Resources.FindObjectsOfTypeAll<GameObject>())
                if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
                    created.transform.parent==null)Object.DestroyImmediate(created);
        }
    }

    [Test]
    public void TranscendenceFlameOutlineFollowsAnimatedSprite()
    {
        GameObject sourceObject=new GameObject("Transcendence Outline Source");
        SpriteRenderer source=sourceObject.AddComponent<SpriteRenderer>();
        source.sprite=PixelStarProjectile.BeanSprite;
        source.sortingOrder=12;
        try
        {
            PixelTranscendenceFlameOutline outline=
                PixelCombatEffect.PlayTranscendenceFlameOutline(source);
            Assert.That(outline,Is.Not.Null);
            Assert.That(outline.transform.childCount,Is.EqualTo(8));
            SpriteRenderer edge=outline.transform.GetChild(0)
                .GetComponent<SpriteRenderer>();
            Assert.That(edge.sprite,Is.SameAs(source.sprite));
            Assert.That(edge.sortingOrder,Is.EqualTo(11));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
        }
    }

    [Test]
    public void ProtagonistClimaxOutlineBuildsThickerRisingPixelLayers()
    {
        GameObject sourceObject=new GameObject("Climax Outline Source");
        SpriteRenderer source=sourceObject.AddComponent<SpriteRenderer>();
        source.sprite=PixelStarProjectile.BeanSprite;
        try
        {
            PixelTranscendenceFlameOutline outline=
                PixelCombatEffect.PlayProtagonistClimaxFlameOutline(source,8f);
            Assert.That(outline,Is.Not.Null);
            Assert.That(outline.transform.childCount,Is.GreaterThan(8));
            float lowest=outline.transform.Cast<Transform>()
                .Min(edge=>edge.localPosition.y);
            float highest=outline.transform.Cast<Transform>()
                .Max(edge=>edge.localPosition.y);
            Assert.That(highest,Is.GreaterThan(-lowest*2f));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    public void ArmorHunt_ShredsAfterImpactWithoutStackingAndRestoresOnExpiryOrCleanse(int team)
    {
        var existing=new System.Collections.Generic.HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        try
        {
            GameObject host=new GameObject("Armor Hunt Test");host.SetActive(false);
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var battleType=typeof(MonsterAutoBattlePrototype);
            var type=battleType.GetNestedType("Unit",System.Reflection.BindingFlags.NonPublic);
            var units=(System.Collections.IList)battleType.GetField("units",flags).GetValue(battle);
            System.Func<int,object> create=side=>
            {
                object unit=System.Activator.CreateInstance(type,true);
                type.GetField("model").SetValue(unit,new GameObject("Armor Hunt Unit"));
                type.GetField("ring").SetValue(unit,new GameObject("Armor Hunt Ring"));
                var field=type.GetField("team");field.SetValue(unit,System.Enum.ToObject(field.FieldType,side));
                type.GetField("alive").SetValue(unit,true);
                type.GetField("hp").SetValue(unit,10000f);type.GetField("maxHp").SetValue(unit,10000f);
                type.GetField("damage").SetValue(unit,100f);type.GetField("defense").SetValue(unit,100f);
                type.GetField("statusResistance").SetValue(unit,40f);
                type.GetField("skillId").SetValue(unit,"raptor_hunting_leap");
                units.Add(unit);return unit;
            };
            object source=create(team),ally=create(team),target=create(1-team),other=create(1-team);
            var statuses=(CombatStatusState)type.GetField("combatStatuses").GetValue(target);
            System.Func<int> shatters=()=>Resources.FindObjectsOfTypeAll<PixelCombatEffect>()
                .Count(effect=>effect.gameObject.activeSelf&&effect.name=="Armor Break Shatter");
            int beforeShatter=shatters();
            var leap=battleType.GetMethod("ApplyRaptorLeapHit",flags);
            var damage=battleType.GetMethod("ApplyDamage",flags);
            System.Func<object,float> defense=unit=>(float)battleType.GetMethod("CurrentDefense",flags).Invoke(battle,new[]{unit});
            System.Func<object,bool,float> hit=(attacker,skill)=>(float)damage.Invoke(battle,
                new object[]{attacker,target,100f,skill,!skill,false,false,0f,0f,1f,false,false,false});
            float normalBasic=hit(ally,false),normalSkill=hit(ally,true);
            type.GetField("hp").SetValue(target,10000f);
            leap.Invoke(battle,new object[]{source,target,1f,-1});
            float normalLeap=10000f-(float)type.GetField("hp").GetValue(target);
            Assert.That(statuses.Has(CombatStatusKind.ArmorBroken),Is.False);
            Assert.That(shatters(),Is.EqualTo(beforeShatter),"Ordinary leap has no armor shatter.");
            ((System.Collections.Generic.List<string>)type.GetField("activeTraitIds").GetValue(source))
                .Add(RaptorCombatRules.ArmorHuntTraitId);
            type.GetField("hp").SetValue(target,10000f);
            leap.Invoke(battle,new object[]{source,target,1f,-1});
            Assert.That(10000f-(float)type.GetField("hp").GetValue(target),
                Is.EqualTo(normalLeap).Within(0.005f),"First leap must not benefit from its own shred.");
            Assert.That(defense(target),Is.EqualTo(50f));
            Assert.That(shatters(),Is.EqualTo(beforeShatter+1),"Armor Hunt must spawn its visible shatter.");
            Assert.That(defense(other),Is.EqualTo(100f));
            Assert.That(battleType.GetMethod("CurrentStatusResistance",flags).Invoke(battle,new[]{target}),Is.EqualTo(40f));
            Assert.That(statuses.Get(CombatStatusKind.ArmorBroken).remaining,Is.EqualTo(4f));
            Assert.That(hit(ally,false),Is.EqualTo(normalBasic*4f/3f).Within(0.01f));
            Assert.That(hit(source,false),Is.EqualTo(normalBasic*4f/3f).Within(0.01f));
            Assert.That(hit(ally,true),Is.EqualTo(normalSkill*4f/3f).Within(0.01f));
            Assert.That(damage.Invoke(battle,new object[]{ally,target,0f,true,false,false,false,0f,100f,1f,false,false,false}),
                Is.EqualTo(100f));
            statuses.Tick(3f);
            ((System.Collections.Generic.List<string>)type.GetField("activeTraitIds").GetValue(ally))
                .Add(RaptorCombatRules.ArmorHuntTraitId);
            leap.Invoke(battle,new object[]{ally,target,1f,-1});
            Assert.That(statuses.Get(CombatStatusKind.ArmorBroken).remaining,Is.EqualTo(4f));
            Assert.That(statuses.Get(CombatStatusKind.ArmorBroken).stacks,Is.EqualTo(1));
            Assert.That(defense(target),Is.EqualTo(50f));
            type.GetField("itemDefenseShredRatio").SetValue(target,0.3f);
            Assert.That(defense(target),Is.EqualTo(50f),"Use strongest shred; never add or multiply reductions.");
            type.GetField("itemDefenseShredRatio").SetValue(target,0.7f);
            Assert.That(defense(target),Is.EqualTo(30f).Within(0.001f));
            type.GetField("itemDefenseShredRatio").SetValue(target,0f);
            statuses.Tick(4f);
            Assert.That(statuses.Has(CombatStatusKind.ArmorBroken),Is.False,"The overhead icon uses this same state.");
            Assert.That(defense(target),Is.EqualTo(100f));
            leap.Invoke(battle,new object[]{source,target,1f,-1});
            battleType.GetMethod("CleanseAllHarmfulEffects",flags).Invoke(battle,new[]{target});
            Assert.That(statuses.Has(CombatStatusKind.ArmorBroken),Is.False);
            Assert.That(defense(target),Is.EqualTo(100f));
            statuses.DebuffImmune=true;
            int immuneShatters=shatters();
            leap.Invoke(battle,new object[]{source,target,1f,-1});
            Assert.That(statuses.Has(CombatStatusKind.ArmorBroken),Is.False);
            Assert.That(shatters(),Is.EqualTo(immuneShatters),"Blocked debuffs must not show a successful break.");
            statuses.DebuffImmune=false;
            leap.Invoke(battle,new object[]{source,target,1f,-1});
            statuses.Clear();
            Assert.That(statuses.Has(CombatStatusKind.ArmorBroken),Is.False);
            Assert.That(defense(target),Is.EqualTo(100f));
            Assert.That(GameMasterData.Database.traits.First(row=>row.id==RaptorCombatRules.ArmorHuntTraitId).displayName,
                Is.EqualTo("갑옷 사냥"));
        }
        finally
        {
            foreach(GameObject created in Resources.FindObjectsOfTypeAll<GameObject>())
                if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
                    created.transform.parent==null)Object.DestroyImmediate(created);
        }
    }

    [Test]
    public void BiteFrames_AreSix32PixelSprites_WithImpactSync()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.RaptorBiteResourcePath);

        Assert.That(frames.Length,Is.EqualTo(6));
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].rect.width,Is.EqualTo(32f));
            Assert.That(frames[i].rect.height,Is.EqualTo(32f));
            Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(frames[i].texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
        float hitTime=0.25f;
        float lead=PixelCombatEffect.RaptorBiteLeadTime(hitTime,1f);
        Assert.That(
            lead+PixelCombatEffect.RaptorBiteImpactFrame/
            PixelCombatEffect.RaptorBiteFramesPerSecond,
            Is.EqualTo(hitTime).Within(0.0001f));
    }

    [Test]
    public void BitePlayback_ClosesBeforeExplosionFrames()
    {
        GameObject anchor=new GameObject("Bite Test Anchor");
        PixelCombatEffect effect=PixelCombatEffect.PlayRaptorBite(anchor.transform,null,1f);
        try
        {
            Sprite[] source=PixelCombatEffect.LoadFrames(PixelCombatEffect.RaptorBiteResourcePath);
            Sprite[] playback=(Sprite[])typeof(PixelCombatEffect)
                .GetField("frames",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(effect);
            CollectionAssert.AreEqual(
                new[]{source[0],source[1],source[2],source[1],source[0]},playback);
        }
        finally
        {
            Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void WolfHuntAfterimage_ReusesSourcePixelSpriteAndSorting()
    {
        GameObject sourceObject=new GameObject("Wolf Afterimage Source");
        SpriteRenderer source=sourceObject.AddComponent<SpriteRenderer>();
        source.sprite=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.RaptorBiteResourcePath)[0];
        source.sortingOrder=12;
        SpriteRenderer ghost=PixelCombatEffect.PlayWolfHuntAfterimage(
            source,Vector3.zero,Vector3.right,
            new Color(0.5f,0.78f,1f,0.62f),WolfCombatRules.AfterimageDuration);
        try
        {
            Assert.That(ghost,Is.Not.Null);
            Assert.That(ghost.sprite,Is.SameAs(source.sprite));
            Assert.That(ghost.sortingOrder,Is.EqualTo(20));
            Assert.That(ghost.color.a,Is.EqualTo(0.62f).Within(0.001f));
            SpriteRenderer glow=ghost.transform.GetChild(0).GetComponent<SpriteRenderer>();
            Assert.That(glow,Is.Not.Null);
            Assert.That(glow.sprite,Is.SameAs(source.sprite));
            Assert.That(glow.sortingOrder,Is.EqualTo(19));
        }
        finally
        {
            if(ghost!=null)Object.DestroyImmediate(ghost.gameObject);
            Object.DestroyImmediate(sourceObject);
        }
    }

    [Test]
    public void WolfSpectralSpirit_ReusesSourceSpriteAndAlternatesSpiritColor()
    {
        GameObject sourceObject=new GameObject("Celestial Wolf Source");
        SpriteRenderer source=sourceObject.AddComponent<SpriteRenderer>();
        source.sprite=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.RaptorBiteResourcePath)[0];
        source.sortingOrder=12;
        SpriteRenderer first=PixelCombatEffect.PlayWolfSpectralSpirit(
            source,Vector3.zero,Vector3.right,0);
        SpriteRenderer second=PixelCombatEffect.PlayWolfSpectralSpirit(
            source,Vector3.zero,Vector3.right,1);
        try
        {
            Assert.That(first,Is.Not.Null);
            Assert.That(second,Is.Not.Null);
            Assert.That(first.name,Is.EqualTo("Celestial Wolf Spirit 1"));
            Assert.That(second.name,Is.EqualTo("Celestial Wolf Spirit 2"));
            Assert.That(first.sprite,Is.SameAs(source.sprite));
            Assert.That(second.sprite,Is.SameAs(source.sprite));
            Assert.That(first.sortingOrder,Is.EqualTo(20));
            Assert.That(first.color,Is.Not.EqualTo(second.color));
        }
        finally
        {
            if(first!=null)Object.DestroyImmediate(first.gameObject);
            if(second!=null)Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(sourceObject);
        }
    }

    [Test]
    public void CriticalFrames_AreSix32PixelSprites()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.CriticalResourcePath);

        Assert.That(frames.Length,Is.EqualTo(6));
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].rect.width,Is.EqualTo(32f));
            Assert.That(frames[i].rect.height,Is.EqualTo(32f));
            Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(frames[i].texture.filterMode,Is.EqualTo(FilterMode.Point));
        }

        GameObject anchor=new GameObject("Critical Sorting Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sortingOrder=7;
        PixelCombatEffect effect=PixelCombatEffect.PlayCritical(anchor.transform,sorting);
        try
        {
            Assert.That(effect.GetComponent<SpriteRenderer>().sortingOrder,
                Is.EqualTo(57));
        }
        finally
        {
            Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void ClawFrames_AreSix32PixelSprites_WithImpactSync()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.RaptorClawResourcePath);

        Assert.That(frames.Length,Is.EqualTo(6));
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].rect.width,Is.EqualTo(32f));
            Assert.That(frames[i].rect.height,Is.EqualTo(32f));
            Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(frames[i].texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
        float hitTime=3f/11f;
        float lead=PixelCombatEffect.RaptorClawLeadTime(hitTime,1f);
        Assert.That(
            lead+PixelCombatEffect.RaptorClawImpactFrame/
            PixelCombatEffect.RaptorClawFramesPerSecond,
            Is.EqualTo(hitTime).Within(0.0001f));
    }

    [Test]
    public void SabertoothClawFrames_AreSix32PixelSprites_WithImpactSync()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.SabertoothClawResourcePath);

        Assert.That(frames.Length,Is.EqualTo(6));
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].rect.width,Is.EqualTo(32f));
            Assert.That(frames[i].rect.height,Is.EqualTo(32f));
            Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(frames[i].texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
        float hitTime=0.25f;
        float lead=PixelCombatEffect.SabertoothClawLeadTime(hitTime,1f);
        Assert.That(
            lead+PixelCombatEffect.SabertoothClawImpactFrame/
            PixelCombatEffect.SabertoothClawFramesPerSecond,
            Is.EqualTo(hitTime).Within(0.0001f));
    }

    [Test]
    public void FangLockFrames_AreSix32PixelSprites_ForThreeHits()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.SabertoothFangLockResourcePath);

        Assert.That(frames.Length,Is.EqualTo(6));
        Assert.That(SabertoothCombatRules.HitCount,Is.EqualTo(3));
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].rect.width,Is.EqualTo(32f));
            Assert.That(frames[i].rect.height,Is.EqualTo(32f));
            Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(frames[i].texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
    }

    [Test]
    public void FangLockEffect_UsesAttackerFacing()
    {
        GameObject anchor=new GameObject("Fang Lock Test Anchor");
        PixelCombatEffect effect=
            PixelCombatEffect.PlaySabertoothFangLock(anchor.transform,null,true);
        PixelCombatEffect execution=PixelCombatEffect.PlaySabertoothFangLock(
            anchor.transform,null,false,SabertoothCombatRules.ExecutionFangEffectScale);
        try
        {
            Assert.IsTrue(effect.GetComponent<SpriteRenderer>().flipX);
            Assert.That(execution.transform.localScale.x,Is.EqualTo(1.5f));
        }
        finally
        {
            Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(execution.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void CrocodileTailStrikeFrames_AreSix32PixelSprites_WithImpactSync()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.CrocodileTailStrikeResourcePath);

        Assert.That(frames.Length,Is.EqualTo(6));
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].rect.width,Is.EqualTo(32f));
            Assert.That(frames[i].rect.height,Is.EqualTo(32f));
            Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(frames[i].texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
        float hitTime=0.25f;
        float lead=PixelCombatEffect.CrocodileTailStrikeLeadTime(hitTime,1f);
        Assert.That(
            lead+PixelCombatEffect.CrocodileTailStrikeImpactFrame/
            PixelCombatEffect.CrocodileTailStrikeFramesPerSecond,
            Is.EqualTo(hitTime).Within(0.0001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CrocodileAbsoluteJaw_ClosesImmediatelyOnTheDamageFrame(bool transcended)
    {
        GameObject anchor=new GameObject("Absolute Jaw Test Anchor");
        PixelCombatEffect bite=PixelCombatEffect.PlayCrocodileJawCrush(anchor.transform,null);
        PixelCombatEffect crush=PixelCombatEffect.PlayCrocodileAbsoluteJaw(anchor.transform,null,transcended);
        try
        {
            Assert.That(crush.gameObject.name,Is.EqualTo("Crocodile Absolute Jaw Crush"));
            Assert.That(crush.transform.localScale.x,
                Is.GreaterThan(bite.transform.localScale.x));
            Assert.That(crush.transform.localScale.x/bite.transform.localScale.x,
                Is.EqualTo((transcended?1.9f:1.75f)/1.5f).Within(.001f));
            Assert.That(crush.GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(bite.GetComponent<SpriteRenderer>().sprite));
            Assert.That(crush.GetComponent<SpriteRenderer>().color,
                Is.Not.EqualTo(bite.GetComponent<SpriteRenderer>().color));
        }
        finally
        {
            Object.DestroyImmediate(bite.gameObject);
            Object.DestroyImmediate(crush.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void CrocodileDeathRollFrames_AreSix32PixelSprites_WithImpactSync()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.CrocodileDeathRollResourcePath);

        Assert.That(frames.Length,Is.EqualTo(6));
        Assert.That(CrocodileCombatRules.HitCount,Is.EqualTo(3));
        for(int i=0;i<frames.Length;i++)
        {
            Assert.That(frames[i].rect.width,Is.EqualTo(32f));
            Assert.That(frames[i].rect.height,Is.EqualTo(32f));
            Assert.That(frames[i].pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(frames[i].texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
        float hitTime=3f/11f;
        float lead=PixelCombatEffect.CrocodileDeathRollLeadTime(hitTime,1f);
        Assert.That(
            lead+PixelCombatEffect.CrocodileDeathRollImpactFrame/
            PixelCombatEffect.CrocodileDeathRollFramesPerSecond,
            Is.EqualTo(hitTime).Within(0.0001f));
    }

    [Test]
    public void KingKongHeavyPunchFrames_AreSix32PixelSprites_WithImpactSync()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.KingKongHeavyPunchResourcePath);
        Assert.That(frames.Length,Is.EqualTo(6));
        Assert.That(frames.All(frame=>frame.rect.width==32f&&frame.rect.height==32f&&
            frame.pixelsPerUnit==32f&&frame.texture.filterMode==FilterMode.Point),Is.True);
        float hitTime=0.25f;
        float lead=PixelCombatEffect.KingKongHeavyPunchLeadTime(hitTime,1f);
        Assert.That(lead+PixelCombatEffect.KingKongHeavyPunchImpactFrame/
            PixelCombatEffect.KingKongHeavyPunchFramesPerSecond,
            Is.EqualTo(hitTime).Within(0.0001f));
    }

    [Test]
    public void KingKongEarthPulseFrames_AreEight32PixelSprites_WithImpactSync()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.KingKongEarthPulseResourcePath);
        Assert.That(frames.Length,Is.EqualTo(8));
        Assert.That(frames.All(frame=>frame.rect.width==32f&&frame.rect.height==32f&&
            frame.pixelsPerUnit==32f&&frame.texture.filterMode==FilterMode.Point),Is.True);
        float hitTime=0.3f;
        float lead=PixelCombatEffect.KingKongEarthPulseLeadTime(hitTime,1f);
        Assert.That(lead+PixelCombatEffect.KingKongEarthPulseImpactFrame/
            PixelCombatEffect.KingKongEarthPulseFramesPerSecond,
            Is.EqualTo(hitTime).Within(0.0001f));
    }

    [Test]
    public void BearPawFramesAndSpiritParticles_UseDistinctEffects()
    {
        Sprite[] frames=PixelCombatEffect.LoadFrames(
            PixelCombatEffect.BearPawSwipeResourcePath);
        Assert.That(frames.Length,Is.EqualTo(6));
        Assert.That(frames.All(frame=>frame.rect.width==32f&&frame.rect.height==32f&&
            frame.pixelsPerUnit==32f&&frame.texture.filterMode==FilterMode.Point),Is.True);
        string[] motions={"Idle","Run","Attack","SkillAttack","Death","Capture","Struggle"};
        int[] frameCounts={4,8,6,6,6,6,8};
        for(int index=0;index<motions.Length;index++)
        {
            Texture2D strip=Resources.Load<Texture2D>(
                PixelCombatEffect.BearMountainHologramResourcePath+motions[index]);
            Assert.That(strip,Is.Not.Null,motions[index]);
            Assert.That(strip.width,Is.EqualTo(frameCounts[index]*256),motions[index]);
            Assert.That(strip.height,Is.EqualTo(256),motions[index]);
        }

        GameObject anchor=new GameObject("Bear Effect Test Anchor");
        SpriteRenderer source=anchor.AddComponent<SpriteRenderer>();
        source.sprite=frames[0];
        source.sortingOrder=12;
        SpriteMonsterAnimationDriver animation=
            anchor.AddComponent<SpriteMonsterAnimationDriver>();
        animation.Configure(frames,frames,frames,frames,frames,frames,frames);
        ParticleSystem burst=PixelCombatEffect.PlayBearSpiritBurst(anchor.transform,null);
        ParticleSystem aura=PixelCombatEffect.PlayBearSpiritAura(anchor.transform,null);
        SpriteRenderer hologram=PixelCombatEffect.PlayBearMountainColossusHologram(
            anchor.transform,source);
        float roarSourceHeight=source.bounds.size.y;
        SpriteRenderer roarSpirit=PixelCombatEffect.PlayBearKingRoarSpirit(
            anchor.transform,source);
        PixelCombatEffect[] ragingStrikes={
            PixelCombatEffect.PlayBearRagingStrike(anchor.transform,source,0,false),
            PixelCombatEffect.PlayBearRagingStrike(anchor.transform,source,1,false),
            PixelCombatEffect.PlayBearRagingStrike(anchor.transform,source,2,false)};
        PixelCombatEffect ragingShockwave=
            PixelCombatEffect.PlayBearRagingHundredStrikesShockwave(
                Vector3.zero,1.5f,source);
        PixelCombatEffect openingRoar=PixelCombatEffect.PlayBearKingRoarBurst(
            Vector3.zero,3.5f,source,false);
        PixelCombatEffect finalRoar=PixelCombatEffect.PlayBearKingRoarBurst(
            Vector3.zero,4f,source,true);
        try
        {
            Assert.That(burst.main.loop,Is.False);
            Assert.That(aura.main.loop,Is.True);
            Assert.That(aura.emission.rateOverTime.constant,Is.EqualTo(9f));
            Assert.That(hologram.name,Is.EqualTo("Bear Mountain Colossus Hologram"));
            Assert.That(hologram.sprite.name,Does.StartWith("OriginalBear_Idle_"));
            float idleHologramHeight=hologram.bounds.size.y;
            animation.PlaySkillAttack();
            typeof(SpriteMonsterAnimationDriver).GetField("currentFrameIndex",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).SetValue(animation,3);
            typeof(PixelBearUpperBodyHologram).GetMethod("LateUpdate",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).Invoke(
                    hologram.GetComponent<PixelBearUpperBodyHologram>(),null);
            typeof(PixelBearKingRoarSpirit).GetMethod("LateUpdate",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).Invoke(
                    roarSpirit.GetComponent<PixelBearKingRoarSpirit>(),null);
            Assert.That(hologram.sprite.name,
                Does.StartWith("OriginalBear_SkillAttack_"));
            Assert.That(hologram.sortingOrder,Is.EqualTo(10));
            Assert.That(hologram.bounds.size.y,
                Is.EqualTo(idleHologramHeight).Within(idleHologramHeight*0.01f));
            Assert.That(roarSpirit.sprite.name,
                Is.EqualTo("OriginalBear_SkillAttack_03"));
            Assert.That(roarSpirit.sharedMaterial.shader.name,
                Is.EqualTo("BecomeZ/Effects/Bear Roar Spirit Fade"));
            Assert.That(roarSpirit.sharedMaterial.GetFloat("_FadeBottom"),
                Is.LessThan(roarSpirit.sharedMaterial.GetFloat("_FadeTop")));
            Assert.That(roarSpirit.transform.localPosition.y,
                Is.LessThan(source.sprite.bounds.max.y));
            Assert.That(roarSpirit.bounds.size.y,
                Is.GreaterThan(roarSourceHeight*1.45f));
            Assert.That(roarSpirit.color.a,Is.EqualTo(0.8f).Within(0.001f));
            ParticleSystem shimmer=hologram.GetComponentsInChildren<ParticleSystem>()
                .Single(system=>system.name=="Bear Mountain Hologram Shimmer");
            Assert.That(shimmer.main.maxParticles,Is.EqualTo(36));
            Assert.That(shimmer.emission.rateOverTime.constant,Is.EqualTo(14f));
            Assert.That(hologram.transform.localPosition.y,
                Is.LessThan(source.sprite.bounds.max.y));
            Assert.That(hologram.transform.localPosition.x,
                Is.LessThan(source.sprite.bounds.center.x));
            Texture2D[] strikeTextures=ragingStrikes.Select(effect=>effect
                .GetComponent<SpriteRenderer>().sprite.texture).ToArray();
            Assert.That(strikeTextures.Distinct().Count(),Is.EqualTo(1),"Paw contacts share one cached blunt impact.");
            Assert.That(ragingStrikes.Select(effect=>effect.name),Is.EqualTo(
                new[]{"Bear Raging Strike 1","Bear Raging Strike 2",
                    "Bear Raging Strike 3"}));
            Assert.That(ragingShockwave,Is.Not.Null);
            Assert.That(ragingShockwave.name,
                Is.EqualTo("Bear Raging Paw Fracture"));
            Assert.That(ragingShockwave.GetComponent<SpriteRenderer>().sprite.name,
                Does.StartWith("BearRagingPawFracture_"));
            Assert.That(openingRoar.name,Is.EqualTo("Bear King Opening Roar"));
            Assert.That(finalRoar.name,Is.EqualTo("Bear King Final Roar"));
            Assert.That(openingRoar.GetComponent<SpriteRenderer>().sprite.texture,
                Is.Not.SameAs(finalRoar.GetComponent<SpriteRenderer>().sprite.texture));
            Assert.That(finalRoar.GetComponent<PixelBearRoarPressure>(),
                Is.Not.Null);
            PixelCombatEffect.StopBearSpiritAura(aura);
            Assert.That(aura==null,Is.True);
        }
        finally
        {
            PixelCombatEffect.StopBearMountainColossusHologram(hologram);
            if(roarSpirit!=null)Object.DestroyImmediate(roarSpirit.gameObject);
            Object.DestroyImmediate(burst.gameObject);
            if(aura!=null)Object.DestroyImmediate(aura.gameObject);
            if(ragingShockwave!=null)
                Object.DestroyImmediate(ragingShockwave.gameObject);
            if(openingRoar!=null)Object.DestroyImmediate(openingRoar.gameObject);
            if(finalRoar!=null)Object.DestroyImmediate(finalRoar.gameObject);
            for(int index=0;index<ragingStrikes.Length;index++)
                if(ragingStrikes[index]!=null)
                    Object.DestroyImmediate(ragingStrikes[index].gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void BearKingRoarPressure_ReusesItsPooledMotionComponent()
    {
        GameObject anchor=new GameObject("Bear Roar Pool Test");
        SpriteRenderer source=anchor.AddComponent<SpriteRenderer>();
        PixelCombatEffect first=PixelCombatEffect.PlayBearKingRoarBurst(
            Vector3.zero,3f,source,false);
        PixelBearRoarPressure motion=
            first.GetComponent<PixelBearRoarPressure>();
        System.Reflection.BindingFlags flags=
            System.Reflection.BindingFlags.Instance|
            System.Reflection.BindingFlags.NonPublic;
        typeof(PixelBearRoarPressure).GetField("elapsed",flags)
            .SetValue(motion,1f);
        typeof(PixelCombatEffect).GetMethod("Release",flags).Invoke(first,null);

        PixelCombatEffect reused=PixelCombatEffect.PlayBearKingRoarBurst(
            Vector3.zero,3f,source,true);
        try
        {
            Assert.That(reused,Is.SameAs(first));
            Assert.That(reused.GetComponents<PixelBearRoarPressure>().Length,
                Is.EqualTo(1));
            Assert.That(typeof(PixelBearRoarPressure).GetField("elapsed",flags)
                .GetValue(reused.GetComponent<PixelBearRoarPressure>()),
                Is.EqualTo(0f));
        }
        finally
        {
            if(reused!=null)Object.DestroyImmediate(reused.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void BearMountainHologram_AdjustsDamiAndIronKumaSilhouettes()
    {
        string[] paths={
            "Enemies/PixelMonsters/025_woongi",
            "Enemies/PixelMonsters/026_dami",
            "Enemies/PixelMonsters/027_ironkuma"};
        GameObject[] instances=new GameObject[paths.Length];
        SpriteRenderer[] holograms=new SpriteRenderer[paths.Length];
        try
        {
            for(int index=0;index<paths.Length;index++)
            {
                instances[index]=Object.Instantiate(Resources.Load<GameObject>(paths[index]));
                SpriteRenderer source=
                    instances[index].GetComponentInChildren<SpriteRenderer>(true);
                holograms[index]=PixelCombatEffect.PlayBearMountainColossusHologram(
                    source.transform,source);
            }
            Assert.That(holograms[1].transform.localPosition.y,
                Is.GreaterThan(holograms[0].transform.localPosition.y));
            Assert.That(holograms[2].transform.localPosition.y,
                Is.GreaterThan(holograms[0].transform.localPosition.y));
        }
        finally
        {
            for(int index=0;index<instances.Length;index++)
            {
                PixelCombatEffect.StopBearMountainColossusHologram(holograms[index]);
                if(instances[index]!=null)Object.DestroyImmediate(instances[index]);
            }
        }
    }

    [Test]
    public void WizardMouseStar_IsPointFiltered_AndCriticalIsDoubleSize()
    {
        Sprite star=PixelStarProjectile.SharedSprite;
        Assert.That(star.rect.width,Is.EqualTo(9f));
        Assert.That(star.rect.height,Is.EqualTo(9f));
        Assert.That(star.texture.filterMode,Is.EqualTo(FilterMode.Point));
        Assert.That(PixelStarProjectile.VisualScale(true),
            Is.EqualTo(PixelStarProjectile.VisualScale(false)*2f));
    }

    [Test]
    public void WizardMouseStarHit_UsesPixelStarParticles()
    {
        ParticleSystem particles=PixelStarProjectile.PlayImpact(
            Vector3.zero,null,Color.magenta,false);
        try
        {
            ParticleSystemRenderer renderer=particles.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer.sharedMaterial.mainTexture,
                Is.SameAs(PixelStarProjectile.SharedSprite.texture));
            Assert.That(particles.main.maxParticles,Is.EqualTo(16));
        }
        finally
        {
            Object.DestroyImmediate(particles.gameObject);
        }
    }

    [Test]
    public void WizardMouseTraitMarker_ShowsStarAboveTarget()
    {
        GameObject target=new GameObject("Wizard Mouse Trait Marker Target");
        SpriteRenderer sorting=target.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        SpriteRenderer marker=PixelCombatEffect.PlayStarMarker(
            "Wizard Mouse Trait Marker",target.transform,sorting,
            new Color(0.95f,0.18f,0.72f,0.9f),1f);
        try
        {
            Assert.That(marker.sprite,Is.SameAs(PixelStarProjectile.SharedSprite));
            Assert.That(marker.bounds.center.y,Is.GreaterThan(sorting.bounds.max.y));
            Assert.That(marker.sortingOrder,Is.GreaterThan(sorting.sortingOrder));
        }
        finally
        {
            Object.DestroyImmediate(marker.gameObject);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void WizardMouseSupernova_FallsAsLargePixelStar()
    {
        GameObject sortingObject=new GameObject("Wizard Supernova Sorting");
        SpriteRenderer sorting=sortingObject.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.SharedSprite;
        PixelWizardSupernova projectile=PixelWizardSupernova.Create(
            Vector3.zero,Vector3.left*3f,4.25f,sorting,Color.cyan,()=>true);
        try
        {
            projectile.BeginDescent();
            SpriteRenderer head=projectile.transform.Find("Giant Meteor Star").GetComponent<SpriteRenderer>();
            SpriteRenderer shadow=projectile.transform.Find("Growing Landing Shadow").GetComponent<SpriteRenderer>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Vector3 start=head.transform.position;float initialSize=shadow.transform.localScale.x;
            float initialAlpha=shadow.color.a;
            typeof(PixelWizardSupernova).GetField("phaseAge",flags).SetValue(projectile,.5f);
            typeof(PixelWizardSupernova).GetMethod("UpdateVisual",flags).Invoke(projectile,null);
            Assert.That(head.sprite,Is.SameAs(PixelStarProjectile.SharedSprite));
            Assert.That(head.transform.localScale.x,Is.GreaterThan(8f));
            Assert.That(Vector3.Distance(head.transform.position,projectile.transform.position),
                Is.LessThan(Vector3.Distance(start,projectile.transform.position)));
            Assert.That(Mathf.Abs(start.x-projectile.transform.position.x),Is.GreaterThan(1f));
            Assert.That(shadow.transform.localScale.x,Is.GreaterThan(initialSize*4f));
            Assert.That(shadow.color.a,Is.GreaterThan(initialAlpha));
            projectile.Impact();
            Assert.That(head.transform.position.x,Is.EqualTo(projectile.transform.position.x));
            Assert.That(head.transform.position.z,Is.EqualTo(projectile.transform.position.z));
        }
        finally
        {
            Object.DestroyImmediate(projectile.gameObject);
            Object.DestroyImmediate(sortingObject);
        }
    }

    [Test]
    public void MonkeyStone_UsesPointFilteredStoneAndThrownProjectile()
    {
        Sprite stone=PixelStarProjectile.StoneSprite;
        Assert.That(stone.rect.width,Is.EqualTo(9f));
        Assert.That(stone.rect.height,Is.EqualTo(7f));
        Assert.That(stone.texture.filterMode,Is.EqualTo(FilterMode.Point));

        GameObject source=new GameObject("Monkey Stone Source");
        GameObject target=new GameObject("Monkey Stone Target");
        PixelStarProjectile projectile=PixelStarProjectile.PlayStone(
            source.transform,null,target.transform,null,Color.gray,null);
        ParticleSystem impact=null;
        try
        {
            Assert.That(projectile.name,Is.EqualTo("Pixel Monkey Stone"));
            Assert.That(projectile.transform.childCount,Is.EqualTo(3));
            Assert.That(projectile.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(stone));

            impact=PixelStarProjectile.PlayImpact(
                Vector3.zero,null,Color.gray,false,true);
            Assert.That(impact.name,Is.EqualTo("Pixel Stone Hit"));
            Assert.That(impact.particleCount,Is.EqualTo(8));
            Assert.That(impact.GetComponent<ParticleSystemRenderer>()
                    .sharedMaterial.mainTexture,
                Is.SameAs(stone.texture));
        }
        finally
        {
            if(impact!=null)Object.DestroyImmediate(impact.gameObject);
            Object.DestroyImmediate(projectile.gameObject);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void MonkeyLastBean_UsesLargePixelFruitAndExplosionParticles()
    {
        Sprite bean=PixelStarProjectile.BeanSprite;
        Assert.That(bean.rect.width,Is.EqualTo(6f));
        Assert.That(bean.rect.height,Is.EqualTo(5f));
        Assert.That(bean.texture.filterMode,Is.EqualTo(FilterMode.Point));

        GameObject source=new GameObject("Monkey Bean Source");
        GameObject target=new GameObject("Monkey Bean Target");
        PixelStarProjectile projectile=PixelStarProjectile.PlayBean(
            source.transform,null,target.transform,null,Color.yellow,true,2.55f,null);
        PixelStarProjectile cloud=PixelStarProjectile.PlayCloudBean(
            source.transform,null,target.transform,null,null);
        ParticleSystem explosion=PixelStarProjectile.PlayBeanImpact(
            Vector3.zero,null,Color.yellow,true,2.55f);
        try
        {
            Assert.That(projectile.name,Is.EqualTo("Pixel Monkey Last Bean"));
            Assert.That(projectile.transform.childCount,Is.EqualTo(3));
            Assert.That(projectile.FlightDuration,Is.InRange(0.46f,0.66f));
            Assert.That(projectile.transform.GetChild(0).GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(PixelStarProjectile.MonkeyFruitSprite));
            Assert.That(cloud.name,Is.EqualTo("Pixel Monkey Cloud Bounce"));
            Assert.That(cloud.transform.childCount,Is.EqualTo(4));
            Assert.That(projectile.transform.GetChild(0).GetComponent<SpriteRenderer>().bounds.size.x,
                Is.GreaterThan(cloud.transform.GetChild(0).GetComponent<SpriteRenderer>().bounds.size.x*2f));
            Assert.That(explosion.name,Is.EqualTo("Pixel Bean Barrage Explosion"));
            Assert.That(explosion.GetComponent<ParticleSystemRenderer>()
                    .sharedMaterial.mainTexture,
                Is.SameAs(bean.texture));
            Assert.That(explosion.main.maxParticles,Is.EqualTo(32));
        }
        finally
        {
            foreach(var effect in Object.FindObjectsOfType<PixelCombatEffect>())
                if(effect.name=="Monkey Heavy Fruit Pop")Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(explosion.gameObject);
            Object.DestroyImmediate(cloud.gameObject);
            Object.DestroyImmediate(projectile.gameObject);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void SpiderProjectilesAndGroundWeb_UsePixelSpritesBehindUnits()
    {
        Assert.That(PixelStarProjectile.SilkNeedleSprite.texture.filterMode,
            Is.EqualTo(FilterMode.Point));
        Assert.That(PixelStarProjectile.WebBundleSprite.texture.filterMode,
            Is.EqualTo(FilterMode.Point));
        GameObject source=new GameObject("Spider Source");
        GameObject target=new GameObject("Spider Target");
        SpriteRenderer sorting=source.AddComponent<SpriteRenderer>();
        sorting.sortingOrder=20;
        PixelStarProjectile needle=PixelStarProjectile.PlaySilkNeedle(
            source.transform,sorting,target.transform,null,Color.cyan,false,null);
        GameObject web=PixelCombatEffect.PlaySpiderWebGround(
            Vector3.zero,1.8f,Color.white,sorting);
        try
        {
            Assert.That(needle.transform.childCount,Is.EqualTo(5));
            SpriteRenderer webRenderer=web.transform.GetChild(0)
                .GetComponent<SpriteRenderer>();
            Assert.That(webRenderer.sortingOrder,Is.LessThan(sorting.sortingOrder));
            Assert.That(webRenderer.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
        }
        finally
        {
            Object.DestroyImmediate(needle.gameObject);
            Object.DestroyImmediate(web);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void NinjaSlashAndVanish_UsePixelVisuals()
    {
        GameObject anchor=new GameObject("Ninja Effect Test Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        PixelCombatEffect slash=PixelCombatEffect.PlayNinjaKunaiSlash(
            anchor.transform,sorting,1f,true,1.3f);
        ParticleSystem vanish=PixelCombatEffect.PlayNinjaVanishBurst(
            anchor.transform,sorting);
        GameObject line=PixelCombatEffect.PlayNinjaLineSlash(
            Vector3.zero,Vector3.right,sorting,new Color(0.48f,0.88f,1f));
        ParticleSystem poison=PixelCombatEffect.PlayNinjaPoisonMist(
            anchor.transform,sorting,3f);
        ParticleSystem flame=PixelCombatEffect.PlayNinjaFlameAfterimage(
            Vector3.zero,1.25f,sorting);
        try
        {
            Sprite sprite=slash.GetComponent<SpriteRenderer>().sprite;
            Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(32f,32f)));
            Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(slash.transform.localScale,Is.EqualTo(Vector3.one*1.3f));
            Assert.That(slash.GetComponent<SpriteRenderer>().flipX,Is.True);
            Assert.That(vanish.main.maxParticles,Is.EqualTo(24));
            Assert.That(vanish.main.simulationSpace,
                Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(line.transform.childCount,Is.EqualTo(1));
            SpriteRenderer lineRenderer=
                line.transform.GetChild(0).GetComponent<SpriteRenderer>();
            Assert.That(lineRenderer.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(lineRenderer.color.b,Is.EqualTo(1f).Within(0.001f));
            Assert.That(poison.main.duration,Is.EqualTo(3f).Within(0.001f));
            Assert.That(poison.emission.rateOverTime.constant,
                Is.EqualTo(11f).Within(0.001f));
            Assert.That(flame.shape.radius,Is.EqualTo(1.25f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(slash.gameObject);
            Object.DestroyImmediate(vanish.gameObject);
            Object.DestroyImmediate(line);
            Object.DestroyImmediate(poison.gameObject);
            Object.DestroyImmediate(flame.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void TyrannoKingJawBite_UsesSixPixelFramesAndImpactTiming()
    {
        GameObject anchor=new GameObject("Tyranno Bite Test Anchor");
        PixelCombatEffect effect=PixelCombatEffect.PlayTyrannoKingJawBite(
            anchor.transform,null,1f);
        try
        {
            Sprite[] frames=(Sprite[])typeof(PixelCombatEffect)
                .GetField("frames",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(effect);
            Assert.That(frames.Length,Is.EqualTo(6));
            Assert.That(frames.All(frame=>frame.rect.size==new Vector2(32f,32f)),
                Is.True);
            Assert.That(frames.All(frame=>frame.texture.filterMode==FilterMode.Point),
                Is.True);
            float hitTime=0.25f;
            float lead=PixelCombatEffect.TyrannoKingJawBiteLeadTime(hitTime,1f);
            Assert.That(lead+PixelCombatEffect.TyrannoKingJawBiteImpactFrame/
                PixelCombatEffect.TyrannoKingJawBiteFramesPerSecond,
                Is.EqualTo(hitTime).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void TyrannoKingJawBite_AllowsEmpoweredScaleAndTint()
    {
        GameObject anchor=new GameObject("Empowered Tyranno Bite Test Anchor");
        Color tint=new Color(1f,0.28f,0.05f);
        PixelCombatEffect effect=PixelCombatEffect.PlayTyrannoKingJawBite(
            anchor.transform,null,1f,1.58f,tint);
        try
        {
            Assert.That(effect.transform.localScale.x,Is.EqualTo(1.58f).Within(0.001f));
            Assert.That(effect.GetComponent<SpriteRenderer>().color,Is.EqualTo(tint));
        }
        finally
        {
            Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void TyrannoTraits_UseDedicatedGroundSpritesAndVisibleHealingCrosses()
    {
        GameObject anchor=new GameObject("Tyranno Trait Visual Test Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelTyrannoLavaFieldEffect lava=PixelCombatEffect.PlayTyrannoLavaField(
            Vector3.zero,2f,TyrannoCombatRules.LavaDuration,sorting);
        PixelSpiritSanctuaryEffect fissure=PixelCombatEffect.PlayTyrannoFissure(
            Vector3.right*3f,2f,1.15f,sorting);
        ParticleSystem healing=PixelCombatEffect.PlayHealingBurst(anchor.transform,sorting);
        try
        {
            Assert.That(lava.name,Is.EqualTo("Magno Lava Footprint"));
            Assert.That(fissure.name,Is.EqualTo("Bonrex Ancient Fissure"));
            SpriteRenderer lavaGround=lava.transform.Find("Lava Ground")
                .GetComponent<SpriteRenderer>();
            Assert.That(lavaGround.sprite,
                Is.SameAs(PixelCombatEffect.TyrannoLavaSpreadFrames[0]));
            Assert.That(PixelCombatEffect.TyrannoLavaSpreadFrames.Length,
                Is.EqualTo(9));
            int firstLavaPixels=PixelCombatEffect.TyrannoLavaSpreadFrames[0]
                .texture.GetPixels32().Count(pixel=>pixel.a>0);
            int finalLavaPixels=PixelCombatEffect.TyrannoLavaGroundSprite
                .texture.GetPixels32().Count(pixel=>pixel.a>0);
            Assert.That(finalLavaPixels,Is.GreaterThan(firstLavaPixels*2));
            Assert.That(finalLavaPixels,Is.LessThan(900),
                "Lava branches should remain thin instead of filling the area.");
            Assert.That(lava.EruptionParticles.main.maxParticles,Is.EqualTo(72));
            Assert.That(lava.EruptionParticles.shape.radius,
                Is.EqualTo(1.76f).Within(0.001f));
            Assert.That(lava.EruptionParticles.velocityOverLifetime.y.mode,
                Is.EqualTo(ParticleSystemCurveMode.Constant));
            typeof(PixelTyrannoLavaFieldEffect).GetField(
                    "elapsed",System.Reflection.BindingFlags.Instance|
                              System.Reflection.BindingFlags.NonPublic)
                .SetValue(lava,0.57f-Time.deltaTime);
            typeof(PixelTyrannoLavaFieldEffect).GetMethod(
                    "Update",System.Reflection.BindingFlags.Instance|
                             System.Reflection.BindingFlags.NonPublic)
                .Invoke(lava,null);
            Assert.That(lavaGround.sprite,
                Is.SameAs(PixelCombatEffect.TyrannoLavaSpreadFrames[6]));
            Assert.That(lava.EruptionParticles.particleCount,Is.GreaterThan(0));
            Assert.That(fissure.GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(PixelCombatEffect.TyrannoFissureGroundSprite));
            Assert.That(PixelCombatEffect.TyrannoLavaGroundSprite,
                Is.Not.SameAs(PixelCombatEffect.TyrannoFissureGroundSprite));
            Assert.That(healing.name,Is.EqualTo("Common Healing Cross Burst"));
            Assert.That(healing.main.maxParticles,Is.EqualTo(32));
            Assert.That(healing.main.startSize.constantMax,Is.GreaterThanOrEqualTo(0.25f));
            Assert.That(healing.velocityOverLifetime.y.mode,
                Is.EqualTo(ParticleSystemCurveMode.Constant));
            Assert.That(healing.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(lava.gameObject);
            Object.DestroyImmediate(fissure.gameObject);
            Object.DestroyImmediate(healing.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void TyrannoTremorTelegraph_HasFullRangeFillAndImpactState()
    {
        GameObject sortingObject=new GameObject("Tyranno Telegraph Sorting");
        SpriteRenderer sorting=sortingObject.AddComponent<SpriteRenderer>();
        Material outlineMaterial=new Material(Shader.Find("Sprites/Default"));
        PixelGroundCastTelegraph telegraph=
            PixelCombatEffect.PlayTyrannoTremorTelegraph(
                Vector3.zero,2f,TyrannoCombatRules.TremorCastSeconds,
                outlineMaterial,sorting);
        try
        {
            LineRenderer outline=telegraph.GetComponent<LineRenderer>();
            Assert.That(outline.positionCount,Is.EqualTo(32));
            Assert.That(telegraph.transform.GetChild(0).GetComponent<MeshFilter>()
                .sharedMesh,Is.Not.Null);

            telegraph.Impact();

            Assert.That(outline.startWidth,Is.EqualTo(0.13f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(telegraph.gameObject);
            Object.DestroyImmediate(outlineMaterial);
            Object.DestroyImmediate(sortingObject);
        }
    }

    [Test]
    public void TyrannoTremorImpact_CoversTheSkillRadiusWithPixelDebris()
    {
        GameObject anchor=new GameObject("Tyranno Impact Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        ParticleSystem effect=PixelCombatEffect.PlayTyrannoTremorImpact(
            Vector3.zero,2f,sorting);
        try
        {
            Assert.That(effect.shape.radius,Is.EqualTo(2f).Within(0.001f));
            Assert.That(effect.main.maxParticles,Is.EqualTo(64));
            Assert.That(effect.velocityOverLifetime.y.mode,
                Is.EqualTo(ParticleSystemCurveMode.Constant));
            Assert.That(effect.velocityOverLifetime.y.constant,
                Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(effect.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void TyrannoTremor_UsesInwardChargeAndSmallTargetHitParticles()
    {
        GameObject anchor=new GameObject("Tyranno Particle Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        ParticleSystem charge=PixelCombatEffect.PlayTyrannoTremorCharge(
            anchor.transform,sorting,TyrannoCombatRules.TremorCastSeconds);
        ParticleSystem hit=PixelCombatEffect.PlayTyrannoTremorHit(
            anchor.transform,sorting);
        try
        {
            Assert.That(charge.main.startSpeed.constantMax,Is.LessThan(0f));
            Assert.That(charge.main.maxParticles,Is.EqualTo(32));
            Assert.That(hit.main.maxParticles,Is.EqualTo(10));
        }
        finally
        {
            Object.DestroyImmediate(charge.gameObject);
            Object.DestroyImmediate(hit.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void MantisSlashWindAndBleed_UsePixelVisuals()
    {
        GameObject anchor=new GameObject("Mantis Effect Test Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.ScytheWindSprite;
        PixelCombatEffect slash=PixelCombatEffect.PlayMantisScytheSlash(
            anchor.transform,sorting,1f,false);
        PixelStarProjectile wind=PixelStarProjectile.PlayScytheWind(
            anchor.transform,sorting,Vector3.right*3.5f,Color.green,1f);
        ParticleSystem bleed=PixelCombatEffect.PlayBleedBurst(anchor.transform,sorting);
        PixelCombatEffect precise=PixelCombatEffect.PlayMantisScytheSlash(
            anchor.transform,sorting,1.3f,true,1.32f,new Color(1f,0.2f,0.12f));
        ParticleSystem pollen=PixelCombatEffect.PlayMantisHallucinatoryPollen(
            anchor.transform,sorting,4f);
        ParticleSystem sand=PixelCombatEffect.PlayMantisSandTrail(
            Vector3.zero,Vector3.forward*3.5f,0.6f,sorting);
        GameObject soul=PixelCombatEffect.PlayMantisSoulThread(
            Vector3.zero,Vector3.right*2f,sorting);
        try
        {
            Sprite[] frames=(Sprite[])typeof(PixelCombatEffect)
                .GetField("frames",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(slash);
            Assert.That(frames.Length,Is.EqualTo(6));
            Assert.That(frames.All(frame=>frame.rect.size==new Vector2(32f,32f)&&
                frame.texture.filterMode==FilterMode.Point),Is.True);
            Assert.That(PixelStarProjectile.ScytheWindSprite.texture.filterMode,
                Is.EqualTo(FilterMode.Point));
            Assert.That(wind.FlightDuration,Is.GreaterThan(0f));
            float expectedHeadY=anchor.transform.TransformPoint(
                sorting.sprite.bounds.center+Vector3.up*sorting.sprite.bounds.extents.y*0.35f).y;
            Assert.That(bleed.transform.position.y,Is.EqualTo(expectedHeadY).Within(0.001f));
            Assert.That(bleed.main.maxParticles,Is.EqualTo(12));
            Assert.That(precise.transform.localScale,Is.EqualTo(Vector3.one*1.32f));
            Assert.That(precise.GetComponent<SpriteRenderer>().color.r,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(pollen.main.duration,Is.EqualTo(4f).Within(0.001f));
            Assert.That(pollen.emission.rateOverTime.constant,
                Is.EqualTo(16f).Within(0.001f));
            Assert.That(sand.shape.shapeType,Is.EqualTo(ParticleSystemShapeType.Box));
            Assert.That(sand.shape.scale.x,Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(soul.transform.childCount,Is.EqualTo(1));
            float hitTime=0.25f;
            Assert.That(PixelCombatEffect.MantisScytheSlashLeadTime(hitTime,1f)+
                PixelCombatEffect.MantisScytheSlashImpactFrame/
                PixelCombatEffect.MantisScytheSlashFramesPerSecond,
                Is.EqualTo(hitTime).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(slash.gameObject);
            Object.DestroyImmediate(wind.gameObject);
            Object.DestroyImmediate(bleed.gameObject);
            Object.DestroyImmediate(precise.gameObject);
            Object.DestroyImmediate(pollen.gameObject);
            Object.DestroyImmediate(sand.gameObject);
            Object.DestroyImmediate(soul);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void MammothTuskRam_UsesIvoryPixelImpactAndGroundDust()
    {
        GameObject anchor=new GameObject("Mammoth Tusk Effect Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelCombatEffect effect=PixelCombatEffect.PlayMammothTuskRam(
            anchor.transform,sorting,1f,false);
        ParticleSystem dust=PixelCombatEffect.PlayMammothTuskDust(
            anchor.transform,sorting);
        Material guideMaterial=new Material(Shader.Find("Sprites/Default"));
        GameObject guide=PixelCombatEffect.PlayMammothChargeGuide(
            Vector3.zero,Vector3.forward*3.5f,0.48f,guideMaterial,sorting);
        try
        {
            Sprite[] frames=(Sprite[])typeof(PixelCombatEffect)
                .GetField("frames",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(effect);
            Assert.That(frames.Length,Is.EqualTo(6));
            Assert.That(frames.All(frame=>frame.texture.filterMode==FilterMode.Point),
                Is.True);
            Assert.That(dust.main.maxParticles,Is.EqualTo(12));
            Assert.That(dust.main.gravityModifier.constant,Is.GreaterThan(0f));
            Assert.That(dust.transform.position.y,Is.LessThan(anchor.transform.position.y));
            Assert.That(guide.transform.childCount,Is.EqualTo(1));
            Transform plate=guide.transform.GetChild(0);
            MeshRenderer plateRenderer=plate.GetComponent<MeshRenderer>();
            MaterialPropertyBlock plateColor=new MaterialPropertyBlock();
            plateRenderer.GetPropertyBlock(plateColor);
            Assert.That(plate.name,Is.EqualTo("Red Ground Plate"));
            Assert.That(plate.GetComponent<LineRenderer>(),Is.Null);
            Assert.That(plate.GetComponent<Collider>(),Is.Null);
            Assert.That(plate.localScale.x,Is.EqualTo(0.96f).Within(0.001f));
            Assert.That(plate.localScale.y,Is.EqualTo(3.5f).Within(0.001f));
            Assert.That(plateColor.GetColor("_Color").r,Is.EqualTo(1f));
            Assert.That(plateColor.GetColor("_Color").a,Is.EqualTo(0.34f));
            float hitTime=0.25f;
            Assert.That(PixelCombatEffect.MammothTuskRamLeadTime(hitTime,1f)+
                PixelCombatEffect.MammothTuskRamImpactFrame/
                PixelCombatEffect.MammothTuskRamFramesPerSecond,
                Is.EqualTo(hitTime).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(dust.gameObject);
            Object.DestroyImmediate(guide);
            Object.DestroyImmediate(guideMaterial);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void BeetleHornAttacks_SeparateHorizontalThrustFromSkillUppercut()
    {
        GameObject anchor=new GameObject("Beetle Horn Effect Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelCombatEffect thrust=PixelCombatEffect.PlayBeetleHornThrust(
            anchor.transform,sorting,1f,false);
        PixelCombatEffect uppercut=PixelCombatEffect.PlayBeetleHornUppercut(
            anchor.transform,sorting,1f,false,1.2f);
        ParticleSystem impact=PixelCombatEffect.PlayBeetleHornImpact(
            anchor.transform,sorting);
        try
        {
            Sprite[] frames=(Sprite[])typeof(PixelCombatEffect)
                .GetField("frames",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(thrust);
            Assert.That(frames.Length,Is.EqualTo(6));
            Assert.That(frames.All(frame=>frame.rect.size==new Vector2(32f,32f)&&
                frame.texture.filterMode==FilterMode.Point),Is.True);
            Assert.That(frames[3].name,Does.StartWith("Beetle Horn Thrust"));
            Assert.That(uppercut.transform.localScale.x,Is.EqualTo(1.2f));
            Assert.That(impact.main.maxParticles,Is.EqualTo(14));
            Assert.That(impact.main.gravityModifier.constant,Is.GreaterThan(0f));
            float hitTime=0.25f;
            Assert.That(PixelCombatEffect.BeetleHornThrustLeadTime(hitTime,1f)+
                PixelCombatEffect.BeetleHornThrustImpactFrame/
                PixelCombatEffect.BeetleHornThrustFramesPerSecond,
                Is.EqualTo(hitTime).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(thrust.gameObject);
            Object.DestroyImmediate(uppercut.gameObject);
            Object.DestroyImmediate(impact.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void MushroomSporeShot_UsesPixelProjectileAndSpreadingImpact()
    {
        GameObject source=new GameObject("Mushroom Spore Source");
        GameObject target=new GameObject("Mushroom Spore Target");
        target.transform.position=Vector3.right*3f;
        SpriteRenderer sourceRenderer=source.AddComponent<SpriteRenderer>();
        SpriteRenderer targetRenderer=target.AddComponent<SpriteRenderer>();
        PixelStarProjectile projectile=PixelStarProjectile.PlaySporeShot(
            source.transform,sourceRenderer,target.transform,targetRenderer,
            Color.green,null);
        PixelStarProjectile split=PixelStarProjectile.PlaySporeSplit(
            target.transform,targetRenderer,target.transform.position+Vector3.forward*2f,
            Color.magenta,null);
        ParticleSystem impact=PixelStarProjectile.PlaySporeImpact(
            target.transform.position,targetRenderer,Color.green);
        Material outlineMaterial=new Material(Shader.Find("Sprites/Default"));
        PixelGroundCastTelegraph charge=PixelCombatEffect.PlayMushroomSporeCharge(
            target.transform.position,2f,1.2f,outlineMaterial,targetRenderer,Color.magenta);
        PixelGroundCastTelegraph child=PixelCombatEffect.PlayMushroomChildCharge(
            target.transform.position,2f,1.2f,outlineMaterial,targetRenderer);
        PixelSpiritSanctuaryEffect poisonZone=PixelCombatEffect.PlayMushroomPoisonZone(
            target.transform.position,2f,3f,targetRenderer);
        ParticleSystem explosion=PixelStarProjectile.PlaySporeExplosion(
            target.transform.position,targetRenderer,Color.magenta,2f);
        try
        {
            Assert.That(projectile.FlightDuration,Is.GreaterThan(0f));
            Assert.That(split.FlightDuration,Is.GreaterThan(0f));
            Assert.That(projectile.GetComponentInChildren<SpriteRenderer>()
                .sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(impact.main.maxParticles,Is.EqualTo(20));
            Assert.That(impact.main.startSpeed.constantMax,Is.GreaterThan(1f));
            Assert.That(charge.transform.Find("Charging Spore"),Is.Not.Null);
            SpriteRenderer childSprite=child.transform.Find("Charging Spore")
                .GetComponent<SpriteRenderer>();
            Assert.That(childSprite.sprite,Is.EqualTo(PixelCombatEffect.MushroomChildSprite));
            Assert.That(childSprite.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(poisonZone.name,Is.EqualTo("Doksirum Poison Mycelium"));
            Assert.That(poisonZone.GetComponent<SpriteRenderer>().sprite,
                Is.EqualTo(PixelCombatEffect.MushroomPoisonGroundSprite));
            Assert.That(poisonZone.Duration,Is.EqualTo(3f));
            Assert.That(charge.GetComponent<LineRenderer>().GetPosition(0).magnitude,
                Is.EqualTo(2f).Within(0.02f));
            Assert.That(explosion.main.maxParticles,Is.EqualTo(64));
            Assert.That(explosion.main.startSize.constantMax,Is.LessThan(0.06f));
            Assert.That(explosion.main.startSpeed.constantMax,Is.LessThan(0.2f));
            Assert.That(explosion.shape.radius,Is.EqualTo(2f).Within(0.001f));
            Assert.That(explosion.shape.scale.y,Is.LessThan(0.03f));
            Assert.That(explosion.noise.enabled,Is.True);
            Assert.That(explosion.colorOverLifetime.enabled,Is.True);
        }
        finally
        {
            Object.DestroyImmediate(projectile.gameObject);
            Object.DestroyImmediate(split.gameObject);
            Object.DestroyImmediate(impact.gameObject);
            Object.DestroyImmediate(charge.gameObject);
            Object.DestroyImmediate(child.gameObject);
            Object.DestroyImmediate(poisonZone.gameObject);
            Object.DestroyImmediate(explosion.gameObject);
            Object.DestroyImmediate(outlineMaterial);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void SpiritDeerBasicAttack_UsesTealLightAndLeafBurst()
    {
        GameObject source=new GameObject("Spirit Deer Light Source");
        GameObject target=new GameObject("Spirit Deer Light Target");
        target.transform.position=Vector3.right*3f;
        SpriteRenderer sourceRenderer=source.AddComponent<SpriteRenderer>();
        SpriteRenderer targetRenderer=target.AddComponent<SpriteRenderer>();
        PixelStarProjectile projectile=PixelStarProjectile.PlaySpiritLight(
            source.transform,sourceRenderer,target.transform,targetRenderer,
            new Color(0.38f,1f,0.72f),null);
        ParticleSystem impact=PixelStarProjectile.PlaySpiritLeafImpact(
            target.transform.position,targetRenderer,new Color(0.38f,1f,0.72f));
        try
        {
            Assert.That(projectile.FlightDuration,Is.GreaterThan(0f));
            Assert.That(projectile.GetComponentInChildren<SpriteRenderer>().sprite,
                Is.EqualTo(PixelStarProjectile.SpiritLightSprite));
            Assert.That(PixelStarProjectile.SpiritLightSprite.texture.filterMode,
                Is.EqualTo(FilterMode.Point));
            Assert.That(impact.main.maxParticles,Is.EqualTo(14));
            Assert.That(impact.main.startSize.constantMax,Is.LessThan(0.09f));
            Assert.That(impact.noise.enabled,Is.True);
        }
        finally
        {
            Object.DestroyImmediate(projectile.gameObject);
            Object.DestroyImmediate(impact.gameObject);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void SpiritDeerSanctuary_UsesStableGroundAndSparseRisingMotes()
    {
        GameObject anchor=new GameObject("Spirit Sanctuary Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelSpiritSanctuaryEffect sanctuary=PixelCombatEffect.PlaySpiritSanctuary(
            Vector3.zero,2f,5f,new Color(0.48f,1f,0.76f),sorting,
            "Pixel Spirit Sanctuary",true);
        try
        {
            SpriteRenderer ground=sanctuary.transform.Find("Sanctuary Ground")
                .GetComponent<SpriteRenderer>();
            ParticleSystem particles=sanctuary.GetComponent<ParticleSystem>();
            Assert.That(ground.sprite,Is.EqualTo(
                PixelCombatEffect.SpiritSanctuaryGroundSprite));
            Assert.That(ground.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(sanctuary.Duration,Is.EqualTo(5f));
            Assert.That(sanctuary.StableGround,Is.True);
            Assert.That(particles.main.loop,Is.True);
            Assert.That(particles.main.maxParticles,Is.EqualTo(56));
            Assert.That(particles.emission.rateOverTime.constant,
                Is.EqualTo(4f));
            Assert.That(particles.shape.radius,Is.EqualTo(1.76f).Within(0.001f));
            Assert.That(particles.velocityOverLifetime.y.constantMax,
                Is.GreaterThan(0f));
            Assert.That(particles.main.startSize.constantMax,
                Is.EqualTo(.14f).Within(.001f));
        }
        finally
        {
            Object.DestroyImmediate(sanctuary.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void GriffinBasicAttack_SlashesDownwardWithGoldenFeathers()
    {
        GameObject anchor=new GameObject("Griffin Claw Effect Anchor");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelCombatEffect claw=PixelCombatEffect.PlayGriffinEagleClaw(
            anchor.transform,sorting,1f,false);
        ParticleSystem feathers=PixelCombatEffect.PlayGriffinFeatherImpact(
            anchor.transform,sorting);
        try
        {
            Sprite[] frames=(Sprite[])typeof(PixelCombatEffect)
                .GetField("frames",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(claw);
            Assert.That(frames.Length,Is.EqualTo(6));
            Assert.That(frames.All(frame=>frame.texture.filterMode==FilterMode.Point),
                Is.True);
            Assert.That(PixelCombatEffect.GriffinEagleClawY(0),
                Is.GreaterThan(PixelCombatEffect.GriffinEagleClawY(25)));
            Assert.That(feathers.main.maxParticles,Is.EqualTo(12));
            Assert.That(feathers.main.gravityModifier.constant,Is.GreaterThan(0f));
            float hitTime=0.25f;
            Assert.That(PixelCombatEffect.GriffinEagleClawLeadTime(hitTime,1f)+
                PixelCombatEffect.GriffinEagleClawImpactFrame/
                PixelCombatEffect.GriffinEagleClawFramesPerSecond,
                Is.EqualTo(hitTime).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(claw.gameObject);
            Object.DestroyImmediate(feathers.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void GriffinSkyDive_HoldsThirdFrameWhileRisingThenSlamsTheMarkedArea()
    {
        GameObject model=new GameObject("Griffin Sky Dive Test");
        SpriteRenderer sorting=model.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        SpriteMonsterAnimationDriver driver=
            model.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        frames[GriffinCombatRules.SkyDiveAscentFrames-2]=
            PixelStarProjectile.StoneSprite;
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        driver.ConfigureHitFrames(3,3);
        Material outlineMaterial=new Material(Shader.Find("Sprites/Default"));
        PixelGroundCastTelegraph telegraph=
            PixelCombatEffect.PlayGriffinSkyDiveTelegraph(
                Vector3.zero,2f,GriffinCombatRules.SkyfallDuration,
                outlineMaterial,sorting);
        ParticleSystem impact=PixelCombatEffect.PlayGriffinSkyDiveImpact(
            Vector3.zero,2f,sorting);
        try
        {
            System.Reflection.MethodInfo advance=
                typeof(SpriteMonsterAnimationDriver).GetMethod(
                    "Advance",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
            System.Reflection.MethodInfo lateUpdate=
                typeof(SpriteMonsterAnimationDriver).GetMethod(
                    "LateUpdate",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
            float flightClock=0f;
            driver.PlaySkillAttackWithHold(
                GriffinCombatRules.SkyDiveAscentFrames-1,
                GriffinCombatRules.SkyfallDuration+GriffinCombatRules.SkyDiveDescentDuration,1f,null,()=>flightClock);
            flightClock=.74f;
            advance.Invoke(driver,new object[]{0.74f});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2));
            advance.Invoke(driver,new object[]{5f});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(2),"Paused combat cannot advance the held flight pose.");
            flightClock=.76f;
            advance.Invoke(driver,new object[]{0.02f});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
            Assert.That(GriffinCombatRules.SkyDiveLift(
                GriffinCombatRules.SkyfallDuration,
                GriffinCombatRules.SkyfallDuration,3f/11f),
                Is.EqualTo(GriffinCombatRules.SkyDiveLiftHeight).Within(0.001f));
            Assert.That(GriffinCombatRules.SkyDiveLift(
                GriffinCombatRules.SkyfallDuration+
                GriffinCombatRules.SkyDiveDescentDuration,
                GriffinCombatRules.SkyfallDuration,
                GriffinCombatRules.SkyDiveDescentDuration),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(GriffinCombatRules.SkyDiveDescentDuration,
                Is.EqualTo(0.05f));
            Assert.That(GriffinCombatRules.SkyDiveLiftHeight,
                Is.EqualTo(5.6f));
            Assert.That(driver.GetSkillAttackFrame(
                    GriffinCombatRules.SkyDiveAscentFrames-2),
                Is.SameAs(PixelStarProjectile.StoneSprite));
            Assert.That(telegraph.transform.Find("Charging Fill").localScale.x,
                Is.LessThan(0.02f));
            telegraph.Impact();
            Assert.That(telegraph.transform.Find("Charging Fill").localScale.x,
                Is.EqualTo(1f));
            Assert.That(impact.shape.radius,Is.EqualTo(2f).Within(0.001f));
            Assert.That(impact.main.gravityModifier.constant,Is.LessThan(0.5f));
        }
        finally
        {
            Object.DestroyImmediate(telegraph.gameObject);
            Object.DestroyImmediate(impact.gameObject);
            Object.DestroyImmediate(outlineMaterial);
            Object.DestroyImmediate(model);
        }
    }

    [Test]
    public void GriffinTraits_UseRoyalClawHellgateHologramAndTreeShield()
    {
        GameObject anchor=new GameObject("Griffin Trait Visual Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelCombatEffect royalClaw=PixelCombatEffect.PlayGriffinEagleClaw(
            anchor.transform,sorting,1f,false,1.7f);
        PixelSpiritSanctuaryEffect hellgate=PixelCombatEffect.PlaySpiritSanctuary(
            Vector3.zero,2f,GriffinCombatRules.HellfireDuration,
            new Color(0.5f,0.025f,0.012f),sorting,"Griphonas Hellgate");
        SpriteRenderer hologram=PixelCombatEffect.PlayWolfHuntAfterimage(
            sorting,Vector3.up*GriffinCombatRules.SkyDiveLiftHeight,Vector3.zero,
            new Color(0.24f,0.9f,1f,0.72f),GriffinCombatRules.AfterimageDelay,
            "Holoponas Hologram Afterimage",
            GriffinCombatRules.AfterimageDelay-
            GriffinCombatRules.SkyDiveDescentDuration,
            PixelStarProjectile.StoneSprite);
        ParticleSystem treeShield=PixelStarProjectile.PlaySpiritLeafImpact(
            Vector3.up,sorting,new Color(0.3f,0.94f,0.42f),
            "Entpone Ancient Tree Embrace",
            0.7f,1.25f,-0.04f,0.18f,2f,20);
        try
        {
            Assert.That(royalClaw.transform.localScale.x,Is.EqualTo(1.7f));
            Assert.That(hellgate.name,Is.EqualTo("Griphonas Hellgate"));
            Assert.That(hellgate.Duration,
                Is.EqualTo(GriffinCombatRules.HellfireDuration));
            Assert.That(hellgate.transform.Find("Sanctuary Ground")
                .GetComponent<SpriteRenderer>().color.r,Is.EqualTo(0.5f));
            Assert.That(hologram.gameObject.name,
                Is.EqualTo("Holoponas Hologram Afterimage"));
            Assert.That(hologram.sprite,
                Is.SameAs(PixelStarProjectile.StoneSprite));
            Assert.That(hologram.transform.position.y,
                Is.EqualTo(GriffinCombatRules.SkyDiveLiftHeight));
            float movementDelay=(float)typeof(PixelWolfHuntAfterimage)
                .GetField("movementDelay",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic)
                .GetValue(hologram.GetComponent<PixelWolfHuntAfterimage>());
            Assert.That(movementDelay,Is.EqualTo(
                GriffinCombatRules.AfterimageDelay-
                GriffinCombatRules.SkyDiveDescentDuration));
            Assert.That(treeShield.name,Is.EqualTo("Entpone Ancient Tree Embrace"));
            Assert.That(treeShield.main.startSize.constantMax,
                Is.GreaterThanOrEqualTo(0.17f));
            Assert.That(treeShield.main.maxParticles,Is.EqualTo(20));
        }
        finally
        {
            Object.DestroyImmediate(royalClaw.gameObject);
            Object.DestroyImmediate(hellgate.gameObject);
            Object.DestroyImmediate(hologram.gameObject);
            Object.DestroyImmediate(treeShield.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void LegacyWingGust_FiresAnimatedTornadoWithWindFeatherImpact()
    {
        GameObject source=new GameObject("Wyvern Wing Gust Source");
        GameObject target=new GameObject("Wyvern Wing Gust Target");
        target.transform.position=Vector3.right*3f;
        SpriteRenderer sourceRenderer=source.AddComponent<SpriteRenderer>();
        SpriteRenderer targetRenderer=target.AddComponent<SpriteRenderer>();
        PixelStarProjectile projectile=PixelStarProjectile.PlayWingGust(
            source.transform,sourceRenderer,target.transform,targetRenderer,
            new Color(0.48f,0.92f,1f),null);
        ParticleSystem impact=PixelStarProjectile.PlayWingGustImpact(
            target.transform.position,targetRenderer,new Color(0.48f,0.92f,1f));
        try
        {
            Assert.That(projectile.name,Is.EqualTo("Pixel Wyvern Wing Gust"));
            Assert.That(projectile.transform.childCount,Is.EqualTo(4));
            Assert.That(projectile.transform.GetChild(0)
                .GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(PixelStarProjectile.WingTornadoFrames[0]));
            Assert.That(PixelStarProjectile.WingTornadoFrames.Length,Is.EqualTo(6));
            Assert.That(PixelStarProjectile.WingTornadoFrames.All(
                frame=>frame.texture.filterMode==FilterMode.Point),Is.True);
            SpriteRenderer head=projectile.transform.GetChild(0)
                .GetComponent<SpriteRenderer>();
            typeof(PixelStarProjectile).GetField(
                "elapsed",System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).SetValue(projectile,0.1f);
            typeof(PixelStarProjectile).GetMethod(
                "UpdateVisual",System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic)
                .Invoke(projectile,new object[]{0.5f});
            Assert.That(head.sprite,Is.Not.SameAs(PixelStarProjectile.WingTornadoFrames[0]));
            Assert.That(projectile.FlightDuration,Is.InRange(0.14f,0.34f));
            Assert.That(impact.name,Is.EqualTo("Pixel Wyvern Wing Gust Impact"));
            Assert.That(impact.noise.enabled,Is.True);
            Assert.That(impact.noise.strength.constant,Is.EqualTo(0.24f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(projectile.gameObject);
            Object.DestroyImmediate(impact.gameObject);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void WyvernBreath_HoldsFourthFrameForCastAndShowsStraightMouthCone()
    {
        GameObject model=new GameObject("Wyvern Breath Test");
        SpriteRenderer sorting=model.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        SpriteMonsterAnimationDriver driver=
            model.AddComponent<SpriteMonsterAnimationDriver>();
        Sprite[] frames=Enumerable.Repeat(PixelStarProjectile.BeanSprite,6).ToArray();
        driver.Configure(frames,frames,frames,frames,frames,frames,frames);
        driver.ConfigureHitFrames(3,3);
        Material guideMaterial=new Material(Shader.Find("Sprites/Default"));
        ParticleSystem breath=PixelCombatEffect.PlayWyvernBreath(
            model.transform,sorting,Vector3.forward,8.5f,1.02f,0.4f,
            guideMaterial);
        GameObject guide=PixelCombatEffect.PlayMammothChargeGuide(
            Vector3.zero,Vector3.forward*8.5f,1.02f,guideMaterial,sorting);
        try
        {
            System.Reflection.MethodInfo advance=
                typeof(SpriteMonsterAnimationDriver).GetMethod(
                    "Advance",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
            System.Reflection.MethodInfo lateUpdate=
                typeof(SpriteMonsterAnimationDriver).GetMethod(
                    "LateUpdate",System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
            driver.PlaySkillAttack(1f,()=>driver.HoldSkillAttackHitFrame(0.4f));
            advance.Invoke(driver,new object[]{0.7f});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
            advance.Invoke(driver,new object[]{0.39f});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(3));
            advance.Invoke(driver,new object[]{0.02f});
            lateUpdate.Invoke(driver,null);
            Assert.That(driver.CurrentFrameIndex,Is.EqualTo(4));
            Assert.That(breath.main.duration,Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(breath.shape.shapeType,Is.EqualTo(ParticleSystemShapeType.Cone));
            Assert.That(breath.shape.angle,Is.GreaterThan(3f));
            Assert.That(breath.main.maxParticles,Is.EqualTo(112));
            Assert.That(breath.emission.rateOverTime.constant,Is.EqualTo(180f));
            Assert.That(breath.sizeOverLifetime.enabled,Is.True);
            Assert.That(breath.sizeOverLifetime.size.curve.Evaluate(0f),
                Is.LessThan(breath.sizeOverLifetime.size.curve.Evaluate(1f)));
            Assert.That(breath.velocityOverLifetime.enabled,Is.True);
            Assert.That(breath.velocityOverLifetime.x.mode,
                Is.EqualTo(breath.velocityOverLifetime.y.mode));
            Assert.That(breath.velocityOverLifetime.z.mode,
                Is.EqualTo(breath.velocityOverLifetime.y.mode));
            Assert.That(breath.velocityOverLifetime.y.curve.Evaluate(0f),
                Is.LessThan(breath.velocityOverLifetime.y.curve.Evaluate(1f)));
            Assert.That(breath.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.Billboard));
            Assert.That(breath.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                Is.SameAs(guideMaterial));
            Assert.That(breath.transform.position.y,Is.GreaterThan(model.transform.position.y));
            Assert.That(breath.transform.position.z,Is.GreaterThanOrEqualTo(0.42f));
            Transform plate=guide.transform.GetChild(0);
            Assert.That(plate.localScale.x,Is.EqualTo(2.04f).Within(0.001f));
            Assert.That(plate.localScale.y,Is.EqualTo(8.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(breath.gameObject);
            Object.DestroyImmediate(guide);
            Object.DestroyImmediate(guideMaterial);
            Object.DestroyImmediate(model);
        }
    }

    [Test]
    public void WyvernTraits_ShowDistinctBreathStatusTrailsAndChainLightning()
    {
        GameObject anchor=new GameObject("Wyvern Trait Effect Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        Material material=new Material(Shader.Find("Sprites/Default"));
        ParticleSystem fireBreath=PixelCombatEffect.PlayWyvernBreath(
            anchor.transform,sorting,Vector3.forward,5f,0.6f,0.4f,material,
            new Color(1f,0.12f,0.02f),new Color(1f,0.82f,0.08f),
            "Igvire Fire Breath");
        GameObject fireTrail=PixelCombatEffect.PlayWyvernFireTrail(
            Vector3.zero,Vector3.forward*5f,1.2f,4f,sorting);
        GameObject glacierTrail=PixelCombatEffect.PlayWyvernGlacierTrail(
            Vector3.zero,Vector3.forward*5f,1.2f,1.4f,sorting);
        ParticleSystem venomMist=PixelCombatEffect.PlayNinjaPoisonMist(
            anchor.transform,sorting,1.2f,"Vivre Venom Breath Mist");
        GameObject lightning=PixelCombatEffect.PlayBeetleLightningArc(
            Vector3.zero,Vector3.right*2f,sorting,
            "Temvire Storm Chain Lightning",0.42f,1.65f);
        try
        {
            Assert.That(fireBreath.name,Is.EqualTo("Igvire Fire Breath"));
            Assert.That(fireBreath.main.startColor.colorMin,
                Is.EqualTo(new Color(1f,0.12f,0.02f)));
            Assert.That(fireBreath.main.startColor.colorMax,
                Is.EqualTo(new Color(1f,0.82f,0.08f)));
            Assert.That(venomMist.name,Is.EqualTo("Vivre Venom Breath Mist"));
            Assert.That(fireTrail.name,Is.EqualTo("Igvire Fire Breath Trail"));
            Assert.That(glacierTrail.name,
                Is.EqualTo("Frovire Glacier Breath Trail"));
            Color fireColor=fireTrail.transform.Find("Pixel Ground Path")
                .GetComponent<SpriteRenderer>().color;
            Color glacierColor=glacierTrail.transform.Find("Pixel Ground Path")
                .GetComponent<SpriteRenderer>().color;
            Assert.That(fireColor.r,Is.GreaterThan(fireColor.b));
            Assert.That(glacierColor.b,Is.GreaterThan(glacierColor.r));
            Assert.That(lightning.name,
                Is.EqualTo("Temvire Storm Chain Lightning"));
            Assert.That(lightning.transform.childCount,Is.GreaterThan(0));
            PixelNinjaLineSlash firstSegment=lightning.transform.GetChild(0)
                .GetComponent<PixelNinjaLineSlash>();
            float lifetime=(float)typeof(PixelNinjaLineSlash).GetField(
                "duration",System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).GetValue(firstSegment);
            Assert.That(lifetime,Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(firstSegment.transform.GetChild(0).localScale.y,
                Is.GreaterThan(3f));
        }
        finally
        {
            Object.DestroyImmediate(fireBreath.gameObject);
            Object.DestroyImmediate(fireTrail);
            Object.DestroyImmediate(glacierTrail);
            Object.DestroyImmediate(lightning);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void Haetae_UsesRaptorBiteAndRoarFromHeadAcrossGroundRadius()
    {
        GameObject anchor=new GameObject("Haetae Effect Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        Material material=new Material(Shader.Find("Sprites/Default"));
        PixelCombatEffect bite=PixelCombatEffect.PlayRaptorBite(
            anchor.transform,sorting,1f);
        ParticleSystem shout=PixelCombatEffect.PlayHaetaeRoarShout(
            anchor.transform,sorting,material);
        PixelHaetaeRoarWave wave=PixelCombatEffect.PlayHaetaeRoarWave(
            Vector3.zero,4.25f,sorting);
        try
        {
            Assert.That(bite.name,Is.EqualTo("Raptor Bite Effect"));
            Assert.That(shout.transform.position.y,
                Is.GreaterThan(sorting.bounds.max.y));
            Assert.That(shout.shape.shapeType,
                Is.EqualTo(ParticleSystemShapeType.Cone));
            Assert.That(shout.emission.burstCount,Is.EqualTo(1));
            Assert.That(wave.name,Is.EqualTo("Haetae Roar Ground Wave"));
            SpriteRenderer ground=wave.GetComponent<SpriteRenderer>();
            Assert.That(ground.sprite,
                Is.SameAs(PixelCombatEffect.SpiritSanctuaryGroundSprite));
            float fullScale=8.5f/ground.sprite.bounds.size.x;
            Assert.That(wave.transform.localScale.x,
                Is.EqualTo(fullScale*0.04f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(bite.gameObject);
            Object.DestroyImmediate(shout.gameObject);
            Object.DestroyImmediate(wave.gameObject);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void HaetaeTraits_UsePurificationFireDiamondAndKarmaEffects()
    {
        GameObject anchor=new GameObject("Haetae Trait Effect Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        Material material=new Material(Shader.Find("Sprites/Default"));
        ParticleSystem shout=PixelCombatEffect.PlayHaetaeRoarShout(
            anchor.transform,sorting,material,new Color(1f,.12f,.02f),
            new Color(1f,.82f,.08f),"Yeomhae Flame Judgment Shout");
        ParticleSystem cleanse=PixelCombatEffect.PlaySpiritDeerCleanse(
            anchor.transform,sorting,"Haeta Purifying Judgment");
        ParticleSystem flame=PixelCombatEffect.PlayNinjaFlameAfterimage(
            Vector3.zero,.42f,sorting);
        SpriteRenderer shield=PixelCombatEffect.PlayBeetleFortressAura(
            anchor.transform,sorting,5f,"Geumgangsu Diamond Guardian",
            new Color(1f,.78f,.24f,.72f));
        ParticleSystem karma=PixelCombatEffect.PlaySpiritDeerEclipseCurse(
            anchor.transform,sorting,"Mukryeong Karma Brand");
        CombatStatusState status=new CombatStatusState();
        status.ApplyStacks(CombatStatusKind.Burning,4f,1,1,null,5f,1f);
        int burningTicks=0;
        status.Tick(1.01f,effect=>
        {
            if(effect.kind==CombatStatusKind.Burning)burningTicks++;
        });
        try
        {
            Assert.That(shout.name,Is.EqualTo("Yeomhae Flame Judgment Shout"));
            Assert.That(shout.main.startColor.colorMin.r,
                Is.GreaterThan(shout.main.startColor.colorMin.g));
            Assert.That(cleanse.name,Is.EqualTo("Haeta Purifying Judgment"));
            Assert.That(flame,Is.Not.Null);
            Assert.That(shield.name,Is.EqualTo("Geumgangsu Diamond Guardian"));
            Assert.That(shield.color.r,Is.GreaterThan(shield.color.b));
            Assert.That(karma.name,Is.EqualTo("Mukryeong Karma Brand"));
            Assert.That(status.Has(CombatStatusKind.Burning),Is.True);
            Assert.That(status.Has(CombatStatusKind.Poison),Is.False);
            Assert.That(burningTicks,Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(shout.gameObject);
            Object.DestroyImmediate(cleanse.gameObject);
            Object.DestroyImmediate(flame.gameObject);
            Object.DestroyImmediate(shield.gameObject);
            Object.DestroyImmediate(karma.gameObject);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void GroundWave_UsesRequestedTraitColor()
    {
        GameObject anchor=new GameObject("Trait Wave Color Test");
        SpriteRenderer sorting=anchor.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        Color requested=new Color(1f,0.52f,0.08f,0.82f);
        PixelHaetaeRoarWave wave=PixelCombatEffect.PlayHaetaeRoarWave(
            Vector3.zero,2f,sorting,requested);
        try
        {
            Color actual=wave.GetComponent<SpriteRenderer>().color;
            Assert.That(actual.r,Is.EqualTo(requested.r).Within(0.001f));
            Assert.That(actual.g,Is.EqualTo(requested.g).Within(0.001f));
            Assert.That(actual.b,Is.EqualTo(requested.b).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(wave.gameObject);
            Object.DestroyImmediate(anchor);
        }
    }

    [Test]
    public void WiloIceField_HoldsBlueLandingAreaAndSlowsAllActions()
    {
        GameObject source=new GameObject("Wilo Ice Field Source");
        SpriteRenderer sorting=source.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        Material material=new Material(Shader.Find("Sprites/Default"));
        PixelGroundCastTelegraph field=PixelCombatEffect.PlayWiloIceField(
            Vector3.zero,2f,RaptorCombatRules.IceSlowDuration,material,sorting);
        try
        {
            Assert.That(field.name,Is.EqualTo("Wilo Ice Field"));
            Assert.That(field.IsHolding,Is.True);
            Assert.That(field.transform.GetChild(0).localScale,Is.EqualTo(Vector3.one));
            Assert.That(RaptorCombatRules.IceSpeedMultiplier(true),Is.EqualTo(0.75f));
            Assert.That(RaptorCombatRules.IceShield(100f),
                Is.EqualTo(15f).Within(0.001f));
            Assert.That(RaptorCombatRules.IceShield(100f,1.4f),
                Is.EqualTo(21f).Within(0.001f));
            Assert.That(RaptorCombatRules.IceChillDuration,Is.EqualTo(4f));
            Assert.That(RaptorCombatRules.IceChillStacksToFreeze,Is.EqualTo(3));
            Assert.That(RaptorCombatRules.IceFreezeDuration,Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(field.gameObject);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void CommonFreezeOverlay_CoversBodyWithIceAndIrregularSurfaceCrystals()
    {
        GameObject target=new GameObject("Freeze Overlay Target");
        SpriteRenderer sorting=target.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        SpriteRenderer overlay=PixelCombatEffect.PlayFreezeOverlay(
            target.transform,sorting,1f);
        try
        {
            Assert.That(overlay.name,Is.EqualTo("Common Freeze Ice Block"));
            Assert.That(overlay.sprite,Is.Null);
            Assert.That(overlay.sortingOrder,Is.EqualTo(sorting.sortingOrder+2));
            var body=overlay.transform.Find("Freeze Solid Ice Body").GetComponent<SpriteRenderer>();
            var spikes=overlay.GetComponentsInChildren<SpriteRenderer>().Where(r=>r!=overlay&&r!=body).ToArray();
            Assert.That(spikes.Length,Is.EqualTo(8));
            float initialLength=spikes[0].transform.localScale.y;
            var shell=overlay.GetComponent<PixelFreezeShell>();
            typeof(PixelFreezeShell).GetMethod("Draw",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                .Invoke(shell,new object[]{0.2f});
            Assert.That(spikes[0].transform.localScale.y,Is.GreaterThan(initialLength*10f));
            Assert.That(body.sprite,Is.SameAs(PixelCombatEffect.FreezeBodySprite));
            Assert.That(body.transform.localPosition,Is.EqualTo(Vector3.zero));
            Assert.That(body.bounds.Contains(sorting.bounds.center),Is.True);
            Assert.That(body.color.a,Is.LessThan(1f));
            for(int x=-1;x<=1;x+=2)
                for(int y=-1;y<=1;y+=2)
                    Assert.That(spikes.Any(r=>
                        (r.transform.localRotation*Vector3.up).x*x>0f&&
                        (r.transform.localRotation*Vector3.up).y*y>0f),Is.True);
            Assert.That(spikes.Any(r=>r.sortingOrder<sorting.sortingOrder),Is.True);
            Assert.That(spikes.Any(r=>r.sortingOrder>sorting.sortingOrder),Is.True);
            foreach(var spike in spikes)
            {
                Assert.That(spike.sprite,Is.SameAs(PixelCombatEffect.FreezeIceSprite));
                Assert.That(Vector3.Dot(spike.transform.localRotation*Vector3.up,
                    spike.transform.localPosition.normalized),Is.GreaterThan(0.9f));
            }
        }
        finally
        {
            Object.DestroyImmediate(overlay.gameObject);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void CommonFreezeDeathShatter_RemovesIceAndEmitsFragments()
    {
        GameObject target=new GameObject("Freeze Shatter Target");
        SpriteRenderer sorting=target.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        PixelCombatEffect.PlayFreezeOverlay(target.transform,sorting,1f);
        ParticleSystem shatter=PixelCombatEffect.PlayFreezeDeathShatter(
            target.transform,sorting);
        try
        {
            Assert.That(target.transform.Find("Common Freeze Ice Block"),Is.Null);
            Assert.That(shatter.name,Is.EqualTo("Common Freeze Death Shatter"));
            Assert.That(shatter.main.maxParticles,Is.EqualTo(24));
            Assert.That(shatter.main.startSize.constantMin,
                Is.GreaterThan(0f));
            Assert.That(shatter.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture,
                Is.SameAs(PixelCombatEffect.FreezeShardSprite.texture));
            Assert.That(shatter.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.Billboard));
        }
        finally
        {
            Object.DestroyImmediate(shatter.gameObject);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void CommonFreezeOverlay_RefreshReusesShellAndCleanseStartsOneThaw()
    {
        var target=new GameObject("Freeze Refresh Target");
        var sorting=target.AddComponent<SpriteRenderer>();sorting.sprite=PixelStarProjectile.BeanSprite;
        var status=new CombatStatusState();status.ApplyTimed(CombatStatusKind.Freeze,1f);
        var overlay=PixelCombatEffect.PlayFreezeOverlay(target.transform,sorting,1f,status);
        ParticleSystem thaw=null;
        try
        {
            status.ApplyTimed(CombatStatusKind.Freeze,2f);
            Assert.That(PixelCombatEffect.PlayFreezeOverlay(target.transform,sorting,2f,status),Is.SameAs(overlay));
            Assert.That(target.GetComponentsInChildren<PixelFreezeShell>().Length,Is.EqualTo(1));
            var shell=overlay.GetComponent<PixelFreezeShell>();
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var update=typeof(PixelFreezeShell).GetMethod("LateUpdate",flags);
            status.Remove(CombatStatusKind.Freeze);update.Invoke(shell,null);
            thaw=GameObject.Find("Common Freeze Thaw Shatter").GetComponent<ParticleSystem>();
            Assert.That(thaw.main.maxParticles,Is.EqualTo(10));
            if(shell!=null)
            {
                Assert.That(typeof(PixelFreezeShell).GetField("releasing",flags).GetValue(shell),Is.True);
                typeof(PixelFreezeShell).GetField("releaseAge",flags).SetValue(shell,.18f);
                update.Invoke(shell,null);
            }
            Assert.That(overlay==null,Is.True);
        }
        finally
        {
            if(thaw!=null)Object.DestroyImmediate(thaw.gameObject);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void CommonFreezeOverlay_DoesNotAppearWhenFreezeWasRejected()
    {
        var target=new GameObject("Freeze Immune Target");
        try
        {
            var status=new CombatStatusState{ControlImmune=true};
            status.ApplyTimed(CombatStatusKind.Freeze,1f);
            Assert.That(PixelCombatEffect.PlayFreezeOverlay(target.transform,null,1f,status),Is.Null);
            Assert.That(target.transform.childCount,Is.Zero);
        }
        finally{Object.DestroyImmediate(target);}
    }

    [Test]
    public void CommonPoisonTick_RaisesPurpleGreenBubblesAboveHealthBar()
    {
        GameObject target=new GameObject("Poison Tick Target");
        SpriteRenderer sorting=target.AddComponent<SpriteRenderer>();
        sorting.sprite=PixelStarProjectile.BeanSprite;
        ParticleSystem poison=PixelCombatEffect.PlayPoisonBurst(
            target.transform,sorting);
        try
        {
            Assert.That(poison.name,Is.EqualTo("Poison Bubbles"));
            Assert.That(poison.transform.position,
                Is.EqualTo(target.transform.position+Vector3.up*1.88f));
            Assert.That(poison.transform.childCount,Is.Zero);
            Assert.That(poison.main.maxParticles,Is.EqualTo(12));
            Assert.That(poison.shape.shapeType,
                Is.EqualTo(ParticleSystemShapeType.Circle));
            Assert.That(poison.main.startSpeed.constant,Is.Zero);
            Assert.That(poison.main.startLifetime.constantMax,
                Is.EqualTo(1.2f));
            Assert.That(poison.main.startSize.constantMax,
                Is.EqualTo(0.14f));
            Assert.That(poison.velocityOverLifetime.enabled,Is.True);
            Assert.That(poison.velocityOverLifetime.y.constantMin,
                Is.EqualTo(0.5f));
            Assert.That(poison.noise.enabled,Is.True);
            Assert.That(poison.main.startColor.colorMin.g,Is.EqualTo(1f));
            Assert.That(poison.main.startColor.colorMax.b,Is.EqualTo(0.82f));
        }
        finally
        {
            Object.DestroyImmediate(poison.gameObject);
            Object.DestroyImmediate(target);
        }
    }

}
