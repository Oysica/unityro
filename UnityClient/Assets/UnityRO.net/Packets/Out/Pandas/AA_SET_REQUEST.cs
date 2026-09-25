public partial class Pandas {

    // Asks for the auto attack settings (answered with Pandas.AA_SET_SNAPSHOT). The server takes
    // one a second (feature.autoattack_button_cooldown). clif.cpp clif_parse_AaSetRequest
    public class AA_SET_REQUEST : OutPacket {

        public const int SIZE = 2;

        public AA_SET_REQUEST() : base(PacketHeader.CA_AA_SET_REQUEST, SIZE) { }
    }
}
