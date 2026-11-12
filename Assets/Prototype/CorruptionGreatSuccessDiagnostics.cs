using System;
using System.Collections.Generic;
using System.Linq;

public static class CorruptionGreatSuccessDiagnostics
{
    private static readonly Dictionary<string,int> activations=
        new Dictionary<string,int>(StringComparer.Ordinal);

    public static void Reset()
    {
        activations.Clear();
    }

    public static void Record(string effectId)
    {
        if(string.IsNullOrEmpty(effectId))return;
        int count;
        activations.TryGetValue(effectId,out count);
        activations[effectId]=count+1;
    }

    public static int Count(string effectId)
    {
        int count;
        return activations.TryGetValue(effectId,out count)?count:0;
    }

    public static IReadOnlyDictionary<string,int> Snapshot()
    {
        return new Dictionary<string,int>(activations,StringComparer.Ordinal);
    }

    public static string Summary()
    {
        string[] supported=CorruptionGreatSuccessRules.SupportedEffectIds
            .OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        int activated=supported.Count(id=>Count(id)>0);
        string missing=string.Join(",",supported.Where(id=>Count(id)==0));
        return "activated="+activated+"/"+supported.Length+
               "; missing="+(missing.Length==0?"none":missing);
    }
}
