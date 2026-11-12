using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

public sealed class ContactHitCameraTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [TestCase(false)] [TestCase(true)]
    public void DamageCastAndDeathReportsNeverCreateOrExtendAnAuthoredShake(bool ownsCast)
    {
        var host=new GameObject("Contact camera policy test");float speed=Time.timeScale;
        try
        {
            var camera=host.AddComponent<Camera>();
            var director=host.AddComponent<CombatCameraDirector>();
            director.Initialize(camera,false);director.SetShakeStrength(1f);director.BeginBattle();
            if(ownsCast)
            {
                var actor=new GameObject("Tracked caster");actor.transform.SetParent(host.transform,false);
                var renderer=actor.AddComponent<SpriteRenderer>();renderer.sprite=PixelStarProjectile.BeanSprite;
                Assert.That(director.TryBeginAnticipationPan(renderer,true),Is.True);
            }
            Time.timeScale=.5f;Vector3 point=Vector3.forward*10f;
            Vector3 screen=camera.WorldToScreenPoint(point);float fov=camera.fieldOfView;
            ReportOrdinaryCombat(director);
            Assert.That(Read<float>(director,"shakeUntil"),Is.Zero);
            Call(director,"ApplyShakeProjection",Time.unscaledTime);
            Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.LessThan(.001f));

            director.ReportGroundSlam();
            float start=Read<float>(director,"shakeStartedAt"),end=Read<float>(director,"shakeUntil");
            Assert.That(end,Is.GreaterThan(start));
            for(int hit=0;hit<100;hit++)ReportOrdinaryCombat(director);
            Assert.That(Read<float>(director,"shakeStartedAt"),Is.EqualTo(start));
            Assert.That(Read<float>(director,"shakeUntil"),Is.EqualTo(end));
            Assert.That(Read<float>(director,"shakeAmplitudePixels"),Is.EqualTo(10f));
            Call(director,"ApplyShakeProjection",Mathf.Lerp(start,end,.1f));
            Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.GreaterThan(.5f));
            Assert.That(director.AnticipationPanActive,Is.EqualTo(ownsCast));
            Assert.That(camera.fieldOfView,Is.EqualTo(fov));Assert.That(Time.timeScale,Is.EqualTo(.5f));
            director.EndBattle();
            Assert.That(Vector3.Distance(camera.WorldToScreenPoint(point),screen),Is.LessThan(.001f));
        }
        finally{Time.timeScale=speed;Object.DestroyImmediate(host);}
    }

    private static void ReportOrdinaryCombat(CombatCameraDirector director)
    {
        director.ReportDamage("caster",true,Vector3.zero,Vector3.right,25f);
        director.ReportSkill("caster",true,Vector3.zero,Vector3.right);
        director.ReportDeath("caster",true,"enemy",true,Vector3.zero,Vector3.right);
    }
    private static T Read<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    private static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
}
