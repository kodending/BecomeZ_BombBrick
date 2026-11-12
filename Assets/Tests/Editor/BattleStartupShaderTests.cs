using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BattleStartupShaderTests
{
    [TestCase("Particles/Standard Unlit")]
    [TestCase("BecomeZ/Effects/Bear Roar Spirit Fade")]
    public void RuntimeOnlyBattleShadersAreRetainedInPlayerBuilds(string name)
    {
        var shader = Shader.Find(name);
        Assert.That(shader, Is.Not.Null, name);
        var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").Single();
        var included = new SerializedObject(graphics).FindProperty("m_AlwaysIncludedShaders");
        Assert.That(Enumerable.Range(0, included.arraySize)
            .Select(i => included.GetArrayElementAtIndex(i).objectReferenceValue),
            Does.Contain(shader), "Shader.Find-only dependencies must survive player stripping: " + name);
    }
}
