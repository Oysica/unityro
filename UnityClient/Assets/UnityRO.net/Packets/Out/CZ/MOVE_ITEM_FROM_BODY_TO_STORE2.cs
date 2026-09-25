public partial class CZ {

    // Puts an inventory item in storage (clif.cpp clif_parse_MoveToKafra).
    // 0364 <inventory index>.W <amount>.L
    public class MOVE_ITEM_FROM_BODY_TO_STORE2 : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_MOVE_ITEM_FROM_BODY_TO_STORE2;
        public const int SIZE = 8;

        private short index;
        private int amount;

        public MOVE_ITEM_FROM_BODY_TO_STORE2(short index, int amount) : base(HEADER, SIZE) {
            this.index = index;
            this.amount = amount;
        }

        public override void Send() {
            Write(index);
            Write(amount);

            base.Send();
        }
    }
}
