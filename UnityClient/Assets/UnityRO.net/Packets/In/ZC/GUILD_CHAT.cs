using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class ZC {

    // Guild chat, "name : message" (clif_guild_message). 017f <len>.W <message>.?B
    [PacketHandler(HEADER, "ZC_GUILD_CHAT", SIZE)]
    public class GUILD_CHAT : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_GUILD_CHAT;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public string Message;

        public void Read(MemoryStreamReader br, int size) {
            Message = br.ReadBinaryString(size).NetworkToText();
        }
    }
}
