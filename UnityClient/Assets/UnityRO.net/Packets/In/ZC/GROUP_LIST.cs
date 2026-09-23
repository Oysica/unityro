using ROIO.Utils;

public partial class ZC {

    // Official ZC_GROUP_LIST / "partyinfo" (packets_struct.hpp:2086,
    // clif.cpp:9009, PACKETVER >= 20171207 header value). Full party member
    // roster (name, map, leader/offline flags). This client doesn't have a
    // party window that consumes this, so it's a pure parse-and-discard
    // registration - without it the packet stream desyncs (see
    // PacketHeader.NEWER_PACKETVER region). SIZE <= 0 tells PacketSerializer
    // to read the packet's own length field (int16 right after the header)
    // and consume exactly that many bytes.
    [PacketHandler(HEADER, "ZC_GROUP_LIST", SIZE)]
    public class GROUP_LIST : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_GROUP_LIST;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // Variable body (partyName + members[]); nothing to act on.
        }
    }
}
