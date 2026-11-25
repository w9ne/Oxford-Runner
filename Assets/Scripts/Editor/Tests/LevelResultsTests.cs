using NUnit.Framework;

/// <summary>Rank from a level score: share of the level's SSS score (D 0, C 20%, B 35%, A 50%, S 65%, SS 80%, SSS 100%).</summary>
public class LevelResultsTests
{
    [Test]
    public void RankThresholds()
    {
        Assert.AreEqual(0, LevelResults.RankFor(0, 8000));      // D
        Assert.AreEqual(0, LevelResults.RankFor(1599, 8000));   // still D
        Assert.AreEqual(1, LevelResults.RankFor(1600, 8000));   // C
        Assert.AreEqual(5, LevelResults.RankFor(6876, 8000));   // SS (Level 1 best so far)
        Assert.AreEqual(6, LevelResults.RankFor(8000, 8000));   // SSS
        Assert.AreEqual(6, LevelResults.RankFor(99999, 8000));  // capped at SSS
    }
}
