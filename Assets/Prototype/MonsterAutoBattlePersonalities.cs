using UnityEngine;

public sealed partial class MonsterAutoBattlePrototype
{
    private bool CanQueuePersonalityReaction(Unit unit)
    {
        return IsCombatActive(unit)&&unit.targetLockTimer<=0f&&
            unit.personalityReconsiderTimer<=0f&&!HasActiveTaunt(unit)&&!HasPreyFocus(unit);
    }

    private bool QueuePersonalityTarget(Unit unit,Unit target,Unit guardedAlly=null)
    {
        if(!CanQueuePersonalityReaction(unit)||unit.personalityPendingTarget!=null&&
           unit.personalityPendingTimer>0f||!IsNearbyPersonalityAlternative(unit,target))return false;
        unit.personalityPendingTarget=target;
        unit.personalityPendingTimer=2f;
        unit.personalityGuardAlly=guardedAlly;
        return true;
    }

    private bool IsNearbyPersonalityAlternative(Unit unit,Unit target)
    {
        if(!IsLocalPersonalityTarget(unit,target)||!HasClearRoute(unit,target))return false;
        float distance=Vector3.Distance(Flat(unit.model.transform.position),Flat(target.model.transform.position));
        // A reaction may take at most one tile of extra approach during an existing engagement.
        return distance<=AttackDistance(unit,target)+TileSize;
    }

    private void UpdatePersonalityReaction(Unit unit)
    {
        if(HasActiveTaunt(unit)||HasPreyFocus(unit))
        {
            unit.personalityPendingTarget=null;unit.personalityGuardAlly=null;
            unit.personalityMoveTimer=0f;StopTimidRetreat(unit);
            return;
        }
        if(unit.personalityPendingTimer<=0f)
        {unit.personalityPendingTarget=null;unit.personalityGuardAlly=null;}
        if(!CanQueuePersonalityReaction(unit)||IsAttackMovementLocked(unit)||
           unit.griffinRootTimer>0f||unit.combatStatuses.Has(CombatStatusKind.Root))return;
        PersonalityCombatPolicy policy=unit.personalityPolicy??PersonalityCombatPolicy.Balanced();
        Unit pending=unit.personalityPendingTarget;
        if(pending!=null)
        {
            unit.personalityPendingTarget=null;unit.personalityPendingTimer=0f;
            if(!IsNearbyPersonalityAlternative(unit,pending))return;
            bool changed=unit.target!=pending;
            SetTarget(unit,pending);
            bool moved=policy.personalityId=="protective"&&
                TryStartPersonalityPosition(unit,unit.personalityGuardAlly);
            if(!changed&&!moved)return;
            if(policy.personalityId=="vengeful")unit.personalityDamageByAttacker.Remove(pending);
            unit.personalityReconsiderTimer=policy.personalityId=="protective"?6f:5f;
            ShowPersonalityCue(unit);
            return;
        }
        if(!IsCombatTargetable(unit.target))return;
        if(policy.personalityId=="cooperative")
        {
            unit.personalityReconsiderTimer=1f;
            Unit preferred=SelectTarget(unit,true);
            int currentSignal=PersonalityTargetSignal(unit,unit.target,PersonalityTargetStyle.Cooperative);
            if(currentSignal==0&&preferred!=unit.target&&preferred!=null&&
               PersonalityTargetSignal(unit,preferred,PersonalityTargetStyle.Cooperative)>0&&
               IsNearbyPersonalityAlternative(unit,preferred))SetTarget(unit,preferred);
            if(PersonalityTargetSignal(unit,unit.target,PersonalityTargetStyle.Cooperative)>0&&
               IsNearbyPersonalityAlternative(unit,unit.target))
            {unit.personalityReconsiderTimer=5f;ShowPersonalityCue(unit);}
        }
        else if(policy.personalityId=="bold")
        {
            unit.personalityReconsiderTimer=1f;
            if(TryStartPersonalityPosition(unit,null))
            {unit.personalityReconsiderTimer=6f;ShowPersonalityCue(unit);}
        }
    }

    private bool TryStartPersonalityPosition(Unit unit,Unit guardedAlly)
    {
        if(!TryFindPersonalityAttackPosition(unit,unit.target,guardedAlly,out Vector3 position))return false;
        unit.personalityMovePosition=position;unit.personalityMoveTimer=1.6f;
        ReleaseAttackSlot(unit);ClearRoute(unit);
        return true;
    }

    private bool TryFindPersonalityAttackPosition(Unit unit,Unit target,Unit guardedAlly,out Vector3 result)
    {
        result=Vector3.zero;
        if(!IsNearbyPersonalityAlternative(unit,target)||
           unit.personalityPolicy.personalityId=="protective"&&!IsCombatActive(guardedAlly))return false;
        Vector3 origin=Flat(unit.model.transform.position),center=Flat(target.model.transform.position);
        float minimum=CombatBodyRadius(unit)+CombatBodyRadius(target)+.54f;
        float radius=IsRangedUnit(unit)
            ?Mathf.Max(minimum,Mathf.Min(Vector3.Distance(origin,center)-TileSize*.65f,AttackDistance(unit,target)-.08f))
            :MeleeArrivalDistance(unit,target);
        bool guarding=IsCombatActive(guardedAlly);
        Vector3 anchor=guarding?Flat(guardedAlly.model.transform.position):center;
        float bestScore=Vector3.Distance(origin,anchor)-TileSize*.15f;
        Vector3 best=origin;
        for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.PI*2f/16f;
            Vector3 position=center+new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle))*radius;
            float step=Vector3.Distance(origin,position);
            if(step>TileSize||step<TileSize*.12f||
               Vector3.Distance(position,center)>AttackDistance(unit,target)||
               !IsOpenAttackPosition(unit,target,position)||
               !MeleeSlotSegmentClear(unit,target,origin,position))continue;
            float score=Vector3.Distance(position,anchor)+step*.25f;
            if(score>=bestScore)continue;
            bestScore=score;best=position;
        }
        if(best==origin)return false;
        result=best;
        return true;
    }

    private bool IsPersonalityPositioning(Unit unit)
    {
        if(unit.personalityMoveTimer<=0f)return false;
        Vector3 position=Flat(unit.model.transform.position);
        if(!IsCombatTargetable(unit.target)||HasActiveTaunt(unit)||HasPreyFocus(unit)||
           Vector3.Distance(position,unit.personalityMovePosition)<=.12f||
           !MeleeSlotSegmentClear(unit,unit.target,position,unit.personalityMovePosition))
        {unit.personalityMoveTimer=0f;return false;}
        return true;
    }

    private void ShowPersonalityCue(Unit unit)
    {
        if(unit.personalityCueCooldown>0f||unit.personalityPolicy==null)return;
        string id=unit.personalityPolicy.personalityId;
        Color color;
        switch(id)
        {
            case "vengeful":color=new Color(1f,.5f,.4f);break;
            case "fickle":color=new Color(.9f,.6f,1f);break;
            case "cooperative":color=new Color(.45f,1f,.9f);break;
            case "protective":color=new Color(1f,.88f,.4f);break;
            case "bold":color=new Color(1f,.7f,.35f);break;
            case "timid":color=new Color(.6f,.8f,1f);break;
            default:return;
        }
        unit.personalityCueCooldown=5f;
        AddCombatFloatText(unit,GameLocalization.TextByKey("combat.personality."+id,"!"),
            color,true,1.1f,personalityNotice:true);
    }
}
