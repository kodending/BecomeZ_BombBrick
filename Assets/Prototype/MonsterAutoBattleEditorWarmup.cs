#if UNITY_EDITOR
using System;
using System.Collections;
using BombBrick.Combat;

public sealed partial class MonsterAutoBattlePrototype
{
    private IEnumerator WarmupEditorCombatMethods()
    {
        // Mono compiles large combat methods on first use; prepare them behind the loading overlay.
        Type[] types={typeof(MonsterAutoBattlePrototype),typeof(VoxelEnemyReaction),
            typeof(SpriteMonsterAnimationDriver),typeof(CombatStatusState),typeof(MonsterTraitRuntime),
            typeof(PermanentResearchSystem),typeof(PersonalityCombatPolicy),typeof(ExpeditionAugmentRuntime),
            typeof(ExpeditionItemSystem),typeof(ExpeditionGrowthSystem),typeof(CombatStatFormula),
            typeof(CombatHudIconArt),typeof(BombBrickUiTheme),typeof(LocalizedGUI),typeof(GameMasterData),
            typeof(MonsterSkillExecutor<SkillExecutionContext>),typeof(PixelCombatEffect),typeof(PixelStarProjectile),
            typeof(PixelNinjaEightGate),typeof(PixelNinjaMoonFx),typeof(PixelNinjaSubstitution),typeof(PixelNinjaSpacetime),
            typeof(SpriteMonsterBillboard),typeof(ScreenSpaceOutlineTarget),
            typeof(LayeredBattleEnvironment),typeof(MeadowAmbientParticles)};
        return BombBrick.Game.Core.GameResourceCache.WarmupEditorMethods(types);
    }

    private IEnumerator WarmupEditorNavigation()
    {
        // Exercise generic navigation containers without assigning a target or applying a route.
        foreach(Unit unit in units)
        {
            if(!IsCombatActive(unit))continue;
            Unit target=SelectNearestTarget(unit);
            if(target==null)continue;
            TryFindGridRoute(unit,
                NearestWalkableCoordinate(NearestGridCoordinate(unit.model.transform.position)),
                NearestWalkableCoordinate(NearestGridCoordinate(target.model.transform.position)),
                out _,out _,out _);
            yield return null;
            bool previousQuery=BeginNavigationQuery();
            try
            {
                var origin=Flat(unit.model.transform.position);
                var position=ClosestMeleeAttackPosition(unit,target);
                IsOpenAttackPosition(unit,target,position);
                MeleeSlotSegmentClear(unit,target,origin,position);
                TryMeleeSlotRoute(unit,target,new[]{position},out _);
            }
            finally{navigationQueryActive=previousQuery;meleeReadabilityBodies.Clear();}
            yield return null;
        }
    }
}
#endif
