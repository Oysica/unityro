using Assets.Scripts.Effects;
using ROIO;
using ROIO.Models.FileTypes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Skills left on the ground: Safety Wall, Pneuma, Sanctuary, Magnus Exorcismus, Warp Portal, ...
/// The server sends each cell of one as a unit of its own (a Sanctuary is 21), drawn with the
/// official effect, over and over, until the server takes it away
/// </summary>
public class SkillUnitManager : MonoBehaviour {

    // skill.hpp e_skill_unit_id
    private const int UNT_WARP_WAITING = 0x80;
    private const int UNT_WARP_ACTIVE = 0x81;
    private const int UNT_DUMMYSKILL = 0x86;

    // The official effect of each kind (data/texture/effect/*.str); the others show nothing, as
    // units the client has no picture for
    private static readonly Dictionary<int, string> EffectFiles = new Dictionary<int, string> {
        { 0x7e, "safetywall" }, // UNT_SAFETYWALL
        { 0x7f, "firewall" },   // UNT_FIREWALL
        { 0x82, "benedictio" }, // UNT_BENEDICTIO
        { 0x83, "sanctuary" },  // UNT_SANCTUARY
        { 0x84, "magnus" },     // UNT_MAGNUS
        { 0x85, "pneuma1" },    // UNT_PNEUMA
        { 0x87, "firepillar" }, // UNT_FIREPILLAR_WAITING
        { 0x88, "firepillar" }, // UNT_FIREPILLAR_ACTIVE
        { 0x8e, "quagmire" },   // UNT_QUAGMIRE
        { 0x90, "skidtrap" },   // UNT_SKIDTRAP
        { 0x93, "landmine" },   // UNT_LANDMINE
    };

    private static SkillUnitManager Instance;
    private static readonly Dictionary<string, STR> Effects = new Dictionary<string, STR>();

    private readonly Dictionary<uint, GameObject> Units = new Dictionary<uint, GameObject>();
    private NetworkClient NetworkClient;
    private PathFinder PathFinder;

    private void Awake() {
        DontDestroyOnLoad(this);
        Instance = this;
        NetworkClient = FindObjectOfType<NetworkClient>();
        // The effects' pictures as the UI's: extracted, else (in the editor) from the GRF
        ROIO.Loaders.EffectLoader.TextureSource = key => TextureAssetLoader.Load(key);
    }

    private void Start() {
        NetworkClient.HookPacket(ZC.SKILL_ENTRY5.HEADER, OnUnitEntry);
        NetworkClient.HookPacket(ZC.SKILL_DISAPPEAR.HEADER, OnUnitDisappear);
    }

    /// <summary>
    /// All of them, left behind with the rest of what was around (a warp, a map change)
    /// </summary>
    public static void Clear() {
        if (Instance == null) {
            return;
        }
        foreach (var unit in Instance.Units.Values) {
            if (unit != null) {
                Destroy(unit);
            }
        }
        Instance.Units.Clear();
    }

    private void OnUnitEntry(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.SKILL_ENTRY5 entry)) {
            return;
        }

        Remove(entry.AID);
        // Hidden traps and the other cells of a skill drawn once over its range come as dummies
        if (!entry.Visible || entry.UnitId == UNT_DUMMYSKILL) {
            return;
        }

        var unit = new GameObject($"Skill Unit {entry.UnitId:x} {entry.AID}");
        unit.transform.SetParent(transform, false);
        // The middle of the cell, as the grid under the cursor and warp portals
        unit.transform.position = new Vector3(entry.X + 0.5f, GroundHeight(entry.X + 0.5f, entry.Y + 0.5f), entry.Y + 0.5f);
        Units[entry.AID] = unit;

        if (entry.UnitId == UNT_WARP_WAITING || entry.UnitId == UNT_WARP_ACTIVE) {
            unit.AddComponent<MapWarpEffect>().StartWarp(unit);
            return;
        }

        if (EffectFiles.TryGetValue(entry.UnitId, out var file)) {
            var str = LoadEffect(file);
            if (str == null) {
                return;
            }
            var effect = new GameObject("Effect");
            effect.transform.SetParent(unit.transform, false);
            effect.AddComponent<Billboard>();
            var renderer = effect.AddComponent<StrEffectRenderer>();
            renderer.Loop = true;
            renderer.Initialize(str);
        }
    }

    private void OnUnitDisappear(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.SKILL_DISAPPEAR disappear) {
            Remove(disappear.AID);
        }
    }

    private void Remove(uint id) {
        if (Units.TryGetValue(id, out var unit)) {
            if (unit != null) {
                Destroy(unit);
            }
            Units.Remove(id);
        }
    }

    private float GroundHeight(float x, float z) {
        // A new one with every map
        if (PathFinder == null) {
            PathFinder = FindObjectOfType<PathFinder>();
        }
        return PathFinder != null && PathFinder.Altitude != null ? (float) PathFinder.Altitude.GetCellHeight(x, z) : 0f;
    }

    /// <summary>
    /// The extracted effect, or in the editor the client's GRF; null when neither has it
    /// </summary>
    private static STR LoadEffect(string name) {
        var key = $"data/texture/effect/{name}.str";
        // A C# null entry means it isn't there; a destroyed one (== null) is loaded again
        if (Effects.TryGetValue(key, out var cached) && ((object) cached == null || cached != null)) {
            return cached;
        }

        STR str = null;
        if (SpriteAssetLoader.HasAddressable(key)) {
            str = Addressables.LoadAssetAsync<STR>(key).WaitForCompletion();
        }
#if UNITY_EDITOR
        else {
            try {
                str = FileManager.Load(key, true) as STR;
            } catch (System.Exception) {
                str = null;
            }
            // Ours to keep: the file cache frees what it holds on map changes
            FileCache.Remove(key);
        }
#endif
        if (str != null) {
            str.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
        Effects[key] = str;
        return str;
    }
}
