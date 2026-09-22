using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CustomUIAddressablesHolder), typeof(RawImage))]
public class CustomButton : Button,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler {

    public string backgroundImage;
    public string hoverImage;
    public string pressedImage;

    private Texture2D backgroundTexture;
    private Texture2D hoverTexture;
    private Texture2D pressedTexture;

    private RawImage rawImage;
    private CustomUIAddressablesHolder AddressablesHolder;

    protected override void OnEnable() {
        if (rawImage == null) {
            rawImage = GetComponent<RawImage>();
        }

        rawImage.texture = null;

        if (AddressablesHolder == null) {
            AddressablesHolder = GetComponent<CustomUIAddressablesHolder>();
        }

        if (AddressablesHolder.backgroundTexture.Asset != null) {
            rawImage.texture = (Texture2D) AddressablesHolder.backgroundTexture.Asset;
        }
    }

    protected override void Start() {
        rawImage.texture = null;
        LoadTextures();
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

    // TEMPORARY BRING-UP STUB: when this button's background can't be shown
    // (empty/broken Addressable GUID, or the load itself throws), the button
    // and any label text on it become effectively invisible/undiscoverable -
    // e.g. the char-select "Start Game" button, which has no valid
    // background/hover/pressed GUIDs in this data set. Tint the RawImage a
    // visible flat color instead of leaving it fully transparent/white-on-
    // white, purely so the button area (and its text child) stays visible
    // and clickable. Real fix is extracting/wiring the actual GRF assets.
    private static readonly Color FallbackTint = new Color(0.25f, 0.45f, 0.85f, 0.55f);

    private void LoadIdleTexture() {
        try {
            if (backgroundTexture == null && AddressablesHolder.backgroundTexture.AssetGUID.Length > 0) {
                backgroundTexture = AddressablesHolder.backgroundTexture.LoadAssetAsync().WaitForCompletion();
                rawImage.texture = backgroundTexture;
            }
        } catch (Exception e) {
            Debug.LogError($"Failed to load background image from {this} {e}");
        } finally {
            if (backgroundTexture == null) {
                rawImage.color = FallbackTint;
            }
        }
    }

    override public void OnPointerDown(PointerEventData eventData) {
        if (pressedTexture == null)
            return;
        rawImage.texture = pressedTexture;
    }

    override public void OnPointerUp(PointerEventData eventData) {
        if (hoverTexture == null)
            return;
        rawImage.texture = hoverTexture;
    }

    override public void OnPointerEnter(PointerEventData pointerEventData) {
        if (hoverTexture == null)
            return;
        rawImage.texture = hoverTexture;
    }

    override public void OnPointerExit(PointerEventData pointerEventData) {
        if (backgroundTexture == null)
            return;
        rawImage.texture = backgroundTexture;
    }

    override public void OnSelect(BaseEventData eventData) {
        if (hoverTexture == null)
            return;
        rawImage.texture = hoverTexture;
    }

    override public void OnDeselect(BaseEventData eventData) {
        if (backgroundTexture == null)
            return;
        rawImage.texture = backgroundTexture;
    }
}
