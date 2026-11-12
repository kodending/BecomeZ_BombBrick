using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

public sealed class AutoBattleTelemetryRecorderTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private AutoBattleTelemetryRecorder recorder;
    private string directory,expectedPath,sessionId;

    [SetUp]
    public void SetUp()
    {
        directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Temp","TelemetryStepTests",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        recorder=new AutoBattleTelemetryRecorder();
        typeof(AutoBattleTelemetryRecorder).GetField("<FilePath>k__BackingField",Private)
            .SetValue(recorder,Path.Combine(directory,"actual.jsonl"));
        expectedPath=Path.Combine(directory,"expected.jsonl");
        sessionId=(string)typeof(AutoBattleTelemetryRecorder).GetField("sessionId",Private).GetValue(recorder);
    }

    [TearDown]
    public void TearDown()
    {
        recorder?.Dispose();
        if(directory==null||!Directory.Exists(directory))return;
        foreach(string file in Directory.GetFiles(directory))File.Delete(file);
        Directory.Delete(directory);
    }

    [Test]
    public void RecordStepMatchesOriginalJsonBytesForEscapesNonFiniteValuesAndMixedEvents()
    {
        const string team="P\\\"layer",skill="skill\\\"id",action="cast \\\"now";
        recorder.RecordStep(float.NaN,17,team,skill,float.PositiveInfinity,float.NegativeInfinity,
            1.234567f,-2.345678f,.875f,-1,.5f,.25f,.75f,2,3,action);
        recorder.RecordDamage(1f,2,3,4f,.5f);
        recorder.RecordStep(-.25f,18,null,"",0,0,0,0,1,2,0,0,0,0,0,null);
        Assert.That(File.Exists(recorder.FilePath),Is.False);
        recorder.Flush();
        string expected=OriginalStep(float.NaN,17,team,skill,float.PositiveInfinity,float.NegativeInfinity,
            1.234567f,-2.345678f,.875f,-1,.5f,.25f,.75f,2,3,action)+Environment.NewLine+
            "{\"schema\":10,\"event\":\"damage\",\"session\":\""+sessionId+
            "\",\"time\":1,\"source\":2,\"target\":3,\"amount\":4,\"target_hp\":0.5}"+Environment.NewLine+
            OriginalStep(-.25f,18,null,"",0,0,0,0,1,2,0,0,0,0,0,null)+Environment.NewLine;
        File.WriteAllText(expectedPath,expected,Encoding.UTF8);
        CollectionAssert.AreEqual(File.ReadAllBytes(expectedPath),File.ReadAllBytes(recorder.FilePath));
    }

    [Test]
    public void RecordStepKeeps128LineFlushOrderingAndDisposedNoOp()
    {
        recorder.RecordDamage(0,1,2,3,1);
        for(int i=0;i<126;i++)recorder.RecordStep(i,i,"Player","skill",0,0,0,0,1,-1,0,0,0,0,0,"");
        Assert.That(File.Exists(recorder.FilePath),Is.False);
        recorder.RecordStep(126,126,"Player","skill",0,0,0,0,1,-1,0,0,0,0,0,"");
        string[] lines=File.ReadAllLines(recorder.FilePath);
        Assert.That(lines,Has.Length.EqualTo(128));
        Assert.That(lines[0],Does.Contain("\"event\":\"damage\""));
        Assert.That(lines[1],Does.Contain("\"unit\":0,"));
        Assert.That(lines[127],Does.Contain("\"unit\":126,"));
        byte[] flushed=File.ReadAllBytes(recorder.FilePath);
        recorder.RecordSkill(2,5,"mixed_skill",1,4);
        CollectionAssert.AreEqual(flushed,File.ReadAllBytes(recorder.FilePath));
        recorder.Dispose();
        lines=File.ReadAllLines(recorder.FilePath);
        Assert.That(lines,Has.Length.EqualTo(129));Assert.That(lines[128],Does.Contain("\"event\":\"skill\""));
        byte[] disposed=File.ReadAllBytes(recorder.FilePath);
        recorder.RecordStep(0,99,"ignored","ignored",0,0,0,0,1,-1,0,0,0,0,0,"ignored");
        recorder.Flush();recorder.Dispose();
        CollectionAssert.AreEqual(disposed,File.ReadAllBytes(recorder.FilePath));
        Assert.That(((StringBuilder)typeof(AutoBattleTelemetryRecorder).GetField("buffer",Private).GetValue(recorder)).Length,Is.Zero);
    }

    private string OriginalStep(float time,int unitId,string team,string skillId,float x,float z,float velocityX,float velocityZ,
        float hpRatio,int targetId,float targetDistance,float nearestAlly,float nearestEnemy,int alliesNearby,int enemiesNearby,string action)
    {
        return "{\"schema\":"+10+",\"event\":\"step\",\"session\":\""+sessionId+"\",\"time\":"+F(time)+
               ",\"unit\":"+unitId+",\"team\":\""+Escape(team)+"\",\"skillId\":\""+Escape(skillId)+"\",\"x\":"+F(x)+
               ",\"z\":"+F(z)+",\"vx\":"+F(velocityX)+",\"vz\":"+F(velocityZ)+",\"hp\":"+F(hpRatio)+
               ",\"target\":"+targetId+",\"target_distance\":"+F(targetDistance)+",\"nearest_ally\":"+F(nearestAlly)+
               ",\"nearest_enemy\":"+F(nearestEnemy)+",\"allies_nearby\":"+alliesNearby+",\"enemies_nearby\":"+enemiesNearby+
               ",\"action\":\""+Escape(action)+"\"}";
    }

    private static string F(float value)
    {
        if(float.IsNaN(value)||float.IsInfinity(value))value=0f;
        return value.ToString("0.#####",CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)=>string.IsNullOrEmpty(value)?string.Empty:
        value.Replace("\\","\\\\").Replace("\"","\\\"");
}
