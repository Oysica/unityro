using UnityEngine;

/// <summary>
/// The shortcut bar: the first row of what the server saves per character, used with F1-F9
/// like the official client, which reads and writes the same slots.
/// </summary>
public class HotkeyBarController : MonoBehaviour {

    // The server keeps two bar pages; the second belongs to the official client's extra bar
    private const short BAR_TAB = 0;

    private static readonly KeyCode[] SlotKeys = {
        KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9
    };

    private UsableContainer[] Slots;

    private void Awake() {
        Slots = GetComponentsInChildren<UsableContainer>(true);
        for (var i = 0; i < Slots.Length; i++) {
            Slots[i].Index = i;
            Slots[i].OnHotkeyChanged = OnHotkeyChanged;
        }

        FindObjectOfType<NetworkClient>().HookPacket(ZC.SHORTCUT_KEY_LIST_V3.HEADER, OnHotkeyList);
    }

    private void Update() {
        for (var i = 0; i < SlotKeys.Length && i < Slots.Length; i++) {
            if (Input.GetKeyDown(SlotKeys[i])) {
                Slots[i].Use();
            }
        }
    }

    private void OnHotkeyList(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.SHORTCUT_KEY_LIST_V3 list && list.Tab == BAR_TAB) {
            for (var i = 0; i < Slots.Length; i++) {
                Slots[i].SetHotkey(list.Hotkeys[i]);
            }
        }
    }

    private void OnHotkeyChanged(UsableContainer slot) {
        new CZ.SHORTCUT_KEY_CHANGE2(BAR_TAB, (short) slot.Index, slot.Hotkey).Send();
    }
}
