using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kafra storage: the item list the server sends when the storage opens, shown in a copy of the
/// inventory window. Items move between the two windows by dragging them across, or with
/// Alt + right click, asking how many when it is a stack.
/// </summary>
public class StorageController : MonoBehaviour {

    public const byte INVTYPE_STORAGE = 2; // clif.cpp enum inventory_type

    private readonly Dictionary<short, ItemInfo> Items = new Dictionary<short, ItemInfo>();
    private InventoryWindowController Window;
    private TMP_Text Title;
    private string StorageName;
    private int Amount;
    private int MaxAmount;

    public bool IsOpen => gameObject.activeSelf;

    public static StorageController Create(InventoryWindowController inventory) {
        var prefab = Resources.Load<InventoryWindowController>("Prefabs/UI/Windows/Inventory");
        var window = Instantiate(prefab, inventory.transform.parent, false);
        window.name = "Storage";

        var storage = window.gameObject.AddComponent<StorageController>();
        storage.Init(window);

        window.ItemSource = () => storage.Items.Values;
        window.OnItemDropped = item => {
            if (!storage.Contains(item)) {
                storage.Store(item);
            }
        };
        inventory.OnItemDropped = item => {
            if (storage.Contains(item)) {
                storage.Take(item);
            }
        };

        window.gameObject.SetActive(false);
        return storage;
    }

    private void Init(InventoryWindowController window) {
        Window = window;
        Title = window.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(it => it.transform.parent.name.StartsWith("Title Bar"));
        AddCloseButton();

        // Hooked here rather than in Awake: the window is inactive until the storage opens
        var networkClient = FindObjectOfType<NetworkClient>();
        networkClient.HookPacket(ZC.INVENTORY_START.HEADER, OnListStart);
        networkClient.HookPacket(ZC.INVENTORY_END.HEADER, OnListEnd);
        networkClient.HookPacket(ZC.NOTIFY_STOREITEM_COUNTINFO.HEADER, OnCount);
        networkClient.HookPacket(ZC.ADD_ITEM_TO_STORE.HEADER, OnItemAdded);
        networkClient.HookPacket(ZC.DELETE_ITEM_FROM_STORE.HEADER, OnItemRemoved);
        networkClient.HookPacket(ZC.CLOSE_STORE.HEADER, OnClosed);
    }

    /// <summary>
    /// The inventory window has no close button; the storage needs one to tell the server.
    /// </summary>
    private void AddCloseButton() {
        var titleBar = Title != null ? Title.transform.parent : transform;
        var close = new GameObject("Close", typeof(RectTransform)).AddComponent<RawImage>();
        close.transform.SetParent(titleBar, false);
        close.texture = TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}basic_interface/sys_close_off.png");
        close.rectTransform.anchorMin = close.rectTransform.anchorMax = close.rectTransform.pivot = new Vector2(1f, 0.5f);
        close.rectTransform.anchoredPosition = new Vector2(-3f, 0f);
        close.rectTransform.sizeDelta = new Vector2(12f, 12f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(RequestClose);
    }

    public bool Contains(ItemInfo item) {
        return item != null && Items.TryGetValue(item.index, out var held) && held == item;
    }

    /// <summary>
    /// Alt + right click on an item of either window.
    /// </summary>
    public void MoveItem(ItemInfo item) {
        if (Contains(item)) {
            Take(item);
        } else {
            Store(item);
        }
    }

    private void Store(ItemInfo item) {
        var inventory = (Session.CurrentSession.Entity as Entity).Inventory;
        if (!IsOpen || inventory.GetItem(item.index) != item || item.wearState > 0) {
            return;
        }
        AskAmount(item, amount => new CZ.MOVE_ITEM_FROM_BODY_TO_STORE2(item.index, amount).Send());
    }

    private void Take(ItemInfo item) {
        if (!IsOpen) {
            return;
        }
        AskAmount(item, amount => new CZ.MOVE_ITEM_FROM_STORE_TO_BODY2(item.index, amount).Send());
    }

    private static void AskAmount(ItemInfo item, Action<int> move) {
        if (item.amount <= 1) {
            move(1);
            return;
        }
        var name = item.IsIdentified ? item.item.identifiedDisplayName : item.item.unidentifiedDisplayName;
        AmountInputBox.Show(name, item.amount, move);
    }

    public void RequestClose() {
        new CZ.CLOSE_STORE().Send();
    }

    /// <summary>
    /// The storage's share of the item list packets, which the ItemManager hooks for the inventory.
    /// </summary>
    public void AddItems(IEnumerable<ItemInfo> items) {
        foreach (var item in items) {
            if (ItemManager.PrepareItem(item)) {
                Items[item.index] = item;
            }
        }
    }

    private void OnListStart(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.INVENTORY_START start && start.InvType == INVTYPE_STORAGE) {
            Items.Clear();
            StorageName = start.Name;
        }
    }

    private void OnListEnd(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.INVENTORY_END end && end.InvType == INVTYPE_STORAGE) {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            // Items go in and out of the inventory, so it opens beside the storage
            var inventory = MapUiController.Instance.InventoryWindow;
            inventory.gameObject.SetActive(true);
            inventory.UpdateEquipment();
            Refresh();
            PlaceBeside(inventory.transform as RectTransform);
        }
    }

    /// <summary>
    /// Next to the inventory wherever it was dragged to, so items can be dragged across: on its
    /// right when that fits on screen, on its left otherwise, top edges level.
    /// </summary>
    private void PlaceBeside(RectTransform inventory) {
        var rect = transform as RectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(inventory);

        var inventoryCorners = new Vector3[4]; // bottom left, top left, top right, bottom right
        inventory.GetWorldCorners(inventoryCorners);
        var screenCorners = new Vector3[4];
        (rect.parent as RectTransform).GetWorldCorners(screenCorners);

        var width = rect.rect.width * rect.lossyScale.x;
        var height = rect.rect.height * rect.lossyScale.y;
        var gap = 8f * rect.lossyScale.x;
        var left = inventoryCorners[2].x + gap + width <= screenCorners[2].x
            ? inventoryCorners[2].x + gap
            : inventoryCorners[1].x - gap - width;
        var top = inventoryCorners[1].y;

        rect.position = new Vector3(left + rect.pivot.x * width, top - (1f - rect.pivot.y) * height, rect.position.z);
    }

    private void OnCount(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.NOTIFY_STOREITEM_COUNTINFO count) {
            Amount = count.Amount;
            MaxAmount = count.MaxAmount;
            UpdateTitle();
        }
    }

    private void OnItemAdded(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.ADD_ITEM_TO_STORE added) {
            var item = added.ItemInfo;
            if (Items.TryGetValue(item.index, out var held)) {
                held.amount += item.amount;
            } else if (ItemManager.PrepareItem(item)) {
                Items[item.index] = item;
            }
            Refresh();
        }
    }

    private void OnItemRemoved(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.DELETE_ITEM_FROM_STORE removed && Items.TryGetValue(removed.Index, out var held)) {
            held.amount -= removed.Amount;
            if (held.amount <= 0) {
                Items.Remove(removed.Index);
            }
            Refresh();
        }
    }

    private void OnClosed(ushort cmd, int size, InPacket packet) {
        Items.Clear();
        gameObject.SetActive(false);
    }

    private void Refresh() {
        if (IsOpen) {
            Window.UpdateEquipment();
        }
        UpdateTitle();
    }

    private void UpdateTitle() {
        if (Title != null) {
            Title.text = MaxAmount > 0 ? $"{StorageName} ({Amount}/{MaxAmount})" : StorageName;
        }
    }
}
