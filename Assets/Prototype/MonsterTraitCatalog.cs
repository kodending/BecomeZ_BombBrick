using System;
using System.Collections.Generic;
using System.Linq;

[Flags]
public enum MonsterCombatTag : long
{
    None=0,
    Melee=1L<<0,
    Ranged=1L<<1,
    Skill=1L<<2,
    DamageSkill=1L<<3,
    SingleTargetSkill=1L<<4,
    MovementSkill=1L<<5,
    StatusOrMarkSkill=1L<<6,
    ElementalSkill=1L<<7,
    Guardian=1L<<8,
    RaptorFamily=1L<<9,
    FrogFamily=1L<<10,
    KingKongFamily=1L<<11,
    JaguarFamily=1L<<12,
    CrocodileFamily=1L<<13,
    GiraffeFamily=1L<<14,
    BearFamily=1L<<15,
    GriffinFamily=1L<<16,
    SpiderFamily=1L<<17,
    ArachneFamily=SpiderFamily,
    NinjaCatFamily=1L<<18,
    TyrannoFamily=1L<<19,
    MonkeyFamily=1L<<20,
    MantisFamily=1L<<21,
    MushroomFamily=1L<<22,
    MammothFamily=1L<<23,
    BeetleFamily=1L<<24,
    SpiritDeerFamily=1L<<25,
    WyvernFamily=1L<<26,
    HaetaeFamily=1L<<27,
    AreaSkill=1L<<28,
    LineSkill=1L<<29,
    SelfCenteredSkill=1L<<30,
    DurationSkill=1L<<31,
    ProjectileSkill=1L<<32,
    SelfBuffSkill=1L<<33,
    SabertoothFamily=1L<<34,
    WizardMouseFamily=1L<<35,
    WolfFamily=1L<<36
}

public sealed class MonsterTraitDefinition
{
    public readonly string id;
    public readonly string displayName;
    public readonly MonsterCombatTag requiredTags;
    public readonly string category;

    public MonsterTraitDefinition(string id,string displayName,
        MonsterCombatTag requiredTags=MonsterCombatTag.None,string category="Rank2Enhancement")
    {
        this.id=id;
        this.displayName=displayName;
        this.requiredTags=requiredTags;
        this.category=category??string.Empty;
    }

    public bool IsRank2=>string.Equals(category,"Rank2Enhancement",StringComparison.OrdinalIgnoreCase);
    public bool IsRank4=>string.Equals(category,"Rank4Transcendence",StringComparison.OrdinalIgnoreCase);
    public bool IsRank3=>!IsRank2&&!IsRank4;
    public bool IsComboBridge=>string.Equals(category,"ComboBridge",StringComparison.OrdinalIgnoreCase);

    public bool IsCompatible(MonsterDefinition definition)
    {
        MonsterCombatTag available=MonsterTraitCatalog.ResolveTags(definition);
        MonsterCombatTag familyMask=MonsterTraitCatalog.FamilyMask;
        MonsterCombatTag requiredFamilies=requiredTags&familyMask;
        MonsterCombatTag requiredCapabilities=requiredTags&~familyMask;
        bool familyCompatible=requiredFamilies==MonsterCombatTag.None||
                              (available&requiredFamilies)!=MonsterCombatTag.None;
        // A family-specific evolution replaces that family's authored skill, so the
        // family match is authoritative. Capability tags describe the replacement;
        // they are not prerequisites the unevolved base skill must already possess.
        return requiredFamilies!=MonsterCombatTag.None
            ?familyCompatible
            :familyCompatible&&(available&requiredCapabilities)==requiredCapabilities;
    }
}

public static class MonsterTraitCatalog
{
    private static GameMasterDatabase cachedDatabase;
    private static IReadOnlyList<MonsterTraitDefinition> cachedTraits;
    private const int ExpectedRank2Count=16;
    private const int ExpectedRank3Count=60;
    private const int ExpectedRank4Count=20;
    public const MonsterCombatTag FamilyMask=MonsterCombatTag.RaptorFamily|
        MonsterCombatTag.FrogFamily|MonsterCombatTag.KingKongFamily|
        MonsterCombatTag.JaguarFamily|MonsterCombatTag.CrocodileFamily|
        MonsterCombatTag.GiraffeFamily|MonsterCombatTag.BearFamily|
        MonsterCombatTag.GriffinFamily|MonsterCombatTag.SpiderFamily|
        MonsterCombatTag.NinjaCatFamily|MonsterCombatTag.TyrannoFamily|
        MonsterCombatTag.MonkeyFamily|MonsterCombatTag.MantisFamily|
        MonsterCombatTag.MushroomFamily|MonsterCombatTag.MammothFamily|
        MonsterCombatTag.BeetleFamily|MonsterCombatTag.SpiritDeerFamily|
        MonsterCombatTag.WyvernFamily|MonsterCombatTag.HaetaeFamily|
        MonsterCombatTag.SabertoothFamily|MonsterCombatTag.WizardMouseFamily|
        MonsterCombatTag.WolfFamily;

    private static readonly List<MonsterTraitDefinition> fallback=new List<MonsterTraitDefinition>
    {
        R2("trait_rank2_vitality","생명 강화"),
        R2("trait_rank2_might","완력 강화"),
        R2("trait_rank2_haste","속행 강화"),
        R2("trait_rank2_armor","철갑 강화"),
        R2("trait_rank2_resistance","항마 강화"),
        R2("trait_rank2_mobility","기동 강화"),
        R2("trait_rank2_technique","기술 강화",MonsterCombatTag.Skill),
        R2("trait_rank2_cycle","순환 가속",MonsterCombatTag.Skill),
        R2("trait_rank2_opening","선제 압박"),
        R2("trait_rank2_finisher","마무리 감각"),
        R2("trait_rank2_pursuit","집요한 추적"),
        R2("trait_rank2_crisis","위기 대응"),
        R2("trait_rank2_suppression","제압 공세"),
        R2("trait_rank2_weakness","약점 포착"),
        R2("trait_rank2_elemental","원소 공명"),
        R2("trait_rank2_guardian","수호 공명"),

        R3("trait_chain_conduction","연쇄 전도","BasicAttackEvolution"),
        R3("trait_burst_reload","장전 공세","BasicAttackEvolution"),
        R3("trait_cross_hunt","교차 사냥","BasicAttackEvolution"),
        R3("trait_overkill_transfer","과잉 전이","BasicAttackEvolution"),
        R3("trait_gravity_knot","중력 결절","BasicAttackEvolution"),

        R3("trait_skill_condense","응축","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.AreaSkill),
        R3("trait_skill_forward_deploy","전방 전개","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.SelfCenteredSkill),
        R3("trait_skill_radial_deploy","방사 전개","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.LineSkill),
        R3("trait_skill_remote_projection","원격 투영","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.MovementSkill),
        R3("trait_skill_branch_path","분기 궤적","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.LineSkill),
        R3("trait_skill_scatter_deploy","산개 전개","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.AreaSkill),
        R3("trait_skill_time_compression","시간 압축","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.DurationSkill),
        R3("trait_skill_overcharge","과충전","SkillEvolution",MonsterCombatTag.Skill),
        R3("trait_skill_distributed_cast","분산 시전","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.SingleTargetSkill),
        R3("trait_skill_instant_manifest","순간 발현","SkillEvolution",MonsterCombatTag.Skill),
        R3("trait_skill_dual_manifest","이중 발현","SkillEvolution",MonsterCombatTag.Skill),
        R3("trait_skill_stance_division","태세 분화","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.SelfBuffSkill),
        R3("trait_skill_effect_spread","효과 전염","SkillEvolution",MonsterCombatTag.Skill|MonsterCombatTag.SelfBuffSkill),

        R3("trait_combo_antigravity","반중력 반동","ComboBridge",MonsterCombatTag.DamageSkill),
        R3("trait_combo_fall_shatter","추락 파쇄","ComboBridge"),
        R3("trait_combo_frost_shackle","서리 족쇄","ComboBridge"),
        R3("trait_combo_venom_mutation","독혈 변이","ComboBridge"),
        R3("trait_combo_chemical_ignition","화학 점화","ComboBridge"),
        R3("trait_combo_open_wound","상처 개방","ComboBridge"),
        R3("trait_combo_morale_veil","사기 장막","ComboBridge"),
        R3("trait_combo_resolve_barrier","결의 방벽","ComboBridge",MonsterCombatTag.Skill)
    };

    private static MonsterTraitDefinition R2(string id,string name,
        MonsterCombatTag tags=MonsterCombatTag.None)
    {return new MonsterTraitDefinition(id,name,tags,"Rank2Enhancement");}

    private static MonsterTraitDefinition R3(string id,string name,string category,
        MonsterCombatTag tags=MonsterCombatTag.None)
    {return new MonsterTraitDefinition(id,name,tags,category);}

    public static IReadOnlyList<MonsterTraitDefinition> All
    {
        get
        {
            GameMasterDatabase database=GameMasterData.Database;
            if(ReferenceEquals(database,cachedDatabase)&&cachedTraits!=null)return cachedTraits;
            if(database==null||database.traits==null)return fallback;
            List<MonsterTraitDefinition> loaded=database.traits
                .Where(row=>row!=null&&row.enabled&&!string.IsNullOrWhiteSpace(row.id)&&
                    !string.Equals(row.category,"Intrinsic",StringComparison.OrdinalIgnoreCase))
                .Select(row=>new MonsterTraitDefinition(
                    row.id,row.displayName,ParseTags(row.requiredTags),row.category)).ToList();
            cachedDatabase=database;
            cachedTraits=loaded.Count(item=>item.IsRank2)==ExpectedRank2Count&&
                   loaded.Count(item=>item.IsRank3)==ExpectedRank3Count&&
                   loaded.Count(item=>item.IsRank4)==ExpectedRank4Count
                ?loaded:fallback;
            return cachedTraits;
        }
    }

    internal static void ClearCache()
    {
        cachedDatabase=null;
        cachedTraits=null;
    }

    public static IReadOnlyList<MonsterTraitDefinition> Rank2=>All.Where(item=>item.IsRank2).ToList();
    public static IReadOnlyList<MonsterTraitDefinition> Rank3=>All.Where(item=>item.IsRank3).ToList();
    public static IReadOnlyList<MonsterTraitDefinition> Rank4=>All.Where(item=>item.IsRank4).ToList();

    public static IReadOnlyList<MonsterTraitDefinition> TestSelectable(
        MonsterDefinition definition,bool rank3)
    {
        GameMasterDatabase database=GameMasterData.Database;
        IEnumerable<MonsterTraitDefinition> source=database==null||database.traits==null
            ?All
            :database.traits.Where(row=>row!=null&&row.enabled&&
                !string.IsNullOrWhiteSpace(row.id)&&
                !string.Equals(row.category,"Intrinsic",StringComparison.OrdinalIgnoreCase))
                .Select(row=>new MonsterTraitDefinition(
                    row.id,row.displayName,ParseTags(row.requiredTags),row.category));
        return source.Where(trait=>(rank3?trait.IsRank3:trait.IsRank2)&&
                trait.IsCompatible(definition))
            .OrderBy(trait=>(trait.requiredTags&FamilyMask)==MonsterCombatTag.None)
            .ThenBy(trait=>trait.displayName)
            .ToList();
    }

    public static MonsterTraitDefinition Get(string id)
    {
        MonsterTraitDefinition active=All.FirstOrDefault(item=>
            string.Equals(item.id,id,StringComparison.Ordinal));
        if(active!=null)return active;
        GameMasterDatabase database=GameMasterData.Database;
        TraitMasterRow row=database==null||database.traits==null?null:database.traits.FirstOrDefault(item=>
            item!=null&&item.enabled&&string.Equals(item.id,id,StringComparison.Ordinal));
        return row==null?null:new MonsterTraitDefinition(
            row.id,row.displayName,ParseTags(row.requiredTags),row.category);
    }

    public static bool IsCompatible(string traitId,MonsterDefinition definition)
    {
        MonsterTraitDefinition trait=Get(traitId);
        return trait!=null&&trait.IsCompatible(definition);
    }

    public static bool ComboPrerequisiteSupported(string traitId,
        MonsterDefinition owner,IEnumerable<MonsterDefinition> roster)
    {
        if(roster==null)return false;
        return roster.Any(other=>other!=null&&other!=owner&&ComboSourceSupports(traitId,other));
    }

    private static bool ComboSourceSupports(string traitId,MonsterDefinition source)
    {
        string behavior=MonsterSkillExecutor<object>.Behavior(source.skillId).ToLowerInvariant();
        switch(traitId)
        {
            case "trait_combo_antigravity":
                return behavior.Contains("earthpulse")||behavior.Contains("skyflip")||
                       behavior.Contains("earthcharge")||behavior.Contains("skydive");
            case "trait_combo_fall_shatter":
                return behavior.Contains("earthpulse")||behavior.Contains("skyflip");
            case "trait_combo_frost_shackle":
                return behavior.Contains("fanglock")||behavior.Contains("captureweb")||
                       behavior.Contains("justiceroar");
            case "trait_combo_venom_mutation":
                return behavior.Contains("crossscythewind");
            case "trait_combo_chemical_ignition":
                return behavior.Contains("sporecolony")||behavior.Contains("wyvernbreath")||
                       behavior.Contains("shadowhunt");
            case "trait_combo_open_wound":
                return (ResolveTags(source)&MonsterCombatTag.StatusOrMarkSkill)!=0;
            case "trait_combo_morale_veil":
                return behavior.Contains("spiritsanctuary")||behavior.Contains("justiceroar")||
                       behavior.Contains("fightingSpirit".ToLowerInvariant());
            case "trait_combo_resolve_barrier":
                return behavior.Contains("earthpulse")||behavior.Contains("justiceroar");
            default:return true;
        }
    }

    public static MonsterCombatTag ParseTags(string rawTags)
    {
        MonsterCombatTag tags=MonsterCombatTag.None;
        foreach(string raw in (rawTags??string.Empty).Split('|',','))
            if(Enum.TryParse(raw.Trim(),true,out MonsterCombatTag parsed))tags|=parsed;
        return tags;
    }

    public static MonsterCombatTag ResolveTags(MonsterDefinition definition)
    {
        if(definition==null)return MonsterCombatTag.None;
        MonsterCombatTag tags=definition.attackRange>1?MonsterCombatTag.Ranged:MonsterCombatTag.Melee;
        string family=(definition.familyId??string.Empty).ToLowerInvariant();
        if(family=="raptor")tags|=MonsterCombatTag.RaptorFamily;
        else if(family=="sabertooth")tags|=MonsterCombatTag.SabertoothFamily;
        else if(family=="frog")tags|=MonsterCombatTag.FrogFamily;
        else if(family=="crocodile")tags|=MonsterCombatTag.CrocodileFamily;
        else if(family=="kingkong")tags|=MonsterCombatTag.KingKongFamily;
        else if(family=="wizard_mouse")tags|=MonsterCombatTag.WizardMouseFamily;
        else if(family=="bear")tags|=MonsterCombatTag.BearFamily;
        else if(family=="wolf")tags|=MonsterCombatTag.WolfFamily;
        else if(family=="arachne"||family=="spider")tags|=MonsterCombatTag.SpiderFamily;
        else if(family=="ninja_cat")tags|=MonsterCombatTag.NinjaCatFamily;
        else if(family=="tyranno")tags|=MonsterCombatTag.TyrannoFamily;
        else if(family=="monkey")tags|=MonsterCombatTag.MonkeyFamily;
        else if(family=="mantis")tags|=MonsterCombatTag.MantisFamily;
        else if(family=="mushroom")tags|=MonsterCombatTag.MushroomFamily;
        else if(family=="mammoth")tags|=MonsterCombatTag.MammothFamily;
        else if(family=="beetle")tags|=MonsterCombatTag.BeetleFamily;
        else if(family=="spirit_deer")tags|=MonsterCombatTag.SpiritDeerFamily;
        else if(family=="griffin")tags|=MonsterCombatTag.GriffinFamily;
        else if(family=="wyvern")tags|=MonsterCombatTag.WyvernFamily;
        else if(family=="haetae")tags|=MonsterCombatTag.HaetaeFamily;

        string behavior=MonsterSkillExecutor<object>.Behavior(definition.skillId).ToLowerInvariant();
        if(string.IsNullOrWhiteSpace(definition.skillId)||behavior=="hunterfocus")return tags;
        tags|=MonsterCombatTag.Skill;
        if(behavior.Contains("guardian")||behavior.Contains("spiritsanctuary"))tags|=MonsterCombatTag.Guardian;
        if(!behavior.Contains("fightingSpirit".ToLowerInvariant())&&!behavior.Contains("guardian")&&
           !behavior.Contains("spiritsanctuary"))tags|=MonsterCombatTag.DamageSkill;
        if(behavior.Contains("assassinleap")||behavior.Contains("fanglock")||
           behavior.Contains("tongueharpoon")||
           behavior.Contains("shadowhunt")||behavior.Contains("deathroll"))
            tags|=MonsterCombatTag.SingleTargetSkill;
        if(behavior.Contains("assassinleap")||behavior.Contains("shadowhunt")||
           behavior.Contains("earthcharge")||behavior.Contains("skydive")||
           behavior.Contains("threesidedhunt"))
            tags|=MonsterCombatTag.MovementSkill;
        if(behavior.Contains("tongueharpoon")||behavior.Contains("crossscythewind")||
           behavior.Contains("earthcharge")||behavior.Contains("wyvernbreath"))
            tags|=MonsterCombatTag.LineSkill;
        if(behavior.Contains("earthpulse")||behavior.Contains("tyranttremor")||
           behavior.Contains("magicstarbarrage")||behavior.Contains("acrobaticbarrage")||
           behavior.Contains("crossscythewind")||behavior.Contains("sporecolony")||
           behavior.Contains("earthcharge")||behavior.Contains("skyflip")||
           behavior.Contains("spiritsanctuary")||behavior.Contains("wyvernbreath")||
           behavior.Contains("justiceroar")||behavior.Contains("skydive")||
           behavior.Contains("threesidedhunt")||
           behavior.Contains("captureweb")||
           behavior.Contains("fightingSpirit".ToLowerInvariant()))
            tags|=MonsterCombatTag.AreaSkill;
        if(behavior.Contains("earthpulse")||behavior.Contains("tyranttremor")||
           behavior.Contains("fightingSpirit".ToLowerInvariant())||behavior.Contains("justiceroar"))
            tags|=MonsterCombatTag.SelfCenteredSkill;
        if(behavior.Contains("captureweb")||behavior.Contains("sporecolony")||
           behavior.Contains("spiritsanctuary")||behavior.Contains("fightingSpirit".ToLowerInvariant()))
            tags|=MonsterCombatTag.DurationSkill;
        if(behavior.Contains("magicstarbarrage")||behavior.Contains("acrobaticbarrage")||
           behavior.Contains("tongueharpoon"))tags|=MonsterCombatTag.ProjectileSkill;
        if(behavior.Contains("fightingSpirit".ToLowerInvariant())||behavior.Contains("guardian")||
           behavior.Contains("justiceroar"))tags|=MonsterCombatTag.SelfBuffSkill;
        if(behavior.Contains("fanglock")||behavior.Contains("tongueharpoon")||
           behavior.Contains("earthpulse")||
           behavior.Contains("captureweb")||behavior.Contains("shadowhunt")||
           behavior.Contains("deathroll")||behavior.Contains("tyranttremor")||
           behavior.Contains("earthcharge")||
           behavior.Contains("skyflip")||behavior.Contains("skydive")||
           behavior.Contains("threesidedhunt")||
           behavior.Contains("justiceroar"))tags|=MonsterCombatTag.StatusOrMarkSkill;
        if(behavior.Contains("sporecolony")||behavior.Contains("wyvernbreath")||
           behavior.Contains("justiceroar"))tags|=MonsterCombatTag.ElementalSkill;
        return tags;
    }
}
