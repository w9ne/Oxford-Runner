using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Sounds. Fills Resources/SoundBank and the spells' cast / hit sounds. Your own
/// sounds (Assets/Sound/Music) take the key moments: dash, parry, player hit, footsteps, the Arcane Blast boom,
/// Level 1 / Level 3 / boss music. Leohpaz RPG Essentials (free) fills the rest. Music streams from disk.
/// Safe to re-run.
/// </summary>
public static class SoundSetup
{
    private const string Mine = "Assets/Sound/Music/";
    private const string Leo = "Assets/Leohpaz/RPG_Essentials_Free/";
    private const string BankPath = "Assets/Resources/SoundBank.asset";

    [MenuItem("Oxford Runner/Setup Sounds")]
    public static void Setup()
    {
        var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null) { bank = ScriptableObject.CreateInstance<SoundBank>(); AssetDatabase.CreateAsset(bank, BankPath); }

        bank.level1Music = Music("level1OST.mp3");
        bank.level2Music = bank.level1Music;   // no Level 2 track yet: Level 1's carries on through the castle
        bank.level3Music = Music("level3OST.mp3");
        bank.menuMusic = bank.level3Music;     // the library theme suits the rune-wall menu
        bank.bossMusic = Music("finalBossOST.mp3");

        bank.dash = Clip(Mine + "dashSFX.mp3");
        bank.parry = Clip(Mine + "parrySFX.mp3");
        bank.playerHit = Clip(Mine + "playerHit.mp3");
        bank.footstep = Clip(Mine + "footstepSounds.mp3");
        bank.blastBoom = Clip(Mine + "arcaneBlastSFX.mp3");
        bank.blastCharge = Clip(Leo + "8_Atk_Magic_SFX/45_Charge_05.wav");

        bank.jump = Clip(Leo + "12_Player_Movement_SFX/30_Jump_03.wav");
        bank.land = Clip(Leo + "12_Player_Movement_SFX/45_Landing_01.wav");
        bank.slide = Clip(Leo + "12_Player_Movement_SFX/52_Dive_02.wav");
        bank.wallRun = Clip(Leo + "12_Player_Movement_SFX/42_Cling_climb_03.wav");
        bank.parryRaise = Clip(Leo + "10_Battle_SFX/39_Block_03.wav");
        bank.playerDeath = Clip(Leo + "10_Battle_SFX/77_flesh_02.wav");
        bank.respawn = Clip(Leo + "8_Buffs_Heals_SFX/30_Revive_03.wav");

        bank.enemyHit = Clip(Leo + "10_Battle_SFX/15_Impact_flesh_02.wav");
        bank.enemyDeath = Clip(Leo + "10_Battle_SFX/69_Enemy_death_01.wav");
        bank.meleeSwing = Clip(Leo + "10_Battle_SFX/22_Slash_04.wav");
        bank.beamIn = Clip(Leo + "12_Player_Movement_SFX/88_Teleport_02.wav");

        bank.areaStart = Clip(Leo + "10_Battle_SFX/55_Encounter_02.wav");
        bank.gateOpen = Clip(Leo + "8_Atk_Magic_SFX/30_Earth_02.wav");
        bank.torch = Clip(Leo + "8_Atk_Magic_SFX/04_Fire_explosion_04_medium.wav");
        bank.levelClear = Clip(Leo + "8_Buffs_Heals_SFX/16_Atk_buff_04.wav");
        bank.portal = Clip(Leo + "12_Player_Movement_SFX/88_Teleport_02.wav");
        bank.drop = Clip(Leo + "8_Atk_Magic_SFX/25_Wind_01.wav");
        bank.victory = Clip(Leo + "8_Buffs_Heals_SFX/30_Revive_03.wav");

        bank.uiHover = Clip(Leo + "10_UI_Menu_SFX/001_Hover_01.wav");
        bank.uiClick = Clip(Leo + "10_UI_Menu_SFX/013_Confirm_03.wav");
        bank.pause = Clip(Leo + "10_UI_Menu_SFX/092_Pause_04.wav");
        bank.unpause = Clip(Leo + "10_UI_Menu_SFX/098_Unpause_04.wav");
        bank.lockOn = Clip(Leo + "10_UI_Menu_SFX/070_Equip_10.wav");
        EditorUtility.SetDirty(bank);

        // Spells: what you hear when they're cast and when they land (enemy bolts too).
        SpellSounds("Spell1_Fireball", Leo + "8_Atk_Magic_SFX/25_Wind_01.wav", Leo + "8_Atk_Magic_SFX/04_Fire_explosion_04_medium.wav");
        SpellSounds("Spell2_FrostLance", Leo + "8_Atk_Magic_SFX/22_Water_02.wav", Leo + "8_Atk_Magic_SFX/13_Ice_explosion_01.wav");
        SpellSounds("Spell3_ChainLightning", Leo + "8_Atk_Magic_SFX/18_Thunder_02.wav", null);
        SpellSounds("Spell4_ArcaneMissiles", Leo + "8_Buffs_Heals_SFX/39_Absorb_04.wav", Leo + "10_Battle_SFX/03_Claw_03.wav");
        SpellSounds("Spell5_ArcaneNova", null, null);   // charge-up + your blast sound, played by NovaSpell itself
        SpellSounds("Enemy_Bolt", Leo + "8_Buffs_Heals_SFX/21_Debuff_01.wav", Leo + "10_Battle_SFX/08_Bite_04.wav");
        SpellSounds("Enemy_MageBolt", Leo + "8_Atk_Magic_SFX/46_Poison_01.wav", Leo + "10_Battle_SFX/08_Bite_04.wav");
        AssetDatabase.SaveAssets();
        Debug.Log("[SoundSetup] SoundBank and spell sounds filled.");
    }

    private static AudioClip Clip(string path)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) Debug.LogWarning("[SoundSetup] Missing " + path);
        return c;
    }

    // Long tracks stream instead of sitting decompressed in memory (~5 min of stereo each).
    private static AudioClip Music(string file)
    {
        var imp = (AudioImporter)AssetImporter.GetAtPath(Mine + file);
        var s = imp.defaultSampleSettings;
        if (s.loadType != AudioClipLoadType.Streaming)
        {
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
        return Clip(Mine + file);
    }

    private static void SpellSounds(string asset, string cast, string hit)
    {
        var spell = AssetDatabase.LoadAssetAtPath<Spell>("Assets/Spells/" + asset + ".asset");
        if (spell == null) { Debug.LogWarning("[SoundSetup] Missing spell " + asset); return; }
        spell.castSound = cast != null ? Clip(cast) : null;
        spell.hitSound = hit != null ? Clip(hit) : null;
        EditorUtility.SetDirty(spell);
    }
}
