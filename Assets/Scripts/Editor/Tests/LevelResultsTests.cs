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

    [Test]
    public void LeaderboardKeepsTopTenHighestFirst()
    {
        var list = Leaderboard.Insert(new System.Collections.Generic.List<Leaderboard.Entry>(), "Ann", 500, out int place);
        Assert.AreEqual(0, place);
        list = Leaderboard.Insert(list, "Bob", 900, out place);
        Assert.AreEqual(0, place);
        Assert.AreEqual("Bob", list[0].name);
        list = Leaderboard.Insert(list, "Cy", 500, out place);
        Assert.AreEqual(2, place);                                   // a tie goes below the older run
        for (int i = 0; i < 10; i++) list = Leaderboard.Insert(list, "X", 1000, out place);
        Assert.AreEqual(Leaderboard.Size, list.Count);
        Leaderboard.Insert(list, "Low", 1, out place);
        Assert.AreEqual(-1, place);                                  // didn't make it
        Assert.AreEqual(2, Leaderboard.Parse("A\t5\nbad line\nB\t9").Count);   // junk lines skipped
        Assert.AreEqual("Wizard", Leaderboard.Clean("  \t "));
        Assert.AreEqual(16, Leaderboard.Clean("AVeryLongNameThatGoesOnAndOn").Length);
    }
}
