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
    }

    [Serializable]
    public class Encounter
    {
        public string name = "Area";
        [Tooltip("Walking into this box starts the area's waves.")]
        public Bounds triggerArea = new Bounds(Vector3.zero, Vector3.one * 20f);
        [Tooltip("Optional: no start while the player is in here (e.g. still on the stairs up). Size 0 = unused.")]
        public Bounds triggerIgnore;
        public Transform[] spawnPoints;
        [Tooltip("Optional: mages spawn here first (e.g. balconies); everyone else only uses spawnPoints.")]
        public Transform[] mageSpawnPoints;
        public List<Wave> waves = new List<Wave>();
        [Tooltip("Enemies already placed in the level that belong to this area (tower casters etc).")]
        public List<Enemy> garrison = new List<Enemy>();
        [Tooltip("This area's share of skeletons among the non-commanders (-1 = the level's skeletonShare).")]
        [Range(-1f, 1f)] public float skeletonShare = -1f;
        [Tooltip("Garrison wakes when the player walks in instead of when the area unlocks (so they can't shoot you on the way there).")]
        public bool garrisonWakesOnEnter;
        [Tooltip("Barriers that block the way into this area; removed when the area unlocks.")]
        public List<GameObject> gates = new List<GameObject>();
        [Tooltip("Closed behind the player when they walk in (SlideShut slides, GateDoor swings shut, anything else is switched on).")]
        public List<GameObject> closeOnEnter = new List<GameObject>();
        [Tooltip("Respawn point once the player is in this area (needed when the way back is closed).")]
        public Transform checkpoint;
        [Tooltip("Areas behind the player, destroyed once they walk in and the way back is shut (frees rendering and memory).")]
        public List<GameObject> unloadOnEnter = new List<GameObject>();

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
    [Tooltip("Never put more than this many enemies on one spawn point.")]
    [SerializeField] private int maxPerSpawnPoint = 3;
    [Tooltip("Commanders per area (-1 = automatic: 1 with 4+ enemies, 2 with 9+).")]
    [SerializeField] private int commandersPerArea = -1;
    [Tooltip("Share of the non-commander enemies that are skeletons (the rest are mages).")]
    [Range(0f, 1f)] [SerializeField] private float skeletonShare = 0.5f;
    [Header("Garrison (placed enemies) stand out: bigger, with a glowing aura")]
    [SerializeField] private GameObject garrisonAura;
    [Tooltip("Additive material for the halo and the beam above each garrison enemy.")]
    [SerializeField] private Material garrisonGlowMaterial;
    [SerializeField] private float garrisonScale = 1.35f;
    [SerializeField] private Color garrisonGlow = new Color(1f, 0.35f, 0.2f);
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

    public string LevelName => levelName;
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
                    MarkGarrison(g);
                    if (i > 0) g.gameObject.SetActive(false);   // asleep until the area unlocks
                }
            foreach (var gate in e.gates) if (gate != null) gate.SetActive(true);
        }

        gameObject.AddComponent<StyleMeter>();
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
            if (!e.started && e.triggerArea.Contains(player.position)
                && !(e.triggerIgnore.size != Vector3.zero && e.triggerIgnore.Contains(player.position)))
            {
                e.started = true;
                if (e.checkpoint != null) Checkpoints.Set(e.checkpoint.position, e.checkpoint.eulerAngles.y);
                foreach (var c in e.closeOnEnter)
                    if (c != null)
                    {
                        var shut = c.GetComponent<SlideShut>();
                        var door = c.GetComponent<GateDoor>();
                        if (shut != null) shut.Close(); else if (door != null) door.Close(); else c.SetActive(true);
                    }
                if (e.garrisonWakesOnEnter) WakeGarrison(e);
                if (e.unloadOnEnter.Count > 0) StartCoroutine(Unload(e.unloadOnEnter));
                SpawnWaves(e);
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
        // Debug: kill the live area only. Later areas' sleeping garrisons are in their alive lists too.
        if (Input.GetKeyDown(KeyCode.K) && current < encounters.Count)
            foreach (var x in encounters[current].alive.ToArray()) if (x != null && x.gameObject.activeInHierarchy) x.Die();
#endif
    }

    private void UnlockArea(Encounter e)
    {
        if (!e.garrisonWakesOnEnter) WakeGarrison(e);
        foreach (var gate in e.gates) if (gate != null) StartCoroutine(OpenGate(gate));
        bannerUntil = Time.time + 4f;
    }

    private static void WakeGarrison(Encounter e)
    {
        foreach (var g in e.garrison) if (g != null) BeamIn.Play(g.gameObject, UnityEngine.Random.Range(0f, 0.4f));
    }

    // Waits for the way back to finish shutting, then destroys what's behind it and frees its meshes.
    private IEnumerator Unload(List<GameObject> behind)
    {
        yield return new WaitForSeconds(5f);
        foreach (var g in behind) if (g != null) Destroy(g);
        yield return null;
        Resources.UnloadUnusedAssets();
    }

    // Collider off at once, then the barrier sinks into the ground and disappears.
    private IEnumerator OpenGate(GameObject gate)
    {
        var torch = gate.GetComponent<Ignite>();
        if (torch != null) { torch.Open(); yield break; }
        var door = gate.GetComponent<GateDoor>();
        if (door != null) { door.Open(); yield break; }
        var slide = gate.GetComponent<SlideShut>();
        if (slide != null) { slide.Open(); yield break; }
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

    // Every enemy of every wave spawns the moment the area starts (overwhelming on purpose).
    // Mages fill mageSpawnPoints first (when set), then share the main points with everyone else.
    private void SpawnWaves(Encounter e)
    {
        int index = 0, total = 0, main = 0, upper = 0;
        int upperSlots = e.mageSpawnPoints != null ? e.mageSpawnPoints.Length * maxPerSpawnPoint : 0;
        foreach (var wave in e.waves) total += wave.enemyCount;
        foreach (var wave in e.waves)
            for (int i = 0; i < wave.enemyCount; i++, index++)
            {
                Enemy type = PickType(index, total, e.skeletonShare >= 0f ? e.skeletonShare : skeletonShare);
                if (type != null && type == magePrefab && upper < upperSlots) Spawn(e, wave, e.mageSpawnPoints, upper++, type);
                else Spawn(e, wave, e.spawnPoints, main++, type);
            }
        e.wavesDone = true;
    }

    // Skeletons and mages mixed by skeletonShare (skeleton first); the last few are commanders.
    private Enemy PickType(int index, int total, float skeletonShare)
    {
        int commanders = commandersPerArea >= 0 ? Mathf.Min(commandersPerArea, total) : total >= 9 ? 2 : total >= 4 ? 1 : 0;
        Enemy pick;
        if (index >= total - commanders) pick = commanderPrefab;
        else
        {
            float mage = 1f - skeletonShare;   // a mage each time the running mage count ticks over
            pick = Mathf.FloorToInt((index + 1) * mage + 0.0001f) > Mathf.FloorToInt(index * mage + 0.0001f) ? magePrefab : skeletonPrefab;
        }
        return pick != null ? pick : enemyPrefab;
    }

    // Garrison enemies are the ones players lose track of: make them bigger and give them a glow you can spot from afar.
    private void MarkGarrison(Enemy g)
    {
        g.transform.localScale *= garrisonScale;
        if (garrisonAura != null)
        {
            var fx = Instantiate(garrisonAura, g.transform);
            fx.transform.localPosition = Vector3.up * 1f;
            foreach (var c in fx.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        if (garrisonGlowMaterial != null)
        {
            GlowPart(g.transform, PrimitiveType.Sphere, new Vector3(0f, 1f, 0f), new Vector3(2.2f, 2.6f, 2.2f));
            GlowPart(g.transform, PrimitiveType.Cylinder, new Vector3(0f, 16f, 0f), new Vector3(0.35f, 15f, 0.35f));   // beam: spot them across the map
        }
        var light = new GameObject("GarrisonGlow").AddComponent<Light>();
        light.transform.SetParent(g.transform, false);
        light.transform.localPosition = Vector3.up * 1.5f;
        light.type = LightType.Point;
        light.color = garrisonGlow;
        light.range = 7f;
        light.intensity = 3f;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    private void GlowPart(Transform parent, PrimitiveType shape, Vector3 localPos, Vector3 worldSize)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = "GarrisonGlow_" + shape;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        Vector3 ps = parent.lossyScale;
        part.transform.localScale = new Vector3(worldSize.x / ps.x, worldSize.y / ps.y, worldSize.z / ps.z);
        var r = part.GetComponent<MeshRenderer>();
        r.sharedMaterial = garrisonGlowMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void Spawn(Encounter e, Wave wave, Transform[] spawnPoints, int index, Enemy prefab)
    {
        if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        int points = spawnPoints.Length;
        int slot = index / points;     // how many already stand on this point
        if (slot >= maxPerSpawnPoint)
        {
            Debug.LogWarning($"[LevelManager] {e.name}: not enough spawn points for enemy {index + 1} (max {maxPerSpawnPoint} per point).");
            return;
        }
        Transform point = spawnPoints[index % points];
        // Spread the ones sharing a point around it so they don't stack, never through a wall or fence.
        Vector3 pos = point.position;
        if (slot > 0)
            for (int k = 0; k < 6; k++)
            {
                Vector3 offset = Quaternion.Euler(0f, slot * 137f + k * 60f, 0f) * Vector3.forward * 1.8f;
                if (Physics.Linecast(pos, pos + offset * 1.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                pos += offset;
                break;
            }
        var enemy = Instantiate(prefab, pos, point.rotation);
        enemy.Configure(wave.enemyHealth, wave.enemySpeed, wave.enemyFireInterval);
        e.alive.Add(enemy);
        BeamIn.Play(enemy.gameObject, UnityEngine.Random.Range(0f, 0.4f));   // beams in instead of popping up
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

    private int hudKey = -1;   // texts are rebuilt only when this changes (no new strings every frame)

    private void UpdateHud()
    {
        if (titleText == null) return;
        titleText.color = new Color(1f, 1f, 1f, Mathf.Clamp01(bannerUntil - Time.time));
        var area = current < encounters.Count ? encounters[current] : null;
        int key = Cleared ? -2 : current * 100000 + (area != null && area.started ? 50000 : 0) + (area != null ? area.alive.Count : 0);
        if (key == hudKey) return;
        hudKey = key;

        titleText.text = Cleared ? "LEVEL CLEARED"
                       : current > 0 ? "AREA CLEARED - THE WAY IS OPEN"
                       : levelName.ToUpperInvariant();
        if (Cleared) { objectiveText.text = "The portal is open. Find it to move on."; return; }
        if (area == null) return;
        string goal = area.started ? "Defeat every enemy to open the way on" : "Head into the next area";
        objectiveText.text = $"Area {current + 1}/{encounters.Count}: {area.name}     {goal}     Enemies: {area.alive.Count}";
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        foreach (var e in encounters) Gizmos.DrawWireCube(e.triggerArea.center, e.triggerArea.size);
    }
}
