using System;
using System.Collections.Generic;
using UnityEngine;

// Follow after the caster animation and late skill contacts have updated its state.
[DefaultExecutionOrder(10050)]
public sealed class CombatCameraDirector : MonoBehaviour
{
    private sealed class Performer
    {
        public string id;
        public float totalDamage;
        public float recentDamage;
        public Vector3 sourcePosition;
        public Vector3 targetPosition;
        public bool defeated;
    }

    private const float SelectionInterval = 0.3f;
    private const float MinimumFocusDuration = 2f;
    private const float FocusSwitchAdvantage = 1.2f;
    private const float RecentDamageDecaySeconds = 5f;
    private const float PerformerZoom = 0.8f;
    private const float MaximumHorizontalShift = 0.045f;
    private const float MaximumVerticalShift = 0.028f;
    private const float FocusSmoothTime = 0.38f;
    private const float ZoomSmoothTime = 0.3f;
    private readonly Dictionary<string, Performer> performers =
        new Dictionary<string, Performer>(StringComparer.Ordinal);
    private Camera battleCamera;
    private bool battleActive;
    private bool automaticFraming;
    private float homeFieldOfView;
    private Vector2 homeLensShift;
    private Vector2 lensShiftVelocity;
    private float fieldOfViewVelocity;
    private string performerFocusId = "";
    private float performerFocusLockedUntil;
    private float performerFocusEligibleAt;
    private float nextSelectionAt;
    private Vector3 eventPosition;
    private string eventActorId = "";
    private int eventPriority;
    private float eventUntil;
    private float eventFocusEligibleAt;
    private float eventZoom;
    private float shakeStartedAt;
    private float shakeUntil;
    private float shakeAmplitudePixels;
    private Vector2 shakeDirection = Vector2.right;
    private Vector2 appliedShakeOffset;
    private float shakeStrength = 1f;
    private float nextBasicAttackShakeAt;
    private float sabertoothBiteShakeUntil;
    private float heavyGroundSlamShakeUntil;
    private float crocodileAbyssShakeUntil;
    private SpriteRenderer anticipationPanSource;
    private Func<bool> anticipationSourceActive;
    private Vector2 anticipationPanOffset,appliedAnticipationPanOffset;
    private float anticipationZoomAmount,appliedAnticipationZoom=1f;
    private bool anticipationPanReturning,projectionApplied,presentationProjectionAutomatic;
    private Matrix4x4 presentationProjectionBase;

    // Existing cast ownership remains available to impact-shake and completion bookkeeping.
    public bool AnticipationPanActive=>anticipationPanSource!=null;
    public Vector3 AnticipationPanOffset=>new Vector3(anticipationPanOffset.x,anticipationPanOffset.y,0f);
    public Vector2 AppliedAnticipationPanOffset=>appliedAnticipationPanOffset;
    public float AppliedAnticipationZoom=>appliedAnticipationZoom;

    // Retained for older editor diagnostics; combat emphasis never owns the game clock.
    public bool SkillFocusActive=>false;
    public bool SkillFocusExecuting=>false;
    public float SkillFocusPlaybackSpeed=>Time.timeScale;
    public bool SkillFocusCastReady=>false;
    public float SkillFocusStrength=>0f;
    public bool BeginSkillFocus()=>false;
    public void CancelSkillFocus() { }
    public void BeginSkillExecution() { }
    public static void PrepareForGameplayPause()
    {
        MonsterAutoBattlePrototype.PrepareForAllyPreparationGameplayPause();
        MonsterAutoBattlePrototype.PrepareForRaptorCinematicGameplayPause();
    }

    public void PrepareForCinematic()
    {
        CancelAnticipationPan(true);
        ClearShakeProjection();
    }

    public string CurrentFocusActorId =>
        Time.unscaledTime < eventUntil
            ? Time.unscaledTime >= eventFocusEligibleAt ? eventActorId : ""
            : Time.unscaledTime >= performerFocusEligibleAt
                ? performerFocusId
                : "";

    public void Initialize(Camera camera,bool automaticFraming=true)
    {
        CancelAnticipationPan(true);
        battleCamera = camera;
        this.automaticFraming=automaticFraming;
        homeFieldOfView = camera == null ? MonsterAutoBattlePrototype.CameraFieldOfView : camera.fieldOfView;
        homeLensShift = camera == null ? Vector2.zero : camera.lensShift;
        ResetPresentation();
        SetShakeStrength(BombBrick.Game.Core.GameUserSettings.Load().CameraShakeScale);
    }

    public void SetShakeStrength(float strength)
    {
        shakeStrength = Mathf.Clamp01(strength);
        ClearShakeProjection();
        if (shakeStrength <= 0f) {shakeUntil=0f;sabertoothBiteShakeUntil=0f;heavyGroundSlamShakeUntil=0f;crocodileAbyssShakeUntil=0f;}
    }

    public void BeginBattle()
    {
        if (battleCamera == null) return;
        CancelAnticipationPan(true);
        performers.Clear();
        performerFocusId = "";
        performerFocusLockedUntil = 0f;
        performerFocusEligibleAt = 0f;
        nextSelectionAt = 0f;
        eventActorId = "";
        eventPriority = 0;
        eventUntil = 0f;
        eventFocusEligibleAt = 0f;
        eventZoom = 0f;
        shakeUntil = 0f;
        sabertoothBiteShakeUntil=0f;
        nextBasicAttackShakeAt = 0f;
        heavyGroundSlamShakeUntil=0f;
        crocodileAbyssShakeUntil=0f;
        battleActive = true;
    }

    public void EndBattle()
    {
        battleActive = false;
        CancelAnticipationPan(true);
        ClearShakeProjection();
        performers.Clear();
        performerFocusId = "";
        eventActorId = "";
        eventUntil = 0f;
        if(Time.unscaledTime>=Mathf.Max(crocodileAbyssShakeUntil,Mathf.Max(sabertoothBiteShakeUntil,heavyGroundSlamShakeUntil)))shakeUntil=0f;
    }

    public bool TryBeginAnticipationPan(SpriteRenderer source,bool alliedActor,Func<bool> sourceIsActive=null)
    {
        if(!battleActive||battleCamera==null||!alliedActor||source==null||!source.enabled||
           !source.gameObject.activeInHierarchy||source.sprite==null||
           sourceIsActive!=null&&!sourceIsActive())return false;
        if(anticipationPanSource!=null&&anticipationSourceActive!=null&&!anticipationSourceActive())
            CancelAnticipationPan();
        if(anticipationPanSource!=null)return false;
        anticipationPanSource=source;
        anticipationSourceActive=sourceIsActive;
        anticipationPanReturning=false;
        return true;
    }

    public void EndAnticipationPan(SpriteRenderer source)
    {
        if(anticipationPanSource!=source)return;
        CancelAnticipationPan();
    }

    public void CancelAnticipationPan(bool immediate=false)
    {
        bool hadPan=anticipationPanSource!=null||anticipationPanReturning||anticipationPanOffset!=Vector2.zero||anticipationZoomAmount!=0f;
        anticipationPanSource=null;
        anticipationSourceActive=null;
        anticipationPanReturning=hadPan;
        if(!immediate)return;
        if(hadPan||appliedAnticipationPanOffset!=Vector2.zero)ClearShakeProjection();
        anticipationPanOffset=Vector2.zero;
        anticipationZoomAmount=0f;
        anticipationPanReturning=false;
    }

    public void ReportDamage(
        string actorId, bool alliedActor, Vector3 sourcePosition,
        Vector3 targetPosition, float damage)
    {
        if (!battleActive || !automaticFraming || !alliedActor || string.IsNullOrEmpty(actorId) || damage <= 0f)
            return;
        if (!performers.TryGetValue(actorId, out Performer performer))
        {
            performer = new Performer { id = actorId };
            performers.Add(actorId, performer);
        }
        performer.totalDamage += damage;
        performer.recentDamage += damage;
        performer.sourcePosition = sourcePosition;
        performer.targetPosition = targetPosition;
        performer.defeated = false;
        if (string.IsNullOrEmpty(performerFocusId))
            SelectPerformer(performer, Time.unscaledTime);
    }

    public void ReportSkill(
        string actorId, bool alliedActor, Vector3 sourcePosition,
        Vector3 targetPosition)
    {
        if (!battleActive) return;
        RequestEventFocus(
            alliedActor ? actorId : "",
            Vector3.Lerp(sourcePosition, targetPosition, 0.58f),
            2, 0.52f, 1.45f, 0f, targetPosition - sourcePosition);
    }

    public void ReportStrongCritical(
        string actorId, bool alliedActor, Vector3 sourcePosition,
        Vector3 targetPosition)
    {
        if (!battleActive) return;
        RequestEventFocus(
            alliedActor ? actorId : "",
            Vector3.Lerp(sourcePosition, targetPosition, 0.72f),
            1, 0.28f, 1.05f, 2.5f, targetPosition - sourcePosition);
    }

    public void ReportGroundSlam(bool heavy=false)
    {
        if(!battleActive)return;
        RequestShake(heavy?12f:10f,Vector3.down,heavy?0.26f:0.22f);
    }

    public void ReportSabertoothDevourBite()
    {
        // Damage may end the battle immediately before or after this final bite lands.
        if(battleCamera!=null&&RequestShake(18f,Vector3.down,.28f))sabertoothBiteShakeUntil=shakeUntil;
    }

    public void ReportFrogGrandSlam()
        =>ReportHeavyGroundSlam(16f,.28f);

    public void ReportTyrannoGodStomp()
        =>ReportHeavyGroundSlam(18f,.30f);

    public void ReportMonkeyStormThunder()
        =>ReportHeavyGroundSlam(14f,.24f);

    private void ReportHeavyGroundSlam(float pixels,float duration)
    {
        if(battleCamera!=null&&RequestShake(pixels,Vector3.down,duration))heavyGroundSlamShakeUntil=shakeUntil;
    }

    public void ReportCrocodileAbyssCrush()
    {
        if(battleCamera!=null&&RequestShake(18f,Vector3.down,.24f))crocodileAbyssShakeUntil=shakeUntil;
    }

    public void ReportBasicAttackImpact(Vector3 direction)
    {
        float now = Time.unscaledTime;
        if (!battleActive || shakeStrength <= 0f || now < nextBasicAttackShakeAt ||
            (now < shakeUntil && shakeAmplitudePixels > 2f)) return;
        nextBasicAttackShakeAt = now + 0.18f;
        RequestShake(2f, direction, 0.08f);
    }

    public void ReportMarchFootstep()
    {
        if(battleActive)RequestShake(6f,Vector3.down,.12f);
    }

    public void ReportPressureBreath(Vector3 direction)
    {
        if(battleActive)RequestShake(6f,direction,.12f);
    }

    public void ReportMonkeyFruitImpact(bool heavy=false)
    {
        if(battleActive)RequestShake(heavy?6f:4.5f,Vector3.down,heavy?.16f:.12f);
    }

    public void ReportGriffinLanding()
    {
        if(battleActive)RequestShake(7f,Vector3.down,.15f);
    }

    public void ReportHaetaeThunder()
    {
        if(battleActive)RequestShake(6f,Vector3.down,.14f);
    }

    public void ReportJawCrush()
    {
        if(!battleActive)return;
        RequestShake(6f,Vector3.down,0.16f);
    }

    public void ReportSilkLanceImpact(bool final)
    {
        // The tip lands just after damage, including a killing blow. No focus/zoom event.
        if(battleCamera==null)return;
        RequestShake(final?11f:3.5f,Vector3.down,final?.22f:.065f);
    }

    public void ReportSpacetimeCollapse()
    {
        if(battleCamera!=null)RequestShake(11f,Vector3.down,.20f);
    }

    public void ReportBearAvatarRoar()
    {
        if(!battleActive)return;
        RequestShake(14f,Vector3.down,.34f);
    }

    public void ReportExpulsion(string defeatedId)
    {
        // Ring-out already has its own short shake; no kill zoom or slow motion.
        if(!string.IsNullOrEmpty(defeatedId)&&performers.TryGetValue(defeatedId,out Performer defeated))
            defeated.defeated=true;
    }

    public void ReportDeath(
        string killerId, bool killerIsAlly, string defeatedId,
        bool defeatedIsEnemy, Vector3 killerPosition, Vector3 defeatedPosition)
    {
        if (!battleActive) return;
        if (!string.IsNullOrEmpty(defeatedId) &&
            performers.TryGetValue(defeatedId, out Performer defeated))
            defeated.defeated = true;
        RequestEventFocus(
            killerIsAlly ? killerId : "", defeatedPosition,
            3, 0.58f, 1.75f, 0f, defeatedPosition - killerPosition);
    }

    public static float DamageFocusScore(float totalDamage, float recentDamage) =>
        Mathf.Max(0f, totalDamage) * 0.6f + Mathf.Max(0f, recentDamage) * 0.4f;

    public static bool ShouldSwitchFocus(
        float currentScore, float candidateScore, bool focusLockExpired) =>
        focusLockExpired && candidateScore > 0f &&
        (currentScore <= 0f ||
         candidateScore + 0.001f >= currentScore * FocusSwitchAdvantage);

    public static bool ShouldSlowFocusedKill(
        string currentFocusActorId, string killerId,
        bool killerIsAlly, bool defeatedIsEnemy) =>
        killerIsAlly && defeatedIsEnemy &&
        !string.IsNullOrEmpty(currentFocusActorId) &&
        string.Equals(currentFocusActorId, killerId, StringComparison.Ordinal);

    private void LateUpdate()
    {
        if (battleCamera == null) return;
        ClearShakeProjection();
        float now = Time.unscaledTime;
        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        UpdateAnticipationPan(dt);
        if (!battleActive)
        {
            RestoreCamera(dt);
            ApplyShakeProjection(now);
            return;
        }

        float decay = Mathf.Exp(-dt / RecentDamageDecaySeconds);
        foreach (Performer performer in performers.Values)
            performer.recentDamage *= decay;
        if (now >= nextSelectionAt)
        {
            nextSelectionAt = now + SelectionInterval;
            ReevaluatePerformer(now);
        }

        Vector3 focusPosition = Vector3.zero;
        float zoom = 0f;
        bool hasFocus = false;
        if (now < eventUntil)
        {
            focusPosition = eventPosition;
            zoom = eventZoom;
            hasFocus = true;
        }
        else
        {
            eventActorId = "";
            eventPriority = 0;
            if (performers.TryGetValue(performerFocusId, out Performer performer) &&
                !performer.defeated)
            {
                focusPosition = Vector3.Lerp(
                    performer.sourcePosition, performer.targetPosition, 0.5f);
                zoom = PerformerZoom;
                hasFocus = true;
            }
        }

        Vector2 desiredShift = hasFocus ? FocusLensShift(focusPosition) : homeLensShift;
        desiredShift.x = Mathf.Clamp(
            desiredShift.x, homeLensShift.x - MaximumHorizontalShift,
            homeLensShift.x + MaximumHorizontalShift);
        desiredShift.y = Mathf.Clamp(
            desiredShift.y, homeLensShift.y - MaximumVerticalShift,
            homeLensShift.y + MaximumVerticalShift);
        desiredShift = PixelSnap(desiredShift);
        battleCamera.lensShift = Vector2.SmoothDamp(
            battleCamera.lensShift, desiredShift, ref lensShiftVelocity,
            FocusSmoothTime, Mathf.Infinity, dt);
        battleCamera.fieldOfView = Mathf.SmoothDamp(
            battleCamera.fieldOfView, homeFieldOfView - zoom,
            ref fieldOfViewVelocity, ZoomSmoothTime, Mathf.Infinity, dt);
        ApplyShakeProjection(now);
    }

    private void UpdateAnticipationPan(float dt)
    {
        if(anticipationPanSource!=null&&(!battleActive||!anticipationPanSource.enabled||
           !anticipationPanSource.gameObject.activeInHierarchy||anticipationPanSource.sprite==null||
           anticipationSourceActive!=null&&!anticipationSourceActive()))
            CancelAnticipationPan();
        // Keep cast ownership for impact shakes and effect completion without reframing the battle.
        anticipationPanOffset=Vector2.zero;
        anticipationZoomAmount=0f;
        anticipationPanReturning=false;
    }

    private void ApplyShakeProjection(float now)
    {
        ClearShakeProjection();
        // Impact shake translates the complete image without a skill pan or zoom.
        appliedShakeOffset=PixelSnap(ShakeOffset(now));
        appliedAnticipationPanOffset=Vector2.zero;
        appliedAnticipationZoom=1f;
        if(appliedShakeOffset!=Vector2.zero)
        {
            presentationProjectionBase=battleCamera.projectionMatrix;
            battleCamera.ResetProjectionMatrix();
            Matrix4x4 automatic=battleCamera.projectionMatrix;
            presentationProjectionAutomatic=true;
            for(int i=0;i<16;i++)
                if(Mathf.Abs(automatic[i]-presentationProjectionBase[i])>.000001f)
                {presentationProjectionAutomatic=false;break;}
            battleCamera.projectionMatrix=Matrix4x4.Translate(
                new Vector3(appliedShakeOffset.x*2f,appliedShakeOffset.y*2f,0f))*presentationProjectionBase;
            projectionApplied=true;
        }
    }

    private void ReevaluatePerformer(float now)
    {
        Performer current = null;
        performers.TryGetValue(performerFocusId, out current);
        if (current != null && current.defeated) current = null;
        Performer best = null;
        float bestScore = 0f;
        foreach (Performer candidate in performers.Values)
        {
            if (candidate.defeated) continue;
            float score = DamageFocusScore(
                candidate.totalDamage, candidate.recentDamage);
            if (score <= bestScore) continue;
            best = candidate;
            bestScore = score;
        }
        if (best == null) return;
        if (current == null)
        {
            SelectPerformer(best, now);
            return;
        }
        if (best == current) return;
        float currentScore = DamageFocusScore(
            current.totalDamage, current.recentDamage);
        if (ShouldSwitchFocus(
                currentScore, bestScore, now >= performerFocusLockedUntil))
            SelectPerformer(best, now);
    }

    private void SelectPerformer(Performer performer, float now)
    {
        performerFocusId = performer.id;
        performerFocusLockedUntil = now + MinimumFocusDuration;
        performerFocusEligibleAt = now + FocusSmoothTime;
    }

    private void RequestEventFocus(
        string actorId, Vector3 position, int priority, float duration,
        float zoom, float shakePixels, Vector3 impactDirection)
    {
        if(!automaticFraming)return;
        float now = Time.unscaledTime;
        if (now < eventUntil && priority < eventPriority) return;
        eventActorId = actorId ?? "";
        eventPosition = position;
        eventPriority = priority;
        eventUntil = now + duration;
        eventFocusEligibleAt = now + 0.12f;
        eventZoom = zoom;
        RequestShake(shakePixels,impactDirection);
    }

    private bool RequestShake(float shakePixels,Vector3 impactDirection,float duration=0.14f)
    {
        float now=Time.unscaledTime;
        if (shakeStrength <= 0f || shakePixels <= 0f ||
            (now < shakeUntil - 0.04f && shakePixels <= shakeAmplitudePixels)) return false;
        Vector3 screenDirection = battleCamera == null
            ? impactDirection
            : battleCamera.transform.InverseTransformDirection(impactDirection);
        shakeDirection = new Vector2(screenDirection.x, screenDirection.y).normalized;
        if (shakeDirection.sqrMagnitude < 0.01f) shakeDirection = Vector2.right;
        shakeAmplitudePixels = shakePixels;
        shakeStartedAt = now;
        shakeUntil = shakeStartedAt + duration;
        sabertoothBiteShakeUntil=0f;
        heavyGroundSlamShakeUntil=0f;
        crocodileAbyssShakeUntil=0f;
        return true;
    }

    private Vector2 FocusLensShift(Vector3 worldPosition)
    {
        Vector3 local = battleCamera.transform.InverseTransformPoint(worldPosition);
        if (local.z <= 0.01f) return homeLensShift;
        float halfHeight = local.z * Mathf.Tan(
            battleCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * Mathf.Max(0.01f, battleCamera.aspect);
        return homeLensShift + new Vector2(
            local.x / Mathf.Max(0.01f, halfWidth) * 0.18f,
            local.y / Mathf.Max(0.01f, halfHeight) * 0.12f);
    }

    private Vector2 ShakeOffset(float now)
    {
        if (now >= shakeUntil || shakeUntil <= shakeStartedAt) return Vector2.zero;
        float progress = Mathf.InverseLerp(shakeStartedAt, shakeUntil, now);
        float envelope = (1f - progress) * (1f - progress);
        Vector2 perpendicular = new Vector2(-shakeDirection.y, shakeDirection.x);
        Vector2 pixels = shakeDirection *
                         (Mathf.Sin(progress * Mathf.PI * 5f) * shakeAmplitudePixels) +
                         perpendicular *
                         (Mathf.Sin(progress * Mathf.PI * 8f) * shakeAmplitudePixels * 0.28f);
        pixels *= envelope * shakeStrength;
        return new Vector2(
            pixels.x / Mathf.Max(1, Screen.width),
            pixels.y / Mathf.Max(1, Screen.height));
    }

    private static Vector2 PixelSnap(Vector2 shift) => new Vector2(
        Mathf.Round(shift.x * Mathf.Max(1, Screen.width)) /
        Mathf.Max(1, Screen.width),
        Mathf.Round(shift.y * Mathf.Max(1, Screen.height)) /
        Mathf.Max(1, Screen.height));

    private void RestoreCamera(float dt)
    {
        battleCamera.lensShift = Vector2.SmoothDamp(
            battleCamera.lensShift, homeLensShift, ref lensShiftVelocity,
            0.2f, Mathf.Infinity, dt);
        battleCamera.fieldOfView = Mathf.SmoothDamp(
            battleCamera.fieldOfView, homeFieldOfView,
            ref fieldOfViewVelocity, 0.2f, Mathf.Infinity, dt);
    }

    private void ResetPresentation()
    {
        battleActive = false;
        if(sabertoothBiteShakeUntil>0f||heavyGroundSlamShakeUntil>0f||crocodileAbyssShakeUntil>0f)shakeUntil=0f;
        sabertoothBiteShakeUntil=0f;
        heavyGroundSlamShakeUntil=0f;
        crocodileAbyssShakeUntil=0f;
        CancelAnticipationPan(true);
        ClearShakeProjection();
        if (battleCamera != null)
        {
            battleCamera.lensShift = homeLensShift;
            battleCamera.fieldOfView = homeFieldOfView;
        }
    }

    private void ClearShakeProjection()
    {
        if(battleCamera!=null&&projectionApplied)
        {
            if(presentationProjectionAutomatic)battleCamera.ResetProjectionMatrix();
            else battleCamera.projectionMatrix=presentationProjectionBase;
        }
        projectionApplied=false;
        appliedShakeOffset=Vector2.zero;
        appliedAnticipationPanOffset=Vector2.zero;
        appliedAnticipationZoom=1f;
    }

    private void OnDisable() => ResetPresentation();
    private void OnDestroy() => ResetPresentation();
}
