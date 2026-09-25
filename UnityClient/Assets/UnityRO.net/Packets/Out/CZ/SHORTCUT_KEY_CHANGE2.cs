public partial class CZ {

    // Saves one slot of the shortcut bar (clif.cpp clif_parse_Hotkey). An empty hotkey clears it.
    // 0b21 <tab>.W <index>.W <is skill>.B <id>.L <count>.W
    public class SHORTCUT_KEY_CHANGE2 : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_SHORTCUT_KEY_CHANGE2;
        public const int SIZE = 13;

        private short tab;
        private short index;
        private Hotkey hotkey;

        public SHORTCUT_KEY_CHANGE2(short tab, short index, Hotkey hotkey) : base(HEADER, SIZE) {
            this.tab = tab;
            this.index = index;
            this.hotkey = hotkey ?? new Hotkey();
        }

        public override void Send() {
            Write(tab);
            Write(index);
            Write((byte) (hotkey.IsSkill ? 1 : 0));
            Write(hotkey.Id);
            Write(hotkey.Count);

            base.Send();
        }
    }
}
