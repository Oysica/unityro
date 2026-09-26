using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// How far each ground skill reaches round the cell it's cast on, in cells: 4 is 9 by 9. From
/// the pre-renewal skill_db (db/pre-re/skill_db.yml): the unit layout, else the unit's own range,
/// else the splash area; for the layouts the server builds itself (Sanctuary, Magnus Exorcismus,
/// Fire Wall, ...) as far as they reach. Not the range it's cast from
/// </summary>
public static class SkillAreas {

    // By level, the last one on for the levels past it
    private static readonly Dictionary<int, int[]> Reaches = new Dictionary<int, int[]> {
        { 12, new[] { 0 } }, // MG_SAFETYWALL
        { 18, new[] { 1 } }, // MG_FIREWALL
        { 21, new[] { 2 } }, // MG_THUNDERSTORM
        { 25, new[] { 1 } }, // AL_PNEUMA
        { 27, new[] { 0 } }, // AL_WARP
        { 47, new[] { 2 } }, // AC_SHOWER
        { 69, new[] { 1 } }, // PR_BENEDICTIO
        { 70, new[] { 2 } }, // PR_SANCTUARY
        { 79, new[] { 3 } }, // PR_MAGNUS
        { 80, new[] { 1 } }, // WZ_FIREPILLAR
        { 83, new[] { 3 } }, // WZ_METEOR
        { 85, new[] { 5 } }, // WZ_VERMILION
        { 87, new[] { 2 } }, // WZ_ICEWALL
        { 89, new[] { 4 } }, // WZ_STORMGUST
        { 91, new[] { 2 } }, // WZ_HEAVENDRIVE
        { 92, new[] { 2 } }, // WZ_QUAGMIRE
        { 110, new[] { 2 } }, // BS_HAMMERFALL
        { 115, new[] { 1 } }, // HT_SKIDTRAP
        { 116, new[] { 1 } }, // HT_LANDMINE
        { 117, new[] { 1 } }, // HT_ANKLESNARE
        { 118, new[] { 1 } }, // HT_SHOCKWAVE
        { 119, new[] { 1 } }, // HT_SANDMAN
        { 120, new[] { 1 } }, // HT_FLASHER
        { 121, new[] { 1 } }, // HT_FREEZINGTRAP
        { 122, new[] { 1 } }, // HT_BLASTMINE
        { 123, new[] { 1 } }, // HT_CLAYMORETRAP
        { 125, new[] { 1 } }, // HT_TALKIEBOX
        { 130, new[] { 3 } }, // HT_DETECTING
        { 140, new[] { 1 } }, // AS_VENOMDUST
        { 220, new[] { 0 } }, // RG_GRAFFITI
        { 221, new[] { 0 } }, // RG_FLAGGRAFFITI
        { 222, new[] { 5 } }, // RG_CLEANER
        { 229, new[] { 1 } }, // AM_DEMONSTRATION
        { 232, new[] { 0 } }, // AM_CANNIBALIZE
        { 233, new[] { 0 } }, // AM_SPHEREMINE
        { 264, new[] { 0 } }, // MO_BODYRELOCATION
        { 285, new[] { 3 } }, // SA_VOLCANO
        { 286, new[] { 3 } }, // SA_DELUGE
        { 287, new[] { 3 } }, // SA_VIOLENTGALE
        { 288, new[] { 3, 3, 4, 4, 5 } }, // SA_LANDPROTECTOR
        { 388, new[] { 0 } }, // WS_SYSTEMCREATE
        { 404, new[] { 2 } }, // PF_FOGWALL
        { 478, new[] { 3 } }, // CR_SLIMPITCHER
        { 483, new[] { 1 } }, // HW_GANBANTEIN
        { 484, new[] { 2 } }, // HW_GRAVITATION
        { 491, new[] { 0 } }, // CR_CULTIVATION
        { 521, new[] { 1 } }, // GS_GROUNDDRIFT
        { 529, new[] { 0 } }, // NJ_SHADOWJUMP
        { 538, new[] { 1, 1, 1, 2, 2, 2, 3, 3, 3, 4 } }, // NJ_SUITON
        { 670, new[] { 1 } }, // NPC_EVILLAND
        { 725, new[] { 2 } }, // NPC_REVERBERATION
        { 739, new[] { 1, 2, 3, 3, 3 } }, // NPC_CLOUD_KILL
        { 2005, new[] { 2 } }, // RK_WINDCUTTER
        { 2008, new[] { 1, 1, 1, 2, 2, 2, 3, 3, 4, 4 } }, // RK_DRAGONBREATH
        { 2213, new[] { 9 } }, // WL_COMET
        { 2216, new[] { 2 } }, // WL_EARTHSTRAIN
        { 2032, new[] { 2 } }, // GC_POISONSMOKE
        { 2044, new[] { 2 } }, // AB_EPICLESIS
        { 2237, new[] { 3 } }, // RA_DETONATOR
        { 2238, new[] { 1 } }, // RA_ELECTRICSHOCKER
        { 2239, new[] { 1 } }, // RA_CLUSTERBOMB
        { 2249, new[] { 1 } }, // RA_MAGENTATRAP
        { 2250, new[] { 1 } }, // RA_COBALTTRAP
        { 2251, new[] { 1 } }, // RA_MAIZETRAP
        { 2252, new[] { 1 } }, // RA_VERDURETRAP
        { 2253, new[] { 1 } }, // RA_FIRINGTRAP
        { 2254, new[] { 1 } }, // RA_ICEBOUNDTRAP
        { 2260, new[] { 2, 3, 4 } }, // NC_COLDSLOWER
        { 2281, new[] { 0 } }, // NC_SILVERSNIPER
        { 2282, new[] { 0 } }, // NC_MAGICDECOY
        { 2299, new[] { 1 } }, // SC_MANHOLE
        { 2300, new[] { 0 } }, // SC_DIMENSIONDOOR
        { 2301, new[] { 2 } }, // SC_CHAOSPANIC
        { 2302, new[] { 2 } }, // SC_MAELSTROM
        { 2303, new[] { 3 } }, // SC_BLOODYLUST
        { 2317, new[] { 0 } }, // LG_OVERBRAND
        { 2321, new[] { 5 } }, // LG_RAYOFGENESIS
        { 2518, new[] { 1, 1, 2, 2, 3 } }, // SR_RIDEINLIGHTNING
        { 2414, new[] { 2 } }, // WM_REVERBERATION
        { 2417, new[] { 5 } }, // WM_DOMINION_IMPULSE
        { 2418, new[] { 5 } }, // WM_SEVERE_RAINSTORM
        { 2419, new[] { 1 } }, // WM_POEMOFNETHERWORLD
        { 2426, new[] { 2, 3, 3, 4, 4 } }, // WM_GREAT_ECHO
        { 2429, new[] { 4, 4, 5, 5, 6 } }, // WM_SOUND_OF_DESTRUCTION
        { 2446, new[] { 3, 3, 3, 4, 4 } }, // SO_EARTHGRAVE
        { 2447, new[] { 3, 3, 3, 4, 4 } }, // SO_DIAMONDDUST
        { 2449, new[] { 3, 3, 4, 4, 5 } }, // SO_PSYCHIC_WAVE
        { 2450, new[] { 3 } }, // SO_CLOUD_KILL
        { 2452, new[] { 3 } }, // SO_WARMER
        { 2453, new[] { 1, 1, 2, 2, 3 } }, // SO_VACUUM_EXTREME
        { 2455, new[] { 1, 1, 2, 2, 3 } }, // SO_ARRULLO
        { 2465, new[] { 1 } }, // SO_FIRE_INSIGNIA
        { 2466, new[] { 1 } }, // SO_WATER_INSIGNIA
        { 2467, new[] { 1 } }, // SO_WIND_INSIGNIA
        { 2468, new[] { 1 } }, // SO_EARTH_INSIGNIA
        { 2479, new[] { 1 } }, // GN_THORNS_TRAP
        { 2482, new[] { 2 } }, // GN_WALLOFTHORN
        { 2483, new[] { 4 } }, // GN_CRAZYWEED
        { 2484, new[] { 1 } }, // GN_CRAZYWEED_ATK
        { 2485, new[] { 2 } }, // GN_DEMONIC_FIRE
        { 2486, new[] { 0 } }, // GN_FIRE_EXPANSION
        { 2487, new[] { 2 } }, // GN_FIRE_EXPANSION_SMOKE_POWDER
        { 2488, new[] { 2 } }, // GN_FIRE_EXPANSION_TEAR_GAS
        { 2490, new[] { 1 } }, // GN_HELLS_PLANT
        { 2555, new[] { 1, 2, 2, 3, 3 } }, // RL_B_TRAP
        { 2564, new[] { 0 } }, // RL_FALLEN_ANGEL
        { 2567, new[] { 1 } }, // RL_FIRE_RAIN
        { 3006, new[] { 1 } }, // KO_BAKURETSU
        { 3008, new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 2 } }, // KO_MUCHANAGE
        { 3009, new[] { 3 } }, // KO_HUUMARANKA
        { 3020, new[] { 2 } }, // KO_ZENKAI
        { 5004, new[] { 1, 1, 1, 2, 2, 2, 3, 3, 4, 4 } }, // RK_DRAGONBREATH_WATER
        { 5006, new[] { 3 } }, // NC_MAGMA_ERUPTION
        { 8020, new[] { 3 } }, // MH_POISON_MIST
        { 8025, new[] { 2, 2, 3, 3, 4 } }, // MH_XENO_SLASHER
        { 8041, new[] { 1, 1, 2, 2, 3 } }, // MH_LAVA_SLIDE
        { 8043, new[] { 1 } }, // MH_VOLCANIC_ASH
        { 8208, new[] { 2 } }, // MA_SHOWER
        { 8209, new[] { 1 } }, // MA_SKIDTRAP
        { 8210, new[] { 0 } }, // MA_LANDMINE
        { 8211, new[] { 1 } }, // MA_SANDMAN
        { 8212, new[] { 1 } }, // MA_FREEZINGTRAP
    };

    /// <summary>
    /// A skill that isn't a ground one, or unknown: a 5 by 5
    /// </summary>
    public const int DEFAULT_REACH = 2;

    /// <param name="level">0 for the widest, as for someone else's cast, whose level isn't sent</param>
    public static int Reach(int skillId, int level = 0) {
        if (!Reaches.TryGetValue(skillId, out var levels)) {
            return DEFAULT_REACH;
        }
        if (level <= 0) {
            return levels.Max();
        }
        return levels[Math.Min(level, levels.Length) - 1];
    }
}
