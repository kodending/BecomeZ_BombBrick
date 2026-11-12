using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class PixelMonsterRosterSetupTests
{
    [Test]
    public void Motions005To080UseIndependentFrames()
    {
        Assert.DoesNotThrow(PixelMonsterRosterSetup.ValidateAll);
    }

    [Test]
    public void Resources001To080ContainEveryMotion()
    {
        string[] motions =
        {
            "Idle", "Run", "Attack", "SkillAttack",
            "Death", "Capture", "Struggle"
        };
        for (int serial = 1;
             serial <= PixelMonsterRosterSetup.LastSerial;
             serial++)
        {
            string folder =
                PixelMonsterRosterSetup.FindSourceFolder(
                    serial.ToString("000"));
            Assert.That(folder, Is.Not.Empty, serial.ToString("000"));
            foreach (string motion in motions)
                Assert.That(
                    PixelMonsterRosterSetup.FindMotionPath(folder, motion),
                    Is.Not.Empty,
                    serial.ToString("000") + " " + motion);
        }
    }

    [Test]
    public void EightyMonstersUseFourShowcasePages()
    {
        Assert.That(
            MonsterAnimationShowcase.PageCount(
                PixelMonsterRosterSetup.LastSerial),
            Is.EqualTo(4));
    }

    [Test]
    public void CombatQaCoversMonsters001To080InTenBatches()
    {
        MonsterDatabase database=Resources.Load<MonsterDatabase>(
            "MonsterData/MonsterDatabase");
        Assert.That(
            MonsterAutoBattlePrototype.RosterQaPageCount(database),
            Is.EqualTo(10));

        string[] serials=Enumerable.Range(0,10)
            .SelectMany(page=>
                MonsterAutoBattlePrototype.RosterQaBatch(database,page))
            .Select(definition=>definition.serialNumber)
            .ToArray();
        Assert.That(serials,Is.EqualTo(
            Enumerable.Range(1,80).Select(value=>value.ToString("000"))));
    }

    [Test]
    public void CombatImplementationAuditCoversEveryMonster()
    {
        MonsterCombatAuditEntry[] entries=
            MonsterCombatImplementationAudit.Analyze().ToArray();

        Assert.That(entries,Has.Length.EqualTo(80));
        Assert.That(
            entries.Select(entry=>entry.serial),
            Is.EqualTo(
                Enumerable.Range(1,80).Select(value=>value.ToString("000"))));
        Assert.That(entries.All(entry=>entry.animationReady),Is.True);
    }
}
