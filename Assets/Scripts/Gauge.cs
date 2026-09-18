using UnityEngine;
using UnityEngine.UI;

public class Gauge : MonoBehaviour
{
    // The UI Image showing the gauge. Its Image Type must be set to "Filled".
    [SerializeField] private Image gaugeFillImage;

    // How much a successful parry fills the gauge (0 to 1).
    [SerializeField, Range(0.01f, 1f)] private float parryFillAmount = 0.34f;

    // How much the gauge costs to use (0 to 1). 1 means it has to be full.
    [SerializeField, Range(0.01f, 1f)] private float useCost = 1f;

    // How fast the bar visually catches up when the value changes.
    [SerializeField] private float fillSpeed = 6f;

    // The actual gauge value, always between 0 and 1.
    private float currentValue = 0f;

    // The value shown on screen right now, smoothly chasing currentValue.
    private float displayedValue = 0f;

    private void Update()
    {
        // Move the displayed value a bit closer to the real value each frame,
        // so the bar animates instead of jumping instantly.
        displayedValue = Mathf.Lerp(displayedValue, currentValue, Time.deltaTime * fillSpeed);
        gaugeFillImage.fillAmount = displayedValue;
    }

    // Call this when the player lands a perfect parry.
    public void OnParrySuccess()
    {
        currentValue = Mathf.Clamp01(currentValue + parryFillAmount);
    }

    // Tries to spend the gauge. Returns true if there was enough, false if not.
    public bool TryUseGauge()
    {
        if (currentValue < useCost)
        {
            return false;
        }

        currentValue -= useCost;
        return true;
    }

    // True once the gauge has enough charge to be used.
    public bool IsReady => currentValue >= useCost;

    // Empties the gauge, e.g. when the player dies or a fight ends.
    public void ResetGauge()
    {
        currentValue = 0f;
    }
}