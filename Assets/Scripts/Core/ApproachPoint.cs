using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks the foot of a stair or ramp. A melee enemy whose target stands higher up and whose straight path is
/// blocked walks to the best of these first (see Enemy), instead of grinding against the wall under the target.
/// </summary>
public class ApproachPoint : MonoBehaviour
{
    private static readonly List<ApproachPoint> all = new List<ApproachPoint>();

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    /// <summary>The point on roughly our level (not where we already are) with the shortest detour to the target.</summary>
    public static Transform Best(Vector3 from, Vector3 target)
    {
        var points = new List<Transform>(all.Count);
        foreach (var p in all) points.Add(p.transform);
        return Best(from, target, points);
    }

    /// <summary>Same, over a given list (testable without a scene).</summary>
    public static Transform Best(Vector3 from, Vector3 target, IEnumerable<Transform> points)
    {
        Transform best = null;
        float bestCost = float.MaxValue;
        foreach (var p in points)
        {
            Vector3 at = p.position;
            if (Mathf.Abs(at.y - from.y) > 1.2f) continue;
            Vector3 flat = at - from; flat.y = 0f;
            if (flat.sqrMagnitude < 4f) continue;
            float cost = flat.magnitude + Vector3.Distance(at, target);
            if (cost < bestCost) { bestCost = cost; best = p; }
        }
        return best;
    }
}
