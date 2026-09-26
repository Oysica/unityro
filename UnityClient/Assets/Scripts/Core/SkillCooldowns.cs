using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// When our skills may be used again: each skill's own cooldown (ZC_SKILL_POSTDELAY), and the delay
/// after any skill (EFST_POSTDELAY), which holds every skill back. The server keeps the time;
/// this is what the shortcuts count down
/// </summary>
public static class SkillCooldowns {

    private struct Wait {
        public float Start;
        public float End;
    }

    private static readonly Dictionary<int, Wait> Cooldowns = new Dictionary<int, Wait>();
    private static Wait AfterCastDelay;

    private static float Now => Time.realtimeSinceStartup;

    public static void StartCooldown(int skillId, float seconds) {
        if (seconds <= 0f) {
            Cooldowns.Remove(skillId);
            return;
        }
        Cooldowns[skillId] = new Wait { Start = Now, End = Now + seconds };
    }

    public static void StartAfterCastDelay(float seconds) {
        AfterCastDelay = seconds > 0f ? new Wait { Start = Now, End = Now + seconds } : new Wait();
    }

    /// <summary>
    /// Seconds before the skill may be used again, and how much of the wait is left, 1 to 0
    /// </summary>
    public static float Remaining(int skillId, out float share) {
        var wait = AfterCastDelay;
        if (Cooldowns.TryGetValue(skillId, out var own) && own.End > wait.End) {
            wait = own;
        }

        var remaining = wait.End - Now;
        if (remaining <= 0f) {
            share = 0f;
            return 0f;
        }
        share = Mathf.Clamp01(remaining / Mathf.Max(0.001f, wait.End - wait.Start));
        return remaining;
    }

    public static bool IsReady(int skillId) {
        return Remaining(skillId, out _) <= 0f;
    }
}
