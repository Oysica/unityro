public partial class CZ {

    public class ENTER2 : OutPacket {

        public const PacketHeader HEADER = PacketHeader.CZ_ENTER2;
        public const int SIZE = 23; // what Send writes; the server reads 23 (clif_parse_WantToConnection)

        private int AccountId, CharacterId, LoginId1;
        private long clienttime;
        private byte sex;

        public ENTER2(int AccountId, int CharacterId, int LoginId1, long clienttime, byte sex) : base(HEADER, SIZE) {
            this.AccountId = AccountId;
            this.CharacterId = CharacterId;
            this.LoginId1 = LoginId1;
            this.clienttime = clienttime;
            this.sex = sex;
        }

        public override void Send() {
            Write(AccountId);
            Write(CharacterId);
            Write(LoginId1);
            Write(clienttime);
            Write(sex);

            base.Send();
        }
    }
}
