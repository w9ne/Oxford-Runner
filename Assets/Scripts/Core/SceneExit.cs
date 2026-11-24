using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger that ends the current scene. If a TutorialManager is present it shows the results first;
/// otherwise the next scene loads immediately (carrying the player's health). In campaign scenes the
/// destination comes from GameFlow's campaign order.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneExit : MonoBehaviour
{
    [SerializeField] private string nextScene = "MovementTest";
    private bool used;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (used || other.GetComponentInParent<FirstPersonController>() == null) return;
        used = true;
        string nextScene = GameFlow.ResolveExit(this.nextScene);   // tutorial-only runs return to the menu
        if (TutorialManager.Instance != null) TutorialManager.Instance.Complete(nextScene);
        else GameFlow.AdvanceTo(nextScene, other.GetComponentInParent<PlayerHealth>());   // carries health over
    }
}
