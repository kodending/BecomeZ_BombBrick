using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private sealed class BeetleGravityVictim
    {
        public Unit unit;
        public BeetleGravityField field;
        public int revision;
        public float phase;
    }

    private sealed class BeetleGravityField
    {
        public CombatSkillClock skillClock=CombatSkillClock.Capture();
        public Unit source,primary;
        public int generation;
        public Vector3 center;
        public float radius,scale,time;
        public bool landed,humming;
        public System.Action cameraEffectCompleted;
        public void CompleteCameraEffect(){var completed=cameraEffectCompleted;cameraEffectCompleted=null;completed?.Invoke();}
        public readonly List<BeetleGravityVictim> victims=new List<BeetleGravityVictim>();
        public PixelBeetleGravity effect;
    }

    private readonly List<BeetleGravityField> beetleGravityFields=new List<BeetleGravityField>();

    private bool TryResolveBeetleRank3(Unit source,Unit target,SkillMasterRow skill,
        float damageScale,float radiusScale,int generation)
    {
        if(HasFamilyTranscendence(source)||!HasRank3(source,BeetleRank3Rules.ZeroGravity)||!IsCombatActive(target))return false;
        var field=new BeetleGravityField{source=source,primary=target,generation=generation,
            center=Flat(target.model.transform.position),scale=damageScale,
            radius=TileSize*BeetleRank3Rules.GravityRadiusTiles*radiusScale,
            cameraEffectCompleted=HoldSkillCameraEffect(source)};
        beetleGravityFields.Add(field);
        foreach(Unit enemy in Rank3Enemies(source,field.center,field.radius))
        {
            var victim=new BeetleGravityVictim{unit=enemy,field=field,phase=field.victims.Count*2.4f};
            field.victims.Add(victim);
            if(enemy.beetleGravityHeld==null&&enemy.beetleFlippedBy==null&&!PreyDisplacementImmune(enemy)&&
               !enemy.combatStatuses.Has(CombatStatusKind.Airborne)&&
               !enemy.combatStatuses.Has(CombatStatusKind.Freeze)&&!enemy.combatStatuses.Has(CombatStatusKind.Knockdown))
            {
                InterruptPositionalAction(enemy);ClearRoute(enemy);
                enemy.hexStepping=false;enemy.velocity=Vector3.zero;
                var status=enemy.combatStatuses.ApplyTimed(CombatStatusKind.Airborne,
                    BeetleRank3Rules.GravityLanding+.05f,source,BeetleRank3Rules.GravityLanding+.05f);
                if(status.remaining>0f)
                {
                    enemy.beetleGravityHeld=victim;
                    SetMoving(enemy,false);enemy.spriteAnimation?.SetStruggling(true);
                }
            }
            victim.revision=enemy.positionRevision;
        }
        if(!mlEpisodeActive)
        {
            field.effect=PixelBeetleGravity.Play(field.center,field.radius,Rank3Renderer(source));
            field.effect.transform.SetParent(transform,true);
        }
        source.traitActivations++;
        PlayBeetleSound(source,BeetleSound.Lift,field.center,.85f);
        return true;
    }

    private void UpdateBeetleGravityFields(float dt)
    {
        for(int i=beetleGravityFields.Count-1;i>=0;i--)
        {
            if(i>=beetleGravityFields.Count)continue;
            var field=beetleGravityFields[i];
            if(PausedBySkillTime(field.skillClock))continue;
            if(!ReleasedSkillValid(field.source,field.generation))
            {EndBeetleGravity(field);continue;}
            field.time+=dt;
            if(field.time>=BeetleRank3Rules.GravityFall)StopBeetleGravitySound(field);
            else if(!field.humming&&field.time>=BeetleRank3Rules.GravityRise)
            {
                // Overlapping fields from one caster share one sustained voice.
                if(!beetleGravityFields.Exists(other=>other.source==field.source&&other.humming))
                    PlayBeetleSound(field.source,BeetleSound.Hover,field.center,.4f);
                field.humming=true;
            }
            bool landing=field.time>=BeetleRank3Rules.GravityLanding;
            foreach(var victim in field.victims)
            {
                Unit enemy=victim.unit;
                if(enemy.beetleGravityHeld!=victim)continue;
                if(!IsCombatActive(enemy)||enemy.positionRevision!=victim.revision||PreyDisplacementImmune(enemy)||
                   enemy.combatStatuses.Get(CombatStatusKind.Airborne)?.source!=field.source||landing)
                {ReleaseBeetleGravityVictim(enemy);continue;}
                float rise=Mathf.Clamp01(field.time/BeetleRank3Rules.GravityRise);
                float fall=Mathf.Clamp01((field.time-BeetleRank3Rules.GravityFall)/BeetleRank3Rules.GravityDrop);
                float lift=Mathf.Sin(rise*Mathf.PI*.5f)*(1f-fall);
                float height=TileSize*(1.15f+.10f*Mathf.Sin(field.time*3f+victim.phase))*lift;
                float roll=(155f+7f*Mathf.Sin(field.time*2f+victim.phase))*lift;
                enemy.spriteBillboard?.SetRollPose(roll,new Vector3(0,.5f,0),Vector3.up*height);
            }
            field.effect?.Pose(field.time);
            if(landing&&!field.landed)
            {
                field.landed=true;
                PlayBeetleSound(field.source,BeetleSound.GravityLand,field.center);
                Unit lightningPrimary=null;
                foreach(var victim in field.victims)
                {
                    Unit enemy=victim.unit;
                    if(!IsCombatActive(enemy)||enemy.positionRevision!=victim.revision||
                       Vector3.Distance(field.center,Flat(enemy.model.transform.position))>field.radius+enemy.collisionRadius)continue;
                    if(lightningPrimary==null||enemy==field.primary)lightningPrimary=enemy;
                    Rank3Hit(field.source,enemy,BeetleRank3Rules.GravityFallRatio*field.scale);
                    if(HasRank3(field.source,BeetleCombatRules.JangpungTraitId))
                        Rank3Hit(field.source,enemy,BeetleCombatRules.HornDamageRatio*field.scale);
                    if(HasRank3(field.source,BeetleCombatRules.SolarbeetleTraitId))
                        Rank3Shred(enemy,BeetleCombatRules.SunHornShredRatio,BeetleCombatRules.SunHornDuration,field.source);
                    if(!mlEpisodeActive)
                    {
                        field.effect?.Land(Flat(enemy.model.transform.position),Rank3Center(enemy));
                        enemy.reaction?.PlayTintHit(new Color(.78f,.7f,1f));
                    }
                }
                // One intrinsic lightning chain per cast, never one per suspended enemy.
                if(lightningPrimary!=null)
                {
                    int hits;
                    float dealt=BeetleLightningDamage(field.source,lightningPrimary,field.center,field.scale,false,out hits);
                    field.source.skillHits+=hits;field.source.skillDamage+=dealt;
                }
                if(!mlEpisodeActive)combatCameraDirector?.ReportGroundSlam(false);
                RecordSkillEvent(field.source,BeetleRank3Rules.ZeroGravity,field.source.skillHits,field.source.skillDamage);
                field.CompleteCameraEffect();
            }
            if(field.time>=BeetleRank3Rules.GravityLanding+.35f)EndBeetleGravity(field);
        }
    }

    private static void ReleaseBeetleGravityVictim(Unit enemy)
    {
        var victim=enemy.beetleGravityHeld;if(victim==null)return;
        enemy.beetleGravityHeld=null;
        enemy.spriteBillboard?.SetRollPose(0,Vector3.zero,Vector3.zero);
        enemy.spriteAnimation?.SetStruggling(false);
        var status=enemy.combatStatuses.Get(CombatStatusKind.Airborne);
        if(status!=null&&status.source==victim.field.source)enemy.combatStatuses.Remove(CombatStatusKind.Airborne);
    }

    private void EndBeetleGravity(BeetleGravityField field)
    {
        field.CompleteCameraEffect();
        StopBeetleGravitySound(field);
        foreach(var victim in field.victims)
            if(victim.unit.beetleGravityHeld==victim)ReleaseBeetleGravityVictim(victim.unit);
        field.effect?.Cancel();beetleGravityFields.Remove(field);
    }

    private void StopBeetleGravitySound(BeetleGravityField field)
    {
        if(!field.humming)return;
        field.humming=false;
        if(!beetleGravityFields.Exists(other=>other.source==field.source&&other.humming))
            BombBrick.Game.Core.GameAudio.StopCombatLoop(field.source.id,beetleSkillSounds[(int)BeetleSound.Hover]);
    }
}
