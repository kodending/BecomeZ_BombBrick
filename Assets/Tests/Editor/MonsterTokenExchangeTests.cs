using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class MonsterTokenExchangeTests
{
    private string directory;
    private PlayerProfileStore store;
    private PlayerProfileSave profile;
    private MonsterDatabase database;
    private OwnedMonsterSave spare;

    [SetUp] public void SetUp()
    {
        directory=Path.Combine(Path.GetTempPath(),"BombBrick.Exchange."+Guid.NewGuid().ToString("N"));
        store=new PlayerProfileStore(directory);
        database=Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        profile=store.LoadOrCreate(database);profile.starterSelectionPending=false;
        spare=store.AcquireMonster(profile,database,"golgaebi");
        while(profile.monsters.Count<=PlayerProfileStore.ExpeditionRosterSize)
            store.AcquireMonster(profile,database,"golgaebi");
        spare.locked=spare.favorite=false;
        profile.expeditionPreset.rosterInstanceIds.RemoveAll(id=>id==spare.instanceId);
        store.Save(profile);
    }

    [TearDown] public void TearDown()
    {
        Assert.That(Path.GetFullPath(directory),Does.StartWith(Path.Combine(Path.GetTempPath(),"BombBrick.Exchange.")));
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }

    [Test] public void ExchangePersistsMonsterRemovalAndTokensExactlyOnce()
    {
        int expected=profile.hunterTokens+PlayerProfileStore.MonsterTokenValue(spare);
        Assert.That(store.ExchangeMonsterForTokens(profile,database,spare.instanceId),Is.True);
        Assert.That(store.ExchangeMonsterForTokens(profile,database,spare.instanceId),Is.False);
        Assert.That(profile.hunterTokens,Is.EqualTo(expected));
        var loaded=store.LoadOrCreate(database);
        Assert.That(loaded.hunterTokens,Is.EqualTo(expected));
        Assert.That(loaded.monsters.Any(m=>m.instanceId==spare.instanceId),Is.False);
    }

    [TestCase("locked")] [TestCase("favorite")] [TestCase("memorial")]
    [TestCase("roster")] [TestCase("active")] [TestCase("minimum")]
    public void ProtectedAndRequiredCompanionsCannotBeExchanged(string reason)
    {
        ExpeditionRunSave run=null;
        if(reason=="locked")spare.locked=true;
        if(reason=="favorite")spare.favorite=true;
        if(reason=="memorial")spare.instanceId=PlayerProfileStore.MemorialChundongiInstanceId;
        if(reason=="roster")profile.expeditionPreset.rosterInstanceIds.Add(spare.instanceId);
        if(reason=="active"){run=new ExpeditionRunSave();run.monsters.Add(new ExpeditionMonsterStateSave{instanceId=spare.instanceId});}
        if(reason=="minimum")while(profile.monsters.Count>8)profile.monsters.Remove(profile.monsters.First(m=>m!=spare));
        int count=profile.monsters.Count,balance=profile.hunterTokens;
        Assert.That(store.ExchangeMonsterForTokens(profile,database,spare.instanceId,run),Is.False);
        Assert.That(profile.monsters.Count,Is.EqualTo(count));Assert.That(profile.hunterTokens,Is.EqualTo(balance));
    }

    [Test] public void SaveFailureKeepsCompanionAndWalletUnchanged()
    {
        Directory.CreateDirectory(store.TemporaryPath);
        int balance=profile.hunterTokens;
        Assert.Throws<UnauthorizedAccessException>(()=>store.ExchangeMonsterForTokens(profile,database,spare.instanceId));
        Assert.That(profile.monsters,Does.Contain(spare));Assert.That(profile.hunterTokens,Is.EqualTo(balance));
    }
}
