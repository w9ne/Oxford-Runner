using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Spell Books
/// One GreyratsLab "Stylized magic book" per spell slot under the player's body (Fire, Ice, Air, Necromancy,
/// Earth for Fireball, Frost Lance, Lightning, Arcane Missiles, Arcane Nova). PlayerBody shows the selected
/// slot's book and pops it in with a coloured spark burst when you switch. The pack's materials are URP, so
/// they are switched to Standard (they only use flat colours). The old open book is hidden. Safe to re-run.
/// </summary>
public static class SpellBookSetup
{
    private const string Dir = "Assets/GreyratsLab/Stylized magic books/";
    private const string Sparks = "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/";
    private static readonly string[] Books = { "Fire", "Ice", "Air", "Necromancy", "Earth" };
    private static readonly string[] Bursts = { "Sparks explode red", "Sparks explode blue", "Sparks explode yellow", "Sparks explode pink", "Sparks explode white" };
    private const float BookScale = 0.42f;   // under the 1.3x body: ~0.25 m long

    private static Color SavedColor(Material mat, string prop)
    {
        var colors = new SerializedObject(mat).FindProperty("m_SavedProperties.m_Colors");
        for (int i = 0; i < colors.arraySize; i++)
        {
            var e = colors.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("first").stringValue == prop) return e.FindPropertyRelative("second").colorValue;
        }
        return Color.white;
    }

    [MenuItem("Oxford Runner/Setup Spell Books")]
    public static void Setup()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Dir + "Materials" }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            Color c = SavedColor(mat, "_BaseColor");   // read the saved value: the missing URP shader exposes no properties
            mat.shader = Shader.Find("Standard");
            mat.color = c;
            mat.SetFloat("_Glossiness", 0.25f);
            EditorUtility.SetDirty(mat);
        }

        var bursts = new GameObject[Books.Length];
        for (int i = 0; i < Books.Length; i++)
            bursts[i] = VfxSetup.OneShot(Sparks + Bursts[i] + ".prefab", "BookSwap_" + Books[i], 0.25f);

        var player = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
        try
        {
            var body = player.GetComponentInChildren<PlayerBody>();
            var bodyRoot = body.transform.Find("PlayerBody") ?? body.transform;
            var old = bodyRoot.Find("SpellBooks");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var holder = new GameObject("SpellBooks").transform;
            holder.SetParent(bodyRoot, false);

            var books = new Transform[Books.Length];
            for (int i = 0; i < Books.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Prefab/" + Books[i] + " book.prefab");
                var b = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
                b.name = Books[i] + "Book";
                b.transform.localScale = Vector3.one * BookScale;
                foreach (var col in b.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                foreach (var r in b.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b.SetActive(i == 0);
                books[i] = b.transform;
            }

            var so = new SerializedObject(body);
            var oldBook = so.FindProperty("book").objectReferenceValue as Transform;
            if (oldBook != null && oldBook.parent != holder) oldBook.gameObject.SetActive(false);
            so.FindProperty("book").objectReferenceValue = books[0];
            var arr = so.FindProperty("spellBooks");
            var fx = so.FindProperty("bookSwapVfx");
            arr.arraySize = fx.arraySize = Books.Length;
            for (int i = 0; i < Books.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = books[i];
                fx.GetArrayElementAtIndex(i).objectReferenceValue = bursts[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        Debug.Log("[SpellBookSetup] Player now swaps between 5 spell books.");
    }
}
