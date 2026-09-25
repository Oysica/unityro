using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NormalEquipmentWindow : MonoBehaviour {

    public List<UIEquipSlot> slots;

    [SerializeField] private Entity WindowEntity;

    public void UpdateEquipment() {
        var entity = (Session.CurrentSession.Entity as Entity);
        WindowEntity.Clone(entity, LayerMask.NameToLayer("UI"), true);
        WindowEntity.SortingGroup.sortingOrder = 3;
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