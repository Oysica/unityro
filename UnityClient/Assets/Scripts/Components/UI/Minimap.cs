using ROIO;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class Minimap : MonoBehaviour {

    [SerializeField] private RawImage PlayerIndicator;
    
    private Texture2D MapThumbTexture;
    private Texture2D PlayerIndicatorTexture;

    private RawImage MapThumb;
    private string CurrentMap;
    private int CurrentZoom = 1;

    // Start is called before the first frame update
    void Start() {
        MapThumb = GetComponent<RawImage>();

        PlayerIndicatorTexture = TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}map/map_arrow.png");
        Session.OnMapChanged += OnMapChanged;

        // The first map is set before the map scene (and this component) exists
        var currentMap = Session.CurrentSession?.CurrentMap;
        if (!string.IsNullOrEmpty(currentMap)) {
            OnMapChanged(currentMap);
        }
    }

    private void OnDestroy() {
        Session.OnMapChanged -= OnMapChanged;
    }

    private void OnMapChanged(string mapName) {
        CurrentMap = Path.GetFileNameWithoutExtension(mapName);
        MapThumbTexture = TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}map/{CurrentMap}.png");

        if (MapThumbTexture == null) {
            return;
        }

        MapThumb.texture = MapThumbTexture;
        var size = CalculateNewSize(MapThumbTexture.width, MapThumbTexture.height, 128, 128);
        (transform as RectTransform).sizeDelta = size;
    }

    private void Update() {
        if (CurrentMap != null && MapThumbTexture == null) {
            OnMapChanged(CurrentMap);
        }
    }

    private Vector2 CalculateNewSize(int srcWidth, int srcHeight, int maxWidth, int maxHeight) {
        var ratio = Mathf.Min((float) maxWidth / (float) srcWidth, (float) maxHeight / (float) srcHeight);
        return new Vector2(srcWidth * ratio, srcHeight * ratio);
    }

}
