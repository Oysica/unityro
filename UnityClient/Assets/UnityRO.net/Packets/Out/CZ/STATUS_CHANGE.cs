public partial class CZ {

    // Spends status points on a base stat (clif.cpp clif_parse_StatusUp).
    // 00bb <status id>.W <amount>.B
    public class STATUS_CHANGE : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_STATUS_CHANGE;
        public const int SIZE = 5;

        private short status;
        private byte amount;

        public STATUS_CHANGE(EntityStatus status, byte amount = 1) : base(HEADER, SIZE) {
            this.status = (short) status;
            this.amount = amount;
        }

        public override void Send() {
            Write(status);
            Write(amount);

            base.Send();
        }
    }
}
