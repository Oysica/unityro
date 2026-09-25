using ROIO.Utils;

public partial class ZC {

    // Official ZC_SHOW_IMAGE2 (PACKET_ZC_SHOW_IMAGE, packets_struct.hpp:5526),
    // the NPC cutin illustration scripts show with "cutin". This client has no
    // cutin UI, so this is a pure parse-and-discard registration - without it
    // the dialog packets batched behind it were dropped and NPCs never talked.
    // Wire size: int16(2) + image(64) + type(1) = 67 bytes total.
    [PacketHandler(HEADER, "ZC_SHOW_IMAGE2", SIZE)]
    public class SHOW_IMAGE2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_SHOW_IMAGE2;
        public const int SIZE = 67;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // Nothing to act on.
        }
    }
}
