using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local leaderboard of campaign runs (name, total score), saved in PlayerPrefs so it survives restarts.
/// Shown from the main menu and the victory screen: place, name, points and the run's rank letter.
/// shortcut: one PC only (PlayerPrefs); move to an online service when runs need comparing across players.
/// </summary>
public static class Leaderboard
{
    public const int Size = 10;
    private const string Key = "LeaderboardRuns";

    public struct Entry { public string name; public int score; }

    public static List<Entry> Load() => Parse(PlayerPrefs.GetString(Key, ""));

    /// <summary>Adds a run and saves. Returns its place (0 = first), or -1 if it didn't make the board.</summary>
    public static int Add(string name, int score)
    {
        var list = Insert(Load(), name, score, out int place);
        PlayerPrefs.SetString(Key, string.Join("\n", list.Select(e => e.name + "\t" + e.score)));
        PlayerPrefs.Save();
        return place;
    }

    /// <summary>Highest first, at most Size; a tie goes below the older run.</summary>
    public static List<Entry> Insert(List<Entry> list, string name, int score, out int place)
    {
        var e = new Entry { name = Clean(name), score = score };
        place = list.Count(x => x.score >= score);
        var result = new List<Entry>(list);
        result.Insert(place, e);
        if (place >= Size) place = -1;
        return result.Take(Size).ToList();
    }

    public static List<Entry> Parse(string saved)
    {
        var list = new List<Entry>();
        foreach (var line in (saved ?? "").Split('\n'))
        {
            var parts = line.Split('\t');
            if (parts.Length == 2 && int.TryParse(parts[1], out int s)) list.Add(new Entry { name = parts[0], score = s });
        }
        return list.OrderByDescending(x => x.score).ToList();
    }

    /// <summary>Names are typed by the player: no separators, trimmed, at most 16 characters.</summary>
    public static string Clean(string name)
    {
        string n = new string((name ?? "").Where(c => c != '\t' && c != '\n' && c != '\r').ToArray()).Trim();
        if (n.Length > 16) n = n.Substring(0, 16);
        return n.Length == 0 ? "Wizard" : n;
    }

    /// <summary>Rank letter for a whole run: the total against the three levels' SSS scores added up.</summary>
    public static int RunRank(int total) => LevelResults.RankFor(total, LevelResults.SssScore.Values.Sum());

    /// <summary>The board as a panel: # / name / points / rank. highlight = the place to mark (or -1).</summary>
    public static RectTransform Panel(Transform parent, int highlight, System.Action onBack)
    {
        var panel = MenuUI.Rect(parent, "Leaderboard", Vector2.zero, new Vector2(820f, 820f), new Color(0.02f, 0.03f, 0.06f, 0.96f));
        MenuUI.Label(panel, "LEADERBOARD", 52, new Vector2(0f, 340f), new Vector2(780f, 70f), MenuUI.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        Row(panel, 270f, "#", "NAME", "POINTS", "RANK", new Color(1f, 1f, 1f, 0.5f), 24);
        MenuUI.Rect(panel, "Rule", new Vector2(0f, 248f), new Vector2(720f, 2f), new Color(MenuUI.Accent.r, MenuUI.Accent.g, MenuUI.Accent.b, 0.5f));
        var list = Load();
        if (list.Count == 0) MenuUI.Label(panel, "No runs yet. Beat your shadow to get on the board.", 26, new Vector2(0f, 120f), new Vector2(760f, 40f), new Color(1f, 1f, 1f, 0.6f));
        for (int i = 0; i < list.Count; i++)
        {
            int rank = RunRank(list[i].score);
            var row = Row(panel, 210f - i * 48f, (i + 1).ToString(), list[i].name, list[i].score.ToString("N0"), StyleMeter.Letters[rank],
                          i == highlight ? new Color(1f, 0.85f, 0.4f) : Color.white, 30);
            row[3].color = StyleMeter.Colors[rank];
        }
        MenuUI.Button(panel, "Back", new Vector2(0f, -350f), onBack, 300f);
        return panel;
    }

    private static Text[] Row(Transform parent, float y, string place, string name, string points, string rank, Color c, int size) => new[]
    {
        MenuUI.Label(parent, place, size, new Vector2(-330f, y), new Vector2(80f, 44f), c, TextAnchor.MiddleLeft, FontStyle.Bold),
        MenuUI.Label(parent, name, size, new Vector2(-70f, y), new Vector2(440f, 44f), c, TextAnchor.MiddleLeft),
        MenuUI.Label(parent, points, size, new Vector2(200f, y), new Vector2(200f, 44f), c, TextAnchor.MiddleRight, FontStyle.Bold),
        MenuUI.Label(parent, rank, size, new Vector2(330f, y), new Vector2(80f, 44f), c, TextAnchor.MiddleCenter, FontStyle.Bold),
    };
}
