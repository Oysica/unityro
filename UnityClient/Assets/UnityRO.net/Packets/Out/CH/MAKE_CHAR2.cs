public partial class CH {

    public class MAKE_CHAR2 : OutPacket {

        // The 36 byte layout below is what the char server reads from 20151001 on, whose id is
        // 0x0a39; it takes 0x0970 too but checks only 31 bytes before reading 36 (char_clif.cpp)
        public const PacketHeader HEADER = PacketHeader.CH_MAKE_CHAR3;
        public const int SIZE = 36;

        public string Name;
        public byte CharNum = 0;
        public ushort HeadPal = 0;
        public ushort Head = 1;
        public int StartJob = 0;
        public byte Sex = 0;

        public MAKE_CHAR2() : base(HEADER, SIZE) { }

        public override void Send() {
            Write(Name, 24);
            Write(CharNum);
            Write(HeadPal);
            Write(Head);
            Write(StartJob);
            Write(Sex);

            base.Send();
        }
    }
}