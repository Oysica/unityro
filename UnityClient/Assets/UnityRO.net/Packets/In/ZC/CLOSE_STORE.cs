using ROIO.Utils;

public partial class ZC {

    // The storage was closed (clif.cpp clif_storageclose).
    // 00f8
    [PacketHandler(HEADER, "ZC_CLOSE_STORE", SIZE)]
    public class CLOSE_STORE : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_CLOSE_STORE;
        public const int SIZE = 2;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
        }
    }
}
