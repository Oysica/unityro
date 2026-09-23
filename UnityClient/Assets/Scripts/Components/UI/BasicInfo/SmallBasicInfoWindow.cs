using System.Globalization;
using TMPro;
using UnityEngine;

public class SmallBasicInfoWindow : DraggableUIWindow {

    [SerializeField]
    private TextMeshProUGUI line1;

    [SerializeField]
    private TextMeshProUGUI line2;

    [SerializeField]
    private TextMeshProUGUI charName;

    private void OnDestroy() {
        (Session.CurrentSession.Entity as Entity).OnParameterUpdated -= OnParameterUpdated;
    }

    private void Start() {
        (Session.CurrentSession.Entity as Entity).OnParameterUpdated += OnParameterUpdated;
    }

    private void OnParameterUpdated() {
        var status = Session.CurrentSession.Entity.GetBaseStatus();
        charName.text = status.name;
        var exp = 0f;
        if (status.next_base_exp > 0) {
            exp = status.base_exp / (float)status.next_base_exp * 100;
        }

        // JobHelper.GetJobName throws KeyNotFoundException when the
        // job-name table is empty (.lub bytecode MoonSharp can't parse -
        // see LuaInterface.cs/Tables.cs). Uncaught, this aborts the whole
        // packet handling call it runs under (reflection-invoked from
        // NetworkClient.TryHandleReceivedPacket with no try-catch there).
        string jobName;
        try {
            jobName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(JobHelper.GetJobName(status.jobId, status.sex).ToLower());
        } catch (System.Exception) {
            jobName = $"Job {status.jobId}";
        }

        line1.text = $"Nv. {status.base_level} / {jobName} / Nv. {status.job_level} / Exp. {exp}%";
        line2.text = $"HP. {status.hp} / {status.max_hp} | SP. {status.sp} / {status.max_sp}";
    }
}