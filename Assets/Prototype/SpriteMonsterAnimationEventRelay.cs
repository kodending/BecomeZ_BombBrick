using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class SpriteMonsterAnimationEventRelay : MonoBehaviour
{
    public const string BasicAttackHitEventName=
        nameof(OnBasicAttackHitFrame);
    public const string SkillAttackHitEventName=
        nameof(OnSkillAttackHitFrame);

    [SerializeField] private SpriteMonsterAnimationDriver driver;

    public SpriteMonsterAnimationDriver Driver => driver;

    public void Bind(SpriteMonsterAnimationDriver value)
    {
        driver=value;
    }

    private void Awake()
    {
        ResolveDriver();
    }

    public void OnBasicAttackHitFrame()
    {
        ResolveDriver();
        if(driver!=null)
            driver.NotifyBasicAttackHitFrameFromAnimation();
    }

    public void OnSkillAttackHitFrame()
    {
        ResolveDriver();
        if(driver!=null)
            driver.NotifySkillAttackHitFrameFromAnimation();
    }

    private void ResolveDriver()
    {
        if(driver==null)
            driver=GetComponentInChildren<SpriteMonsterAnimationDriver>(true);
    }
}
