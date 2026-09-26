using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class EscapeWindow : DraggableUIWindow, IEscapeWindowController {

    [SerializeField]
    private PacketLogWindow PacketLogWindow;

    [SerializeField]
    private SoundSettingsWindow SoundSettingsWindow;

    [SerializeField]
    private GameObject ButtonPrefab;
    
    [SerializeField]
    private GameObject Body;

    private EntityControl EntityControl;
    private Toggle CurrentToggle;
    private bool IsPlayerDead;
    private bool Built;

    private void Awake() {
        EntityControl = FindObjectOfType<EntityControl>();
        // Title bars across the whole window, as its body (they took their picture's width)
        RoWidgets.FitTitleBar(transform as RectTransform);
        if (SoundSettingsWindow != null) {
            RoWidgets.FitTitleBar(SoundSettingsWindow.transform as RectTransform);
        }
    }

    void Start() {
        // Dying first opens the window: its buttons are already there, and they're the dead's
        if (!Built) {
            BuildButtons();
        }
    }
    
    public void Show() {
        gameObject.SetActive(true);
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public void BuildButtons(bool isPlayerDead = false) {
        Built = true;
        IsPlayerDead = isPlayerDead;
        foreach (Transform child in Body.transform) {
            Destroy(child.gameObject);
        }

        // Dead: back to the save point, or stay down here (waiting to be revived), as in the
        // official client's window
        if (isPlayerDead) {
            BuildButton("移動到儲存場所", () => {
                new CZ.RESTART(CZ.RESTART.TYPE_SAVE_POINT).Send();
                BuildButtons();
                Hide();
            });
            BuildButton("繼續遊戲", () => Hide());
            return;
        }

        BuildButton("Character select", () => new CZ.RESTART(CZ.RESTART.TYPE_CHAR_SELECT).Send());

#if DEBUG
        BuildButton("Packet log", () => { PacketLogWindow.Show(); });
        BuildButton("Close shop", () => { new CZ.NPC_TRADE_QUIT().Send(); });
#endif

        BuildButton("內掛系統", () => {
            Hide();
            if (AutoAttackWindow.Instance != null) {
                AutoAttackWindow.Instance.Show();
            }
        });
        BuildButton(MobileControls.Enabled ? "手機操作：開" : "手機操作：關", () => {
            MobileControls.Enabled = !MobileControls.Enabled;
            BuildButtons(IsPlayerDead);
        });
        BuildButton("Sound Settings", () => { SoundSettingsWindow.Show(); });
        BuildButton("Exit game", () => Application.Quit());
        BuildButton("Cancel", () => Hide());
    }

    private void BuildButton(string label, UnityAction onClick) {
        GameObject goButton = Instantiate(ButtonPrefab);
        goButton.transform.SetParent(Body.transform, false);

        TextMeshProUGUI textMeshProUGUI = goButton.GetComponentInChildren<TextMeshProUGUI>();
        textMeshProUGUI.text = label;
        
        Button button = goButton.GetComponent<Button>();
        button.onClick.AddListener(onClick);
    }
}
