using System.Collections.Generic;
using System.Linq;
using BombBrick.Combat;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class BeetleOverturnPassenger
    {
        public Unit unit;
        public BeetleOverturnState owner;
        public Vector3 from;
        public int revision,seed;
        public bool flying;
    }
    private sealed class BeetleOverturnState
    {
        public CombatSkillClock skillClock=CombatSkillClock.Capture();
        public Unit source;
        public int generation;
        public float time,damageScale=1f;
        public bool launched,landed;
        public readonly List<BeetleOverturnPassenger> passengers=new List<BeetleOverturnPassenger>();
        public PixelBeetleOverturn effect;
    }
    private readonly List<BeetleOverturnState> beetleOverturns=new List<BeetleOverturnState>();
    private readonly HashSet<Vector2Int> beetleOverturnPreviewTiles=new HashSet<Vector2Int>();

    private bool BeginBeetleImperialHorn(Unit source,SkillMasterRow skill)
    {
        if(!IsCombatActive(source)||!HasFamilyTranscendence(source)||source.definition.familyId!="beetle"||
           skill==null||source.beetleOverturn!=null)return false;
        var enemies=units.Where(u=>u.team!=source.team&&IsCombatTargetable(u)).OrderBy(u=>u.id).ToArray();
        if(enemies.Length==0)return false;
        var state=new BeetleOverturnState{source=source,generation=combatGeneration,
            damageScale=source.skillTraitDamageMultiplier};
        for(int index=0;index<enemies.Length;index++)
        {
            Unit enemy=enemies[index];
            state.passengers.Add(new BeetleOverturnPassenger{unit=enemy,owner=state,
                revision=enemy.positionRevision,from=Flat(enemy.model.transform.position),seed=index});
        }
        source.beetleOverturn=state;beetleOverturns.Add(state);
        source.beetleSkyFlipPending=false;source.beetleSkyFlipTarget=null;
        source.skillTimer=source.skillCooldownDuration;source.skillUses++;source.traitActivations++;
        ClearRoute(source);ClearUnitInertia(source);source.hitSlideVelocity=Vector3.zero;
        source.hexStepping=false;source.velocity=Vector3.zero;SetMoving(source,false);
        source.attackMovementLockTimer=Mathf.Max(source.attackMovementLockTimer,BeetleOverturnRules.End/CombatStatFormula.CombatAnimationRate);
        source.currentAction="beetle_imperial_horn_prepare";
        source.spriteAnimation?.InterruptCurrentAction(true);PoseBeetleImperialHorn(source,0f);
        ApplyPostSkillItemEffects(source);
        if(!mlEpisodeActive)
        {
            Bounds area=new Bounds(state.passengers[0].from,Vector3.zero);
            foreach(var passenger in state.passengers)area.Encapsulate(passenger.from);
            area.Expand(TileSize*1.2f);
            Vector3 direction=Flat(area.center-source.model.transform.position);
            if(direction.sqrMagnitude<.001f)direction=source.team==Team.Player?Vector3.right:Vector3.left;
            RotateUnit(source,direction,1f);source.spriteBillboard?.SetFacingDirection(direction,true);
            state.effect=PixelBeetleOverturn.Play(new List<Bounds>{area},Rank3Renderer(source),direction.normalized);
            state.effect.transform.SetParent(transform,true);
            QueueRank4Cinematic(source,enemies);
            PlayBeetleSound(source,BeetleSound.Warning,Rank3Center(source),.7f);
        }
        return true;
    }

    private static void PoseBeetleImperialHorn(Unit source,float time)
    {
        source.spriteAnimation?.PoseImperialHorn(time,PixelBeetleOverturn.SourceFrames(source.definition.monsterId,
            source.resourcePath!=null&&source.resourcePath.IndexOf("/Shiny/",System.StringComparison.OrdinalIgnoreCase)>=0));
    }

    private bool BeetleImperialHornValid(BeetleOverturnState state)
    {
        Unit source=state.source;
        return !battleEnding&&state.generation==combatGeneration&&source.beetleOverturn==state&&
            IsCombatActive(source)&&source.hp>0f&&!SpiderSilkBound(source)&&source.giraffeStunTimer<=0f&&
            !source.combatStatuses.Has(CombatStatusKind.Freeze)&&!source.combatStatuses.Has(CombatStatusKind.Knockdown)&&
            !source.combatStatuses.Has(CombatStatusKind.Airborne)&&!source.combatStatuses.Has(CombatStatusKind.Fear);
    }

    private void UpdateBeetleOverturns(float dt)
    {
        for(int index=beetleOverturns.Count-1;index>=0;index--)
        {
            if(index>=beetleOverturns.Count)continue;
            var state=beetleOverturns[index];
            if(PausedBySkillTime(state.skillClock))continue;
            if(rank4Presentation?.request.source==state.source&&rank4Presentation.request.context.family=="beetle"||
               rank4PresentationQueue.Any(r=>r.source==state.source&&r.context.family=="beetle"&&Rank4PresentationValid(r)))continue;
            AdvanceBeetleOverturn(state,dt);
        }
    }

    private void AdvanceBeetleOverturn(BeetleOverturnState state,float dt)
    {
        if(!BeetleImperialHornValid(state)){EndBeetleOverturn(state);return;}
        Unit source=state.source;
        state.time=Mathf.Min(BeetleOverturnRules.End,state.time+Mathf.Max(0f,dt)*CombatStatFormula.CombatAnimationRate);
        PoseBeetleImperialHorn(source,state.time);state.effect?.Pose(state.time);
        if(!state.launched&&state.time>=BeetleOverturnRules.Warning)
        {
            state.launched=true;
            foreach(var passenger in state.passengers)
            {
                Unit enemy=passenger.unit;
                if(!IsCombatTargetable(enemy)||PreyDisplacementImmune(enemy)||enemy.beetleOverturnedBy!=null)continue;
                InterruptPositionalAction(enemy);ClearRoute(enemy);ClearUnitInertia(enemy);
                enemy.hitSlideVelocity=Vector3.zero;enemy.hexStepping=false;enemy.velocity=Vector3.zero;
                passenger.revision=enemy.positionRevision;passenger.from=Flat(enemy.model.transform.position);
                float duration=BeetleOverturnRules.Flight/CombatStatFormula.CombatAnimationRate+.05f;
                var airborne=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Airborne,duration,source,duration);
                if(airborne.source!=source||airborne.remaining<=0f)continue;
                passenger.flying=true;enemy.beetleOverturnedBy=passenger;
                SetMoving(enemy,false);enemy.spriteAnimation?.SetStruggling(true);
            }
            if(!mlEpisodeActive)PlayBeetleSound(source,BeetleSound.Overturn,Rank3Center(source),.9f);
        }
        foreach(var passenger in state.passengers)
        {
            Unit enemy=passenger.unit;if(!passenger.flying||enemy.beetleOverturnedBy!=passenger)continue;
            if(!IsCombatActive(enemy)||enemy.positionRevision!=passenger.revision||PreyDisplacementImmune(enemy)||
               enemy.combatStatuses.Get(CombatStatusKind.Airborne)?.source!=source)
            {ReleaseBeetleOverturnPassenger(enemy);continue;}
            float direction=(passenger.seed%2==0?1f:-1f)*(source.team==Team.Player?1f:-1f);
            SpriteRenderer body=Rank3Renderer(enemy);
            Vector3 pivot=body?.sprite==null?new Vector3(0f,.5f,0f):PixelMantisReaper.VisibleBody(body.sprite).center;
            if(body!=null&&body.flipX)pivot.x=-pivot.x;
            Vector3 offset=TileSize*BeetleOverturnRules.AirOffset(state.time,passenger.seed);offset.x*=source.team==Team.Player?1f:-1f;
            enemy.spriteBillboard?.SetRollPose(direction*BeetleOverturnRules.Roll(state.time,passenger.seed),pivot,
                offset);
        }
        if(state.launched&&!state.landed&&state.time>=BeetleOverturnRules.Landing)
        {
            state.landed=true;float total=0f;int hits=0;Vector3 impact=Vector3.zero;
            foreach(var passenger in state.passengers)
            {
                Unit enemy=passenger.unit;
                if(enemy.beetleOverturnedBy==passenger)ReleaseBeetleOverturnPassenger(enemy);
                if(!IsCombatTargetable(enemy)||enemy.team==source.team)continue;
                Vector3 at=Flat(enemy.model.transform.position);state.effect?.Land(at);impact=at;
                float dealt=ApplySkillDamage(source,enemy,new SkillDamageFormulaResult(
                    SkillPower(source)*BeetleOverturnRules.DamageRatio*state.damageScale,0f,0f),
                    applyTraitMultiplier:false,allowHitSlide:false,bypassProjectileWalls:true);
                source.skillDamage+=dealt;source.skillHits++;total+=dealt;hits++;
                if(source.beetleOverturn!=state)return;
            }
            if(hits>0&&!mlEpisodeActive)
            {
                combatCameraDirector?.ReportGroundSlam(true);
                PlayBeetleSound(source,BeetleSound.OverturnLand,impact,1.1f);
                meadowAmbientParticles?.Impact(impact,TileSize*2f,Rank3Renderer(source));
            }
            RecordSkillEvent(source,BeetleOverturnRules.TraitId,hits,total);
        }
        if(state.time>=BeetleOverturnRules.End)EndBeetleOverturn(state);
    }

    private static void ReleaseBeetleOverturnPassenger(Unit unit)
    {
        var passenger=unit.beetleOverturnedBy;if(passenger==null)return;
        unit.beetleOverturnedBy=null;if(!passenger.flying)return;
        passenger.flying=false;unit.spriteBillboard?.SetRollPose(0f,Vector3.zero,Vector3.zero);
        unit.spriteAnimation?.SetStruggling(false);
        var airborne=unit.combatStatuses.Get(CombatStatusKind.Airborne);
        if(airborne?.source==passenger.owner.source)unit.combatStatuses.Remove(CombatStatusKind.Airborne);
    }

    private void EndBeetleOverturn(BeetleOverturnState state)
    {
        foreach(var passenger in state.passengers)
            if(passenger.unit.beetleOverturnedBy==passenger)ReleaseBeetleOverturnPassenger(passenger.unit);
        Unit source=state.source;
        if(source.beetleOverturn==state)
        {
            source.beetleOverturn=null;source.spriteAnimation?.EndBeetleOverturnPose();
            source.attackMovementLockTimer=0f;source.decisionTimer=0f;source.currentAction="beetle_imperial_horn_complete";
        }
        // The first film already renders the full landing tail; returning cannot restart its hidden native dust.
        if(state.time>=BeetleOverturnRules.End&&rank4Presentation?.request.source==source&&
           rank4Presentation.request.context.family=="beetle")
            state.effect?.Pose(BeetleOverturnRules.Landing+PixelBeetleOverturn.Afterglow);
        state.effect?.Cancel();beetleOverturns.Remove(state);
    }

    private void UpdateBeetleOverturnPreview()
    {
        var previous=beetleOverturnPreviewTiles.ToArray();beetleOverturnPreviewTiles.Clear();
        Unit source=draggingUnit?selectedUnit:hoveredUnit??selectedUnit;
        if(!battleStarted&&source!=null&&HasFamilyTranscendence(source)&&source.definition.familyId=="beetle")
            foreach(Tile tile in tiles.Values)
                if(!tile.bench&&tile.walkable&&tile.deploymentTeam!=source.team)beetleOverturnPreviewTiles.Add(tile.coordinate);
        foreach(var coordinate in previous)if(tiles.TryGetValue(coordinate,out Tile tile))RefreshPlacementMarker(tile);
        foreach(var coordinate in beetleOverturnPreviewTiles)RefreshPlacementMarker(tiles[coordinate]);
    }
}
