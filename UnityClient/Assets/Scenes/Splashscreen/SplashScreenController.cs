using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ROIO;
using ROIO.Utils;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashScreenController : MonoBehaviour {

    [SerializeField]
    private AssetLabelReference[] LabelsToPrefetch;

    [SerializeField]
    private TextMeshProUGUI labelText;

    [SerializeField]
    private TextMeshProUGUI DownloadSizeText;

    [SerializeField]
    private Slider Slider;

    void Start() {
        StartCoroutine(Initialize());
    }

    // TEMPORARY BRING-UP INSTRUMENTATION - remove once the splash hang is
    // diagnosed. The MCP-for-Unity bridge used to inspect live editor state
    // has been too flaky this session to read the running label text
    // reliably, so this traces progress through Editor.log instead, which
    // works with zero tooling dependency.
    private IEnumerator Initialize() {
        Debug.Log("[bring-up] Initialize: start");
        labelText.text = "Checking for updates...";
        Debug.Log("[bring-up] Initialize: awaiting Addressables.InitializeAsync()");
        // yield return Addressables.InitializeAsync(); directly on the handle
        // never resumed in this Unity/Addressables combo (verified: 90s idle
        // wait, zero exceptions, zero domain reload; meanwhile the SAME call
        // via execute_code and via .Task-await elsewhere in this same Play
        // session both completed fine) - polling IsDone sidesteps whatever
        // coroutine/AsyncOperationHandle interop issue that is.
        var initHandle = Addressables.InitializeAsync();
        Debug.Log($"[bring-up] Initialize: handle valid={initHandle.IsValid()}");
        float pollStart = Time.realtimeSinceStartup;
        while (!initHandle.IsDone) {
            Debug.Log($"[bring-up] Initialize: polling t={Time.realtimeSinceStartup - pollStart:F1}s "
                + $"Status={initHandle.Status} Percent={initHandle.PercentComplete:F2} "
                + $"Exception={initHandle.OperationException}");
            yield return new WaitForSeconds(1f);
        }
        Debug.Log($"[bring-up] Initialize: Addressables ready, timeScale={Time.timeScale}, waiting 1s realtime");
        // WaitForSeconds hung right here in earlier runs, immediately after
        // Addressables itself finished successfully - a plain time-based
        // yield with nothing to do with Addressables. WaitForSeconds scales
        // with Time.timeScale; if that's 0, it never completes. Switched to
        // WaitForSecondsRealtime to test/bypass that specifically.
        yield return new WaitForSecondsRealtime(1f);
        Debug.Log("[bring-up] Initialize: 1s realtime wait done");

        Debug.Log("[bring-up] Initialize: starting PrefetchAssets");
        StartCoroutine(PrefetchAssets());
    }

    private IEnumerator PrefetchAssets() {
        Debug.Log("[bring-up] PrefetchAssets: start (UNITY_EDITOR path skips download block)");
#if !UNITY_EDITOR
        var downloadSize = Addressables.GetDownloadSizeAsync(LabelsToPrefetch).WaitForCompletion();

        if (downloadSize <= 0) {
            yield return FetchConfigs();
        }

        foreach (var label in LabelsToPrefetch) {
            var handle = Addressables.DownloadDependenciesAsync(label, true);

            while(!handle.IsDone) {
                var downloadStatus = handle.GetDownloadStatus();
                var downloadedMbs = downloadStatus.DownloadedBytes / 1024f / 1024f;
                var totalMbs = (downloadStatus.TotalBytes / 1024f / 1024f);

                var progress = Conversions.SafeDivide(downloadedMbs, totalMbs);

                var text = $"Downloading {label.labelString}";
                labelText.text = text;
                DownloadSizeText.text = $"{downloadedMbs}MB / {totalMbs}MB";
                Slider.value = progress;

                yield return null;
            }

            yield return handle;
        }
#endif
        Debug.Log("[bring-up] PrefetchAssets: calling FetchConfigs");
        yield return FetchConfigs();
        Debug.Log("[bring-up] PrefetchAssets: FetchConfigs returned");
    }

    private IEnumerator FetchConfigs() {
        Debug.Log("[bring-up] FetchConfigs: start");
        labelText.text = "Fetching remote configuration...";
        Debug.Log("[bring-up] FetchConfigs: loading LocalConfigs.json.txt via Addressables");
        var localRequest = Addressables.LoadAssetAsync<TextAsset>("LocalConfigs.json.txt");
        yield return localRequest;
        Debug.Log($"[bring-up] FetchConfigs: LocalConfigs loaded, status={localRequest.Status}");

        var localConfig = JObject.Parse(localRequest.Result.text);
        var localConfiguration = JsonConvert.DeserializeObject<LocalConfiguration>(localConfig.ToString());
        Debug.Log($"[bring-up] FetchConfigs: remoteConfigLocation={localConfiguration.remoteConfigLocation}");

        var remoteRequest = UnityWebRequest.Get(localConfiguration.remoteConfigLocation);
        Debug.Log("[bring-up] FetchConfigs: sending UnityWebRequest");
        yield return remoteRequest.SendWebRequest();
        Debug.Log($"[bring-up] FetchConfigs: UnityWebRequest done, result={remoteRequest.result}, error={remoteRequest.error}, text={remoteRequest.downloadHandler?.text}");

        var remoteConfig = JObject.Parse(remoteRequest.downloadHandler.text);
        var remoteConfiguration = JsonConvert.DeserializeObject<RemoteConfiguration>(remoteConfig.ToString());

        FindObjectOfType<GameManager>().SetConfigurations(remoteConfiguration, localConfiguration);
        Debug.Log("[bring-up] FetchConfigs: loading LoginScene");

        SceneManager.LoadScene("LoginScene");
    }
}
