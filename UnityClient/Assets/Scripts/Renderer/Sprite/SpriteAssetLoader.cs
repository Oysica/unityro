using Assets.Scripts.Renderer.Sprite;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
using ROIO;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
#endif

/// <summary>
/// Loads an entity sprite (SpriteData, atlas and its own palette) by its GRF path without extension.
///
/// Extracted Addressables assets are used when they exist. In the editor, sprites that were never
/// extracted are decoded straight from the client's GRF (DataUtility loads it on every domain reload),
/// so NPCs, monsters and dropped items are visible and clickable without bulk-extracting the ~15k
/// sprites this data set has. Builds only see extracted sprites.
/// </summary>
public static class SpriteAssetLoader {

    public class LoadedSprite {
        public SpriteData Data;
        public Texture2D Atlas;
        public Texture2D Palette;
    }

    private static readonly Dictionary<string, LoadedSprite> GrfSprites = new Dictionary<string, LoadedSprite>();
    private static readonly Dictionary<string, Texture2D> GrfPalettes = new Dictionary<string, Texture2D>();
    private static readonly HashSet<string> ReportedMissing = new HashSet<string>();

    /// <summary>
    /// Checks the Addressables catalog first, so a missing asset doesn't log an InvalidKeyException.
    /// </summary>
    public static bool HasAddressable(string key) {
        var handle = Addressables.LoadResourceLocationsAsync(key);
        var locations = handle.WaitForCompletion();
        var found = locations != null && locations.Count > 0;
        Addressables.Release(handle);
        return found;
    }

    /// <param name="path">GRF path without extension, e.g. data/sprite/npc/4_f_kafra1</param>
    /// <param name="addressablePath">Addressable key without extension when it differs from <paramref name="path"/></param>
    /// <returns>null when the sprite exists neither as an Addressable nor in the GRF</returns>
    public static LoadedSprite Load(string path, string addressablePath = null) {
        addressablePath ??= path;
        if (HasAddressable($"{addressablePath}.asset") && HasAddressable($"{addressablePath}.png")) {
            return new LoadedSprite {
                Data = Addressables.LoadAssetAsync<SpriteData>($"{addressablePath}.asset").WaitForCompletion(),
                Atlas = Addressables.LoadAssetAsync<Texture2D>($"{addressablePath}.png").WaitForCompletion(),
                Palette = HasAddressable($"{addressablePath}_pal.png")
                    ? Addressables.LoadAssetAsync<Texture2D>($"{addressablePath}_pal.png").WaitForCompletion()
                    : null
            };
        }

#if UNITY_EDITOR
        if (GrfSprites.TryGetValue(path, out var cached)) {
            return cached;
        }

        var loaded = LoadFromGrf(path);
        GrfSprites[path] = loaded;
        if (loaded != null) {
            return loaded;
        }
#endif

        if (ReportedMissing.Add(path)) {
            Debug.LogWarning($"Missing sprite: {path}");
        }
        return null;
    }

    /// <summary>
    /// Loads a palette (e.g. a clothes or hair color) by path without extension, or null if it doesn't exist.
    /// </summary>
    public static Texture2D LoadPalette(string path) {
        if (HasAddressable($"{path}.png")) {
            return Addressables.LoadAssetAsync<Texture2D>($"{path}.png").WaitForCompletion();
        }

#if UNITY_EDITOR
        if (!GrfPalettes.TryGetValue(path, out var palette)) {
            // .pal files are 256 RGBA entries, the same layout as the palette embedded in a .spr
            var bytes = FileManager.ReadSync($"{path}.pal")?.ToArray();
            if (bytes != null && bytes.Length >= 1024) {
                palette = new Texture2D(256, 1, TextureFormat.RGBA32, false, true) {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                palette.LoadRawTextureData(bytes[..1024]);
                palette.Apply();
            }
            GrfPalettes[path] = palette;
        }
        return palette;
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static LoadedSprite LoadFromGrf(string path) {
        var spr = FileManager.ReadSync($"{path}.spr")?.ToArray();
        if (spr == null) {
            return null;
        }

        var loader = new CustomSpriteLoader();
        ACT act;
        try {
            // FileManager.Load throws for a missing file
            act = FileManager.Load($"{path}.act") as ACT;
            loader.Load(spr, System.IO.Path.GetFileName($"{path}.spr"));
        } catch (System.Exception e) {
            Debug.LogWarning($"Could not decode sprite {path} from the GRF: {e.Message}");
            return null;
        }
        if (act == null) {
            return null;
        }

        var data = ScriptableObject.CreateInstance<SpriteData>();
        data.act = act;
        data.rects = loader.SpriteRects;

        return new LoadedSprite {
            Data = data,
            Atlas = ToIndexAtlas(loader.Atlas),
            Palette = loader.Palette
        };
    }

    /// <summary>
    /// Matches what DataUtility.ProcessAtlas imports extracted atlases as: a linear, point-filtered
    /// single channel texture whose red value is the palette index the sprite shader looks up.
    /// PackTextures doesn't guarantee that format for the in-memory atlas.
    /// </summary>
    private static Texture2D ToIndexAtlas(Texture2D packed) {
        var atlas = new Texture2D(packed.width, packed.height, TextureFormat.R8, false, true) {
            name = packed.name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        atlas.SetPixels32(packed.GetPixels32());
        atlas.Apply(false, true);
        return atlas;
    }
#endif
}
