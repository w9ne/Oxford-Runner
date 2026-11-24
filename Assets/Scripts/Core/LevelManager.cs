using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runs a campaign level. The level is split into encounters (areas) that are fought in order.
/// Only one area is live at a time: walking into it starts its waves (spawned from its spawn points),
/// and its garrison (enemies already placed in the level, e.g. tower casters) wakes up when the area
/// unlocks. Clearing an area (waves done + everyone dead) opens the gates into the next one; later
/// areas stay locked and asleep until then. Clearing the last area opens the exit portal.
///
/// Also: sets the start checkpoint, restores health carried from the previous level, and shows the
/// level name, objective and enemies-left counter at the top of the screen.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Serializable]
    public class Wave
    {
        public int enemyCount = 3;
        public float enemyHealth = 50f;
        public float enemySpeed = 3.5f;
        public float enemyFireInterval = 3f;
        public float spawnInterval = 1f;
    }

    [Serializable]
    public class Encounter
    {
        public string name = "Area";
        [Tooltip("Walking into this box starts the area's waves.")]
        public Bounds triggerArea = new Bounds(Vector3.zero, Vector3.one * 20f);
        public Transform[] spawnPoints;
        public List<Wave> waves = new List<Wave>();
        [Tooltip("Enemies already placed in the level that belong to this area (tower casters etc).")]
        public List<Enemy> garrison = new List<Enemy>();
        [Tooltip("Barriers that block the way into this area; removed when the area unlocks.")]
        public List<GameObject> gates = new List<GameObject>();

        [NonSerialized] public bool started, wavesDone;
        [NonSerialized] public readonly List<Enemy> alive = new List<Enemy>();
    }

    [SerializeField] private string levelName = "Level 1";
    [SerializeField] private Enemy enemyPrefab;                 // fallback for any type that isn't set
    [Header("Enemy types (mix: skeletons first, then mages, 1-2 commanders per area)")]
    [SerializeField] private Enemy skeletonPrefab;
    [SerializeField] private Enemy magePrefab;
    [SerializeField] private Enemy commanderPrefab;
    [SerializeField] private Transform playerStart;
    [SerializeField] private GameObject exitPortal;            // inactive until the level is cleared
    [SerializeField] private float timeBetweenWaves = 3f;
    [Tooltip("Spawn every enemy of every wave the moment an area starts (overwhelming), instead of wave by wave.")]
    [SerializeField] private bool spawnAllAtOnce = true;
    [Tooltip("Never put more than this many enemies on one spawn point.")]
    [SerializeField] private int maxPerSpawnPoint = 3;
    [SerializeField] private List<Encounter> encounters = new List<Encounter>();

    /// <summary>Used by the editor level builders to wire a generated level.</summary>
    public void Configure(string name, Enemy prefab, Transform start, GameObject portal, List<Encounter> list)
    {
        levelName = name;
        enemyPrefab = prefab;
        playerStart = start;
        exitPortal = portal;
        encounters = list;
    }

    /// <summary>Used by the editor level builders to set the enemy type prefabs.</summary>
    public void ConfigureTypes(Enemy skeleton, Enemy mage, Enemy commander)
    {
        skeletonPrefab = skeleton;
        magePrefab = mage;
        commanderPrefab = commander;
    }

    public bool Cleared { get; private set; }
    public event Action LevelCleared;

    private int current;          // index of the live area
    private Transform player;
    private Text titleText, objectiveText;
    private float bannerUntil;

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            var health = p.GetComponent<PlayerHealth>();
            if (health != null && GameFlow.CarriedHealth > 0f) health.SetHealth(GameFlow.CarriedHealth);
            GameFlow.CarriedHealth = -1f;
        }
        if (playerStart != null) Checkpoints.Set(playerStart.position, playerStart.eulerAngles.y);
        if (exitPortal != null) exitPortal.SetActive(false);

        for (int i = 0; i < encounters.Count; i++)
        {
            var e = encounters[i];
            foreach (var g in e.garrison)
                if (g != null)
                {
                    e.alive.Add(g);
                    if (i > 0) g.gameObject.SetActive(false);   // asleep until the area unlocks
                }
            foreach (var gate in e.gates) if (gate != null) gate.SetActive(true);
        }

        BuildHud();
        bannerUntil = Time.time + 4f;
    }

    private void Update()
    {
        if (player == null) return;
        foreach (var e in encounters) e.alive.RemoveAll(x => x == null || x.IsDead);

        if (!Cleared && current < encounters.Count)
        {
            var e = encounters[current];
            if (!e.started && e.triggerArea.Contains(player.position))
            {
                e.started = true;
                StartCoroutine(RunWaves(e));
            }
            if (e.started && e.wavesDone && e.alive.Count == 0)
            {
                current++;
                if (current < encounters.Count) UnlockArea(encounters[current]);
                else OpenPortal();
            }
        }
        else if (!Cleared && encounters.Count == 0) OpenPortal();
        UpdateHud();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.K))   // debug: kill everything that's currently alive
            foreach (var e in encounters) foreach (var x in e.alive.ToArray()) if (x != null) x.Die();
#endif
    }

    private void UnlockArea(Encounter e)
    {
        foreach (var g in e.garrison) if (g != null) g.gameObject.SetActive(true);
        foreach (var gate in e.gates) if (gate != null) StartCoroutine(OpenGate(gate));
        bannerUntil = Time.time + 4f;
    }

    // Collider off at once, then the barrier sinks into the ground and disappears.
    private IEnumerator OpenGate(GameObject gate)
    {
        foreach (var c in gate.GetComponentsInChildren<Collider>()) c.enabled = false;
        Transform t = gate.transform;
        Vector3 start = t.position, scale = t.localScale;
        const float duration = 1.2f;
        for (float k = 0f; k < 1f; k += Time.deltaTime / duration)
        {
            float s = 1f - k * k;
            t.localScale = new Vector3(scale.x, scale.y * s, scale.z);
            t.position = start + Vector3.down * (scale.y * (1f - s) * 0.5f);
            yield return null;
        }
        gate.SetActive(false);
    }

    private IEnumerator RunWaves(Encounter e)
    {
        if (spawnAllAtOnce)
        {
            int index = 0, total = 0;
            foreach (var wave in e.waves) total += wave.enemyCount;
            foreach (var wave in e.waves)
                for (int i = 0; i < wave.enemyCount; i++, index++) Spawn(e, wave, index, PickType(index, total));
            e.wavesDone = true;
            yield break;
        }
        for (int w = 0; w < e.waves.Count; w++)
        {
            var wave = e.waves[w];
            for (int i = 0; i < wave.enemyCount; i++)
            {
                Spawn(e, wave, i, PickType(i, wave.enemyCount));
                yield return new WaitForSeconds(wave.spawnInterval);
            }
            // Next wave once this one's spawns are dead (the garrison doesn't hold waves back).
            while (e.alive.Exists(x => x != null && !x.IsDead && !e.garrison.Contains(x))) yield return null;
            if (w < e.waves.Count - 1) yield return new WaitForSeconds(timeBetweenWaves);
        }
        e.wavesDone = true;
    }

    // Split evenly, favouring skeletons; the last 1 (4+ enemies) or 2 (9+ enemies) are commanders.
    private Enemy PickType(int index, int total)
    {
        int commanders = total >= 9 ? 2 : total >= 4 ? 1 : 0;
        Enemy pick;
        if (index >= total - commanders) pick = commanderPrefab;
        else pick = index % 2 == 0 ? skeletonPrefab : magePrefab;
        return pick != null ? pick : enemyPrefab;
    }

    private void Spawn(Encounter e, Wave wave, int index, Enemy prefab)
    {
        if (prefab == null || e.spawnPoints == null || e.spawnPoints.Length == 0) return;
        int points = e.spawnPoints.Length;
        int slot = index / points;     // how many already stand on this point
        if (slot >= maxPerSpawnPoint)
        {
            Debug.LogWarning($"[LevelManager] {e.name}: not enough spawn points for enemy {index + 1} (max {maxPerSpawnPoint} per point).");
            return;
        }
        Transform point = e.spawnPoints[index % points];
        // Spread the ones sharing a point around it so they don't stack.
        Vector3 offset = slot == 0 ? Vector3.zero : Quaternion.Euler(0f, slot * 137f, 0f) * Vector3.forward * 1.8f;
        var enemy = Instantiate(prefab, point.position + offset, point.rotation);
        enemy.Configure(wave.enemyHealth, wave.enemySpeed, wave.enemyFireInterval);
        e.alive.Add(enemy);
    }

    private void OpenPortal()
    {
        Cleared = true;
        if (exitPortal != null) exitPortal.SetActive(true);
        bannerUntil = Time.time + 4f;
        LevelCleared?.Invoke();
    }

    // ---------- HUD (top centre) ----------
    private void BuildHud()
    {
        var canvas = MenuUI.Canvas("LevelHUD", 6).transform;
        canvas.SetParent(transform, false);
        titleText = MenuUI.Label(canvas, levelName.ToUpperInvariant(), 64, Vector2.zero, new Vector2(1200f, 90f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Anchor(titleText.rectTransform, -200f);
        titleText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
        objectiveText = MenuUI.Label(canvas, "", 28, Vector2.zero, new Vector2(1200f, 40f), new Color(1f, 1f, 1f, 0.9f));
        Anchor(objectiveText.rectTransform, -40f);
        objectiveText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
    }

    private static void Anchor(RectTransform rt, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
    }

    private void UpdateHud()
    {
        if (titleText == null) return;
        float a = Mathf.Clamp01(bannerUntil - Time.time);
        titleText.text = Cleared ? "LEVEL CLEARED"
                       : current > 0 ? "AREA CLEARED - THE WAY IS OPEN"
                       : levelName.ToUpperInvariant();
        titleText.color = new Color(1f, 1f, 1f, a);

        if (Cleared) { objectiveText.text = "The portal is open. Find it to move on."; return; }
        if (current >= encounters.Count) return;
        var area = encounters[current];
        string goal = area.started ? "Defeat every enemy to open the way on" : "Head into the next area";
        objectiveText.text = $"Area {current + 1}/{encounters.Count}: {area.name}     {goal}     Enemies: {area.alive.Count}";
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        foreach (var e in encounters) Gizmos.DrawWireCube(e.triggerArea.center, e.triggerArea.size);
    }
}
