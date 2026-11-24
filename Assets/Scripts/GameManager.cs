using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the game's stage / wave progression.
/// A stage contains several waves; difficulty scales through enemy count, health and attack rate.
/// Enemies spawn from designated door points and walk toward the player.
///
/// Later responsibilities (per the proposal) hook into the events below:
/// scoring and health reset (WaveStarted / WaveCompleted), spell unlocks (StageCompleted), final boss.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Serializable]
    public class WaveData
    {
        public int enemyCount = 3;
        public float enemyHealth = 50f;
        public float enemySpeed = 3.5f;
        [Tooltip("Seconds between enemy spell casts (used once enemy attacks exist).")]
        public float enemyFireInterval = 3f;
        [Tooltip("Seconds between each enemy spawning in this wave.")]
        public float spawnInterval = 1f;
    }

    [Serializable]
    public class StageData
    {
        public string name = "Stage";
        public List<WaveData> waves = new List<WaveData>();
    }

    public enum State { WaitingToStart, Spawning, Fighting, BetweenWaves, BetweenStages, Finished }

    public static GameManager Instance { get; private set; }

    [Header("Spawning")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;     // the arena's doors

    [Header("Timing")]
    [SerializeField] private float startDelay = 3f;
    [SerializeField] private float timeBetweenWaves = 5f;
    [SerializeField] private float timeBetweenStages = 8f;

    [Header("Stages")]
    [SerializeField] private List<StageData> stages = DefaultStages();

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private KeyCode debugKillAllKey = KeyCode.K;

    public event Action<int, int> WaveStarted;     // stageIndex, waveIndex
    public event Action<int, int> WaveCompleted;   // stageIndex, waveIndex
    public event Action<int> StageCompleted;       // stageIndex
    public event Action AllStagesCompleted;

    public State CurrentState { get; private set; } = State.WaitingToStart;
    public int StageIndex { get; private set; }
    public int WaveIndex { get; private set; }
    public int EnemiesAlive => alive.Count;
    public int EnemiesLeftToSpawn { get; private set; }
    public float Countdown { get; private set; }

    private readonly List<Enemy> alive = new List<Enemy>();
    private int lastSpawnIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("GameManager: assign an enemy prefab and at least one spawn point.", this);
            enabled = false;
            return;
        }
        StartCoroutine(RunGame());
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(debugKillAllKey))
            foreach (var e in alive.ToArray()) e.Die();
#endif
    }

    private IEnumerator RunGame()
    {
        CurrentState = State.WaitingToStart;
        yield return CountdownRoutine(startDelay);

        for (StageIndex = 0; StageIndex < stages.Count; StageIndex++)
        {
            var stage = stages[StageIndex];
            for (WaveIndex = 0; WaveIndex < stage.waves.Count; WaveIndex++)
            {
                yield return RunWave(stage.waves[WaveIndex]);
                WaveCompleted?.Invoke(StageIndex, WaveIndex);

                bool lastWaveOfStage = WaveIndex == stage.waves.Count - 1;
                if (!lastWaveOfStage)
                {
                    CurrentState = State.BetweenWaves;
                    yield return CountdownRoutine(timeBetweenWaves);
                }
            }

            StageCompleted?.Invoke(StageIndex);
            if (StageIndex < stages.Count - 1)
            {
                CurrentState = State.BetweenStages;
                yield return CountdownRoutine(timeBetweenStages);
            }
        }

        CurrentState = State.Finished;
        AllStagesCompleted?.Invoke();
    }

    private IEnumerator RunWave(WaveData wave)
    {
        CurrentState = State.Spawning;
        WaveStarted?.Invoke(StageIndex, WaveIndex);

        EnemiesLeftToSpawn = wave.enemyCount;
        while (EnemiesLeftToSpawn > 0)
        {
            SpawnEnemy(wave);
            EnemiesLeftToSpawn--;
            if (EnemiesLeftToSpawn > 0) yield return new WaitForSeconds(wave.spawnInterval);
        }

        CurrentState = State.Fighting;
        while (alive.Count > 0) yield return null;
    }

    private IEnumerator CountdownRoutine(float seconds)
    {
        Countdown = seconds;
        while (Countdown > 0f)
        {
            Countdown -= Time.deltaTime;
            yield return null;
        }
        Countdown = 0f;
    }

    private void SpawnEnemy(WaveData wave)
    {
        // Pick a random door, avoiding the same one twice in a row when possible.
        int i = UnityEngine.Random.Range(0, spawnPoints.Length);
        if (spawnPoints.Length > 1 && i == lastSpawnIndex) i = (i + 1) % spawnPoints.Length;
        lastSpawnIndex = i;

        Transform door = spawnPoints[i];
        Enemy enemy = Instantiate(enemyPrefab, door.position, door.rotation);
        enemy.Configure(wave.enemyHealth, wave.enemySpeed, wave.enemyFireInterval);
        enemy.Died += OnEnemyDied;
        alive.Add(enemy);
    }

    private void OnEnemyDied(Enemy enemy)
    {
        enemy.Died -= OnEnemyDied;
        alive.Remove(enemy);
    }

    /// <summary>Overwrites the stage list in this scene with the defaults below (used after retuning them).</summary>
    [ContextMenu("Reset Stages To Defaults")]
    public void ResetStagesToDefaults() => stages = DefaultStages();

    private static List<StageData> DefaultStages()
    {
        // Difficulty ramps through count, health and attack frequency.
        return new List<StageData>
        {
            new StageData { name = "Stage 1", waves = new List<WaveData>
            {
                new WaveData { enemyCount = 3, enemyHealth = 50, enemySpeed = 3.5f, enemyFireInterval = 3.0f, spawnInterval = 1.2f },
                new WaveData { enemyCount = 4, enemyHealth = 50, enemySpeed = 3.5f, enemyFireInterval = 2.8f, spawnInterval = 1.0f },
                new WaveData { enemyCount = 5, enemyHealth = 55, enemySpeed = 3.8f, enemyFireInterval = 2.6f, spawnInterval = 1.0f },
            }},
            new StageData { name = "Stage 2", waves = new List<WaveData>
            {
                new WaveData { enemyCount = 5, enemyHealth = 60, enemySpeed = 4.0f, enemyFireInterval = 2.4f, spawnInterval = 0.9f },
                new WaveData { enemyCount = 6, enemyHealth = 65, enemySpeed = 4.0f, enemyFireInterval = 2.2f, spawnInterval = 0.8f },
                new WaveData { enemyCount = 8, enemyHealth = 70, enemySpeed = 4.2f, enemyFireInterval = 2.0f, spawnInterval = 0.8f },
            }},
            new StageData { name = "Stage 3", waves = new List<WaveData>
            {
                new WaveData { enemyCount = 7, enemyHealth = 80, enemySpeed = 4.5f, enemyFireInterval = 1.8f, spawnInterval = 0.7f },
                new WaveData { enemyCount = 9, enemyHealth = 85, enemySpeed = 4.5f, enemyFireInterval = 1.6f, spawnInterval = 0.6f },
                new WaveData { enemyCount = 11, enemyHealth = 95, enemySpeed = 4.8f, enemyFireInterval = 1.5f, spawnInterval = 0.6f },
            }},
        };
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 18, font = MenuUI.Font };
        style.normal.textColor = Color.white;

        string stageName = StageIndex < stages.Count ? stages[StageIndex].name : "";
        int waveCount = StageIndex < stages.Count ? stages[StageIndex].waves.Count : 0;
        string text;
        switch (CurrentState)
        {
            case State.WaitingToStart: text = $"Starting in {Countdown:0.0}"; break;
            case State.BetweenWaves:   text = $"{stageName} - next wave in {Countdown:0.0}"; break;
            case State.BetweenStages:  text = $"{stageName} complete! Next stage in {Countdown:0.0}"; break;
            case State.Finished:       text = "All stages complete!"; break;
            default:
                text = $"{stageName} - Wave {WaveIndex + 1}/{waveCount}   Enemies: {EnemiesAlive + EnemiesLeftToSpawn}";
                break;
        }
        GUI.Label(new Rect(16, 16, 600, 30), text, style);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUI.Label(new Rect(16, 42, 600, 30), $"[{debugKillAllKey}] kill all enemies (debug)", style);
#endif
    }
}
