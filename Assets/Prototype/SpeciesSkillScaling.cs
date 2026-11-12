using UnityEngine;

// Species-level conversion. Family skill ratios remain shared across all evolutions.
public static class SpeciesSkillScaling
{
    public static bool UsesAttack(MonsterGrowthMasterRow row)
    {
        return row==null||string.IsNullOrEmpty(row.skillScalingStat)||row.skillScalingStat=="Attack";
    }

    public static float Power(MonsterGrowthMasterRow row,float attack,float maxHealth,float defense)
    {
        if(UsesAttack(row))return Mathf.Max(0f,attack);
        float stat=row.skillScalingStat=="MaxHealth"?maxHealth:defense;
        return Mathf.Max(0f,stat)*Mathf.Max(0f,row.skillScalingRatio);
    }

    // A rampage action retains exactly one ordinary attack, plus 190% skill power.
    public static float RagingBasicShare(float attack,int strike)
    {
        return Mathf.Max(0f,attack)*(strike==0?.7f:strike==1?.3f:0f);
    }

    public static float RagingSkillShare(float power,int strike)
    {
        return Mathf.Max(0f,power)*(strike==0?0f:strike==1?.4f:1.5f);
    }

    public static string Summary(MonsterGrowthMasterRow row,float attack,float maxHealth,float defense)
    {
        if(row==null||row.scalingVersion<=0)return "";
        if(GameLocalization.IsEnglish)
            return "Role: "+GameLocalization.Text(row.growthRole)+" | Skill power = "+
                (row.skillScalingStat=="MaxHealth"?"max HP":row.skillScalingStat=="Defense"?"defense":"attack damage")+
                " x "+((UsesAttack(row)?1f:row.skillScalingRatio)*100f).ToString("0.##")+"% ("+
                Power(row,attack,maxHealth,defense).ToString("0.#")+"). Ordinary attacks retain 100% attack damage.";
        string stat=row.skillScalingStat=="MaxHealth"?"최대 체력":row.skillScalingStat=="Defense"?"방어력":"공격력";
        float ratio=UsesAttack(row)?1f:row.skillScalingRatio;
        return "역할: "+row.growthRole+" · 주계수: "+stat+"\n스킬 위력 = "+stat+" "+
            (ratio*100f).ToString("0.##")+"% (현재 "+Power(row,attack,maxHealth,defense).ToString("0.#")+
            "). 스킬 설명의 위력 배율에 적용됩니다. 일반 평타는 공격력 100%.";
    }
}
