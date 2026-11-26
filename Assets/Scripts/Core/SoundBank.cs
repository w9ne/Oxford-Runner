using UnityEngine;

/// <summary>
/// Every sound and music track in one asset (Resources/SoundBank, filled by Oxford Runner > Setup Sounds).
/// The player's own picks (Assets/Sound) come first; Leohpaz RPG Essentials fills the rest. Spell cast / hit
/// sounds live on the spell assets themselves.
/// </summary>
[CreateAssetMenu(menuName = "Oxford Runner/Sound Bank", fileName = "SoundBank")]
public class SoundBank : ScriptableObject
{
    [Header("Music")]
    public AudioClip menuMusic, level1Music, level2Music, level3Music, bossMusic;

    [Header("Player")]
    public AudioClip dash, jump, land, footstep, slide, wallRun, parry, parryRaise, playerHit, playerDeath, respawn;

    [Header("Arcane Blast (player and boss)")]
    public AudioClip blastCharge, blastBoom;

    [Header("Enemies")]
    public AudioClip enemyHit, enemyDeath, meleeSwing, beamIn;

    [Header("World")]
    public AudioClip areaStart, gateOpen, torch, levelClear, portal, drop, victory;

    [Header("UI")]
    public AudioClip uiHover, uiClick, pause, unpause, lockOn;
}
