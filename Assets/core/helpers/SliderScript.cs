using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene UI helper that synchronizes a Unity slider with Controller brightness.
/// </summary>
public class SliderScript : MonoBehaviour
{
    [SerializeField] Slider _slider;
    [SerializeField] TextMeshProUGUI _slidertext;
    [SerializeField] Controller controller;
    // Start is called before the first frame update
    void Start()
    {
        _slider.onValueChanged.AddListener((v) =>
        {
            controller.brightness = (byte)v;
            _slidertext.text = v.ToString("000");
        });
    }

    // Update is called once per frame
    /// <summary>
    /// Mirrors Controller.brightness back into the slider so external changes stay visible.
    /// </summary>
    void Update()
    {
        _slider.value = controller.brightness;
    }
}
