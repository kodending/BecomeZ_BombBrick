using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="BombBrick/Monster Database",fileName="MonsterDatabase")]
public sealed class MonsterDatabase : ScriptableObject
{
    public List<MonsterDefinition> monsters=new List<MonsterDefinition>();
    [NonSerialized] private Dictionary<string,MonsterDefinition> byId;

    public MonsterDefinition Get(string monsterId)
    {
        if(monsterId=="raptor_reference_palette_v2")
            monsterId="velos";
        if(byId==null)
        {
            byId=new Dictionary<string,MonsterDefinition>(StringComparer.Ordinal);
            foreach(MonsterDefinition monster in monsters)
                if(monster!=null&&!string.IsNullOrWhiteSpace(monster.monsterId))
                    byId[monster.monsterId]=monster;
        }
        return monsterId!=null&&byId.TryGetValue(monsterId,out MonsterDefinition value)
            ?value
            :null;
    }
}
