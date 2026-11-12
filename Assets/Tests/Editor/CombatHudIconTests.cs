using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatHudIconTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [Test]
    public void EveryIconUsesBuffOrDebuffColorBasedOnTheAffectedUnit()
    {
        foreach(CombatStatusKind kind in System.Enum.GetValues(typeof(CombatStatusKind)))
        {
            bool buff=kind==CombatStatusKind.Shield||kind==CombatStatusKind.ComboShielded||
                kind==CombatStatusKind.ComboRally;
            Assert.That(CombatHudIconArt.Accent(kind),Is.EqualTo(buff
                ?CombatHudIconArt.BuffAccent:CombatHudIconArt.DebuffAccent),kind.ToString());
        }
        var debuffs=new[]{CombatEffectIconKind.Ice,CombatEffectIconKind.DefenseDown,
            CombatEffectIconKind.ResistanceDown,CombatEffectIconKind.AttackDown,CombatEffectIconKind.HealingDown,
            CombatEffectIconKind.DeathMark,CombatEffectIconKind.Hunted,CombatEffectIconKind.ArmorBroken,
            CombatEffectIconKind.DeepFang,CombatEffectIconKind.Capture,CombatEffectIconKind.AttackSpeedDown,
            CombatEffectIconKind.MoveSpeedDown,CombatEffectIconKind.Stun,CombatEffectIconKind.ChorusMark,
            CombatEffectIconKind.SporeMark,CombatEffectIconKind.Taunted,CombatEffectIconKind.DamageAmplified};
        var buffs=new[]{CombatEffectIconKind.Shield,CombatEffectIconKind.AttackUp,CombatEffectIconKind.Haste,
            CombatEffectIconKind.Royal,CombatEffectIconKind.ToughScales,CombatEffectIconKind.DamageReduction,
            CombatEffectIconKind.CombinedCast,CombatEffectIconKind.ReturnMark,CombatEffectIconKind.TranscendenceGene,
            CombatEffectIconKind.Transcended,CombatEffectIconKind.ApexPredator,CombatEffectIconKind.AttackSpeedUp,
            CombatEffectIconKind.MoveSpeedUp,CombatEffectIconKind.DefenseUp,CombatEffectIconKind.ResistanceUp,
            CombatEffectIconKind.SkillPowerUp,CombatEffectIconKind.DebuffImmunity,CombatEffectIconKind.ControlImmunity,
            CombatEffectIconKind.Untargetable,CombatEffectIconKind.Invulnerable,CombatEffectIconKind.HealthUp,
            CombatEffectIconKind.Reflection,CombatEffectIconKind.DamageLink};
        Assert.That(buffs.Concat(debuffs),Is.EquivalentTo(System.Enum.GetValues(typeof(CombatEffectIconKind))));
        foreach(var kind in debuffs)
            Assert.That(CombatHudIconArt.Accent(kind),Is.EqualTo(CombatHudIconArt.DebuffAccent),kind.ToString());
        foreach(var kind in buffs)
            Assert.That(CombatHudIconArt.Accent(kind),Is.EqualTo(CombatHudIconArt.BuffAccent),kind.ToString());
        Assert.That(CombatHudIconArt.BuffAccent.g,Is.GreaterThan(CombatHudIconArt.BuffAccent.r));
        Assert.That(CombatHudIconArt.DebuffAccent.r,Is.GreaterThan(CombatHudIconArt.DebuffAccent.g));
        Assert.That(CombatHudIconArt.Accent(CombatStatusKind.Root),
            Is.EqualTo(CombatHudIconArt.Accent(CombatEffectIconKind.AttackSpeedDown)));
        Assert.That(CombatHudIconArt.Accent(CombatStatusKind.Silence),
            Is.EqualTo(CombatHudIconArt.Accent(CombatStatusKind.Root)));
        Assert.That(CombatHudIconArt.Accent(CombatStatusKind.ComboRally),
            Is.EqualTo(CombatHudIconArt.Accent(CombatEffectIconKind.AttackSpeedUp)));
    }

    [Test]
    public void EveryStatusAndTimedEffectUsesImportedGameArtwork()
    {
        foreach(CombatStatusKind kind in System.Enum.GetValues(typeof(CombatStatusKind)))
            AssertArtwork(CombatHudIconArt.Get(kind),kind.ToString());
        foreach(CombatEffectIconKind kind in System.Enum.GetValues(typeof(CombatEffectIconKind)))
            AssertArtwork(CombatHudIconArt.Get(kind),kind.ToString());
        Assert.That(typeof(MonsterAutoBattlePrototype).GetMethod("SetCombatHudIconTooltip",Private),
            Is.Null,"Combat status icons must not open effect-name/description text.");
    }

    private static void AssertArtwork(Texture2D icon,string name)
    {
        Assert.That(icon,Is.Not.Null,name);
        // Unity can recreate the managed wrapper after an asset reimport.
        Assert.That(icon.GetInstanceID(),Is.EqualTo(
            Resources.Load<Texture2D>("UI/CombatStatusIcons/"+name).GetInstanceID()),name);
        Assert.That(icon.width,Is.EqualTo(64),name);
        Assert.That(icon.height,Is.EqualTo(64),name);
        Assert.That(icon.filterMode,Is.EqualTo(FilterMode.Bilinear),name);
        Assert.That(icon.wrapMode,Is.EqualTo(TextureWrapMode.Clamp),name);
        Assert.That(icon.mipmapCount,Is.EqualTo(1),name);
    }

    [TestCase("침묵")]
    [TestCase("공속 -30%")]
    [TestCase("정화")]
    [TestCase("BUFF STOLEN")]
    [TestCase("SHIELD BURST +50%")]
    [TestCase("특성 발동")]
    [TestCase("123")]
    public void EffectNoticesNeverCreateFloatingText(string label)
    {
        var host=new GameObject("Icon-only Feedback Test");host.SetActive(false);
        var model=new GameObject("Feedback Target");
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var bt=typeof(MonsterAutoBattlePrototype);
            var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
            object unit=System.Activator.CreateInstance(ut,true);
            ut.GetField("model").SetValue(unit,model);
            var floats=(IList)bt.GetField("combatFloatTexts",Private).GetValue(battle);
            var add=bt.GetMethod("AddCombatFloatText",Private);
            add.Invoke(battle,new object[]{unit,label,Color.white,true,1f,false,false,false});
            Assert.That(floats.Count,Is.Zero);
            add.Invoke(battle,new object[]{unit,"123!",Color.white,true,1f,true,false,false});
            add.Invoke(battle,new object[]{unit,"+45",Color.green,false,1f,true,false,false});
            Assert.That(floats.Count,Is.EqualTo(2),"Damage and healing numbers remain visible.");
        }
        finally { Object.DestroyImmediate(model);Object.DestroyImmediate(host); }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(12)]
    [TestCase(13)]
    [TestCase(32)]
    public void EveryIconStaysAboveHealthAndWrapsWithoutOverlap(int count)
    {
        Rect bar=new Rect(180f,300f,52f,8f);
        var icons=Enumerable.Range(0,count)
            .Select(index=>MonsterAutoBattlePrototype.CombatHudIconRect(bar,index,count)).ToArray();
        for(int i=0;i<count;i++)
        {
            Assert.That(icons[i].yMax,Is.LessThanOrEqualTo(bar.yMin-6f));
            Assert.That(icons[i].size,Is.EqualTo(new Vector2(16f,16f)));
            for(int j=0;j<i;j++)Assert.That(icons[i].Overlaps(icons[j]),Is.False);
        }
        for(int row=0;row<(count+5)/6;row++)
        {
            int first=row*6,last=Mathf.Min(first+5,count-1);
            Assert.That((icons[first].xMin+icons[last].xMax)*0.5f,
                Is.EqualTo(bar.center.x).Within(0.001f));
            if(row>0)Assert.That(icons[first].yMax,Is.LessThan(icons[first-1].yMin));
        }
    }

    [TestCase(0,false)] [TestCase(0,true)]
    [TestCase(1,false)] [TestCase(1,true)]
    [TestCase(7,false)] [TestCase(7,true)]
    [TestCase(13,false)] [TestCase(13,true)]
    public void AnimatedFearIndicatorStaysSeparateFromStatusRowsAndHoveredStars(int count,bool hovered)
    {
        var layout=typeof(MonsterAutoBattlePrototype).GetMethod("FearIndicatorRect",BindingFlags.Static|BindingFlags.NonPublic);
        Rect bar=new Rect(180f,300f,52f,8f);
        Rect last=count>0?MonsterAutoBattlePrototype.CombatHudIconRect(bar,count-1,count):bar;
        Rect stars=MonsterAutoBattlePrototype.RankStarsRect(bar);
        if(count>0)stars.y=last.yMin-22f;
        Rect first=(Rect)layout.Invoke(null,new object[]{bar,count,hovered,0f,0});
        bool moved=false;
        for(int frame=0;frame<60;frame++)
        {
            Rect actual=(Rect)layout.Invoke(null,new object[]{bar,count,hovered,frame/60f,0});
            Assert.That(actual.size,Is.EqualTo(new Vector2(24f,24f)));
            Assert.That(actual.yMax,Is.LessThan((hovered?stars:last).yMin-4f));
            moved|=Vector2.Distance(first.center,actual.center)>1f;
        }
        Assert.That(moved,Is.True,"The separate fear icon must bob and tremble while the status lasts.");
    }

    [TestCase(FrogStolenBuffKind.AttackDamage,1f,CombatEffectIconKind.AttackUp)]
    [TestCase(FrogStolenBuffKind.AttackDamage,-1f,CombatEffectIconKind.AttackDown)]
    [TestCase(FrogStolenBuffKind.Defense,1f,CombatEffectIconKind.DefenseUp)]
    [TestCase(FrogStolenBuffKind.Defense,-1f,CombatEffectIconKind.DefenseDown)]
    [TestCase(FrogStolenBuffKind.MoveSpeed,1f,CombatEffectIconKind.MoveSpeedUp)]
    [TestCase(FrogStolenBuffKind.MoveSpeed,-1f,CombatEffectIconKind.MoveSpeedDown)]
    [TestCase(FrogStolenBuffKind.AttackSpeed,1f,CombatEffectIconKind.AttackSpeedUp)]
    [TestCase(FrogStolenBuffKind.AttackSpeed,-1f,CombatEffectIconKind.AttackSpeedDown)]
    public void StolenBuffIconsDistinguishTheAffectedStatAndDirection(
        FrogStolenBuffKind kind,float magnitude,CombatEffectIconKind expected)
    {
        var resolve=typeof(MonsterAutoBattlePrototype).GetMethod("FrogStolenBuffIcon",
            BindingFlags.Static|BindingFlags.NonPublic);
        Assert.That(resolve.Invoke(null,new object[]{kind,magnitude}),Is.EqualTo(expected));
    }

    [TestCase("frogSlowTimer","MoveSpeedDown")]
    [TestCase("giraffeStunTimer","Stun")]
    [TestCase("rank3SkillLockTimer","Silence")]
    [TestCase("griffinRootTimer","Root")]
    [TestCase("crocodileImmobilizedTimer","Root")]
    [TestCase("rank3AttackBonusTimer","AttackUp")]
    [TestCase("rank3AttackSpeedBonusTimer","AttackSpeedUp")]
    [TestCase("rank3MoveSpeedBonusTimer","MoveSpeedUp")]
    [TestCase("rank3DefenseBonusTimer","DefenseUp")]
    [TestCase("rank3ActionSlowTimer","AttackSpeedDown")]
    [TestCase("rank3ForceImmuneTimer","ControlImmunity")]
    [TestCase("itemOpeningImmunityTimer","DebuffImmunity")]
    [TestCase("ninjaUntargetableTimer","Untargetable")]
    [TestCase("augmentReturnInvulnerabilityTimer","Invulnerable")]
    [TestCase("tyrannoResistanceShredTimer","ResistanceDown")]
    [TestCase("corruptStatusResistanceReductionTimer","ResistanceDown")]
    [TestCase("corruptIncomingDamageAmpTimer","DamageAmplified")]
    [TestCase("mushroomSporeMarkTimer","SporeMark")]
    [TestCase("itemBurnTimer","Burning")]
    [TestCase("solarBurnTimer","Burning")]
    [TestCase("guardTimer","DamageReduction")]
    public void FamilyAndItemTimersShowIconsAndRemoveThemAtExpiry(string field,string expected)
    {
        var host=new GameObject("Timer HUD Test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var bt=typeof(MonsterAutoBattlePrototype);
            var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
            object unit=System.Activator.CreateInstance(ut,true);
            var timer=ut.GetField(field);
            var collect=bt.GetMethod("CollectCombatHudIcons",Private);
            var icons=(IList)bt.GetField("combatHudIcons",Private).GetValue(battle);
            System.Func<object,string> name=entry=>
                (entry.GetType().GetField("status").GetValue(entry)??
                 entry.GetType().GetField("effect").GetValue(entry)).ToString();
            timer.SetValue(unit,2f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(name),Does.Contain(expected));
            timer.SetValue(unit,0f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(name),Does.Not.Contain(expected));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ActiveCombatKindsAppearPermanentMarksStayHiddenAndEquivalentSourcesShareOneSlot()
    {
        var host=new GameObject("All Status HUD Test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var bt=typeof(MonsterAutoBattlePrototype);
            var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
            object unit=System.Activator.CreateInstance(ut,true);
            var statuses=(CombatStatusState)ut.GetField("combatStatuses").GetValue(unit);
            var effects=(CombatEffectIconState)ut.GetField("combatEffectIcons").GetValue(unit);
            var collect=bt.GetMethod("CollectCombatHudIcons",Private);
            var icons=(IList)bt.GetField("combatHudIcons",Private).GetValue(battle);
            foreach(CombatStatusKind kind in System.Enum.GetValues(typeof(CombatStatusKind)))
            {
                statuses.Clear();
                if(kind==CombatStatusKind.Shield)statuses.ApplyShield(2f,30f);
                else statuses.ApplyTimed(kind,2f);
                collect.Invoke(battle,new[]{unit});
                if(kind==CombatStatusKind.Fear)
                    Assert.That(icons.Count,Is.Zero,"Fear has its own animated overhead indicator.");
                else Assert.That(icons.Count,Is.GreaterThan(0),kind.ToString());
                statuses.Tick(3f);
                collect.Invoke(battle,new[]{unit});
                Assert.That(icons.Count,Is.Zero,kind.ToString());
            }
            foreach(CombatEffectIconKind kind in System.Enum.GetValues(typeof(CombatEffectIconKind)))
            {
                effects.Show(kind,2f);
                collect.Invoke(battle,new[]{unit});
                bool permanent=kind==CombatEffectIconKind.CombinedCast||kind==CombatEffectIconKind.ReturnMark||
                    kind==CombatEffectIconKind.TranscendenceGene||kind==CombatEffectIconKind.Transcended;
                Assert.That(icons.Count,Is.EqualTo(permanent?0:1),kind.ToString());
                effects.Tick(3f);
                collect.Invoke(battle,new[]{unit});
                Assert.That(icons.Count,Is.Zero,kind.ToString());
            }
            statuses.ApplyTimed(CombatStatusKind.ComboBurning,2f);
            statuses.ApplyTimed(CombatStatusKind.Burning,2f);
            statuses.ApplyShield(2f,30f);
            statuses.ApplyTimed(CombatStatusKind.ComboShielded,2f);
            effects.Show(CombatEffectIconKind.Shield,2f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.EqualTo(2),"One burn and one shield, irrespective of sources.");
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase("crocodileToughHideTimer","ToughScales",CombatEffectIconKind.DamageReduction)]
    [TestCase("bearResolveTimer","AttackUp",CombatEffectIconKind.AttackSpeedUp)]
    [TestCase("bearResolveTimer","AttackUp",CombatEffectIconKind.DefenseUp)]
    [TestCase("bearResolveTimer","AttackUp",CombatEffectIconKind.DamageReduction)]
    [TestCase("kingKongSlowTimer","Ice",CombatEffectIconKind.MoveSpeedDown)]
    [TestCase("raptorSlowTimer","Ice",CombatEffectIconKind.AttackSpeedDown)]
    [TestCase("mammothSlowTimer","Ice",CombatEffectIconKind.MoveSpeedDown)]
    [TestCase("frogRoyalAttackSpeedTimer","Royal",CombatEffectIconKind.ResistanceUp)]
    [TestCase("HallucinatoryPollen","HallucinatoryPollen",CombatEffectIconKind.AttackSpeedDown)]
    public void CompoundEffectsUseOneIconWithoutHidingIndependentEffects(
        string field,string representative,CombatEffectIconKind independent)
    {
        var host=new GameObject("Compact Compound HUD Test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var bt=typeof(MonsterAutoBattlePrototype);
            var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
            object unit=System.Activator.CreateInstance(ut,true);
            var statuses=(CombatStatusState)ut.GetField("combatStatuses").GetValue(unit);
            var effects=(CombatEffectIconState)ut.GetField("combatEffectIcons").GetValue(unit);
            var collect=bt.GetMethod("CollectCombatHudIcons",Private);
            var icons=(IList)bt.GetField("combatHudIcons",Private).GetValue(battle);
            if(field=="HallucinatoryPollen")statuses.ApplyTimed(CombatStatusKind.HallucinatoryPollen,2f);
            else ut.GetField(field).SetValue(unit,2f);
            if(field=="bearResolveTimer")
            {
                ((IList)ut.GetField("activeTraitIds").GetValue(unit)).Add(BearCombatRules.IronKumaTraitId);
                ut.GetField("bearFrenzyStacks").SetValue(unit,3);
                ut.GetField("bearMountainColossusActive").SetValue(unit,true);
            }
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),Is.EquivalentTo(new[]{representative}));
            if(field=="bearResolveTimer")
                Assert.That(icons[0].GetType().GetField("stacks").GetValue(icons[0]),Is.EqualTo(3));
            effects.Show(independent,3f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),
                Is.EquivalentTo(new[]{representative,independent.ToString()}));
            if(field=="HallucinatoryPollen")statuses.Tick(2f);
            else
            {
                Assert.That(ut.GetField(field).GetValue(unit),Is.EqualTo(2f),"Drawing must not change gameplay timers.");
                ut.GetField(field).SetValue(unit,0f);
            }
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),Is.EquivalentTo(new[]{independent.ToString()}));
            effects.Tick(3f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.Zero);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase("itemTemporary")]
    [TestCase("itemPostSkill")]
    [TestCase("itemTwilight")]
    public void PairedItemStatsUseOneIconAndResistanceOnlyBuffsRemainVisible(string prefix)
    {
        var host=new GameObject("Paired Item HUD Test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var bt=typeof(MonsterAutoBattlePrototype);
            var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
            object unit=System.Activator.CreateInstance(ut,true);
            var collect=bt.GetMethod("CollectCombatHudIcons",Private);
            var icons=(IList)bt.GetField("combatHudIcons",Private).GetValue(battle);
            ut.GetField(prefix+"StatTimer").SetValue(unit,2f);
            ut.GetField(prefix+"DefenseBonus").SetValue(unit,10f);
            ut.GetField(prefix+"ResistanceBonus").SetValue(unit,10f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),Is.EquivalentTo(new[]{"DefenseUp"}));
            // Another source granting resistance only must not be swallowed by the paired buff.
            string other=prefix=="itemTemporary"?"itemTwilight":"itemTemporary";
            ut.GetField(other+"StatTimer").SetValue(unit,2f);
            ut.GetField(other+"ResistanceBonus").SetValue(unit,10f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),Is.EquivalentTo(new[]{"DefenseUp","ResistanceUp"}));
            ut.GetField(other+"StatTimer").SetValue(unit,0f);
            ut.GetField(prefix+"DefenseBonus").SetValue(unit,0f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),Is.EquivalentTo(new[]{"ResistanceUp"}));
            ut.GetField(prefix+"StatTimer").SetValue(unit,0f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.Zero);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void PermanentGrowthAndItemStatsStayOffHudButDetailsAndTemporaryRoyalBuffRemain()
    {
        var host=new GameObject("Permanent HUD Filter Test");host.SetActive(false);
        try
        {
            var battle=host.AddComponent<MonsterAutoBattlePrototype>();
            var bt=typeof(MonsterAutoBattlePrototype);
            var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
            object unit=System.Activator.CreateInstance(ut,true);
            var collect=bt.GetMethod("CollectCombatHudIcons",Private);
            var icons=(IList)bt.GetField("combatHudIcons",Private).GetValue(battle);
            var effects=(CombatEffectIconState)ut.GetField("combatEffectIcons").GetValue(unit);
            var run=new ExpeditionRunSave();
            var state=new ExpeditionMonsterStateSave{instanceId="hud_permanent",transcended=true};
            state.appliedSpecialItemIds.Add("special_combined_cast_core");
            state.appliedSpecialItemIds.Add("special_return_mark");
            run.monsters.Add(state);
            bt.GetField("expeditionRun",Private).SetValue(battle,run);
            ut.GetField("stableId").SetValue(unit,state.instanceId);
            foreach(string field in new[]{"itemAttackDamageRamp","itemPermanentAttackSpeedBonus",
                "itemDefenseRamp","itemResistanceRamp","itemSkillPowerRamp","itemDamageReduction"})
                ut.GetField(field).SetValue(unit,0.25f);
            ut.GetField("itemCrownEmpowered").SetValue(unit,true);
            ut.GetField("regionGuardian").SetValue(unit,true);
            effects.Show(CombatEffectIconKind.Royal);
            foreach(var kind in new[]{CombatEffectIconKind.CombinedCast,CombatEffectIconKind.ReturnMark,
                CombatEffectIconKind.TranscendenceGene,CombatEffectIconKind.Transcended})effects.Show(kind);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.Zero);
            Assert.That(effects.Has(CombatEffectIconKind.Royal),Is.True,"Display filtering must not erase effect state.");
            string details=(string)bt.GetMethod("PermanentMonsterMarkText",Private).Invoke(battle,
                new object[]{state,state.instanceId});
            Assert.That(details.Split(new[]{" / "},System.StringSplitOptions.None).Length,Is.EqualTo(3));
            ut.GetField("frogRoyalAttackSpeedTimer").SetValue(unit,2f);
            ut.GetField("rank3AttackSpeedBonusTimer").SetValue(unit,3f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),Is.EquivalentTo(new[]{"Royal","AttackSpeedUp"}));
            ut.GetField("frogRoyalAttackSpeedTimer").SetValue(unit,0f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Cast<object>().Select(IconName),Is.EquivalentTo(new[]{"AttackSpeedUp"}));
            ut.GetField("rank3AttackSpeedBonusTimer").SetValue(unit,0f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.Zero);
            Assert.That(ut.GetField("itemPermanentAttackSpeedBonus").GetValue(unit),Is.EqualTo(0.25f));
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static string IconName(object entry)
    {
        return (entry.GetType().GetField("status").GetValue(entry)??
            entry.GetType().GetField("effect").GetValue(entry)).ToString();
    }

    [TestCase(0)]
    [TestCase(1)]
    public void SharedListContainsBuffsDebuffsAndDropsExpiredIcons(int side)
    {
        GameObject host=new GameObject("Unified Combat Icons");host.SetActive(false);
        var battle=host.AddComponent<MonsterAutoBattlePrototype>();
        var bt=typeof(MonsterAutoBattlePrototype);
        var ut=bt.GetNestedType("Unit",BindingFlags.NonPublic);
        object unit=System.Activator.CreateInstance(ut,true);
        var team=ut.GetField("team");team.SetValue(unit,System.Enum.ToObject(team.FieldType,side));
        var statuses=(CombatStatusState)ut.GetField("combatStatuses").GetValue(unit);
        var effects=(CombatEffectIconState)ut.GetField("combatEffectIcons").GetValue(unit);
        var collect=bt.GetMethod("CollectCombatHudIcons",Private);
        var icons=(IList)bt.GetField("combatHudIcons",Private).GetValue(battle);
        try
        {
            statuses.ApplyTimed(CombatStatusKind.Root,1.2f,null,0.3f);
            statuses.ApplyTimed(CombatStatusKind.Silence,2.7f);
            statuses.ApplyStacks(CombatStatusKind.Poison,2f,3,5);
            statuses.ApplyTimed(CombatStatusKind.Burning,2f);
            statuses.ApplyShield(2f,30f);
            statuses.ApplyTimed(CombatStatusKind.Freeze,1f);
            statuses.ApplyTimed(CombatStatusKind.ArmorBroken,2f,null,0.5f);
            effects.Show(CombatEffectIconKind.AttackUp,2f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.EqualTo(9));
            var rows=icons.Cast<object>().ToArray();
            System.Func<object,string,object> get=(row,name)=>row.GetType().GetField(name).GetValue(row);
            Assert.That(rows.Count(row=>Equals(get(row,"status"),CombatStatusKind.Root)),Is.EqualTo(1));
            Assert.That(rows.Count(row=>Equals(get(row,"status"),CombatStatusKind.Silence)),Is.EqualTo(1));
            Assert.That(rows.Count(row=>get(row,"status")==null&&
                Equals(get(row,"effect"),CombatEffectIconKind.AttackSpeedDown)),Is.EqualTo(1));
            Assert.That(get(rows.Single(row=>Equals(get(row,"status"),CombatStatusKind.Poison)),"stacks"),
                Is.EqualTo(3));
            statuses.Remove(CombatStatusKind.Root);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.EqualTo(7),"Root and attack-speed down leave; silence stays above HP.");
            statuses.Clear();
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.EqualTo(1),"Independent attack buff must remain.");
            effects.Tick(2f);
            collect.Invoke(battle,new[]{unit});
            Assert.That(icons.Count,Is.Zero);
        }
        finally { Object.DestroyImmediate(host); }
    }
}
