using NUnit.Framework;

/// <summary>Campaign order: Tutorial -> Level 1 -> 2 -> 3 -> main menu.</summary>
public class GameFlowTests
{
    [Test]
    public void CampaignRunsInOrderThenBackToMenu()
    {
        Assert.AreEqual(GameFlow.Level1Scene, GameFlow.NextInCampaign(GameFlow.TutorialScene));
        Assert.AreEqual(GameFlow.Level2Scene, GameFlow.NextInCampaign(GameFlow.Level1Scene));
        Assert.AreEqual(GameFlow.Level3Scene, GameFlow.NextInCampaign(GameFlow.Level2Scene));
        Assert.AreEqual(GameFlow.MainMenuScene, GameFlow.NextInCampaign(GameFlow.Level3Scene));
        Assert.AreEqual(GameFlow.MainMenuScene, GameFlow.NextInCampaign("NotAScene"));
    }
}
