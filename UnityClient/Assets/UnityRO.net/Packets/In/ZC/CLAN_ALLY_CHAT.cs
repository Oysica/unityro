using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class ZC {

    // Clan (代表公會) chat, "name : message" (clif_clan_message; the name field is left empty).
    // 098e <len>.W <name>.24B <message>.?B
    [PacketHandler(HEADER, "ZC_NOTIFY_CLAN_CHAT", SIZE)]
    public class NOTIFY_CLAN_CHAT : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_CLAN_CHAT;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public string Name;
        public string Message;

        public void Read(MemoryStreamReader br, int size) {
            Name = br.ReadBinaryString(24).NetworkToText();
            Message = br.ReadBinaryString(size - 24).NetworkToText();
        }
    }

    // Guild alliance (公會聯盟) chat, "name : message" (Pandas clif_ally_send_message).
    // 0bde <len>.W <message>.?B
    [PacketHandler(HEADER, "ZC_ALLY_CHAT", SIZE)]
    public class ALLY_CHAT : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_ALLY_CHAT;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public string Message;

        public void Read(MemoryStreamReader br, int size) {
            Message = br.ReadBinaryString(size).NetworkToText();
        }
    }
}
