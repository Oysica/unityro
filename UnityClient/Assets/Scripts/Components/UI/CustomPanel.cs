using Assets.Scripts.Utils.Extensions;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CustomUIAddressablesHolder))]
public class CustomPanel : RawImage,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler {

    public string backgroundImage;
    public string hoverImage;
    public string pressedImage;
    public bool overrideSize = false;

    private Texture2D backgroundTexture;
    private Texture2D hoverTexture;
    private Texture2D pressedTexture;

    private CustomUIAddressablesHolder AddressablesHolder;

    protected override void OnEnable() {
        // Registers the graphic with its canvas: without it the panel is never raycast,
        // so clicks and hovers on it (e.g. the title bar close button) went nowhere
        base.OnEnable();
        texture = null;
        if (AddressablesHolder == null) {
            AddressablesHolder = gameObject.GetComponent<CustomUIAddressablesHolder>();
        }

        // What SetBackground picked (e.g. the selected tab) stays when the window is reopened
        if (backgroundTexture != null) {
            texture = backgroundTexture;
        } else if (AddressablesHolder.backgroundTexture.Asset != null) {
            texture = (Texture2D) AddressablesHolder.backgroundTexture.Asset;
        }
    }

    protected override void Start() {
        texture = null;
        LoadTextures();
        texture = backgroundTexture;
    }

    private void LoadTextures() {
        LoadIdleTexture();
        LoadHoverTexture();
        LoadPressedTexture();
    }

    private void LoadPressedTexture() {
        try {
            if (pressedTexture == null && AddressablesHolder.pressedTexture.AssetGUID.Length > 0) {
                 pressedTexture = AddressablesHolder.pressedTexture.LoadAssetAsync().WaitForCompletion();
            }
        } catch (Exception e) {
            Debug.LogError($"Failed to load pressed image from {this} {e}");
        }
    }

    private void LoadHoverTexture() {
        try {
            if (hoverTexture == null && AddressablesHolder.hoverTexture.AssetGUID.Length > 0) {
                hoverTexture = AddressablesHolder.hoverTexture.LoadAssetAsync().WaitForCompletion();
            }
        } catch (Exception e) {
            Debug.LogError($"Failed to load hover image from {this} {e}");
        }
    }

    private void LoadIdleTexture() {
        try {
            if (backgroundTexture == null && AddressablesHolder.backgroundTexture.AssetGUID.Length > 0) {
                backgroundTexture = AddressablesHolder.backgroundTexture.LoadAssetAsync().WaitForCompletion();
                texture = backgroundTexture;
                if (overrideSize)
                    SetNativeSize();
            }
        } catch (Exception e) {
            Debug.LogError($"Failed to load background image from {this} {e}");
        }
    }

    public void SetBackground(string path) {
        // Only some of these were extracted (the first inventory tab, not the others); the editor
        // reads the rest from the GRF. A missing one used to throw and stop the tab from switching
        var loaded = TextureAssetLoader.Load(DBManager.INTERFACE_PATH + path);
        if (loaded == null) {
            return;
        }

        backgroundTexture = loaded;
        texture = backgroundTexture;
        GetComponent<RectTransform>().sizeDelta = new Vector2(backgroundTexture.width, backgroundTexture.height);
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (pressedTexture != null) {
            texture = pressedTexture;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (hoverTexture != null) {
            texture = hoverTexture;
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (backgroundTexture != null) {
            texture = backgroundTexture;
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (hoverTexture != null) {
            texture = hoverTexture;
        }
    }
}