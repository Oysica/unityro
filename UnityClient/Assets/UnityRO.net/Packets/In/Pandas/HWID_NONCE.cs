using ROIO.Utils;

public partial class Pandas {

    // Sent unconditionally by the server as the reply to the first non-
    // inter-server packet on a login-server session (see loginclif.cpp,
    // Pandas_HWID_Tracking). UnityRO has no HWID/Gshield.dll to answer with,
    // so this is a pure parse-and-discard: registering it here just keeps
    // the packet stream in sync. Wire format (server struct
    // PACKET_AC_HWID_NONCE, loginclif.cpp:326): int16 packetType(2) +
    // uint32 magic(4) + uint8 nonce[16] = 22 bytes total.
    [PacketHandler(HEADER, "AC_HWID_NONCE", SIZE)]
    public class HWID_NONCE : InPacket {

        public const PacketHeader HEADER = PacketHeader.AC_HWID_NONCE;
        public const int SIZE = 22;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // body is magic(4) + nonce(16) = 20 bytes; nothing to act on.
        }
    }
}
