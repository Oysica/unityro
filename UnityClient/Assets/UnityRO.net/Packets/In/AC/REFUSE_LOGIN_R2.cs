using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class AC {

    // Login refused, the form this server sends (common/packets.hpp PACKET_AC_REFUSE_LOGIN, header 0x83e).
    // Unhandled, a wrong password just left the login screen doing nothing.
    // 083e <error>.L <unblock time>.20B
    [PacketHandler(HEADER, "AC_REFUSE_LOGIN_R2", SIZE)]
    public class REFUSE_LOGIN_R2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.AC_REFUSE_LOGIN_R2;
        public const int SIZE = 26;
        public PacketHeader Header => HEADER;

        // 0 unregistered id, 1 wrong password, 2 expired, 3 rejected, 6 banned until UnblockTime, ...
        // (login-server loginclif.cpp logclif_auth_failed)
        public uint ErrorCode;
        public string UnblockTime;

        public void Read(MemoryStreamReader br, int size) {
            ErrorCode = br.ReadUInt();
            UnblockTime = br.ReadBinaryString(20).NetworkToText();
        }
    }
}
