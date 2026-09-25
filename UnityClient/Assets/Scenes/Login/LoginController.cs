using ROIO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginController : MonoBehaviour {

    public InputField usernameField;
    public InputField passwordField;
    public RawImage background;
    
    private NetworkClient NetworkClient;
    private RemoteConfiguration RemoteConfiguration;
    
    void Start() {
        background.SetLoginBackground();
        
        NetworkClient = FindObjectOfType<NetworkClient>();
        RemoteConfiguration = FindObjectOfType<GameManager>().RemoteConfiguration;

        NetworkClient.HookPacket(AC.ACCEPT_LOGIN3.HEADER, this.OnLoginResponse);
        NetworkClient.HookPacket(AC.REFUSE_LOGIN_R2.HEADER, this.OnLoginRefused);
    }

    private void OnLoginRefused(ushort cmd, int size, InPacket packet) {
        if (packet is AC.REFUSE_LOGIN_R2 refused) {
            SystemMessageBox.Show(RefuseMessage(refused.ErrorCode, refused.UnblockTime));
        }
    }

    /// <summary>
    /// The client message for a login-server refusal code (loginclif.cpp logclif_auth_failed).
    /// </summary>
    private static string RefuseMessage(uint errorCode, string unblockTime) {
        int msgId = errorCode switch {
            0 => 6,     // MSI_INCORRECT_USERID
            1 => 7,     // MSI_INCORRECT_PASSWORD
            2 => 8,     // MSI_ID_EXPIRED
            3 => 9,     // MSI_ACCESS_DENIED
            6 => 449,   // MSI_LOGIN_REFUSE_BLOCKED_UNTIL
            7 => 2423,  // MSI_REFUSE_OVER_USERLIMIT
            9 => 703,   // MSI_REFUSE_BAN_BY_DBA
            10 => 704,  // MSI_REFUSE_EMAIL_NOT_CONFIRMED
            11 => 705,  // MSI_REFUSE_BAN_BY_GM
            12 => 706,  // MSI_REFUSE_TEMP_BAN_FOR_DBWORK
            13 => 707,  // MSI_REFUSE_SELF_LOCK
            14 or 15 => 708, // MSI_REFUSE_NOT_PERMITTED_GROUP
            _ => 2424   // MSI_REFUSE_ERRORCODE: "... (%d)"
        };

        var text = Tables.MsgStringTable[$"{msgId}"] as string ?? $"Login refused ({errorCode})";
        return text.Replace("%s", unblockTime).Replace("%d", $"{errorCode}");
    }

    void Update() {
        TabBehaviour();
    }

    private void TabBehaviour() {
        EventSystem currentEvent = EventSystem.current;

        if (currentEvent.currentSelectedGameObject == null || !Input.GetKeyDown(KeyCode.Tab))
            return;

        Selectable current = currentEvent.currentSelectedGameObject.GetComponent<Selectable>();
        if (current == null)
            return;
 
        bool up = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Selectable next = up ? current.FindSelectableOnUp() : current.FindSelectableOnDown();
        next = current == next || next == null ? Selectable.allSelectablesArray[0] : next;
        currentEvent.SetSelectedGameObject(next.gameObject);
    }

    public void OnLoginClicked() {
        var username = usernameField.text;
        var password = passwordField.text;

        if (username.Length == 0 || password.Length == 0) {
            return;
        }

        TryConnectAndLogin(username, password);
    }

    public void OnExitClicked() {

    }

    private async void TryConnectAndLogin(string username, string password) {
        await NetworkClient.ChangeServer(RemoteConfiguration.loginServer, int.Parse(RemoteConfiguration.loginPort));
        new CA.LOGIN(username, password, 10, 10).Send();
    }

    private void OnLoginResponse(ushort cmd, int size, InPacket packet) {
        if (packet is AC.ACCEPT_LOGIN3) {
            var pkt = packet as AC.ACCEPT_LOGIN3;

            NetworkClient.State.LoginInfo = pkt;
            SceneManager.LoadSceneAsync("CharServerSelectionScene");
        }
    }
}
