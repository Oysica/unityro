using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryWindowController : DraggableUIWindow, IDropHandler {

    [SerializeField]
    private GridLayoutGroup GridLayout;

    [SerializeField]
    private InventoryCell GridCellPrefab;

    [SerializeField]
    private UIItem UIItemPrefab;

    [SerializeField]
    private InventoryType CurrentTab = InventoryType.ITEM;

    [SerializeField]
    private CustomPanel Tabs;

    [SerializeField]
    private string ResName;

    public const int MIN_WINDOW_COLUMNS = 6;
    public const int MAX_WINDOW_COLUMNS = 8;
    public const int MIN_WINDOW_ROWS = 6;

    /// <summary>
    /// What the window lists; the character's inventory when unset. The storage window reuses this one.
    /// </summary>
    public Func<IEnumerable<ItemInfo>> ItemSource;

    /// <summary>
    /// An item dragged in from another window (e.g. between the inventory and the storage).
    /// </summary>
    public Action<ItemInfo> OnItemDropped;

    private List<InventoryCell> Cells = new List<InventoryCell>();
    private int CURRENT_WINDOW_COLUMNS = MAX_WINDOW_COLUMNS;

    private void Awake() {
        if (Cells.IsEmpty()) {
            InitGrid();
        }
    }

    private void InitGrid() {
        EnsureCells(CURRENT_WINDOW_COLUMNS * CURRENT_WINDOW_COLUMNS);
    }

    /// <summary>
    /// Adds rows until the grid holds <paramref name="count"/> items (storage holds hundreds).
    /// </summary>
    private void EnsureCells(int count) {
        while (Cells.Count < count) {
            for (int i = 0; i < CURRENT_WINDOW_COLUMNS; i++) {
                var cell = Instantiate<InventoryCell>(GridCellPrefab);
                cell.transform.SetParent(GridLayout.transform, false);
                Cells.Add(cell);
            }
        }
    }

    public void UpdateEquipment() {
        if (Cells.IsEmpty()) {
            InitGrid();
        }

        var items = ItemSource != null ? ItemSource() : GetInventoryItems();
        var filteredInventory = items.Where(it => it.tab == CurrentTab).ToList();
        EnsureCells(filteredInventory.Count);
        for (int i = 0; i < Cells.Count; i++) {
            if (i < filteredInventory.Count) {
                Cells[i].SetItem(filteredInventory[i]);
            } else {
                Cells[i].SetItem(null);
            }
        }
    }

    private static IEnumerable<ItemInfo> GetInventoryItems() {
        var inventory = (Session.CurrentSession.Entity as Entity).Inventory;
        if (inventory == null) {
            return Enumerable.Empty<ItemInfo>();
        }
        return inventory.ItemList.Where(it => it.wearState <= 0 || it.itemType == (int)ItemType.AMMO);
    }

    public void OnDrop(PointerEventData eventData) {
        var item = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<GenericUIItem>() : null;
        if (item == null || item.ItemInfo == null) {
            return;
        }

        // Dragged off the equipment window onto the inventory: take it off
        if (ItemSource == null && item is UIEquipSlot && item.ItemInfo.wearState > 0) {
            (Session.CurrentSession.Entity as Entity).Inventory.OnTakeOffItem(item.ItemInfo.index);
            return;
        }

        OnItemDropped?.Invoke(item.ItemInfo);
    }

    public void ChangeCurrentTab(int newTab) {
        if ((InventoryType)newTab != CurrentTab) {
            CurrentTab = (InventoryType)newTab;
        }

        Tabs.SetBackground($"{ResName}{(int)CurrentTab + 1}.png");
        UpdateEquipment();
    }
}
