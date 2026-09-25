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
    /// <param name="grfPath">Path in the GRF when it can't be derived from <paramref name="key"/>
    /// (e.g. a model texture whose name had to be sanitized for Addressables)</param>
    /// <returns>null when the texture exists neither as an Addressable nor in the GRF</returns>
    public static Texture2D Load(string key, string grfPath = null) {
        if (SpriteAssetLoader.HasAddressable(key)) {
            return Addressables.LoadAssetAsync<Texture2D>(key).WaitForCompletion();
        }

#if UNITY_EDITOR
        grfPath ??= key;
        // A C# null entry means the GRF doesn't have it; a destroyed texture (== null) is loaded again
        if (GrfTextures.TryGetValue(grfPath, out var texture) && ((object) texture == null || texture != null)) {
            return texture;
        }

        texture = null;
        // The GRF keeps these as .bmp mostly; effects also use .tga and .jpg
        foreach (var extension in new[] { System.IO.Path.GetExtension(grfPath), ".bmp", ".tga", ".jpg" }) {
            var file = System.IO.Path.ChangeExtension(grfPath, extension);
            if (FileManager.ReadSync(file) == null) {
                continue;
            }
            try {
                // Load hands back the raw decoded image instead of a Texture2D when it can't cache it
                texture = FileManager.Load(file, true) switch {
                    Texture2D loaded => loaded,
                    B83.Image.BMP.BMPImage bmp => bmp.ToTexture2D(),
                    ROIO.Loaders.TGALoader.TGAImage tga => tga.ToTexture2D(),
                    FileManager.RawImage jpg => DecodeImage(jpg.data),
                    _ => null
                };
            } catch (System.Exception) {
                texture = null;
            }
            break;
        }
        // Only this cache references it, so leaving a scene would unload it otherwise
        if (texture != null) {
            texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
        GrfTextures[grfPath] = texture;
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
