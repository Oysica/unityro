public partial class Pandas {

    // Starts or stops auto attack (自動練功) or auto support (自動輔助); only one runs at a time.
    // The server takes one a second (feature.autoattack_button_cooldown).
    // clif.cpp clif_parse_AaToggle (0f20) / clif_parse_AaToggleSupport (0f21)
    public class AA_TOGGLE : OutPacket {

        public const int SIZE = 2;

        public AA_TOGGLE(bool support) : base(support ? PacketHeader.CA_AA_TOGGLE_SUPPORT : PacketHeader.CA_AA_TOGGLE, SIZE) { }
    }
}
