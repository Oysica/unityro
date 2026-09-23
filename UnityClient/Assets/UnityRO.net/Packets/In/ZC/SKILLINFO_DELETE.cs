using ROIO.Utils;

public partial class ZC {

    // Official ZC_SKILLINFO_DELETE (packets.hpp:1082). This client has no
    // logic that removes a single skill entry from the skill window on the
    // fly, so this is a pure parse-and-discard registration - without it
    // the packet stream desyncs (see PacketHeader.NEWER_PACKETVER region).
    // Wire size: int16(2) + skillID(2) = 4 bytes total.
    [PacketHandler(HEADER, "ZC_SKILLINFO_DELETE", SIZE)]
    public class SKILLINFO_DELETE : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_SKILLINFO_DELETE;
        public const int SIZE = 4;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 2-byte body (skillID); nothing to act on.
        }
    }
}
