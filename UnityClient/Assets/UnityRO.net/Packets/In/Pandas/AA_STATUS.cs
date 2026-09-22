using ROIO.Utils;

public partial class Pandas {

    // Pushed unconditionally to every online player every ~5s by map-server
    // (see clif.cpp, clif_aa_status/aa_status_push_timer) so the official
    // client's Gshield.dll overlay can show live auto-attack status.
    // UnityRO has no such overlay, so this is a pure parse-and-discard -
    // registering it here just keeps the packet stream in sync. Wire format
    // (server struct PACKET_AA_STATUS, clif.cpp, statically asserted to be
    // 101 bytes total including the 2-byte header) is intentionally not
    // reproduced field-by-field here since nothing reads it.
    [PacketHandler(HEADER, "AC_AA_STATUS", SIZE)]
    public class AA_STATUS : InPacket {

        public const PacketHeader HEADER = PacketHeader.AC_AA_STATUS;
        public const int SIZE = 101;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 99-byte body; nothing to act on.
        }
    }
}
