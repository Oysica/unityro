using ROIO;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class Minimap : MonoBehaviour {

    private const float PARTY_MARKER_SIZE = 6f;
    private static readonly Color PartyMarkerColor = new Color(1f, 0.55f, 0.8f, 1f);

    [SerializeField] private RawImage PlayerIndicator;

    private Texture2D MapThumbTexture;
    private Texture2D PlayerIndicatorTexture;

    private RawImage MapThumb;
    private string CurrentMap;
    private int CurrentZoom = 1;

    private PathFinder PathFinder;
    private EntityManager EntityManager;
    private readonly List<Image> PartyMarkers = new List<Image>();

    // Start is called before the first frame update
    void Start() {
        MapThumb = GetComponent<RawImage>();
        EntityManager = FindObjectOfType<EntityManager>();

        PlayerIndicatorTexture = TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}map/map_arrow.png");
        SetUpPlayerIndicator();
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
        UpdateMarkers();
    }

    private Vector2 CalculateNewSize(int srcWidth, int srcHeight, int maxWidth, int maxHeight) {
        var ratio = Mathf.Min((float) maxWidth / (float) srcWidth, (float) maxHeight / (float) srcHeight);
        return new Vector2(srcWidth * ratio, srcHeight * ratio);
    }

    private void SetUpPlayerIndicator() {
        if (PlayerIndicator == null) {
            return;
        }
        if (PlayerIndicatorTexture != null) {
            PlayerIndicator.texture = PlayerIndicatorTexture;
        }
        var rect = PlayerIndicator.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        var side = PlayerIndicatorTexture != null ? Mathf.Clamp(Mathf.Max(PlayerIndicatorTexture.width, PlayerIndicatorTexture.height), 8, 16) : 12;
        rect.sizeDelta = new Vector2(side, side);
        PlayerIndicator.raycastTarget = false;
        // Over the party markers
        rect.SetAsLastSibling();
    }

    /// <summary>
    /// Where we and the party members on our map are, the map picture being the whole map
    /// </summary>
    private void UpdateMarkers() {
        if (PathFinder == null) {
            PathFinder = FindObjectOfType<PathFinder>();
        }
        var cells = PathFinder != null && PathFinder.Altitude != null
            ? new Vector2(PathFinder.Altitude.getWidth(), PathFinder.Altitude.getHeight())
            : Vector2.zero;
        var visible = MapThumbTexture != null && cells.x > 0 && cells.y > 0;

        var self = Session.CurrentSession?.Entity as Entity;
        if (PlayerIndicator != null) {
            var showSelf = visible && self != null;
            PlayerIndicator.gameObject.SetActive(showSelf);
            if (showSelf) {
                PlayerIndicator.rectTransform.anchoredPosition = ToMinimap(self.transform.position, cells);
                // The arrow points up, north; Direction goes round clockwise from south
                PlayerIndicator.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f - (int) self.Direction * 45f);
            }
        }

        var shown = 0;
        var party = PartyWindow.Instance;
        if (visible && party != null && party.InParty) {
            foreach (var member in party.Members) {
                if (!member.IsOnline || member.X < 0 || member.AID == Session.CurrentSession.AccountID || !IsOnThisMap(member.Map)) {
                    continue;
                }
                // One in sight is where we see it; the server tells where the others are now and then
                var entity = EntityManager != null ? EntityManager.FindEntity(member.AID) : null;
                var position = entity != null ? entity.transform.position : new Vector3(member.X, 0f, member.Y);
                PartyMarker(shown++).rectTransform.anchoredPosition = ToMinimap(position, cells);
            }
        }
        for (var i = shown; i < PartyMarkers.Count; i++) {
            PartyMarkers[i].gameObject.SetActive(false);
        }
    }

    private bool IsOnThisMap(string map) {
        return !string.IsNullOrEmpty(map) && string.Equals(Path.GetFileNameWithoutExtension(map), CurrentMap, StringComparison.OrdinalIgnoreCase);
    }

    private Vector2 ToMinimap(Vector3 position, Vector2 cells) {
        var size = (transform as RectTransform).rect.size;
        return new Vector2((position.x + 0.5f) / cells.x * size.x, (position.z + 0.5f) / cells.y * size.y);
    }

    private Image PartyMarker(int index) {
        while (PartyMarkers.Count <= index) {
            var marker = new GameObject("Party Marker", typeof(RectTransform)).AddComponent<Image>();
            marker.transform.SetParent(transform, false);
            marker.color = PartyMarkerColor;
            marker.raycastTarget = false;
            marker.gameObject.AddComponent<Outline>().effectColor = Color.black;
            var rect = marker.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(PARTY_MARKER_SIZE, PARTY_MARKER_SIZE);
            PartyMarkers.Add(marker);
            if (PlayerIndicator != null) {
                PlayerIndicator.rectTransform.SetAsLastSibling();
            }
        }
        var found = PartyMarkers[index];
        found.gameObject.SetActive(true);
        return found;
    }
}
