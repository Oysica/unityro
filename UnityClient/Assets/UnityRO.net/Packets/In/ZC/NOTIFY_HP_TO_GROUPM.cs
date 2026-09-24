using ROIO.Utils;

public partial class ZC {

    // Official ZC_NOTIFY_HP_TO_GROUPM (packets_struct.hpp:5316, PACKETVER >=
    // 20100119 non-ZERO branch). Party member HP/MaxHP update (for the
    // party window health bars). This client has no party UI that consumes
    // this, so it's a pure parse-and-discard registration - without it the
    // packet stream desyncs (see PacketHeader.NEWER_PACKETVER region). Wire
    // size: int16(2) + AID(4) + hp(4) + maxhp(4) = 14 bytes total.
    [PacketHandler(HEADER, "ZC_NOTIFY_HP_TO_GROUPM", SIZE)]
    public class NOTIFY_HP_TO_GROUPM : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_HP_TO_GROUPM;
        public const int SIZE = 14;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 12-byte body; nothing to act on.
        }
    }
}
