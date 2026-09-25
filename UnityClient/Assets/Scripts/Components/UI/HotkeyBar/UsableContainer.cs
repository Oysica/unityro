using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One slot of the shortcut bar. It keeps what the server saves (an item or skill id) and looks
/// the item or skill up when used, so it keeps working as the inventory changes.
/// </summary>
public class UsableContainer : MonoBehaviour,
    IDropHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler {

    [SerializeField] private RawImage UsableImage;

    public int Index { get; set; }
    public Hotkey Hotkey { get; private set; }
    public Action<UsableContainer> OnHotkeyChanged;

    private Canvas Canvas;
    private RectTransform DragImage;

    private void Awake() {
        Canvas = Canvas.FindMainCanvas();
        SetHotkey(null);
    }

    public void SetHotkey(Hotkey hotkey) {
        Hotkey = hotkey == null || hotkey.IsEmpty ? null : hotkey;
        UsableImage.texture = GetTexture();
        UsableImage.enabled = UsableImage.texture != null;
    }

    private Texture2D GetTexture() {
        if (Hotkey == null) {
            return null;
        }

        if (Hotkey.IsSkill) {
            return SkillTable.Skills.TryGetValue((short) Hotkey.Id, out var skill)
                ? TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}item/{skill.SkillTag.ToLower()}.png")
                : null;
        }

        return TextureAssetLoader.Load(DBManager.GetItemResPath(Hotkey.Id, true));
    }

    public void Use() {
        if (Hotkey == null) {
            return;
        }

        var entity = Session.CurrentSession.Entity as Entity;
        if (Hotkey.IsSkill) {
            var skillInfo = entity.SkillTree.OwnedSkillsInfos.Find(it => it.SkillID == Hotkey.Id);
            if (skillInfo != null && skillInfo.Level > 0) {
                var level = (short) Mathf.Clamp(Hotkey.Count, 1, skillInfo.Level);
                entity.GetComponent<EntityControl>().UseSkill(skillInfo, level);
            }
        } else {
            var itemInfo = entity.Inventory.ItemList.Find(it => it.ItemID == Hotkey.Id);
            if (itemInfo != null) {
                GenericUIItem.UseItem(itemInfo);
            }
        }
    }

    public void OnDrop(PointerEventData eventData) {
        var dragged = eventData.pointerDrag;
        if (dragged == null) {
            return;
        }

        var slot = dragged.GetComponent<UsableContainer>();
        var item = dragged.GetComponent<GenericUIItem>();
        var skill = dragged.GetComponent<UISkill>();

        if (slot != null) {
            if (slot == this) {
                return;
            }
            // Moving onto another slot swaps the two
            var previous = Hotkey;
            ChangeHotkey(slot.Hotkey);
            slot.ChangeHotkey(previous);
        } else if (item != null && item.ItemInfo != null) {
            ChangeHotkey(new Hotkey { IsSkill = false, Id = item.ItemInfo.ItemID, Count = (short) item.ItemInfo.amount });
        } else if (skill != null && skill.Skill != null) {
            ChangeHotkey(new Hotkey { IsSkill = true, Id = skill.Skill.SkillId, Count = (short) skill.GetDisplayNumber() });
        }
    }

    private void ChangeHotkey(Hotkey hotkey) {
        SetHotkey(hotkey);
        OnHotkeyChanged?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.clickCount == 2) {
            Use();
        }
    }

    // Dragging a slot onto another moves it; letting it go anywhere else clears it
    public void OnBeginDrag(PointerEventData eventData) {
        if (Hotkey == null) {
            return;
        }

        var image = new GameObject("HotkeyDrag").AddComponent<RawImage>();
        image.transform.SetParent(Canvas.transform, false);
        image.transform.SetAsLastSibling();
        image.texture = UsableImage.texture;
        image.raycastTarget = false;
        image.SetNativeSize();

        DragImage = image.rectTransform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(Canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out var globalMousePos)
        ) {
            DragImage.position = globalMousePos;
        }
    }

    public void OnDrag(PointerEventData eventData) {
        if (DragImage != null) {
            DragImage.anchoredPosition += eventData.delta / Canvas.scaleFactor;
        }
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (DragImage == null) {
            return;
        }

        Destroy(DragImage.gameObject);
        DragImage = null;

        var target = eventData.pointerCurrentRaycast.gameObject;
        if (target == null || target.GetComponentInParent<UsableContainer>() == null) {
            ChangeHotkey(null);
        }
    }
}
