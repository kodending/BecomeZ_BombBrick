using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class MonkeyArcTests
{
    [TestCase(false)][TestCase(true)]
    public void ImperialHornLandingContinuesAfterBattleStateCleanupWithoutMoreDamageAndClearsForTheNextWave(bool nextWave)
    {
        BeetleOverturnBoard();
        Scene owner=EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(battle.gameObject,owner);
        SceneManager.MoveGameObjectToScene(Model(source),owner);
        try
        {
            var reference=Model(source).AddComponent<SpriteRenderer>();
            reference.sprite=PixelBeetleOverturn.SourceFrames("jangpung",false)[7];
            BeginImperialHorn();object state=Get(source,"beetleOverturn");
            var effect=PixelBeetleOverturn.Play(new System.Collections.Generic.List<Bounds>{
                new Bounds(Model(target).transform.position,new Vector3(4f,0f,3f))},reference,Vector3.right);
            SceneManager.MoveGameObjectToScene(effect.gameObject,owner);
            state.GetType().GetField("effect").SetValue(state,effect);
            AdvanceImperialHorn(BeetleOverturnRules.Landing+.001f);
            Assert.That(Hp(target),Is.EqualTo(4700f).Within(.01f));
            var dust=effect.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Eight drawing ground impact 0");
            Sprite first=dust.sprite;

            MammothCall("PreserveBattleEffects");
            typeof(MonsterAutoBattlePrototype).GetField("battleEnding",Private).SetValue(battle,true);
            MammothCall("ResetFamilyTranscendenceStates");
            Assert.That(Get(source,"beetleOverturn"),Is.Null);
            Assert.That(CombatEffectEndPlayback.IsPreserving(effect),Is.True);
            Assert.That(effect.enabled,Is.False,"Only harmless end playback owns the remaining visual clock.");
            UnityEngine.Object.DestroyImmediate(Model(source));

            var playback=effect.GetComponent<CombatEffectEndPlayback>();
            typeof(CombatEffectEndPlayback).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(playback,new object[]{.45f});
            AdvanceImperialHorn(2f);
            Assert.That(effect!=null&&dust.enabled&&dust.color.a>0f,Is.True,
                "The landing must survive beyond the obsolete 0.4-second cutoff.");
            Assert.That(dust.sprite,Is.Not.SameAs(first));
            Assert.That(Hp(target),Is.EqualTo(4700f).Within(.01f));
            Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
            Assert.That(Get(target,"beetleOverturnedBy"),Is.Null);

            if(nextWave)CombatEffectEndPlayback.Clear(owner);
            else typeof(CombatEffectEndPlayback).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(playback,new object[]{PixelBeetleOverturn.Afterglow});
            Assert.That(effect==null&&dust==null&&playback==null,Is.True);
            Assert.That(Resources.FindObjectsOfTypeAll<CombatEffectEndPlayback>().Any(p=>p!=null&&p.gameObject.scene==owner),Is.False);
            Assert.That(Hp(target),Is.EqualTo(4700f).Within(.01f));
            Assert.That(Get(source,"skillHits"),Is.EqualTo(1));
        }
        finally
        {
            CombatEffectEndPlayback.Clear(owner);
            if(owner.IsValid()&&owner.isLoaded)EditorSceneManager.ClosePreviewScene(owner);
        }
    }
}
