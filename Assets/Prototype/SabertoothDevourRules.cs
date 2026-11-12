public static class SabertoothDevourRules
{
    public const string TraitId="trait_transcend_sabertooth_absolute_predation";
    public const string DisplayName="대포식";
    public const float InhaleDuration=.45f;
    public const float PullDuration=1.1f;
    public const float HoldDuration=.65f;
    public const float SnapDuration=.1f;
    public const float RecoveryDuration=.36f;
    public const float BiteClampHold=.11f;
    public const float PullStart=InhaleDuration;
    public const float PullEnd=PullStart+PullDuration;
    public const float SnapStart=PullEnd+HoldDuration;
    public const float HitTime=SnapStart+SnapDuration*.55f;
    public const float Duration=SnapStart+SnapDuration+RecoveryDuration;
    public const float DamageRatio=1.8f;
}
