public partial class CZ {

    // Takes a storage item into the inventory (clif.cpp clif_parse_MoveFromKafra).
    // 0365 <storage index>.W <amount>.L
    public class MOVE_ITEM_FROM_STORE_TO_BODY2 : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_MOVE_ITEM_FROM_STORE_TO_BODY2;
        public const int SIZE = 8;

        private short index;
        private int amount;

        public MOVE_ITEM_FROM_STORE_TO_BODY2(short index, int amount) : base(HEADER, SIZE) {
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
