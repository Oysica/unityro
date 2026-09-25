using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
using ROIO;
#endif

/// <summary>
/// Loads a UI texture (item icon, collection image, ...) by its Addressable key.
///
/// In the editor, textures that were never extracted are read from the client's GRF, where they
/// are stored as .bmp (magenta becomes transparent), the same fallback SpriteAssetLoader gives
/// entity sprites. Builds only see extracted textures.
/// </summary>
public static class TextureAssetLoader {

    private static readonly Dictionary<string, Texture2D> GrfTextures = new Dictionary<string, Texture2D>();

    /// <param name="key">Addressable key, e.g. data/texture/.../item/apple.png</param>
    /// <returns>null when the texture exists neither as an Addressable nor in the GRF</returns>
    public static Texture2D Load(string key) {
        if (SpriteAssetLoader.HasAddressable(key)) {
            return Addressables.LoadAssetAsync<Texture2D>(key).WaitForCompletion();
        }

#if UNITY_EDITOR
        if (!GrfTextures.TryGetValue(key, out var texture)) {
            // The GRF keeps these as .bmp mostly; effects also use .tga and .jpg
            foreach (var extension in new[] { ".bmp", ".tga", ".jpg" }) {
                var file = System.IO.Path.ChangeExtension(key, extension);
                if (FileManager.ReadSync(file) == null) {
                    continue;
                }
                try {
                    texture = FileManager.Load(file) switch {
                        Texture2D bmp => bmp,
                        ROIO.Loaders.TGALoader.TGAImage tga => tga.ToTexture2D(),
                        FileManager.RawImage jpg => DecodeImage(jpg.data),
                        _ => null
                    };
                } catch (System.Exception) {
                    texture = null;
                }
                break;
            }
            GrfTextures[key] = texture;
        }
        return texture;
#else
        return null;
#endif
    }

    private static Texture2D DecodeImage(byte[] data) {
        var texture = new Texture2D(2, 2);
        return texture.LoadImage(data) ? texture : null;
    }
}
