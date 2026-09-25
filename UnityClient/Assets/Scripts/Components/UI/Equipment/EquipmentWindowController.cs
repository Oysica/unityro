using UnityEngine;
using UnityEngine.EventSystems;

public class EquipmentWindowController : DraggableUIWindow, IDropHandler {

    [SerializeField] private NormalEquipmentWindow NormalWindow;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void UpdateEquipment() {
        NormalWindow.UpdateEquipment();
    }

    public void EquipAmmo(ItemInfo item) {
        NormalWindow.EquipAmmo(item);
    }

    internal void UnequipAmmo() {
        NormalWindow.UnequipAmmo();
    }

    /// <summary>
    /// Dragging equipment or arrows from the inventory onto this window puts them on, as a double click does.
    /// </summary>
    public void OnDrop(PointerEventData eventData) {
        var dragged = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<GenericUIItem>() : null;
        var item = dragged != null ? dragged.ItemInfo : null;
        if (item == null || item.wearState > 0) {
            return;
        }

        // Only what the character carries: a storage item has an index of its own
        var inventory = (Session.CurrentSession.Entity as Entity).Inventory;
        if (inventory.GetItem(item.index) != item) {
            return;
        }

        switch ((ItemType) item.itemType) {
            case ItemType.WEAPON:
            case ItemType.EQUIP:
            case ItemType.PETEQUIP:
            case ItemType.AMMO:
                GenericUIItem.UseItem(item);
                break;
        }
    }
}
