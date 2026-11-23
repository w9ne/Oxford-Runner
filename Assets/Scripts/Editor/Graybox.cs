using UnityEditor;
using UnityEngine;

/// <summary>Shared helpers for the editor level builders (arena, tutorial, ...).</summary>
public static class Graybox
{
    private const string MatDir = "Assets/Materials/";
    public const string WallRunTag = "WallRun";

    public static Material WallRun => Load("Graybox_WallRun");
    public static Material Platform => Load("Graybox_Platform");
    public static Material Wall => Load("Graybox_Wall");
    public static Material Goal => Load("Graybox_Goal");
    public static Material Enemy => Load("Graybox_Enemy");
    public static Material Door => Load("Graybox_Door");

    private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");

    public static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        if (parent != null) t.SetParent(parent, false);
        return t;
    }

    /// <summary>Static cube with the given world-space centre, size, material and optional yaw / WallRun tag.</summary>
    public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat,
                                 bool wallRun = false, float yaw = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (wallRun) go.tag = WallRunTag;
        go.isStatic = true;
        return go;
    }

    /// <summary>Invisible trigger box (no renderer).</summary>
    public static BoxCollider Trigger(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = center;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        box.isTrigger = true;
        return box;
    }
}
