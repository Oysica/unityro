using ROIO.Utils;

public partial class ZC {

    // A character's emotion, shown to everyone around (clif.cpp clif_receive_emote). Pack 0 is the
    // basic emotions of the emotion sprite; other packs are emote shop purchases.
    // 0bea <id>.L <pack id>.W <emotion id>.W
    [PacketHandler(HEADER, "ZC_RECEIVE_EMOTE", SIZE)]
    public class RECEIVE_EMOTE : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_RECEIVE_EMOTE;
        public const int SIZE = 10;
        public PacketHeader Header => HEADER;

        public uint AID;
        public ushort PackId;
        public ushort EmotionId;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            PackId = br.ReadUShort();
            EmotionId = br.ReadUShort();
        }
    }
}
