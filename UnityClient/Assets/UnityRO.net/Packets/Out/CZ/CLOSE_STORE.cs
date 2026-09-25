public partial class CZ {

    // Closes the storage (clif.cpp clif_parse_CloseKafra).
    // 0193 (00f7 on old clients)
    public class CLOSE_STORE : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_CLOSE_STORE;
        public const int SIZE = 2;

        public CLOSE_STORE() : base(HEADER, SIZE) { }
    }
}
