using NUnit.Framework;
using UnityEngine;

public sealed class MeadowAmbientParticlesTests
{
    [Test]
    public void AmbientAndRepeatedImpactsStayBoundedAndDoNotConsumeBattleRandomness()
    {
        var host=new GameObject("Meadow Test");
        try
        {
            var camera=host.AddComponent<Camera>();
            string randomBefore=JsonUtility.ToJson(Random.state);
            var effect=MeadowAmbientParticles.Create(host.transform,camera,new Bounds(Vector3.zero,new Vector3(20f,.1f,12f)));
            var reference=host.AddComponent<SpriteRenderer>();reference.sortingOrder=10;
            for(int i=0;i<20;i++)effect.Impact(Vector3.zero,4f,reference);
            Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(randomBefore));
            var renderers=effect.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(renderers.Length,Is.EqualTo(58));
            var quadrants=new System.Collections.Generic.HashSet<int>();
            foreach(var renderer in renderers)if(renderer.name=="Drifting Leaf")
            {
                Vector3 point=camera.WorldToViewportPoint(renderer.transform.position);
                quadrants.Add((point.x<.5f?0:1)+(point.y<.5f?0:2));
                Assert.That(Quaternion.Angle(renderer.transform.rotation,camera.transform.rotation),Is.LessThan(.01f));
                Assert.That(renderer.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            }
            Assert.That(quadrants.Count,Is.EqualTo(4),"Ambient leaves must cover the whole view, not just the arena.");
            foreach(var renderer in renderers)
            {
                Assert.That(renderer.enabled,Is.True);
                Assert.That(renderer.sortingOrder,Is.EqualTo(reference.sortingOrder));
            }
            effect.Footstep(Vector3.zero,Vector3.right,.5f,reference);
            foreach(var renderer in renderers)Assert.That(renderer.name,Is.Not.EqualTo("Footstep Dust"),"Walking must preserve a full impact burst.");
            effect.ClearImpacts();
            foreach(var renderer in renderers)
                Assert.That(renderer.enabled,Is.EqualTo(renderer.name!="Impact Grass"));
            effect.Footstep(Vector3.zero,Vector3.right,.5f,reference);
            int dustCount=0;
            foreach(var renderer in renderers)if(renderer.name=="Footstep Dust"&&renderer.enabled)dustCount++;
            Assert.That(dustCount,Is.EqualTo(2));
            for(int i=0;i<20;i++)effect.Impact(Vector3.zero,4f,reference);
            foreach(var renderer in renderers)Assert.That(renderer.name,Is.Not.EqualTo("Footstep Dust"),"Reused impact slots must restore the leaf sprite.");
            Assert.That(effect.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(58));
            Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(randomBefore));
            effect.enabled=false;
            // Non-ExecuteAlways lifecycle callbacks need an explicit call in EditMode.
            typeof(MeadowAmbientParticles).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(effect,null);
            effect.Impact(Vector3.zero,4f,reference);
            foreach(var renderer in renderers)Assert.That(renderer.enabled,Is.False);
        }
        finally
        {
            foreach(var effect in host.GetComponentsInChildren<MeadowAmbientParticles>())
                typeof(MeadowAmbientParticles).GetMethod("OnDestroy",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(effect,null);
            Object.DestroyImmediate(host);
        }
    }
}
