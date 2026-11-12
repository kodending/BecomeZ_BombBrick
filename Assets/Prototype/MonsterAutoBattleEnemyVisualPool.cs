using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class EnemyVisual
    {
        public GameObject model, ring;
    }

    private const int EnemyVisualPoolLimit = 24;
    private readonly Dictionary<string, Stack<EnemyVisual>> enemyVisualPool =
        new Dictionary<string, Stack<EnemyVisual>>();
    private int pooledEnemyVisualCount;
    private bool recyclingEnemyVisuals;

    private EnemyVisual TakeEnemyVisual(string resourcePath)
    {
        if (!enemyVisualPool.TryGetValue(resourcePath, out var pool)) return null;
        while (pool.Count > 0)
        {
            var visual = pool.Pop();
            pooledEnemyVisualCount--;
            if (visual.model != null && visual.ring != null) return visual;
            if (visual.model != null) Destroy(visual.model);
            if (visual.ring != null) Destroy(visual.ring);
        }
        return null;
    }

    private bool ReturnEnemyVisual(Unit unit)
    {
        if (!recyclingEnemyVisuals || unit.team != Team.Enemy || unit.model == null || unit.ring == null ||
            unit.stableId == null || !unit.stableId.StartsWith("enemy_") ||
            pooledEnemyVisualCount >= EnemyVisualPoolLimit || CombatEffectEndFade.IsFading(unit.model.transform))
            return false;
        unit.spriteAnimation?.ResetForReuse();
        unit.spriteBillboard?.SetRollPose(0f, Vector3.zero, Vector3.zero);
        unit.outline?.SetOutlined(false);
        unit.model.SetActive(false);
        unit.ring.SetActive(false);
        if (!enemyVisualPool.TryGetValue(unit.resourcePath, out var pool))
            enemyVisualPool[unit.resourcePath] = pool = new Stack<EnemyVisual>();
        pool.Push(new EnemyVisual { model = unit.model, ring = unit.ring });
        pooledEnemyVisualCount++;
        // Old effect callbacks must never regain a reference to a reused actor.
        unit.model = null; unit.ring = null; unit.spriteAnimation = null; unit.spriteBillboard = null;
        unit.animator = null; unit.reaction = null; unit.outline = null;
        return true;
    }
}
