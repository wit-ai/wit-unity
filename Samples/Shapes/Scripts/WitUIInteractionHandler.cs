using System;
using System.Collections;
using com.facebook.witai;
using com.facebook.witai.lib;
using TMPro;
using UnityEngine;

public class WitUIInteractionHandler : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textArea;
    [SerializeField] private Wit wit;
    [SerializeField] private bool showJson;

    private string pendingText;

    private void Update()
    {
        if (null != pendingText)
        {
            textArea.text = pendingText;
            pendingText = null;
        }
    }

    public void OnResponse(WitResponseNode response)
    {
        if(!showJson) textArea.text = response["text"];
    }

    public void OnError(string error, string message)
    {
        textArea.text = $"Error: {error}\n\n{message}";
    }

    public void ToggleActivation()
    {
        if(wit.Active) wit.Deactivate();
        else
        {
            textArea.text = "The mic is active, start speaking now.";
            var request = wit.DoActivate();

            // The raw response comes back on a different thread. We store the
            // message received for display on the next frame.
            if(showJson) request.onRawResponse = (response) => pendingText = response;
        }
    }
}
