using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Trading with another player (交易), laid out as the official window: what they put in on the
/// left, ours on the right, each side's zeny under it, and OK / 交易 / 取消. Items go in dragged
/// onto it from the inventory, by Alt + right click or a double tap. The request and its answer
/// are here too.
/// </summary>
public class TradeWindow : MonoBehaviour, IDropHandler {

    private class Offer {
        public string Name;
        public Texture2D Icon;
        public int Amount;
    }

    private class PendingItem {
        public ItemInfo Item;
        public int Amount;
    }

    private const float WIDTH = 330f;
    private const float SIDE = 158f;
    private const float PANEL_HEIGHT = 172f;
    private const float ROW_HEIGHT = 26f;
    private static readonly Color LockedColor = new Color32(222, 231, 247, 255);
    private static readonly Color TradeChatColor = new Color32(255, 214, 140, 255);

    public static TradeWindow Instance { get; private set; }
    public bool IsOpen => gameObject.activeSelf;

    private MapUiController UI;
    private RectTransform Root;
    private Image TheirPanel;
    private Image OurPanel;
    private TextMeshProUGUI TheirTitle;
    private TextMeshProUGUI OurTitle;
    private RectTransform TheirList;
    private RectTransform OurList;
    private TextMeshProUGUI TheirZeny;
    private TMP_InputField OurZeny;
    private Button OkButton;
    private Button TradeButton;
    private GameObject RequestPopup;

    // Who it's with: asked by us, or asking us
    private string PartnerName;
    private bool WeLocked;
    private bool TheyLocked;
    private int TheirZenyAmount;
    private readonly List<Offer> Theirs = new List<Offer>();
    private readonly List<Offer> Ours = new List<Offer>();
    private readonly Dictionary<short, PendingItem> Pending = new Dictionary<short, PendingItem>();

    public static TradeWindow Create(MapUiController ui) {
        TradeWindow window = null;
        // The X is 取消, as in the official window
        var root = RoWidgets.Window(ui.transform, "Trade Window", "交易", new Vector2(WIDTH, PANEL_HEIGHT + 52f), () => window.Cancel());
        window = root.gameObject.AddComponent<TradeWindow>();
        window.UI = ui;
        window.Root = root;
        window.Build();
        root.gameObject.SetActive(false);
        return window;
    }

    private void Awake() {
        Instance = this;
        var network = FindObjectOfType<NetworkClient>();
        network.HookPacket(ZC.REQ_EXCHANGE_ITEM2.HEADER, OnAsked);
        network.HookPacket(ZC.ACK_EXCHANGE_ITEM2.HEADER, OnAnswered);
        network.HookPacket(ZC.ADD_EXCHANGE_ITEM4.HEADER, OnTheirItem);
        network.HookPacket(ZC.ACK_ADD_EXCHANGE_ITEM.HEADER, OnOurItem);
        network.HookPacket(ZC.CONCLUDE_EXCHANGE_ITEM.HEADER, OnLocked);
        network.HookPacket(ZC.CANCEL_EXCHANGE_ITEM.HEADER, OnCancelled);
        network.HookPacket(ZC.EXEC_EXCHANGE_ITEM.HEADER, OnDone);
        network.HookPacket(ZC.EXCHANGEITEM_UNDO.HEADER, delegate { });
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void Say(string text) {
        if (UI != null && UI.ChatBox != null) {
            UI.ChatBox.DisplayText(text, TradeChatColor, ChatBoxController.Category.System);
        }
    }

    #region Asking

    /// <summary>
    /// A player tapped on the map: we ask them
    /// </summary>
    public void RequestTrade(Entity player) {
        PartnerName = player.GetBaseStatus().name;
        new CZ.REQ_EXCHANGE_ITEM(player.AID).Send();
        Say($"已向 {PartnerName} 提出交易要求。");
    }

    private void OnAsked(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.REQ_EXCHANGE_ITEM2 asked)) {
            return;
        }
        PartnerName = asked.Name;
        if (RequestPopup != null) {
            Destroy(RequestPopup);
        }
        // The X refuses, as 拒絕
        var window = RoWidgets.Window(UI.transform, "Trade Request", "交易要求", new Vector2(230f, 88f), () => Answer(false));
        RequestPopup = window.gameObject;
        var text = RoWidgets.Text(window, $"{asked.Name} (Lv {asked.Level}) 想要與你交易。", 12f, RoWidgets.TextColor, new Vector2(8f, -24f), new Vector2(214f, 32f));
        text.enableWordWrapping = true;
        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "接受", () => Answer(true));
        RoWidgets.Button(buttons, "拒絕", () => Answer(false));
        window.SetAsLastSibling();
    }

    private void Answer(bool accept) {
        new CZ.ACK_EXCHANGE_ITEM(accept).Send();
        if (RequestPopup != null) {
            Destroy(RequestPopup);
            RequestPopup = null;
        }
    }

    private void OnAnswered(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.ACK_EXCHANGE_ITEM2 answer)) {
            return;
        }
        var name = PartnerName ?? "";
        switch (answer.Result) {
            case 3:
                Open(name, answer.Level);
                break;
            case 0:
                Say("距離太遠，無法交易。");
                break;
            case 1:
                Say("找不到這個角色。");
                break;
            case 4:
                Say($"{name} 拒絕了交易。");
                break;
            case 5:
                Say($"{name} 正在與其他人交易。");
                break;
            default:
                Say("交易失敗。");
                break;
        }
    }

    #endregion

    #region Trading

    private void Open(string partner, short level) {
        WeLocked = TheyLocked = false;
        TheirZenyAmount = 0;
        Theirs.Clear();
        Ours.Clear();
        Pending.Clear();
        TheirTitle.text = $"<noparse>{partner}</noparse> (Lv {level})";
        var self = Session.CurrentSession?.Entity as Entity;
        OurTitle.text = self != null ? $"<noparse>{self.GetBaseStatus().name}</noparse>" : "我";
        OurZeny.text = "0";

        Root.anchoredPosition = Vector2.zero;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
        // What's put in comes from there
        UI.InventoryWindow.gameObject.SetActive(true);
        Say($"開始與 {partner} 交易。");
    }

    private void Close() {
        Pending.Clear();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// An item of the inventory into the trade (asking how many of a stack)
    /// </summary>
    public void OfferItem(ItemInfo item) {
        if (!IsOpen || WeLocked || item == null) {
            return;
        }
        var inventory = (Session.CurrentSession.Entity as Entity).Inventory;
        if (inventory.GetItem(item.index) != item) {
            return;
        }
        if (item.wearState > 0) {
            Say("穿戴中的裝備無法交易。");
            return;
        }
        void Put(int amount) {
            Pending[item.index] = new PendingItem { Item = item, Amount = amount };
            new CZ.ADD_EXCHANGE_ITEM(item.index, amount).Send();
        }
        if (item.amount <= 1) {
            Put(1);
        } else {
            AmountInputBox.Show(ItemName(item), item.amount, Put);
        }
    }

    public void OnDrop(PointerEventData eventData) {
        var dragged = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<GenericUIItem>() : null;
        if (dragged != null) {
            OfferItem(dragged.ItemInfo);
        }
    }

    private void OnOurItem(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.ACK_ADD_EXCHANGE_ITEM ack) || ack.Index == 0 || !Pending.TryGetValue(ack.Index, out var pending)) {
            return;
        }
        Pending.Remove(ack.Index);
        switch (ack.Result) {
            case 0:
                Ours.Add(new Offer { Name = ItemName(pending.Item), Icon = pending.Item.res, Amount = pending.Amount });
                Refresh();
                break;
            case 1:
                Say("對方的負重會超過上限，無法放入。");
                break;
            case 3:
                Say("對方的物品欄已滿，無法放入。");
                break;
            case 4:
                Say("超過該物品可交易的數量。");
                break;
            default:
                Say("無法放入交易。");
                break;
        }
    }

    private void OnTheirItem(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.ADD_EXCHANGE_ITEM4 added)) {
            return;
        }
        if (added.ItemId == 0) {
            TheirZenyAmount = added.Amount;
            Refresh();
            return;
        }
        var item = DBManager.GetItem((int) added.ItemId);
        var name = item == null ? $"#{added.ItemId}" : added.Identified ? item.identifiedDisplayName : item.unidentifiedDisplayName;
        if (added.Refine > 0) {
            name = $"+{added.Refine} {name}";
        }
        Theirs.Add(new Offer {
            Name = name,
            Icon = item != null ? TextureAssetLoader.Load(DBManager.GetItemResPath(item, added.Identified)) : null,
            Amount = added.Amount
        });
        Refresh();
    }

    /// <summary>
    /// OK: our zeny goes in, then nothing more from us
    /// </summary>
    private void Lock() {
        if (!IsOpen || WeLocked) {
            return;
        }
        int.TryParse(OurZeny.text, out var zeny);
        var self = Session.CurrentSession?.Entity as Entity;
        var have = self != null ? self.GetBaseStatus().zeny : 0;
        zeny = Mathf.Clamp(zeny, 0, have);
        OurZeny.SetTextWithoutNotify(zeny.ToString());
        if (zeny > 0) {
            new CZ.ADD_EXCHANGE_ITEM(0, zeny).Send();
        }
        new CZ.CONCLUDE_EXCHANGE_ITEM().Send();
    }

    private void OnLocked(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.CONCLUDE_EXCHANGE_ITEM locked)) {
            return;
        }
        if (locked.Them) {
            TheyLocked = true;
        } else {
            WeLocked = true;
        }
        Refresh();
    }

    private void Trade() {
        if (WeLocked && TheyLocked) {
            new CZ.EXEC_EXCHANGE_ITEM().Send();
        }
    }

    private void Cancel() {
        if (IsOpen) {
            new CZ.CANCEL_EXCHANGE_ITEM().Send();
        }
        Close();
    }

    private void OnCancelled(ushort cmd, int size, InPacket packet) {
        if (IsOpen) {
            Say("交易取消。");
        }
        Close();
    }

    private void OnDone(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.EXEC_EXCHANGE_ITEM done) {
            Say(done.Result == 0 ? "交易完成。" : "交易失敗。");
        }
        Close();
    }

    private static string ItemName(ItemInfo item) {
        var name = item.item == null ? $"#{item.ItemID}" : item.IsIdentified ? item.item.identifiedDisplayName : item.item.unidentifiedDisplayName;
        return item.refine > 0 ? $"+{item.refine} {name}" : name;
    }

    #endregion

    #region Building

    private void Build() {
        var title = Root.Find("Title Bar");
        if (title != null) {
            // Dragging the title moves the window
            var drag = title.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            entry.callback.AddListener(data => {
                Root.anchoredPosition += ((PointerEventData) data).delta / GetComponentInParent<Canvas>().scaleFactor;
            });
            drag.triggers.Add(entry);
        }

        TheirPanel = Side(new Vector2(6f, -20f), out TheirTitle, out TheirList);
        OurPanel = Side(new Vector2(WIDTH - 6f - SIDE, -20f), out OurTitle, out OurList);

        RoWidgets.Text(TheirPanel.transform, "Zeny", 12f, RoWidgets.TextColor, new Vector2(6f, -PANEL_HEIGHT + 20f), new Vector2(40f, 16f));
        TheirZeny = RoWidgets.Text(TheirPanel.transform, "0", 12f, RoWidgets.TextColor, new Vector2(46f, -PANEL_HEIGHT + 20f), new Vector2(SIDE - 52f, 16f), TextAlignmentOptions.TopRight);
        RoWidgets.Text(OurPanel.transform, "Zeny", 12f, RoWidgets.TextColor, new Vector2(6f, -PANEL_HEIGHT + 20f), new Vector2(40f, 16f));
        OurZeny = RoWidgets.InputField(OurPanel.transform, "0", 10, new Vector2(46f, -PANEL_HEIGHT + 21f), new Vector2(SIDE - 52f, 17f));
        OurZeny.contentType = TMP_InputField.ContentType.IntegerNumber;
        OurZeny.textComponent.alignment = TextAlignmentOptions.MidlineRight;

        var buttons = RoWidgets.ButtonRow(Root);
        OkButton = RoWidgets.Button(buttons, "OK", Lock);
        TradeButton = RoWidgets.Button(buttons, "交易", Trade);
        RoWidgets.Button(buttons, "取消", Cancel);
    }

    // One side: its owner, the items in (a list that scrolls) and the zeny
    private Image Side(Vector2 position, out TextMeshProUGUI title, out RectTransform list) {
        var panel = AaWidgets.NewImage("Side", Root, Color.white);
        panel.gameObject.AddComponent<Outline>().effectColor = RoWidgets.LineColor;
        var rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(SIDE, PANEL_HEIGHT);

        title = RoWidgets.Text(rect, "", 12f, new Color32(8, 49, 123, 255), new Vector2(6f, -3f), new Vector2(SIDE - 12f, 16f));

        list = AaWidgets.ScrollList(rect, true, out var scroll, 2f, new RectOffset(2, 2, 2, 2));
        var view = scroll.transform as RectTransform;
        view.anchorMin = view.anchorMax = view.pivot = new Vector2(0f, 1f);
        view.anchoredPosition = new Vector2(2f, -21f);
        view.sizeDelta = new Vector2(SIDE - 4f, PANEL_HEIGHT - 46f);
        return panel;
    }

    private void Refresh() {
        Fill(TheirList, Theirs);
        Fill(OurList, Ours);
        TheirZeny.text = TheirZenyAmount.ToString("N0");
        TheirPanel.color = TheyLocked ? LockedColor : Color.white;
        OurPanel.color = WeLocked ? LockedColor : Color.white;
        OurZeny.interactable = !WeLocked;
        OkButton.interactable = !WeLocked;
        TradeButton.interactable = WeLocked && TheyLocked;
    }

    private static void Fill(RectTransform list, List<Offer> offers) {
        AaWidgets.Clear(list);
        foreach (var offer in offers) {
            var row = AaWidgets.NewRect("Offer", list);
            AaWidgets.Layout(row, height: ROW_HEIGHT);
            RoWidgets.Picture(row, "Icon", offer.Icon, new Vector2(1f, -1f), new Vector2(24f, 24f));
            var name = offer.Amount > 1 ? $"{offer.Name} x {offer.Amount}" : offer.Name;
            RoWidgets.Text(row, $"<noparse>{name}</noparse>", 11f, RoWidgets.TextColor, new Vector2(28f, -5f), new Vector2(SIDE - 36f, 16f));
        }
    }

    #endregion
}
