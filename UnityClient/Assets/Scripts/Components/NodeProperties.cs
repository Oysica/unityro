using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class NodeProperties : MonoBehaviour {
    //hierarchy
    public int nodeId;
    public string parentName;
    public string mainName;
    public string textureName;

    private MeshRenderer MeshRenderer;

    internal bool isChild {
        get { return !string.IsNullOrEmpty(parentName) && !parentName.Equals(mainName); }
    }

    public void SetTextureName(string textureName) {
        this.textureName = textureName;
    }

    private void Start() {
        MeshRenderer = GetComponent<MeshRenderer>();
        LoadTexture();
    }

    private void LoadTexture() {
        if (MeshRenderer.material.mainTexture != null)
            return;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(textureName);
        var directory = Path.GetDirectoryName(textureName);
        var path = Path.Combine("data", "texture", directory, $"{nameWithoutExtension}.png").SanitizeForAddressables();
        // Only prontera's model textures were extracted; elsewhere the editor reads them from the GRF
        var grfPath = Path.Combine("data", "texture", textureName).Replace('\\', '/');
        var texture = TextureAssetLoader.Load(path, grfPath, mapTexture: true);

        if (texture == null) {
            var filename = nameWithoutExtension.ToLowerInvariant();
            var newPath = Path.Combine("data", "texture", directory, $"{filename}.png").SanitizeForAddressables();
            texture = TextureAssetLoader.Load(newPath, grfPath, mapTexture: true);
        }

        MeshRenderer.material.mainTexture = texture;
    }
}
