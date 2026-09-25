public partial class CZ {

    public class REQ_TAKEOFF_EQUIP : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_REQ_TAKEOFF_EQUIP;
        // Named SIZE so the constructor picks it up; as "size" it silently bound to an outer
        // SIZE of -1 and went out as a variable-length packet the server misread (0x00ab is 4 bytes)
        public const int SIZE = 4;

        public REQ_TAKEOFF_EQUIP() : base(HEADER, SIZE) { }

        public short index;

        public override void Send() {
            Write(index);

            base.Send();
        }
    }
}
