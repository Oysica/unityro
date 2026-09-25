using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class ZC {

    // Official ZC_NPC_CHAT: colored NPC/monster chat, e.g. what a shop NPC says when clicked.
    // 02c1 <packet len>.W <id>.L <color>.L <message>.?B (clif_messagecolor_target)
    // The color is 0x00BBGGRR.
    [PacketHandler(HEADER, "ZC_NPC_CHAT")]
    public class NPC_CHAT : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NPC_CHAT;
        public PacketHeader Header => HEADER;

        public uint GID;
        public uint Color;
        public string Message;

        public void Read(MemoryStreamReader br, int size) {
            GID = br.ReadUInt();
            Color = br.ReadUInt();
            Message = br.ReadBinaryString((int) (br.Length - br.Position)).NetworkToText();
        }
    }
}
