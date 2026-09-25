using ROIO.Utils;
using ROIO.Utils.Extensions;

// Whispers (clif.cpp clif_wis_message, clif_wis_end; packets_struct.hpp PACKET_ZC_WHISPER)
public partial class ZC {

    // A whisper to us. 09de <len>.W <sender id>.L <sender>.24B <admin>.B <message>.?B
    [PacketHandler(HEADER, "ZC_WHISPER02", SIZE)]
    public class WHISPER02 : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_WHISPER02;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public uint SenderGID;
        public string Sender;
        public bool IsAdmin;
        public string Message;

        public void Read(MemoryStreamReader br, int size) {
            SenderGID = br.ReadUInt();
            Sender = br.ReadBinaryString(24).NetworkToText();
            IsAdmin = br.ReadByte() != 0;
            Message = br.ReadBinaryString(size - 29).NetworkToText();
        }
    }

    // What came of a whisper we sent. 09df <result: 0 sent, 1 not online, 2 ignored by them, 3 they ignore everyone>.B <char id>.L
    [PacketHandler(HEADER, "ZC_ACK_WHISPER02", SIZE)]
    public class ACK_WHISPER02 : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_ACK_WHISPER02;
        public const int SIZE = 7;
        public PacketHeader Header => HEADER;

        public byte Result;
        public uint CID;

        public void Read(MemoryStreamReader br, int size) {
            Result = (byte) br.ReadByte();
            CID = br.ReadUInt();
        }
    }
}
