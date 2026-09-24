using ROIO.Utils;

public partial class ZC {

    // Official ZC_NOTIFY_POSITION_TO_GROUPM (packets_struct.hpp:5298).
    // Party member position update (for the minimap/party window). This
    // client has no party UI that consumes this, so it's a pure
    // parse-and-discard registration - without it the packet stream
    // desyncs (see PacketHeader.NEWER_PACKETVER region). Wire size:
    // int16(2) + AID(4) + xPos(2) + yPos(2) = 10 bytes total.
    [PacketHandler(HEADER, "ZC_NOTIFY_POSITION_TO_GROUPM", SIZE)]
    public class NOTIFY_POSITION_TO_GROUPM : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_POSITION_TO_GROUPM;
        public const int SIZE = 10;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 8-byte body; nothing to act on.
        }
    }
}
