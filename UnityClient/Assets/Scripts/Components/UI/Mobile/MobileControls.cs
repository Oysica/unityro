using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Playing on a touch screen: a joystick to walk, buttons to attack, use skills and pick up, and
/// targets picked for you. On by default on phones and tablets; the options window switches it.
/// </summary>
public static class MobileControls {

    private const string PREF_KEY = "UnityRO.MobileControls";

    // How far, in cells, the attack button and attack skills look for a monster
    public const float TARGET_RANGE = 14f;
    // A target already picked is kept a bit further
    private const float TARGET_KEEP_RANGE = 20f;
    public const float PICKUP_RANGE = 8f;

    private const string AUTO_LOCK_PREF_KEY = "UnityRO.MobileAutoLock";

    private static bool? enabled;
    private static bool? autoLock;

    public static event Action<bool> Changed;

    /// <summary>
    /// Lock onto the nearest monster whenever there's no target, as mobile games do
    /// </summary>
    public static bool AutoLock {
        get {
            if (!autoLock.HasValue) {
                autoLock = PlayerPrefs.GetInt(AUTO_LOCK_PREF_KEY, 1) == 1;
            }
            return autoLock.Value;
        }
        set {
            autoLock = value;
            PlayerPrefs.SetInt(AUTO_LOCK_PREF_KEY, value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value) {
                Target = null;
            }
        }
    }

    public static bool Enabled {
        get {
            if (!enabled.HasValue) {
                enabled = PlayerPrefs.GetInt(PREF_KEY, Application.isMobilePlatform ? 1 : 0) == 1;
            }
            return enabled.Value;
        }
        set {
            if (Enabled == value) {
                return;
            }
            enabled = value;
            PlayerPrefs.SetInt(PREF_KEY, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }

    /// <summary>
    /// What the attack button and attack skills go for, until it dies or gets away: a monster, or
    /// an enemy player where players fight. A player tapped elsewhere is locked on too, for
    /// support skills
    /// </summary>
    public static Entity Target { get; set; }

    /// <summary>
    /// The target, or the nearest monster (or enemy player) when there's none
    /// </summary>
    public static Entity FindTarget(Entity self) {
        if (!IsValidTarget(Target, self)) {
            Target = FindNearbyTargets(self, TARGET_RANGE).FirstOrDefault();
        }
        return Target;
    }

    /// <summary>
    /// The next monster (or enemy player) around, nearest first
    /// </summary>
    public static Entity NextTarget(Entity self) {
        var targets = FindNearbyTargets(self, TARGET_RANGE);
        if (targets.Count == 0) {
            Target = null;
            return null;
        }

        var index = Target == null ? -1 : targets.IndexOf(Target);
        Target = targets[(index + 1) % targets.Count];
        return Target;
    }

    /// <summary>
    /// Monsters, and players where they may be fought, nearest first
    /// </summary>
    public static List<Entity> FindNearbyTargets(Entity self, float range) {
        var position = self.transform.position;
        return UnityEngine.Object.FindObjectsOfType<Entity>()
            .Where(entity => entity != self && (entity.Type == EntityType.MOB || MapRules.IsEnemy(entity)) && IsAlive(entity) && CellDistance(position, entity.transform.position) <= range)
            .OrderBy(entity => CellDistance(position, entity.transform.position))
            .ToList();
    }

    public static List<Entity> FindNearby(Entity self, EntityType type, float range) {
        var position = self.transform.position;
        return UnityEngine.Object.FindObjectsOfType<Entity>()
            .Where(entity => entity != self && entity.Type == type && IsAlive(entity) && CellDistance(position, entity.transform.position) <= range)
            .OrderBy(entity => CellDistance(position, entity.transform.position))
            .ToList();
    }

    /// <summary>
    /// A live monster (or enemy player) still close enough to go for
    /// </summary>
    public static bool IsValidTarget(Entity entity, Entity self) {
        return entity != null
            && (entity.Type == EntityType.MOB || MapRules.IsEnemy(entity))
            && IsAlive(entity)
            && CellDistance(self.transform.position, entity.transform.position) <= TARGET_KEEP_RANGE;
    }

    /// <summary>
    /// Another player locked on, alive and near: support skills go on them
    /// </summary>
    public static bool IsSelected(Entity entity, Entity self) {
        return entity != null
            && entity.Type == EntityType.PC
            && entity != self
            && IsAlive(entity)
            && CellDistance(self.transform.position, entity.transform.position) <= TARGET_KEEP_RANGE;
    }

    private static bool IsAlive(Entity entity) {
        return entity.gameObject.activeInHierarchy && (entity.EntityViewer == null || entity.EntityViewer.State != SpriteState.Dead);
    }

    /// <summary>
    /// Cells apart the way the server counts ranges: the longer of the two axes
    /// </summary>
    public static float CellDistance(Vector3 a, Vector3 b) {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z));
    }
}
