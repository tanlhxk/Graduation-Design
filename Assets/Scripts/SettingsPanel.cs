using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    public Slider volumeSlider;
    //public Toggle fullscreenToggle;

    private void Start()
    {
        // 加载已保存的设置
        volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.75f);
        //fullscreenToggle.isOn = Screen.fullScreen;

        // 添加监听器
        volumeSlider.onValueChanged.AddListener(SetVolume);
        //fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    public void SetFullscreen(bool isFull)
    {
        Screen.fullScreen = isFull;
        PlayerPrefs.SetInt("Fullscreen", isFull ? 1 : 0);
    }
}