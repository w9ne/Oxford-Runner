using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays sounds and music. One persistent object with a small pool of AudioSources (2D for the player's own
/// sounds, 3D for things in the world) and two music sources that crossfade; each scene picks its track
/// (the same track keeps playing across scenes, e.g. Tutorial -> Level 1 -> Level 2). Adds PlayerSounds to the
/// player in every scene. Clips come from Resources/SoundBank.
/// </summary>
public class Sfx : MonoBehaviour
{
    private static SoundBank bank;
    /// <summary>Never null: an empty bank (silence) if Resources/SoundBank is missing.</summary>
    public static SoundBank Bank => bank != null ? bank : bank = Resources.Load<SoundBank>("SoundBank") ?? ScriptableObject.CreateInstance<SoundBank>();
    private const float MusicVolume = 0.45f;
    private static Sfx instance;
    private readonly AudioSource[] pool = new AudioSource[24];
    private AudioSource[] music;
    private int next, musicIndex;
    private float fadeUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        var go = new GameObject("Sfx");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Sfx>();
    }

    private void Awake()
    {
        for (int i = 0; i < pool.Length; i++) pool[i] = gameObject.AddComponent<AudioSource>();
        music = new[] { gameObject.AddComponent<AudioSource>(), gameObject.AddComponent<AudioSource>() };
        foreach (var m in music) { m.loop = true; m.volume = 0f; m.ignoreListenerPause = true; }
        SceneManager.sceneLoaded += (s, _) => OnScene(s);
        OnScene(SceneManager.GetActiveScene());
    }

    private void OnScene(Scene scene)
    {
        PlayMusic(SceneTrack(scene.name));
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null && p.GetComponent<PlayerSounds>() == null) p.AddComponent<PlayerSounds>();
    }

    private static AudioClip SceneTrack(string scene) =>
        scene == GameFlow.MainMenuScene ? Bank.menuMusic
        : scene == GameFlow.Level3Scene ? Bank.level3Music
        : scene == GameFlow.Level2Scene ? Bank.level2Music
        : Bank.level1Music;   // tutorial and Level 1

    /// <summary>Crossfades to a track (no-op if it's already playing).</summary>
    public static void PlayMusic(AudioClip clip, float fade = 1.5f)
    {
        if (instance == null) return;
        var cur = instance.music[instance.musicIndex];
        if (cur.clip == clip && cur.isPlaying) return;
        instance.musicIndex = 1 - instance.musicIndex;
        var nxt = instance.music[instance.musicIndex];
        nxt.clip = clip;
        nxt.volume = 0f;
        if (clip != null) nxt.Play();
        instance.fadeUntil = Time.unscaledTime + fade;
    }

    /// <summary>Back to this scene's own track (after the boss fight resets).</summary>
    public static void SceneMusic() => PlayMusic(SceneTrack(SceneManager.GetActiveScene().name));

    private void Update()
    {
        // Crossfade: the current track rises to full, the other falls away.
        float step = Time.unscaledDeltaTime / 1.5f * MusicVolume;
        for (int i = 0; i < 2; i++)
        {
            var m = music[i];
            m.volume = Mathf.MoveTowards(m.volume, i == musicIndex && m.clip != null ? MusicVolume : 0f, step);
            if (i != musicIndex && m.volume <= 0f && m.isPlaying) m.Stop();
        }
    }

    /// <summary>A sound from the player's own position (no falloff).</summary>
    public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.05f) => Emit(clip, Vector3.zero, false, volume, pitchJitter, 0f);

    /// <summary>A sound in the world: loud within ~6 m, gone past ~60 m.</summary>
    public static void At(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.05f) => Emit(clip, position, true, volume, pitchJitter, 0f);

    /// <summary>Only the last <paramref name="seconds"/> of a clip, so a long build-up ends exactly when something goes off.</summary>
    public static void Tail(AudioClip clip, float seconds, Vector3? position = null, float volume = 1f)
    {
        if (clip == null) return;
        Emit(clip, position ?? Vector3.zero, position.HasValue, volume, 0f, Mathf.Max(0f, clip.length - seconds));
    }

    private static void Emit(AudioClip clip, Vector3 position, bool spatial, float volume, float pitchJitter, float startAt)
    {
        if (instance == null || clip == null) return;
        var src = instance.pool[instance.next];
        instance.next = (instance.next + 1) % instance.pool.Length;   // oldest voice gets reused
        if (spatial)
        {
            // Pool sources live on the persistent object: move a child there for 3D sounds.
            src = instance.Spatial(src);
            src.transform.position = position;
        }
        src.Stop();
        src.clip = clip;
        src.volume = volume;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.time = Mathf.Min(startAt, clip.length - 0.01f);
        src.Play();
    }

    // Each 2D pool source has a 3D twin on its own child object.
    private readonly AudioSource[] spatial = new AudioSource[24];
    private AudioSource Spatial(AudioSource flat)
    {
        int i = System.Array.IndexOf(pool, flat);
        if (spatial[i] == null)
        {
            var go = new GameObject("Voice3D");
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 6f;
            s.maxDistance = 60f;
            s.dopplerLevel = 0f;
            spatial[i] = s;
        }
        return spatial[i];
    }
}
