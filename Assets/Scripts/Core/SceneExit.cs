using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger that ends the current scene. The tutorial shows its results card; campaign levels show the rank
/// screen (LevelResults); otherwise the next scene loads immediately. Health carries over. In campaign scenes the
/// destination comes from GameFlow's campaign order.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneExit : MonoBehaviour
{
    [SerializeField] private string nextScene = "";   // empty: next in the campaign
    private bool used;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (used || other.GetComponentInParent<FirstPersonController>() == null) return;
        used = true;
        string nextScene = GameFlow.ResolveExit(this.nextScene);   // tutorial-only runs return to the menu
        var health = other.GetComponentInParent<PlayerHealth>();
        if (TutorialManager.Instance != null) TutorialManager.Instance.Complete(nextScene);
        else if (StyleMeter.Instance != null) { PortalFall.Begin(nextScene, health); LevelResults.Show(nextScene, health); }   // fall + rank card, then drop into the next level
        else GameFlow.AdvanceTo(nextScene, health);
    }
}
