using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SabertoothFangLockTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [TestCase("expire")]
    [TestCase("cleanse")]
    [TestCase("immunity")]
    [TestCase("death")]
    [TestCase("hidden")]
    public void BindingAndDebuffIconsFollowActualStatusAndReleaseCleanly(string ending)
    {
        GameObject target=new GameObject("Fang Binding Test");
        var reference=target.AddComponent<SpriteRenderer>();
        reference.sprite=PixelStarProjectile.BeanSprite;
        var statuses=new CombatStatusState();
        PixelCombatEffect binding=null,reused=null;
        try
        {
            Assert.That(SabertoothCombatRules.HasFangLockDebuff(statuses),Is.False);
            statuses.ControlImmune=true;
            statuses.ApplyTimed(CombatStatusKind.Root,1.2f,null,0.3f);
            Assert.That(PixelCombatEffect.PlaySabertoothFangBinding(
                target.transform,reference,statuses),Is.Null);
            statuses.ControlImmune=false;
            statuses.ApplyTimed(CombatStatusKind.Root,1.2f); // A plain root is not attack-speed down.
            Assert.That(SabertoothCombatRules.HasFangLockDebuff(statuses),Is.False);
            statuses.ApplyTimed(CombatStatusKind.Root,1.2f,null,0.3f);
            Assert.That(SabertoothCombatRules.HasFangLockDebuff(statuses),Is.True);
            binding=PixelCombatEffect.PlaySabertoothFangBinding(target.transform,reference,statuses);
            Assert.That(binding.IsFangLockBinding(statuses),Is.True);
            var frames=(Sprite[])typeof(PixelCombatEffect).GetField("frames",Private).GetValue(binding);
            Assert.That(frames.Length,Is.EqualTo(6));
            Assert.That(frames.All(frame=>frame.rect.width==48&&
                frame.texture.filterMode==FilterMode.Point),Is.True);
            Assert.That(frames.All(frame=>frame.name.StartsWith("Tiger Jaw Hold ")),Is.True);
            Assert.That(PixelCombatEffect.FangLockRootIcon,
                Is.Not.SameAs(PixelCombatEffect.FangLockSlowIcon));
            Assert.That(PixelCombatEffect.FangLockRootIcon.width,Is.EqualTo(64));
            Assert.That(PixelCombatEffect.FangLockSlowIcon.filterMode,Is.EqualTo(FilterMode.Bilinear));

            var update=typeof(PixelCombatEffect).GetMethod("Update",Private);
            typeof(PixelCombatEffect).GetField("elapsed",Private).SetValue(binding,0.9f);
            update.Invoke(binding,null);
            Assert.That(binding.gameObject.activeSelf,Is.True,"Binding must outlast its initial frames.");
            float baseScale=binding.transform.localScale.x;
            binding.PulseFangLockBite(1.28f);
            Assert.That(binding.GetComponent<SpriteRenderer>().sprite,Is.SameAs(frames[0]));
            Assert.That(binding.transform.localScale.x,Is.EqualTo(baseScale*1.28f).Within(0.001f));
            binding.PulseFangLockBite();
            Assert.That(binding.transform.localScale.x,Is.EqualTo(baseScale).Within(0.001f));
            statuses.Tick(0.6f);
            statuses.ApplyTimed(CombatStatusKind.Root,1.6f,null,0.3f);
            update.Invoke(binding,null);
            Assert.That(binding.IsFangLockBinding(statuses),Is.True,"Refreshing must keep the same binding.");

            if(ending=="expire")statuses.Tick(2f);
            else if(ending=="cleanse")statuses.RemoveAllHarmful();
            else if(ending=="immunity")statuses.DebuffImmune=true;
            else if(ending=="death")statuses.Clear();
            else target.SetActive(false);
            update.Invoke(binding,null);
            Assert.That(binding.gameObject.activeSelf,Is.False);
            Assert.That(binding.IsFangLockBinding(statuses),Is.False);
            if(ending!="hidden")Assert.That(SabertoothCombatRules.HasFangLockDebuff(statuses),Is.False);
            target.SetActive(true);
            reused=PixelCombatEffect.PlaySabertoothFangLock(target.transform,reference,false);
            Assert.That(reused.IsFangLockBinding(statuses),Is.False,"Pool reuse must clear status lifetime.");
            Assert.That(reused.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("Tiger Crushing Bite "));
            typeof(PixelCombatEffect).GetField("elapsed",Private).SetValue(reused,1f);
            update.Invoke(reused,null);
            Assert.That(reused.gameObject.activeSelf,Is.False,"A normal hit must not loop after pool reuse.");
        }
        finally
        {
            if(binding!=null)Object.DestroyImmediate(binding.gameObject);
            if(reused!=null)Object.DestroyImmediate(reused.gameObject);
            Object.DestroyImmediate(target);
        }
    }

    [TestCase(0,0f,1000f,0f,false)]
    [TestCase(1,0f,1000f,0f,false)]
    [TestCase(0,100f,1000f,0f,false)]
    [TestCase(1,900f,1000f,0f,false)]
    [TestCase(0,900f,3000f,0f,false)]
    [TestCase(1,900f,3000f,0.5f,false)]
    [TestCase(0,900f,3000f,0.5f,true)]
    [TestCase(1,900f,1000f,0f,true)]
    public void RealSkillKeepsHealthDamageTrueAndDebuffsInSync(
        int team,float armor,float health,float damageReduction,bool echo)
    {
        var existing=new HashSet<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
        try
        {
            GameObject host=new GameObject("Fang Lock Combat Test");host.SetActive(false);
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var bt=typeof(MonsterAutoBattlePrototype);
            var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
            var units=(IList)bt.GetField("units",Private).GetValue(battle);
            bt.GetField("combatBounds",Private).SetValue(battle,new Bounds(Vector3.zero,Vector3.one*100f));
            System.Func<int,object> create=side=>
            {
                object unit=System.Activator.CreateInstance(ut,true);
                var model=new GameObject("Fang Unit");model.transform.position=Vector3.right*side;
                ut.GetField("model").SetValue(unit,model);
                ut.GetField("ring").SetValue(unit,new GameObject("Fang Ring"));
                var teamField=ut.GetField("team");
                teamField.SetValue(unit,System.Enum.ToObject(teamField.FieldType,side));
                ut.GetField("definition").SetValue(unit,
                    Resources.Load<MonsterDefinition>("MonsterData/Monster_geomchi"));
                ut.GetField("skillId").SetValue(unit,SabertoothCombatRules.SkillId);
                ut.GetField("alive").SetValue(unit,true);
                ut.GetField("hp").SetValue(unit,1000f);ut.GetField("maxHp").SetValue(unit,1000f);
                ut.GetField("damage").SetValue(unit,100f);ut.GetField("attackInterval").SetValue(unit,1f);
                var statuses=(CombatStatusState)ut.GetField("combatStatuses").GetValue(unit);
                statuses.Applied+=effect=>bt.GetMethod("HandleCombatStatusApplied",Private)
                    .Invoke(battle,new object[]{unit,effect});
                units.Add(unit);return unit;
            };
            object source=create(team),target=create(1-team);
            ut.GetField("hp").SetValue(target,health);ut.GetField("maxHp").SetValue(target,health);
            ut.GetField("defense").SetValue(target,armor);
            ut.GetField("itemDamageReduction").SetValue(target,damageReduction);
            float expected=180f*100f/(100f+armor)*(1f-damageReduction)+health*0.2f;
            var statuses=(CombatStatusState)ut.GetField("combatStatuses").GetValue(target);
            ut.GetField("fangLockTarget").SetValue(source,target);
            var skill=GameMasterData.Skill(SabertoothCombatRules.SkillId);
            Assert.That(skill.baseCooldown,Is.EqualTo(7.5f));
            Assert.That(skill.statusValue,Is.EqualTo(0.3f));
            if(echo)
            {
                ut.GetField("skillEchoId").SetValue(source,skill.id);
                ut.GetField("skillEchoTarget").SetValue(source,target);
                ut.GetField("rank3EchoScale").SetValue(source,0.65f);
                bt.GetMethod("ExecuteSkillEcho",Private).Invoke(battle,new[]{source});
                Assert.That(health-(float)ut.GetField("hp").GetValue(target),
                    Is.EqualTo(expected*0.65f).Within(0.01f));
                Assert.That(ut.GetField("skillHits").GetValue(source),Is.EqualTo(1));
                Assert.That(SabertoothCombatRules.HasFangLockDebuff(statuses),Is.True);
                return;
            }
            var routine=(IEnumerator)bt.GetMethod("ResolveFangLock",Private).Invoke(battle,
                new object[]{source,skill,bt.GetField("combatGeneration",Private).GetValue(battle)});
            Assert.That(routine.MoveNext(),Is.True);
            Assert.That(health-(float)ut.GetField("hp").GetValue(target),
                Is.EqualTo(expected/3f).Within(0.01f));
            Assert.That(statuses.Get(CombatStatusKind.Root).remaining,Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(SabertoothCombatRules.HasFangLockDebuff(statuses),Is.True);
            Assert.That((float)bt.GetMethod("CurrentAttackInterval",Private).Invoke(battle,new[]{target}),
                Is.GreaterThan(1.4f));
            var binding=(PixelCombatEffect)ut.GetField("fangLockDebuffEffect").GetValue(target);
            Assert.That(binding.IsFangLockBinding(statuses),Is.True);
            statuses.ApplyTimed(CombatStatusKind.Root,1.2f,source,0.3f);
            Assert.That(ut.GetField("fangLockDebuffEffect").GetValue(target),Is.SameAs(binding));
            statuses.Tick(0.6f);
            Assert.That(routine.MoveNext(),Is.True);
            Assert.That(health-(float)ut.GetField("hp").GetValue(target),
                Is.EqualTo(expected*2f/3f).Within(0.01f));
            statuses.Tick(0.6f);
            Assert.That(routine.MoveNext(),Is.False);
            Assert.That((float)ut.GetField("hp").GetValue(target),
                Is.EqualTo(health-expected).Within(0.01f));
            Assert.That(ut.GetField("skillHits").GetValue(source),Is.EqualTo(3));
            Assert.That(SabertoothCombatRules.HasFangLockDebuff(statuses),Is.False);
            Assert.That((float)bt.GetMethod("CurrentAttackInterval",Private).Invoke(battle,new[]{target}),
                Is.EqualTo(1f).Within(0.001f));
            typeof(PixelCombatEffect).GetMethod("Update",Private).Invoke(binding,null);
            Assert.That(binding.gameObject.activeSelf,Is.False);
        }
        finally
        {
            foreach(var created in Resources.FindObjectsOfTypeAll<GameObject>())
                if(created!=null&&!existing.Contains(created)&&created.scene.IsValid()&&
                    created.transform.parent==null)Object.DestroyImmediate(created);
        }
    }
}
