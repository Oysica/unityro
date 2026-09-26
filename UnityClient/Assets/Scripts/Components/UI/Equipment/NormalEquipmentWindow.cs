using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NormalEquipmentWindow : MonoBehaviour {

    public List<UIEquipSlot> slots;

    [SerializeField] private Entity WindowEntity;

    // The character is drawn by a camera of its own into a picture on the window: drawn straight
    // onto the UI, it went over every other window, whichever was on top
    private const int PREVIEW_LAYER = 31; // unnamed and unused: only the preview camera draws it
    private static readonly Vector3 PREVIEW_ORIGIN = new Vector3(0f, -10000f, 0f); // out of the map camera's sight
    private const int PREVIEW_RESOLUTION = 3; // texture pixels per UI unit, sharp on big screens too

    // Where the background (equipwin_bg2, 280x157) has things, from its top left corner:
    // the middle column the character stands in, the slot ovals' rows, and the slot columns
    private static readonly Rect PREVIEW_AREA = new Rect(114f, 0f, 55f, 130f);
    private static readonly float[] SLOT_ROWS = { 16.5f, 41.5f, 67.5f, 93.5f, 119.5f };
    private const float SLOT_HEIGHT = 26f;
    private const float COLUMN_WIDTH = 110f;
    private const float LEFT_NAME_START = 32f; // where the left column's grey labels start
    private const float RIGHT_NAME_END = 76f;  // and where the right column's end, by the oval
    // The arrows' row, above the character's head (about 22 down the column)
    private const float AMMO_HEIGHT = 20f;
    private const float AMMO_ICON = 18f;

    private Camera PreviewCamera;
    private RenderTexture PreviewTexture;
    private GameObject PreviewRig;
    private bool LaidOut;

    private void OnEnable() {
        Setup();
        if (PreviewCamera != null) {
            PreviewCamera.enabled = true;
        }
    }

    private void OnDisable() {
        if (PreviewCamera != null) {
            PreviewCamera.enabled = false;
        }
    }

    private void OnDestroy() {
        if (PreviewRig != null) {
            Destroy(PreviewRig);
        }
        if (PreviewTexture != null) {
            PreviewTexture.Release();
            Destroy(PreviewTexture);
        }
    }

    public void UpdateEquipment() {
        // The window may still be closed, so not set up yet
        Setup();

        var entity = (Session.CurrentSession.Entity as Entity);
        WindowEntity.Clone(entity, PREVIEW_LAYER, true);
        WindowEntity.SetReady(true, true);

        slots.ForEach(slot => slot.SetItem(null));

        var inventory = entity.Inventory;
        if (inventory == null || inventory.IsEmpty) return;

        // By where each item is worn (wearState), not everywhere it could go (location): two
        // accessories share a location, which threw here and filled both slots with either one.
        // Arrows count too, or every inventory change emptied the ammo slot
        foreach (var item in inventory.ItemList.Where(IsItemEquipped)) {
            foreach (var slot in slots) {
                if ((item.wearState & (int)slot.location) > 0) {
                    slot.SetItem(item);
                }
            }
        }
    }

    private void Setup() {
        if (!LaidOut) {
            LaidOut = true;
            LayOutSlots();
        }
        if (PreviewRig == null && WindowEntity != null) {
            BuildPreview();
        }
    }

    /// <summary>
    /// Each slot on its oval of the background: the layout groups stacked them a few pixels too
    /// high, and the right column narrower than its ovals, so its icons sat on the labels
    /// </summary>
    private void LayOutSlots() {
        foreach (var slot in slots) {
            var column = slot.transform.parent;
            if (column == transform) {
                LayOutAmmoSlot(slot);
                continue;
            }

            var layout = column.GetComponent<VerticalLayoutGroup>();
            if (layout != null) {
                layout.enabled = false;
            }

            var right = (column as RectTransform).anchorMin.x > 0.5f;
            var row = Mathf.Min(slot.transform.GetSiblingIndex(), SLOT_ROWS.Length - 1);
            var rect = slot.transform as RectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, SLOT_HEIGHT);
            rect.anchoredPosition = new Vector2(0f, -SLOT_ROWS[row]);

            if (slot.icon != null) {
                slot.icon.rectTransform.anchoredPosition = new Vector2(right ? -7f : 5f, 0f);
            }

            // The name where the grey label is, beside the icon, on the row's middle
            var name = slot.itemName;
            if (name != null) {
                name.rectTransform.anchorMin = new Vector2(right ? 0f : LEFT_NAME_START / COLUMN_WIDTH, 0f);
                name.rectTransform.anchorMax = new Vector2(right ? RIGHT_NAME_END / COLUMN_WIDTH : 1f, 1f);
                name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
                name.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                name.enableWordWrapping = false;
                name.overflowMode = TextOverflowModes.Ellipsis;
            }
        }
    }

    /// <summary>
    /// The arrows over the character's head, at the top of the middle column: below its feet is
    /// kept for the cart and the mount
    /// </summary>
    private void LayOutAmmoSlot(UIEquipSlot slot) {
        var rect = slot.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(PREVIEW_AREA.x, -1f);
        rect.sizeDelta = new Vector2(PREVIEW_AREA.width, AMMO_HEIGHT);

        if (slot.icon != null) {
            var icon = slot.icon.rectTransform;
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(2f, 0f);
            icon.sizeDelta = new Vector2(AMMO_ICON, AMMO_ICON);
        }

        var name = slot.itemName;
        if (name != null) {
            name.rectTransform.anchorMin = new Vector2((AMMO_ICON + 3f) / PREVIEW_AREA.width, 0f);
            name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.enableWordWrapping = false;
            name.enableAutoSizing = true;
            name.fontSizeMin = 7f;
            name.fontSizeMax = name.fontSize;
            name.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    /// <summary>
    /// The character, far below the map, filmed into a picture in the background's middle column
    /// </summary>
    private void BuildPreview() {
        var window = transform as RectTransform;
        var size = window.rect.size;
        var character = WindowEntity.transform;
        // UI units per character unit, and where the character stood, from the window's middle
        var scale = character.localScale.x;
        var feet = (Vector2) character.localPosition;
        var areaCenter = new Vector2(
            PREVIEW_AREA.x + PREVIEW_AREA.width / 2f - size.x * window.pivot.x,
            size.y * (1f - window.pivot.y) - PREVIEW_AREA.y - PREVIEW_AREA.height / 2f
        );

        PreviewTexture = new RenderTexture(
            Mathf.CeilToInt(PREVIEW_AREA.width * PREVIEW_RESOLUTION),
            Mathf.CeilToInt(PREVIEW_AREA.height * PREVIEW_RESOLUTION),
            16, RenderTextureFormat.ARGB32
        ) { name = "Equipment Preview" };

        var picture = new GameObject("Character View", typeof(RectTransform)).AddComponent<RawImage>();
        picture.transform.SetParent(transform, false);
        picture.transform.SetSiblingIndex(character.GetSiblingIndex());
        picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0f, 1f);
        picture.rectTransform.pivot = new Vector2(0f, 1f);
        picture.rectTransform.anchoredPosition = new Vector2(PREVIEW_AREA.x, -PREVIEW_AREA.y);
        picture.rectTransform.sizeDelta = PREVIEW_AREA.size;
        picture.texture = PreviewTexture;
        picture.raycastTarget = false;

        PreviewRig = new GameObject("Equipment Preview");
        PreviewRig.transform.position = PREVIEW_ORIGIN;
        character.SetParent(PreviewRig.transform, false);
        character.localPosition = Vector3.zero;
        character.localScale = Vector3.one;
        character.gameObject.layer = PREVIEW_LAYER;

        var offset = (areaCenter - feet) / scale;
        PreviewCamera = new GameObject("Camera").AddComponent<Camera>();
        PreviewCamera.transform.SetParent(PreviewRig.transform, false);
        PreviewCamera.transform.localPosition = new Vector3(offset.x, offset.y, -10f);
        PreviewCamera.orthographic = true;
        PreviewCamera.orthographicSize = PREVIEW_AREA.height / scale / 2f;
        PreviewCamera.nearClipPlane = 0.1f;
        PreviewCamera.farClipPlane = 20f;
        PreviewCamera.cullingMask = 1 << PREVIEW_LAYER;
        PreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        PreviewCamera.backgroundColor = Color.clear;
        PreviewCamera.allowHDR = false;
        PreviewCamera.allowMSAA = false;
        PreviewCamera.depth = -10;
        PreviewCamera.targetTexture = PreviewTexture;
        PreviewCamera.enabled = isActiveAndEnabled;
    }

    private bool IsItemEquipped(ItemInfo it) {
        return it.wearState > 0 &&
            it.itemType != (int)ItemType.CARD;
    }

    internal void UnequipAmmo() {
        slots.Find(it => it.location == EquipLocation.AMMO).SetItem(null);
    }

    public void EquipAmmo(ItemInfo item) {
        slots.Find(it => it.location == EquipLocation.AMMO).SetItem(item);
    }
}
