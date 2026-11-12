using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight rigid-bone locomotion for the voxel tyrannosaurus test model.
/// It keeps the authored bind pose and adds a small walk cycle without requiring
/// an Animator Controller or modifying the imported FBX.
/// </summary>
public sealed class VoxelTyrannosaurusProceduralMotion : MonoBehaviour
{
    private sealed class BonePose
    {
        public Transform transform;
        public Quaternion bindRotation;
    }

    private readonly Dictionary<Transform, BonePose> poses = new Dictionary<Transform, BonePose>();
    private readonly List<BonePose> tail = new List<BonePose>();
    private BonePose spine;
    private BonePose neck;
    private BonePose head;
    private BonePose leftFoot;
    private BonePose rightFoot;
    private BonePose leftLeg;
    private BonePose rightLeg;
    private float phase;
    private float hitPulse;
    private float groundOffset;

    public float GroundOffset => groundOffset;

    public void Initialize()
    {
        poses.Clear();
        tail.Clear();
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == transform) continue;
            BonePose pose = new BonePose { transform = current, bindRotation = current.localRotation };
            poses[current] = pose;

            string name = current.name.ToLowerInvariant();
            if (name == "spine") spine = pose;
            else if (name == "neck") neck = pose;
            else if (name == "head") head = pose;
            else if (name.StartsWith("tail_", StringComparison.Ordinal)) tail.Add(pose);
            else if (name == "foot_l") leftFoot = pose;
            else if (name == "foot_r") rightFoot = pose;
            else if (leftLeg == null && IsLeft(name) && ContainsAny(name, "leg", "thigh", "shin")) leftLeg = pose;
            else if (rightLeg == null && IsRight(name) && ContainsAny(name, "leg", "thigh", "shin")) rightLeg = pose;
        }
        tail.Sort((a, b) => string.CompareOrdinal(a.transform.name, b.transform.name));
    }

    public void PlayHit()
    {
        hitPulse = 1f;
    }

    public void SetGroundOffset(float value)
    {
        groundOffset = value;
    }

    public void ApplyMotion(Vector3 worldPosition, Quaternion worldRotation, bool moving, float normalizedSpeed, float dt)
    {
        if (poses.Count == 0) Initialize();
        ResetPose();

        float targetRate = moving ? Mathf.Lerp(4.8f, 7.2f, Mathf.Clamp01(normalizedSpeed)) : 1.8f;
        phase += dt * targetRate;
        hitPulse = Mathf.MoveTowards(hitPulse, 0f, dt * 7.5f);

        float stride = moving ? Mathf.Sin(phase) : 0f;
        float strideOpposite = -stride;
        float bob = moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.075f : Mathf.Sin(phase * 0.5f) * 0.018f;
        float bodyPitch = moving ? Mathf.Sin(phase * 2f) * 1.4f : Mathf.Sin(phase * 0.5f) * 0.8f;
        float hitKick = hitPulse * 7f;

        transform.SetPositionAndRotation(worldPosition + Vector3.up * (groundOffset + bob), worldRotation * Quaternion.Euler(-hitKick, 0f, 0f));

        Rotate(spine, bodyPitch, moving ? stride * 1.8f : 0f, 0f);
        Rotate(neck, -bodyPitch * 0.75f, -stride * 1.2f, 0f);
        Rotate(head, bodyPitch * 0.35f, stride * 0.7f, 0f);
        Rotate(leftLeg, stride * 18f, 0f, 0f);
        Rotate(rightLeg, strideOpposite * 18f, 0f, 0f);
        Rotate(leftFoot, strideOpposite * 13f, 0f, 0f);
        Rotate(rightFoot, stride * 13f, 0f, 0f);

        for (int i = 0; i < tail.Count; i++)
        {
            float delayedWave = Mathf.Sin(phase - i * 0.48f);
            float amount = moving ? Mathf.Lerp(5f, 11f, i / Mathf.Max(1f, tail.Count - 1f)) : 2.2f;
            Rotate(tail[i], 0f, delayedWave * amount, 0f);
        }
    }

    private void ResetPose()
    {
        foreach (BonePose pose in poses.Values)
            if (pose.transform != null) pose.transform.localRotation = pose.bindRotation;
    }

    private static void Rotate(BonePose pose, float x, float y, float z)
    {
        if (pose?.transform == null) return;
        pose.transform.localRotation = pose.bindRotation * Quaternion.Euler(x, y, z);
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
            if (value.Contains(tokens[i])) return true;
        return false;
    }

    private static bool IsLeft(string value)
    {
        return value.EndsWith("_l", StringComparison.Ordinal) || value.Contains("left");
    }

    private static bool IsRight(string value)
    {
        return value.EndsWith("_r", StringComparison.Ordinal) || value.Contains("right");
    }
}
