public partial class CZ {

    // Shows an emotion to everyone around (clif.cpp clif_parse_receive_emote); at most one a second.
    // 0be9 <pack id>.W <emotion id>.W
    public class SEND_EMOTE : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_SEND_EMOTE;
        public const int SIZE = 6;

        private ushort packId;
        private ushort emotionId;

        public SEND_EMOTE(int emotionId, int packId = 0) : base(HEADER, SIZE) {
            this.packId = (ushort) packId;
            this.emotionId = (ushort) emotionId;
        }

        public override void Send() {
            Write(packId);
            Write(emotionId);

            base.Send();
        }
    }
}
