using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Kill point multipliers (StyleMeter) and stair-foot routing for melee enemies (ApproachPoint).</summary>
public class ScoringAndRoutingTests
{
    [Test]
    public void KillMultipliersStackByOneAndAHalf()
    {
        Assert.AreEqual(1f, StyleMeter.KillMultiplier(false, false, false, 1), 1e-5f);     // plain kill
        Assert.AreEqual(1.5f, StyleMeter.KillMultiplier(false, false, true, 1), 1e-5f);    // air kill
        Assert.AreEqual(1.5f, StyleMeter.KillMultiplier(true, false, true, 1), 1e-5f);     // wall-run counts once (it is airborne too)
        Assert.AreEqual(1.5f, StyleMeter.KillMultiplier(false, false, false, 2), 1e-5f);   // multikill
        Assert.AreEqual(2.25f, StyleMeter.KillMultiplier(false, true, false, 3), 1e-5f);   // slide kill in a 3-streak
    }

    [Test]
    public void ApproachPicksShortestDetourOnOurLevel()
    {
        var made = new List<GameObject>();
        Transform Point(float x, float y, float z) { var g = new GameObject("p"); g.transform.position = new Vector3(x, y, z); made.Add(g); return g.transform; }
        try
        {
            Vector3 me = new Vector3(10f, 1f, 0f), target = new Vector3(0f, 3f, 10f);
            var near = Point(4f, 1f, 6f);       // short detour
            var far = Point(30f, 1f, -20f);     // long way round
            var upstairs = Point(1f, 3f, 9f);   // not on our level
            var underfoot = Point(10.5f, 1f, 0.5f);   // where we already are
            Assert.AreSame(near, ApproachPoint.Best(me, target, new[] { far, upstairs, near, underfoot }));
            Assert.IsNull(ApproachPoint.Best(me, target, new[] { upstairs, underfoot }));
        }
        finally { foreach (var g in made) Object.DestroyImmediate(g); }
    }
}
