using UnityEngine;

/// <summary>Crosshair aiming shared by spell casting and parry reflection.</summary>
public static class Aim
{
    private static readonly RaycastHit[] hits = new RaycastHit[32];

    /// <summary>
    /// World point under the screen centre, ignoring colliders under <paramref name="ignoreRoot"/>.
    /// Falls back to a point maxDistance along the view if nothing is hit.
    /// </summary>
    public static Vector3 PointUnderCrosshair(Transform view, Transform ignoreRoot, float maxDistance, out float distance)
    {
        Vector3 target = view.position + view.forward * maxDistance;
        distance = maxDistance;
        int count = Physics.RaycastNonAlloc(view.position, view.forward, hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
            if (h.distance < distance) { distance = h.distance; target = h.point; }
        }
        return target;
    }

    /// <summary>
    /// Direction from <paramref name="origin"/> to whatever is under the crosshair. Very close targets
    /// use the view direction instead so shots from an off-centre hand don't veer sideways.
    /// </summary>
    public static Vector3 DirectionFrom(Vector3 origin, Transform view, Transform ignoreRoot, float maxDistance = 200f)
    {
        Vector3 target = PointUnderCrosshair(view, ignoreRoot, maxDistance, out float distance);
        if (distance < 1.5f) return view.forward;
        return (target - origin).normalized;
    }
}
