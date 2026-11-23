using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger that ends the current scene. If a TutorialManager is present it shows the results first;
/// otherwise the next scene loads immediately.
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
        if (TutorialManager.Instance != null) TutorialManager.Instance.Complete(nextScene);
        else SceneManager.LoadScene(nextScene);
    }
}
