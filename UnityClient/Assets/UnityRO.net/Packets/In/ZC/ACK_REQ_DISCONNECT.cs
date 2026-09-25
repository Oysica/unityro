using ROIO.Utils;

public partial class ZC {

    // The map server's answer to leaving the game (clif.cpp clif_disconnect_ack). It also
    // answers a return to character select that prevent_logout blocks, e.g. right after a fight.
    // 018b <result>.W (0 = may leave, 1 = cannot leave now)
    [PacketHandler(HEADER, "ZC_ACK_REQ_DISCONNECT", SIZE)]
    public class ACK_REQ_DISCONNECT : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_ACK_REQ_DISCONNECT;
        public const int SIZE = 4;
        public PacketHeader Header => HEADER;

        public short Result;

        public void Read(MemoryStreamReader br, int size) {
            Result = br.ReadShort();
        }
    }
}
