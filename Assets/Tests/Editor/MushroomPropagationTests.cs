using System.Collections;
using NUnit.Framework;

public sealed partial class MonkeyArcTests
{
    private IList Propagations=>(IList)typeof(MonsterAutoBattlePrototype).GetField("mushroomPropagations",Private).GetValue(battle);

    [TestCase(MushroomRank3Rules.SporeResonance)]
    [TestCase(MushroomRank3Rules.ParasiticCrown)]
    [TestCase(MushroomRank3Rules.MycokingDescent)]
    public void DeathBloomReplacesSavedRankFourPropagationWithoutChangingSelectedRankThree(string trait)
    {
        GasReady();Trait(trait);Set(source,"augmentTranscended",true);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        Assert.That(ReaperCall("BeginMushroomPropagation",source,1f,1f),Is.Null);
        Assert.That(((IList)Get(source,"activeTraitIds")).Contains(trait),Is.True);
        Assert.That(Propagations.Count,Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RankFourOrdinaryGasAndEchoKeepTheirDamageWithoutArmingMigration(bool echo)
    {
        GasReady();Trait(MushroomRank3Rules.SporeResonance);Set(source,"augmentTranscended",true);
        typeof(MonsterAutoBattlePrototype).GetField("mlEpisodeActive",Private).SetValue(battle,true);
        GasCreate(echo:echo);GasTick(.5f);
        Assert.That(Hp(target),Is.EqualTo(4980f));Assert.That(Propagations.Count,Is.Zero);
        MoltDamage(source,target,99999f);ReaperCall("UpdateMushroomPropagations",1f);
        Assert.That(Propagations.Count,Is.Zero);
        Assert.That(GasZones.Count,Is.EqualTo(1),"Ordinary gas remains; no migrated copy is created.");
    }
}
