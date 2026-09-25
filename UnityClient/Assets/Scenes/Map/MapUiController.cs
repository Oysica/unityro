using UnityEngine;
using UnityEngine.SceneManagement;

public class MapUiController : MonoBehaviour {

    public static MapUiController Instance;

    [SerializeField] private Tooltip Tooltip;
    [SerializeField] private ItemDetailsWindow ItemDetailsPrefab;
    [SerializeField] private NpcBoxController NpcBox;
    [SerializeField] private NpcBoxMenuController NpcMenu;
    [SerializeField] private NpcShopController ShopController;
    [SerializeField] private PopupController PopupController;
    [SerializeField] public EquipmentWindowController EquipmentWindow;
    [SerializeField] public InventoryWindowController InventoryWindow;
    [SerializeField] public StatsWindowController StatsWindow;
    [SerializeField] public SkillWindowController SkillWindow;
    [SerializeField] public ChatBoxController ChatBox;
    [SerializeField] public NpcShopTypeSelectorController ShopDealType;
    [SerializeField] public EscapeWindow EscapeWindow;
    [SerializeField] public MenuController Menu;
    [SerializeField] public PacketLogWindow PacketLogWindow;

    public StorageController Storage { get; private set; }

    private NetworkClient NetworkClient;
    private GameManager GameManager;
    private EntityManager EntityManager;

    void Awake() {
        if (Instance == null) {
            Instance = this;
        }

        NetworkClient = FindObjectOfType<NetworkClient>();
        GameManager = FindObjectOfType<GameManager>();
        EntityManager = FindObjectOfType<EntityManager>();

        NetworkClient.HookPacket(ZC.SAY_DIALOG.HEADER, NpcBox.OnNpcMessage);
        NetworkClient.HookPacket(ZC.CLOSE_DIALOG.HEADER, NpcBox.AddCloseButton);
        NetworkClient.HookPacket(ZC.WAIT_DIALOG.HEADER, NpcBox.AddNextButton);
        NetworkClient.HookPacket(ZC.CLOSE_SCRIPT.HEADER, NpcBox.CloseAndReset);
        NetworkClient.HookPacket(ZC.MENU_LIST.HEADER, NpcMenu.SetMenu);
        NetworkClient.HookPacket(ZC.SELECT_DEALTYPE.HEADER, ShopDealType.DisplayDealTypeSelector);
        NetworkClient.HookPacket(ZC.PC_PURCHASE_ITEMLIST.HEADER, ShopController.DisplayShop);
        NetworkClient.HookPacket(ZC.PC_SELL_ITEMLIST.HEADER, ShopController.DisplayShop);
        NetworkClient.HookPacket(ZC.PC_PURCHASE_RESULT.HEADER, ShopController.OnPurchaseResult);
        NetworkClient.HookPacket(ZC.PC_SELL_RESULT.HEADER, ShopController.OnSellResult);
        NetworkClient.HookPacket(ZC.RESTART_ACK.HEADER, OnRestartAnswer);
        NetworkClient.HookPacket(ZC.ACK_REQ_DISCONNECT.HEADER, OnDisconnectAnswer);

        NpcMenu.OnNpcMenuSelected = OnNpcMenuSelected;

        PacketLogWindow.Hide();
        StatusIconsController.Create(transform);
        Storage = StorageController.Create(InventoryWindow);
        MobileControlsController.Create(this);
        AutoAttackWindow.Create(this);
        PartyWindow.Create(this);
    }

    public void DisplayItemDetails(ItemInfo itemInfo, Vector2 position) {
        var details = Instantiate(ItemDetailsPrefab);
        details.SetItem(itemInfo);
        details.transform.position = position;
        details.transform.SetParent(gameObject.transform);
    }

    private void Update() {
        // Home opens the auto attack window, as it does the official client's overlay; not while typing
        if (Input.GetKeyDown(KeyCode.Home) && AutoAttackWindow.Instance != null) {
            var selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || selected.GetComponent<TMPro.TMP_InputField>() == null) {
                AutoAttackWindow.Instance.ToggleVisible();
            }
        }

        // Alt+Z opens the party window, as in the official client
        if (Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && PartyWindow.Instance != null) {
            PartyWindow.Instance.ToggleVisible();
        }

        if (Event.current == null)
            return;

        if (!Event.current.isKey || Event.current.keyCode == KeyCode.None)
            return;

        switch (Event.current.type) {
            case EventType.KeyDown:
                if (Event.current.modifiers == EventModifiers.Alt) {
                    switch (Event.current.keyCode) {
                        case KeyCode.Q:
                            EquipmentWindow.ToggleActive();
                            break;
                        case KeyCode.E:
                            InventoryWindow.ToggleActive();
                            break;
                        case KeyCode.A:
                            StatsWindow.ToggleActive();
                            break;
                        case KeyCode.S:
                            SkillWindow.ToggleActive();
                            break;
                        default:
                            break;
                    }
                }
                if (Event.current.keyCode == KeyCode.Escape) {
                    EscapeWindow.ToggleActive();
                } 
                break;
            default:
                break;
        }
    }

    void OnNpcMenuSelected(uint NAID, byte index) {
        if (index == 255) {
            NpcBox.gameObject.SetActive(false);
        }

        new CZ.CHOOSE_MENU() {
            NAID = NAID,
            Index = index
        }.Send();
    }

    public void DisplayPopup(Texture2D itemRes, string label) {
        PopupController.DisplayPopup(itemRes, label);
    }

    public void UpdateEquipment() {
        EquipmentWindow.UpdateEquipment();
        InventoryWindow.UpdateEquipment();
    }

    public void DisplayTooltip(string text, Vector3 position, Vector2? pivot = null) {
        Tooltip.SetText(text, position, pivot);
    }

    public void HideTooltip() {
        Tooltip.SetText(null, Vector3.zero);
    }

    public void OnMenuClick(int itemType) {
        var menuItemType = (MenuController.MenuItemType) itemType;
        switch (menuItemType) {
            case MenuController.MenuItemType.STATUS:
                StatsWindow.ToggleActive();
                break;
            case MenuController.MenuItemType.EQUIPMENT:
                EquipmentWindow.ToggleActive();
                break;
            case MenuController.MenuItemType.SKILL:
                SkillWindow.ToggleActive();
                break;
            case MenuController.MenuItemType.OPTIONS:
                EscapeWindow.ToggleActive();
                break;
            case MenuController.MenuItemType.INVENTORY:
                InventoryWindow.ToggleActive();
                break;
            default:
                break;
        }
    }

    public void OnRestartAnswer(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.RESTART_ACK pkt) {
            if (pkt.type == 0) {
                ChatBox.DisplayMessage(502, ChatMessageType.ERROR);
            }
            else {
                OnRestart();
            }
        }
    }

    /// <summary>
    /// A return to character select that prevent_logout blocks (e.g. right after a fight)
    /// is answered with this instead of ZC_RESTART_ACK.
    /// </summary>
    private void OnDisconnectAnswer(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.ACK_REQ_DISCONNECT pkt && pkt.Result != 0) {
            ChatBox.DisplayMessage(502, ChatMessageType.ERROR);
        }
    }

    /// <summary>
    /// The map server has handed the login back to the char server (char_mapif.cpp
    /// chmapif_parse_authok), which takes this client back with the session it logged in with.
    /// </summary>
    public async void OnRestart() {
        // Nothing may reach the char server that only the map server understands
        NetworkClient.StopHeartBeat();
        var control = (Session.CurrentSession.Entity as Entity)?.GetComponent<EntityControl>();
        if (control != null) {
            control.enabled = false;
        }
        NetworkClient.Disconnect();

        NetworkClient.HookPacket(HC.ACCEPT_ENTER.HEADER, OnCharServerEntered);
        NetworkClient.HookPacket(HC.REFUSE_ENTER.HEADER, OnCharServerRefused);

        var loginInfo = NetworkClient.State.LoginInfo;
        var charServer = NetworkClient.State.CharServer;
        var remoteConfig = GameManager.RemoteConfiguration;
        var charIp = remoteConfig.useSameIpForEveryServer ? remoteConfig.loginServer : charServer.IP.ToString();

        await NetworkClient.ChangeServer(charIp, charServer.Port);
        NetworkClient.SkipBytes(4);
        new CH.ENTER(loginInfo.AccountID, loginInfo.LoginID1, loginInfo.LoginID2, loginInfo.Sex).Send();
    }

    private void OnCharServerEntered(ushort cmd, int size, InPacket packet) {
        if (packet is HC.ACCEPT_ENTER ACCEPT_ENTER) {
            NetworkClient.State.CurrentCharactersInfo = ACCEPT_ENTER;
            LeaveMap();
            SceneManager.LoadScene("CharSelectionScene");
        }
    }

    private void OnCharServerRefused(ushort cmd, int size, InPacket packet) {
        NetworkClient.Disconnect();
        LeaveMap();
        SceneManager.LoadScene("LoginScene");
        SystemMessageBox.Show(ROIO.Tables.MsgStringTable["9"] as string ?? "Rejected from server"); // MSI_ACCESS_DENIED
    }

    /// <summary>
    /// Clears what outlives the map scene, so the next character starts from an empty world.
    /// </summary>
    private void LeaveMap() {
        var player = Session.CurrentSession?.Entity as Entity;
        if (player != null) {
            Destroy(player.gameObject);
        }
        EntityManager.ClearEntities();
        GameManager.UnloadMap();
    }
}
