using NUnit.Framework;
using UnityEngine;

/// <summary>SpellProjectile.HitsSphere decides if an enemy bolt meets the parry barrier.</summary>
public class ParrySphereTests
{
    [Test]
    public void BoltHeadingInHitsAtSphereSurface()
    {
        Assert.IsTrue(SpellProjectile.HitsSphere(new Vector3(0f, 0f, -5f), Vector3.forward, 4f, Vector3.zero, 2f, out Vector3 p));
        Assert.AreEqual(-2f, p.z, 1e-4f);
    }

    [Test]
    public void BoltTooShortOrHeadingAwayMisses()
    {
        Assert.IsFalse(SpellProjectile.HitsSphere(new Vector3(0f, 0f, -5f), Vector3.forward, 2f, Vector3.zero, 2f, out _));
        Assert.IsFalse(SpellProjectile.HitsSphere(new Vector3(0f, 0f, -5f), Vector3.back, 10f, Vector3.zero, 2f, out _));
        Assert.IsFalse(SpellProjectile.HitsSphere(new Vector3(3f, 0f, -5f), Vector3.forward, 10f, Vector3.zero, 2f, out _));
    }

    [Test]
    public void BoltStartingInsideHits()
    {
        Assert.IsTrue(SpellProjectile.HitsSphere(new Vector3(0f, 0f, 1f), Vector3.back, 0.1f, Vector3.zero, 2f, out Vector3 p));
        Assert.AreEqual(1f, p.z, 1e-4f);
    }
}
