using System.Collections.Generic;

/// <summary>
/// The basic emotions (emote pack 0): which action of data/sprite/이팩트/emotion shows each one,
/// and the chat commands that send them ("/!", "/ho", ...). From roBrowser's DB/Emotions.js.
/// </summary>
public static class EmotionTable {

    public const string SPRITE_PATH = "data/sprite/\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae/emotion"; // 이팩트 as the GRF names it

    // Emotion id -> action in the sprite; they differ (e.g. ET_SWEAT 4 is action 5)
    private static readonly int[] SpriteActions = {
        0, // 0 ET_SURPRISE
        1, // 1 ET_QUESTION
        2, // 2 ET_DELIGHT
        3, // 3 ET_THROB
        5, // 4 ET_SWEAT
        6, // 5 ET_AHA
        7, // 6 ET_FRET
        8, // 7 ET_ANGER
        9, // 8 ET_MONEY
        10, // 9 ET_THINK
        12, // 10 ET_SCISSOR
        11, // 11 ET_ROCK
        13, // 12 ET_WRAP
        14, // 13 ET_FLAG
        4, // 14 ET_BIGTHROB
        15, // 15 ET_THANKS
        16, // 16 ET_KEK
        17, // 17 ET_SORRY
        18, // 18 ET_SMILE
        19, // 19 ET_PROFUSELY_SWEAT
        20, // 20 ET_SCRATCH
        21, // 21 ET_BEST
        22, // 22 ET_STARE_ABOUT
        23, // 23 ET_HUK
        24, // 24 ET_O
        25, // 25 ET_X
        26, // 26 ET_HELP
        27, // 27 ET_GO
        28, // 28 ET_CRY
        29, // 29 ET_KIK
        30, // 30 ET_CHUP
        31, // 31 ET_CHUPCHUP
        32, // 32 ET_HNG
        33, // 33 ET_OK
        1000, // 34 ET_CHAT_PROHIBIT
        34, // 35 ET_INDONESIA_FLAG
        35, // 36 ET_STARE
        36, // 37 ET_HUNGRY
        37, // 38 ET_COOL
        38, // 39 ET_MERONG
        39, // 40 ET_SHY
        40, // 41 ET_GOODBOY
        41, // 42 ET_SPTIME
        42, // 43 ET_SEXY
        43, // 44 ET_COMEON
        44, // 45 ET_SLEEPY
        45, // 46 ET_CONGRATULATION
        46, // 47 ET_HPTIME
        47, // 48 ET_PH_FLAG
        48, // 49 ET_MY_FLAG
        49, // 50 ET_SI_FLAG
        50, // 51 ET_BR_FLAG
        51, // 52 ET_SPARK
        52, // 53 ET_CONFUSE
        53, // 54 ET_OHNO
        54, // 55 ET_HUM
        55, // 56 ET_BLABLA
        56, // 57 ET_OTL
        57, // 58 ET_DICE1
        58, // 59 ET_DICE2
        59, // 60 ET_DICE3
        60, // 61 ET_DICE4
        61, // 62 ET_DICE5
        62, // 63 ET_DICE6
        63, // 64 ET_INDIA_FLAG
        64, // 65 ET_LUV
        65, // 66 ET_FLAG8
        66, // 67 ET_FLAG9
        67, // 68 ET_MOBILE
        68, // 69 ET_MAIL
        69, // 70 ET_ANTENNA0
        70, // 71 ET_ANTENNA1
        71, // 72 ET_ANTENNA2
        72, // 73 ET_ANTENNA3
        73, // 74 ET_HUM2
        74, // 75 ET_ABS
        75, // 76 ET_OOPS
        76, // 77 ET_SPIT
        77, // 78 ET_ENE
        78, // 79 ET_PANIC
        79, // 80 ET_WHISP
        80, // 81 ET_!QUEST
        81, // 82 ET_?QUEST
        82, // 83 ET_!JOB
        83, // 84 ET_?JOB
        84, // 85 ET_!EVENT
        85, // 86 ET_?EVENT
        86, // 87 ET_???
    };

    private static readonly Dictionary<string, int> Commands = new Dictionary<string, int> {
        { "!", 0 }, // ET_SURPRISE
        { "?", 1 }, // ET_QUESTION
        { "ho", 2 }, // ET_DELIGHT
        { "delight", 2 }, // ET_DELIGHT
        { "rlQma", 2 }, // ET_DELIGHT
        { "lv", 3 }, // ET_THROB
        { "heart", 3 }, // ET_THROB
        { "gkxm", 3 }, // ET_THROB
        { "swt", 4 }, // ET_SWEAT
        { "sweat", 4 }, // ET_SWEAT
        { "Eka", 4 }, // ET_SWEAT
        { "ic", 5 }, // ET_AHA
        { "aha", 5 }, // ET_AHA
        { "dkgk", 5 }, // ET_AHA
        { "an", 6 }, // ET_FRET
        { "fret", 6 }, // ET_FRET
        { "Wkwmd", 6 }, // ET_FRET
        { "ag", 7 }, // ET_ANGER
        { "ghk", 7 }, // ET_ANGER
        { "anger", 7 }, // ET_ANGER
        { "$", 8 }, // ET_MONEY
        { "money", 8 }, // ET_MONEY
        { "ehs", 8 }, // ET_MONEY
        { "...", 9 }, // ET_THINK
        { "scissors", 10 }, // ET_SCISSOR
        { "rkdnl", 10 }, // ET_SCISSOR
        { "gawi", 10 }, // ET_SCISSOR
        { "rock", 11 }, // ET_ROCK
        { "wnajr", 11 }, // ET_ROCK
        { "bawi", 11 }, // ET_ROCK
        { "qkdnl", 11 }, // ET_ROCK
        { "paper", 12 }, // ET_WRAP
        { "qh", 12 }, // ET_WRAP
        { "bo", 12 }, // ET_WRAP
        { "lv2", 14 }, // ET_BIGTHROB
        { "thx", 15 }, // ET_THANKS
        { "wah", 16 }, // ET_KEK
        { "sry", 17 }, // ET_SORRY
        { "sorry", 17 }, // ET_SORRY
        { "heh", 18 }, // ET_SMILE
        { "smile", 18 }, // ET_SMILE
        { "swt2", 19 }, // ET_PROFUSELY_SWEAT
        { "hmm", 20 }, // ET_SCRATCH
        { "no1", 21 }, // ET_BEST
        { "??", 22 }, // ET_STARE_ABOUT
        { "omg", 23 }, // ET_HUK
        { "oh", 24 }, // ET_O
        { "o", 24 }, // ET_O
        { "X", 25 }, // ET_X
        { "x", 25 }, // ET_X
        { "hlp", 26 }, // ET_HELP
        { "help", 26 }, // ET_HELP
        { "go", 27 }, // ET_GO
        { "sob", 28 }, // ET_CRY
        { "gg", 29 }, // ET_KIK
        { "kis", 30 }, // ET_CHUP
        { "kis2", 31 }, // ET_CHUPCHUP
        { "pif", 32 }, // ET_HNG
        { "ok", 33 }, // ET_OK
        { "bzz", 36 }, // ET_STARE
        { "e1", 36 }, // ET_STARE
        { "rice", 37 }, // ET_HUNGRY
        { "e2", 37 }, // ET_HUNGRY
        { "awsm", 38 }, // ET_COOL
        { "e3", 38 }, // ET_COOL
        { "meh", 39 }, // ET_MERONG
        { "e4", 39 }, // ET_MERONG
        { "shy", 40 }, // ET_SHY
        { "e5", 40 }, // ET_SHY
        { "pat", 41 }, // ET_GOODBOY
        { "e6", 41 }, // ET_GOODBOY
        { "mp", 42 }, // ET_SPTIME
        { "e7", 42 }, // ET_SPTIME
        { "slur", 43 }, // ET_SEXY
        { "e8", 43 }, // ET_SEXY
        { "com", 44 }, // ET_COMEON
        { "e9", 44 }, // ET_COMEON
        { "yawn", 45 }, // ET_SLEEPY
        { "e10", 45 }, // ET_SLEEPY
        { "grat", 46 }, // ET_CONGRATULATION
        { "e11", 46 }, // ET_CONGRATULATION
        { "hp", 47 }, // ET_HPTIME
        { "e12", 47 }, // ET_HPTIME
        { "fsh", 52 }, // ET_SPARK
        { "e13", 52 }, // ET_SPARK
        { "spin", 53 }, // ET_CONFUSE
        { "e14", 53 }, // ET_CONFUSE
        { "sigh", 54 }, // ET_OHNO
        { "e15", 54 }, // ET_OHNO
        { "dum", 55 }, // ET_HUM
        { "e16", 55 }, // ET_HUM
        { "crwd", 56 }, // ET_BLABLA
        { "e17", 56 }, // ET_BLABLA
        { "desp", 57 }, // ET_OTL
        { "otl", 57 }, // ET_OTL
        { "e18", 57 }, // ET_OTL
        { "dice", 58 }, // ET_DICE1
        { "e19", 58 }, // ET_DICE1
        { "love", 65 }, // ET_LUV
        { "e20", 65 }, // ET_LUV
        { "mobile", 68 }, // ET_MOBILE
        { "e21", 68 }, // ET_MOBILE
        { "mail", 69 }, // ET_MAIL
        { "e22", 69 }, // ET_MAIL
        { "antenna0", 70 }, // ET_ANTENNA0
        { "e23", 70 }, // ET_ANTENNA0
        { "antenna1", 71 }, // ET_ANTENNA1
        { "e24", 71 }, // ET_ANTENNA1
        { "antenna2", 72 }, // ET_ANTENNA2
        { "e25", 72 }, // ET_ANTENNA2
        { "antenna3", 73 }, // ET_ANTENNA3
        { "e26", 73 }, // ET_ANTENNA3
        { "hum", 74 }, // ET_HUM2
        { "e27", 74 }, // ET_HUM2
        { "abs", 75 }, // ET_ABS
        { "e28", 75 }, // ET_ABS
        { "oops", 76 }, // ET_OOPS
        { "e29", 76 }, // ET_OOPS
        { "spit", 77 }, // ET_SPIT
        { "e30", 77 }, // ET_SPIT
        { "ene", 78 }, // ET_ENE
        { "e31", 78 }, // ET_ENE
        { "panic", 79 }, // ET_PANIC
        { "e32", 79 }, // ET_PANIC
        { "whisp", 80 }, // ET_WHISP
        { "e33", 80 }, // ET_WHISP
    };

    /// <returns>-1 when the emotion has no action in the sprite</returns>
    public static int GetSpriteAction(int emotion) {
        return emotion >= 0 && emotion < SpriteActions.Length ? SpriteActions[emotion] : -1;
    }

    /// <summary>The emotion a chat command like "/ho" sends, without its slash.</summary>
    public static bool TryGetCommand(string command, out int emotion) {
        return Commands.TryGetValue(command, out emotion);
    }
}
