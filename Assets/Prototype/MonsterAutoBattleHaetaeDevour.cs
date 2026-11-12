using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private readonly List<Unit> haetaeDevourers=new List<Unit>();
    private readonly Dictionary<Team,PixelHaetaeDevourer> haetaeDevourEffects=new Dictionary<Team,PixelHaetaeDevourer>();
    private readonly Dictionary<Unit,float[]> haetaeDevourFeedback=new Dictionary<Unit,float[]>();
    private readonly HashSet<Team> haetaeDevourAppearancePending=new HashSet<Team>();

    private Unit HaetaeDevourerFor(Unit recipient)
    {
        if(recipient==null||recipient.inStorage||!battleStarted||battleEnding)return null;
        foreach(var caster in haetaeDevourers)
            if(caster.team!=recipient.team&&IsCombatActive(caster)&&caster.hp>0f&&HasFamilyTranscendence(caster))return caster;
        return null;
    }

    private void BindHaetaeDevour(Unit unit)
    {
        unit.combatStatuses.externalBenefitBlock=()=>HaetaeDevourerFor(unit)!=null;
        unit.combatStatuses.benefitBlocked=kind=>ShowHaetaeDevour(unit,kind);
        unit.combatEffectIcons.rejectShow=kind=>HaetaeBuffIcon(kind)&&unit.combatStatuses.BenefitsBlocked;
    }

    private static bool HaetaeBuffIcon(CombatEffectIconKind kind)
    {
        switch(kind)
        {
            case CombatEffectIconKind.Shield:case CombatEffectIconKind.AttackUp:case CombatEffectIconKind.Haste:
            case CombatEffectIconKind.Royal:case CombatEffectIconKind.ToughScales:case CombatEffectIconKind.DamageReduction:
            case CombatEffectIconKind.AttackSpeedUp:case CombatEffectIconKind.MoveSpeedUp:case CombatEffectIconKind.DefenseUp:
            case CombatEffectIconKind.ResistanceUp:case CombatEffectIconKind.SkillPowerUp:case CombatEffectIconKind.DebuffImmunity:
            case CombatEffectIconKind.ControlImmunity:return true;
            default:return false;
        }
    }

    private void BeginHaetaeDevourers()
    {
        foreach(var caster in units.Where(u=>IsCombatActive(u)&&u.hp>0f&&IsHaetaeFamily(u)&&HasFamilyTranscendence(u)).ToArray())
            if(!haetaeDevourers.Contains(caster))
            {haetaeDevourers.Add(caster);caster.traitActivations++;QueueRank4Cinematic(caster);}
        foreach(var unit in units)
        {
            BindHaetaeDevour(unit);
            if(HaetaeDevourerFor(unit)!=null)RemoveHaetaeOpeningBenefits(unit);
        }
        UpdateHaetaeDevourers();
    }

    // All grant sites (including old scalar shields) use the same gate before changing combat state.
    private static float GateHaetaeBenefit(Unit unit,float value,CombatBenefitKind kind=CombatBenefitKind.Enhancement)=>
        value>0f&&unit.combatStatuses.RejectBenefit(kind)?0f:value;

    private void UpdateHaetaeDevourers()
    {
        if(mlEpisodeActive||!battleStarted||battleEnding)return;
        foreach(var caster in haetaeDevourers)
        {
            if(!IsCombatActive(caster)||caster.hp<=0f||!HasFamilyTranscendence(caster))continue;
            PixelHaetaeDevourer current;
            if(haetaeDevourEffects.TryGetValue(caster.team,out current)&&current!=null&&!current.Ending)continue;
            Team team=caster.team;int generation=combatGeneration;
            bool cinematicHidden=HasPendingHaetaeCinematic(team);
            var fx=PixelHaetaeDevourer.Create(Rank3Renderer(caster),combatBounds,team==Team.Player,()=>battleElapsed,
                ()=>this!=null&&battleStarted&&!battleEnding&&generation==combatGeneration&&
                    haetaeDevourers.Any(u=>u.team==team&&IsCombatActive(u)&&u.hp>0f&&HasFamilyTranscendence(u)),
                point=>
                {
                    if(this==null||generation!=combatGeneration||!battleStarted||battleEnding)return;
                    // The team's shared beast can outlive the caster that first summoned it.
                    var living=haetaeDevourers.FirstOrDefault(u=>u.team==team&&IsCombatActive(u)&&u.hp>0f&&HasFamilyTranscendence(u));
                    if(living!=null)PlayHaetaeSound(living,HaetaeSound.Swallow,point,.9f);
                },point=>
                {
                    if(this==null||generation!=combatGeneration||!battleStarted||battleEnding)return;
                    var living=haetaeDevourers.FirstOrDefault(u=>u.team==team&&IsCombatActive(u)&&u.hp>0f&&HasFamilyTranscendence(u));
                    if(living!=null)PlayHaetaeSound(living,HaetaeSound.Absorb,point,.9f);
                },cinematicHidden);
            if(fx==null)continue;
            fx.transform.SetParent(transform,true);haetaeDevourEffects[team]=fx;
            if(cinematicHidden)haetaeDevourAppearancePending.Add(team);
            else
            {haetaeDevourAppearancePending.Remove(team);PlayHaetaeSound(caster,HaetaeSound.Appear,fx.MouthPosition,.9f);}
        }
    }

    private void ShowHaetaeDevour(Unit recipient,CombatBenefitKind kind)
    {
        if(mlEpisodeActive||!IsCombatActive(recipient))return;
        Unit caster=HaetaeDevourerFor(recipient);if(caster==null)return;
        float[] next;
        if(!haetaeDevourFeedback.TryGetValue(recipient,out next))
            haetaeDevourFeedback[recipient]=next=new[]{-1f,-1f,-1f};
        if(battleElapsed<next[(int)kind])return;
        next[(int)kind]=battleElapsed+.3f;
        UpdateHaetaeDevourers();
        PixelHaetaeDevourer fx;
        if(haetaeDevourEffects.TryGetValue(caster.team,out fx)&&fx!=null&&!fx.Ending)
        {
            fx.Absorb(Rank3Center(recipient),kind);
        }
    }

    private void RemoveHaetaeOpeningBenefits(Unit u)
    {
        float shield=TotalVisibleShield(u);
        if(shield>0f){RemoveVisibleShield(u,shield);ShowHaetaeDevour(u,CombatBenefitKind.Shield);}
        bool enhancement=u.combatStatuses.Has(CombatStatusKind.ComboRally);
        u.combatStatuses.Remove(CombatStatusKind.ComboRally);u.combatStatuses.Remove(CombatStatusKind.ComboShielded);
        enhancement|=u.itemAttackSpeedTimer>0f||u.itemPostSkillAttackSpeedTimer>0f||u.itemStatusRecoveryAttackSpeedTimer>0f||
            u.itemTemporaryStatTimer>0f||u.itemPostSkillStatTimer>0f||u.itemTwilightStatTimer>0f||u.familyAttackSpeedTimer>0f||
            u.familyMoveSpeedTimer>0f||u.familyDamageReductionTimer>0f||u.rank3AttackBonusTimer>0f||u.rank3DefenseBonusTimer>0f||
            u.rank3AttackSpeedBonusTimer>0f||u.rank3MoveSpeedBonusTimer>0f||u.guardTimer>0f||u.crocodileToughHideTimer>0f||
            u.frogRoyalAttackSpeedTimer>0f||u.kingKongRallyTimer>0f||u.giraffeDanceAttackSpeedTimer>0f||u.packHuntSpeedTimer>0f||
            u.wolfChaseTimer>0f||u.mammothHasteTimer>0f||u.itemOpeningImmunityTimer>0f||u.spiritLifeReturnTimer>0f;
        u.itemAttackSpeedTimer=u.itemPostSkillAttackSpeedTimer=u.itemStatusRecoveryAttackSpeedTimer=0f;
        u.itemTemporaryStatTimer=u.itemPostSkillStatTimer=u.itemTwilightStatTimer=0f;
        u.familyAttackSpeedTimer=u.familyMoveSpeedTimer=u.familyDamageReductionTimer=0f;
        u.rank3AttackBonusTimer=u.rank3DefenseBonusTimer=u.rank3AttackSpeedBonusTimer=u.rank3MoveSpeedBonusTimer=0f;
        u.guardTimer=u.crocodileToughHideTimer=u.frogRoyalAttackSpeedTimer=u.kingKongRallyTimer=u.giraffeDanceAttackSpeedTimer=0f;
        u.packHuntSpeedTimer=u.wolfChaseTimer=u.mammothHasteTimer=u.itemOpeningImmunityTimer=u.spiritLifeReturnTimer=0f;
        u.rank3ForceImmuneTimer=u.growthStatusRecoveryGuardTimer=0f;u.growthKillHasteExpirations.Clear();
        if(u.bearResolveTimer>0f){u.bearResolveAttackSpeedBonus=u.bearResolveDamageReduction=0f;enhancement=true;}
        if(u.frogStolenBuffMagnitude>0f){ClearFrogStolenBuff(u);enhancement=true;}
        enhancement|=u.frogStolenBuffs.RemoveAll(b=>b.magnitude>0f)>0;
        if(enhancement)ShowHaetaeDevour(u,CombatBenefitKind.Enhancement);
        foreach(var kind in u.combatEffectIcons.ActiveKinds.Where(HaetaeBuffIcon).ToArray())u.combatEffectIcons.Hide(kind);
    }

    private void ClearHaetaeDevourers()
    {
        haetaeDevourers.Clear();haetaeDevourFeedback.Clear();haetaeDevourAppearancePending.Clear();
        foreach(var fx in haetaeDevourEffects.Values)if(fx!=null)fx.Cancel();
        haetaeDevourEffects.Clear();
    }
}
