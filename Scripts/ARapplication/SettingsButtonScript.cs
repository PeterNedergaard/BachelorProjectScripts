using UnityEngine;
using UnityEngine.UI;

public class SettingsButtonScript : MonoBehaviour
{
    public GameObject settingsPanel;

    public void ToggleSettingsPanel()
    {
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

}
