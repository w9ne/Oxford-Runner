using UnityEngine;

/// <summary>
/// Level 3's torch hall to the boss door. Clearing the level (stepping into the hall) shows the rank card for
/// a few seconds while you keep walking. Torches light up just ahead of the player, and past unloadZ the
/// upper hall behind is destroyed to free memory.
/// </summary>
public class TorchHall : MonoBehaviour
{
    public Ignite[] torches;          // sorted by z
    public float lightAhead = 12f;
    public float resultsSeconds = 6f;
    public GameObject unload;
    public float unloadZ;
    private Transform player;
    private int next;

    private void Start()
    {
        var level = FindAnyObjectByType<LevelManager>();
        if (level != null) level.LevelCleared += () => LevelResults.Show(null, null, resultsSeconds);
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    private void Update()
    {
        if (player == null) return;
        float z = player.position.z;
        while (next < torches.Length && torches[next].transform.position.z < z + lightAhead) torches[next++].Open();
        if (unload != null && z > unloadZ)
        {
            Destroy(unload);
            unload = null;
            Resources.UnloadUnusedAssets();
        }
    }
}
