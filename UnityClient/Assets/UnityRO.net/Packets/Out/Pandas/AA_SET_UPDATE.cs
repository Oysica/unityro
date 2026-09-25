using UnityEngine;

public partial class Pandas {

    // Saves the auto attack settings; the server answers with a new Pandas.AA_SET_SNAPSHOT, at
    // most once a second. The time left is never sent (see AutoAttackSettings.ToBytes).
    // clif.cpp clif_parse_AaSetUpdate
    public class AA_SET_UPDATE : OutPacket {

        public const int SIZE = 2 + AutoAttackSettings.SIZE;

        private readonly byte[] block;

        public AA_SET_UPDATE(AutoAttackSettings settings) : base(PacketHeader.CA_AA_SET_UPDATE, SIZE) {
            block = settings.ToBytes(forUpdate: true);
        }

        public override void Send() {
            // The server reads a fixed size: anything else would throw off every packet after it
            if (block.Length != AutoAttackSettings.SIZE) {
                Debug.LogError($"Auto attack settings are {block.Length} bytes, not {AutoAttackSettings.SIZE}: not sent");
                return;
            }

            Write(block);
            base.Send();
        }
    }
}
