using ROIO.Utils;

public partial class ZC {

    // The saved shortcut bar, sent once per bar page when entering a map (clif.cpp clif_hotkeys_send).
    // 0b20 <rotate>.B <tab>.W { <is skill>.B <id>.L <count>.W }*38
    [PacketHandler(HEADER, "ZC_SHORTCUT_KEY_LIST_V4", SIZE)]
    public class SHORTCUT_KEY_LIST_V3 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_SHORTCUT_KEY_LIST_V4;
        public const int SIZE = 271;
        public const int HOTKEY_COUNT = 38;
        public PacketHeader Header => HEADER;

        public byte Rotate;
        public short Tab;
        public Hotkey[] Hotkeys;

        public void Read(MemoryStreamReader br, int size) {
            Rotate = (byte) br.ReadByte();
            Tab = br.ReadShort();

            Hotkeys = new Hotkey[HOTKEY_COUNT];
            for (var i = 0; i < HOTKEY_COUNT; i++) {
                Hotkeys[i] = new Hotkey {
                    IsSkill = br.ReadByte() == 1,
                    Id = br.ReadInt(),
                    Count = br.ReadShort()
                };
            }
        }
    }
}
