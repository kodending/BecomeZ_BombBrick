using UnityEngine;

public static class ConstrainedCcdIkSolver
{
    public static float Solve(
        Transform[] joints,
        Transform endBone,
        Vector3 endLocalPoint,
        Vector3 target,
        Quaternion[] animationLocalRotations,
        float[] maximumCorrectionAngles,
        int iterations=3,
        float weight=1f)
    {
        if(joints==null||joints.Length==0||endBone==null)return float.PositiveInfinity;
        if(animationLocalRotations==null||animationLocalRotations.Length!=joints.Length)
            return float.PositiveInfinity;
        weight=Mathf.Clamp01(weight);
        iterations=Mathf.Clamp(iterations,1,12);

        for(int iteration=0;iteration<iterations;iteration++)
        {
            for(int index=joints.Length-1;index>=0;index--)
            {
                Transform joint=joints[index];
                if(joint==null)continue;
                Vector3 end=endBone.TransformPoint(endLocalPoint);
                Vector3 toEnd=end-joint.position;
                Vector3 toTarget=target-joint.position;
                if(toEnd.sqrMagnitude<0.0000001f||toTarget.sqrMagnitude<0.0000001f)continue;

                Quaternion delta=Quaternion.FromToRotation(toEnd,toTarget);
                Quaternion desiredWorld=delta*joint.rotation;
                Quaternion desiredLocal=joint.parent==null
                    ?desiredWorld:Quaternion.Inverse(joint.parent.rotation)*desiredWorld;
                float limit=maximumCorrectionAngles!=null&&index<maximumCorrectionAngles.Length
                    ?Mathf.Max(0f,maximumCorrectionAngles[index]):180f;
                Quaternion constrained=ClampCorrection(
                    animationLocalRotations[index],desiredLocal,limit);
                joint.localRotation=Quaternion.Slerp(joint.localRotation,constrained,weight);
            }
            if(Vector3.SqrMagnitude(endBone.TransformPoint(endLocalPoint)-target)<0.000001f)break;
        }
        return Vector3.Distance(endBone.TransformPoint(endLocalPoint),target);
    }

    public static Quaternion ClampCorrection(
        Quaternion animationLocalRotation,Quaternion desiredLocalRotation,float maximumAngle)
    {
        if(maximumAngle<=0f)return animationLocalRotation;
        return Quaternion.RotateTowards(
            animationLocalRotation,desiredLocalRotation,Mathf.Min(180f,maximumAngle));
    }
}
