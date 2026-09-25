using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
using Newtonsoft.Json;
using ROIO;
using UnityEngine.Networking;
#endif

/// <summary>
/// Loads sound effects (data/wav/...) and background music.
///
/// In the editor, sounds that were never extracted are read from the client's GRF, the same
/// fallback SpriteAssetLoader and TextureAssetLoader give sprites and textures; background music
/// isn't in the GRF, so it comes from the client's BGM folder. Builds only see extracted audio.
/// </summary>
public static class AudioAssetLoader {

    private static readonly Dictionary<string, AudioClip> GrfClips = new Dictionary<string, AudioClip>();
    private static readonly HashSet<string> MissingClips = new HashSet<string>();
    private static readonly Dictionary<string, AudioClip> BgmClips = new Dictionary<string, AudioClip>();

    /// <returns>null when the sound exists neither as an Addressable nor in the GRF</returns>
    public static AudioClip Load(string path) {
        var key = path.SanitizeForAddressables();
        if (SpriteAssetLoader.HasAddressable(key)) {
            return Addressables.LoadAssetAsync<AudioClip>(key).WaitForCompletion();
        }

#if UNITY_EDITOR
        // Sounds.Clear destroys a map's clips when it's left, so a cached clip can come back
        // destroyed (== null); load it again then, but don't retry sounds the GRF doesn't have
        if (GrfClips.TryGetValue(path, out var clip) && clip != null) {
            return clip;
        }
        if (MissingClips.Contains(path)) {
            return null;
        }
        try {
            clip = FileManager.ReadSync(path) == null ? null : FileManager.Load(path, true) as AudioClip;
        } catch (System.Exception) {
            clip = null;
        }
        if (clip == null) {
            MissingClips.Add(path);
        } else {
            clip.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            GrfClips[path] = clip;
        }
        return clip;
#else
        return null;
#endif
    }

    /// <param name="name">Map music file as mp3nametable lists it, e.g. 08.mp3</param>
    /// <returns>null when the music exists neither as an Addressable nor in the BGM folder</returns>
    public static async Task<AudioClip> LoadBgmAsync(string name) {
        var key = Path.Combine("bgm", name).SanitizeForAddressables();
        if (SpriteAssetLoader.HasAddressable(key)) {
            return await Addressables.LoadAssetAsync<AudioClip>(key).Task;
        }

#if UNITY_EDITOR
        if (BgmClips.TryGetValue(name, out var cached) && ((object) cached == null || cached != null)) {
            return cached;
        }

        AudioClip clip = null;
        var file = Path.Combine(ReadBgmFolder() ?? "", name);
        if (File.Exists(file)) {
            using var request = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(Path.GetFullPath(file)).AbsoluteUri, AudioType.MPEG);
            var operation = request.SendWebRequest();
            while (!operation.isDone) {
                await Task.Yield();
            }
            if (request.result == UnityWebRequest.Result.Success) {
                clip = DownloadHandlerAudioClip.GetContent(request);
                clip.name = name;
                clip.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            } else {
                Debug.LogWarning($"Could not load music {file}: {request.error}");
            }
        }
        BgmClips[name] = clip;
        return clip;
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// The client's BGM folder from StreamingAssets/config.json. Read directly rather than through
    /// ConfigurationLoader.Init, which overwrites config.json with defaults if loading it fails.
    /// </summary>
    private static string ReadBgmFolder() {
        try {
            var json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "config.json"));
            return JsonConvert.DeserializeObject<Configuration>(json)?.BgmPath;
        } catch (System.Exception e) {
            Debug.LogWarning($"Could not read the BGM folder from config.json: {e.Message}");
            return null;
        }
    }
#endif
}
