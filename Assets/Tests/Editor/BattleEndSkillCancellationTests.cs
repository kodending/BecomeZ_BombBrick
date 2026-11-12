using NUnit.Framework;

public sealed partial class MonkeyArcTests
{
    [Test] public void BattleEndStopsManualChargeBeforeItsEffectCanBeReleased()
    {
        var charge=StartMammoth("kogason");
        typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
        MammothCall("AdvanceMammothCharge",source,charge,.95f);
        Assert.That(Get(source,"mammothCharge"),Is.Null);
        Assert.That(Hp(target),Is.EqualTo(5000f));
        Assert.That((float)Get(source,"attackMovementLockTimer"),Is.Zero);
    }

    [Test] public void BattleEndStopsTheRemainingBarrageShots()
    {
        StartCast();Assert.That(cast.MoveNext(),Is.True);
        int shots=Shots().Length;
        typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
        Assert.That(cast.MoveNext(),Is.False);
        Assert.That(Shots().Length,Is.EqualTo(shots));
        Assert.That(Hp(target),Is.EqualTo(5000f));
    }
}
