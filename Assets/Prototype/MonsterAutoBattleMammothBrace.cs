using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BombBrick.Game.Core;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class MammothBraceState
    {
        public Vector3 origin,direction;
        public float time,scale,radiusScale,received;
        public float shareSoundAt=-10f,guardSoundAt=-10f;
        public int generation,revision;
        public bool aimed,struck;
        public PixelMammothBrace effect;
    }
    private int mammothSharedDamageDepth;

    private bool MammothBraceValid(Unit source,MammothBraceState brace)=>!battleEnding&&source.mammothBrace==brace&&
        brace.generation==combatGeneration&&source.positionRevision==brace.revision&&IsCombatActive(source)&&
        !SpiderSilkBound(source)&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
        !source.combatStatuses.Has(CombatStatusKind.Fear)&&source.giraffeStunTimer<=0f;

    private bool MammothBraceProtecting(Unit source)=>source!=null&&source.mammothBrace!=null&&
        MammothBraceValid(source,source.mammothBrace)&&IsCombatTargetable(source)&&
        source.augmentReturnInvulnerabilityTimer<=0f&&
        !source.combatStatuses.Has(CombatStatusKind.Freeze)&&
        source.mammothBrace.time>=MammothRank3Rules.BraceWindup&&
        source.mammothBrace.time<MammothRank3Rules.BraceGuardEnd;

    private Unit FindMammothBraceProtector(Unit ally)
    {
        if(!IsCombatTargetable(ally))return null;
        Unit nearest=null;float distance=float.MaxValue;
        Vector3 point=Flat(ally.model.transform.position);
        foreach(Unit candidate in units)
        {
            if(candidate==ally||candidate.team!=ally.team||!MammothBraceProtecting(candidate))continue;
            float d=(Flat(candidate.model.transform.position)-point).sqrMagnitude;
            float radius=TileSize*MammothRank3Rules.BraceRadiusTiles*candidate.mammothBrace.radiusScale;
            if(d>radius*radius||d>=distance)continue;
            nearest=candidate;distance=d;
        }
        return nearest!=null&&ally.combatStatuses.RejectBenefit()?null:nearest;
    }

    private float ShareMammothBraceDamage(Unit attacker,Unit ally,float damage,bool fromSkill)
    {
        if(damage<=0f||attacker==null||attacker.team==ally.team||mammothSharedDamageDepth>0)return damage;
        Unit protector=FindMammothBraceProtector(ally);if(protector==null)return damage;
        var brace=protector.mammothBrace;
        float shared=damage*MammothRank3Rules.BraceShareRatio;
        ally.preventedDamage+=shared;
        protector.preventedDamage+=shared*(1-MammothRank3Rules.BraceTransferRatio);
        if(!mlEpisodeActive)
        {
            brace.effect?.ProtectedHit(Rank3Center(ally),brace.time);
            if(brace.time-brace.shareSoundAt>=.12f)
            {
                brace.shareSoundAt=brace.time;
                PlayMammothSound(protector,MammothSound.Share,Rank3Center(ally),.65f);
            }
            ally.reaction?.PlayTintHit(new Color(1f,.87f,.53f));
        }
        // Already mitigated on the ally: no second armor/outgoing scaling, sharing or attack procs.
        // Normal shields, lethal survival and death cleanup still run on the protector.
        mammothSharedDamageDepth++;
        try
        {
            float taken=ApplyDamage(attacker,protector,0f,fromSkill,false,false,false,0f,
                shared*MammothRank3Rules.BraceTransferRatio,1f,false,true);
            if(fromSkill)attacker.skillDamage+=taken;
        }
        finally{mammothSharedDamageDepth--;}
        return damage-shared;
    }

    private void NotifyMammothBraceHit(Unit attacker,Unit unit,float damage,float contactDamage)
    {
        if(contactDamage<=0f||!MammothBraceProtecting(unit))return;
        var brace=unit.mammothBrace;brace.received+=damage;
        // Shared damage reacts visually when its packet reaches the hide. Direct hits react now,
        // including hits fully absorbed by an ordinary shield. HP and shield calculations stay immediate.
        if(mlEpisodeActive||mammothSharedDamageDepth>0)return;
        brace.effect?.GuardHit(attacker==null?Rank3Center(unit)+brace.direction:Rank3Center(attacker),brace.time);
    }

    private void StartMammothBrace(Unit source,int generation)
    {
        if(source.mammothBrace!=null)return;
        Vector3 origin=Flat(source.model.transform.position),direction=source.mammothChargeEnd-origin;
        if(direction.sqrMagnitude<.001f)direction=source.facingDirection;
        if(direction.sqrMagnitude<.001f)direction=Vector3.forward;
        var brace=new MammothBraceState{origin=origin,direction=direction.normalized,generation=generation,
            revision=source.positionRevision,scale=source.mammothChargeDamageMultiplier,radiusScale=source.mammothChargeRadiusMultiplier};
        source.mammothBrace=brace;source.mammothChargePending=false;source.mammothChargeTarget=null;
        ClearRoute(source);source.hexStepping=false;source.velocity=Vector3.zero;
        SetMoving(source,false);
        PlayMammothSound(source,MammothSound.Prepare,origin,.65f);
        if(!mlEpisodeActive)
        {
            brace.effect=PixelMammothBrace.Play(Rank3Renderer(source),source.definition.monsterId,origin,brace.direction,brace.radiusScale);
            brace.effect.transform.SetParent(transform,true);
            brace.effect.guardContact=(point,time)=>
            {
                if(!MammothBraceValid(source,brace)||time-brace.guardSoundAt<.12f)return;
                brace.guardSoundAt=time;
                PlayMammothSound(source,MammothSound.Guard,point,.75f);
            };
        }
        if(source.spriteAnimation!=null)
        {
            if(!source.spriteAnimation.PlayMammothCharge(()=>MammothBraceValid(source,brace),
                time=>AdvanceMammothBrace(source,brace,time),()=>EndMammothBrace(source,brace),false,false,true))
                EndMammothBrace(source,brace);
        }
        else if(isActiveAndEnabled)StartCombatCoroutine(source,MammothBraceFallback(source,brace));
    }

    private IEnumerator MammothBraceFallback(Unit source,MammothBraceState brace)
    {
        while(MammothBraceValid(source,brace)&&brace.time<MammothRank3Rules.BraceEnd)
        {
            if(!paused&&!source.combatStatuses.Has(CombatStatusKind.Freeze))AdvanceMammothBrace(source,brace,brace.time+CombatSkillClock.Delta);
            yield return null;
        }
        EndMammothBrace(source,brace);
    }

    private void AdvanceMammothBrace(Unit source,MammothBraceState brace,float time)
    {
        if(!MammothBraceValid(source,brace)){EndMammothBrace(source,brace);return;}
        brace.time=time;
        float load=Mathf.Clamp01(brace.received/Mathf.Max(1f,source.maxHp*.2f));
        source.currentAction=time<MammothRank3Rules.BraceWindup?"mountain_brace_prepare":
            time<MammothRank3Rules.BraceGuardEnd?"mountain_brace_guard":"mountain_brace_sweep";
        if(!brace.aimed&&time>=MammothRank3Rules.BraceGuardEnd)
        {
            brace.aimed=true;
            Unit target=units.Where(u=>u.team!=source.team&&IsCombatTargetable(u))
                .OrderBy(u=>(Flat(u.model.transform.position)-brace.origin).sqrMagnitude).FirstOrDefault();
            if(IsCombatTargetable(target))
            {
                Vector3 toward=Flat(target.model.transform.position)-brace.origin;
                if(toward.sqrMagnitude>.001f)brace.direction=toward.normalized;
            }
            RotateUnit(source,brace.direction,1f);
        }
        if(brace.effect!=null)
        {
            brace.effect.allies.Clear();
            if(MammothBraceProtecting(source))foreach(Unit ally in units)
                if(ally!=source&&ally.team==source.team&&IsCombatTargetable(ally)&&
                    Vector3.Distance(brace.origin,Flat(ally.model.transform.position))<=TileSize*MammothRank3Rules.BraceRadiusTiles*brace.radiusScale)
                    brace.effect.allies.Add(Flat(ally.model.transform.position));
            brace.effect.Pose(time,brace.direction,load);
        }
        if(brace.struck||time<MammothRank3Rules.BraceStrike)return;
        brace.struck=true;
        brace.effect?.Sweep(time);
        float radius=TileSize*MammothRank3Rules.BraceSweepTiles*brace.radiusScale,damage=0f;
        var hits=new HashSet<Unit>();
        foreach(Unit enemy in units.ToArray())
        {
            if(!IsCombatTargetable(enemy)||enemy.team==source.team)continue;
            Vector3 offset=Flat(enemy.model.transform.position)-brace.origin;
            if(offset.magnitude>radius+enemy.collisionRadius||offset.sqrMagnitude>.001f&&
                Vector3.Dot(offset.normalized,brace.direction)<MammothRank3Rules.BraceHalfAngleCos)continue;
            bool steel=HasRank3(source,MammothCombatRules.IrontuskTraitId);
            float dealt=ApplySkillDamage(source,enemy,MammothCombatRules.Damage(SkillPower(source),
                MammothRank3Rules.BraceDamageRatio,steel?CurrentDefense(source):0f,steel)*brace.scale,false);
            hits.Add(enemy);damage+=dealt;source.skillDamage+=dealt;source.skillHits++;
            if(!MammothBraceValid(source,brace))return;
            if(!mlEpisodeActive)
            {
                brace.effect?.Hit(Rank3Center(enemy),time);
                enemy.reaction?.PlayTintHit(PixelMammothWallRush.HitColor(source.definition.monsterId));
                enemy.spriteAnimation?.PlayImpactRecoil(brace.direction,.15f);
            }
        }
        ApplyMammothChargeTraits(source,hits,brace.origin,brace.origin+brace.direction*radius);
        if(!mlEpisodeActive){combatCameraDirector?.ReportGroundSlam(load>.6f);
            meadowAmbientParticles?.Impact(brace.origin+brace.direction*radius*.5f,radius*.65f,Rank3Renderer(source));}
        PlayMammothSound(source,MammothSound.Counter,brace.origin,1.1f);
        source.traitActivations++;RecordSkillEvent(source,MammothRank3Rules.MountainBrace,hits.Count,damage);
    }

    private void EndMammothBrace(Unit source,MammothBraceState brace)
    {
        brace.effect?.Cancel();
        if(source.mammothBrace!=brace)return;
        source.mammothBrace=null;source.mammothChargePending=false;source.mammothChargeTarget=null;
        source.mammothChargeDamageMultiplier=source.mammothChargeRadiusMultiplier=1f;
        source.attackMovementLockTimer=0f;source.decisionTimer=0f;
        if(IsCombatActive(source))source.currentAction="mountain_brace_complete";
    }
}
