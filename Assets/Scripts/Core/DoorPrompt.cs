using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows "Enter? [E]" while the player is within range; E opens the door (once).</summary>
public class DoorPrompt : MonoBehaviour
{
    public GateDoor door;
    public float range = 7f;
    private Transform player;
    private Text label;

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        var canvas = MenuUI.Canvas("DoorPromptCanvas", 8).transform;
        canvas.SetParent(transform, false);
        label = MenuUI.Label(canvas, "Enter?  [E]", 40, new Vector2(0f, -120f), new Vector2(600f, 60f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        label.enabled = false;
    }

    private void Update()
    {
        bool near = player != null && !GameFlow.Paused && (player.position - transform.position).sqrMagnitude < range * range;
        label.enabled = near;
        if (!near || !Input.GetKeyDown(KeyCode.E)) return;
        door.Open();
        Destroy(gameObject);
    }
}
