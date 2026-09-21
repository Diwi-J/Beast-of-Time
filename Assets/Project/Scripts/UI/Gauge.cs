using UnityEngine;
using UnityEngine.UI;

public class Gauge : MonoBehaviour
{
    // The empty bar / frame image that sits behind the fill.
    [SerializeField] private Image gaugeBackgroundImage;

    // The image that fills up. The script sets it to "Filled" for you.
    [SerializeField] private Image gaugeFillImage;

    // How much a successful parry fills the gauge (0 to 1).
    [SerializeField, Range(0.01f, 1f)] private float parryFillAmount = 0.34f;

    // How much the gauge costs to use (0 to 1). 1 means it has to be full.
    [SerializeField, Range(0.01f, 1f)] private float useCost = 1f;

    // How fast the bar visually catches up when the value changes.
    [SerializeField] private float fillSpeed = 6f;

    // The real gauge value, and the smoothed value shown on screen.
    private float currentValue = 0f;
    private float displayedValue = 0f;

    private void Awake()
    {
        // fillAmount only works when the Image Type is Filled.
        gaugeFillImage.type = Image.Type.Filled;
        gaugeFillImage.fillMethod = Image.FillMethod.Horizontal;
        gaugeFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void Update()
    {
        // Move the displayed value closer to the real value each frame so the bar animates.
        displayedValue = Mathf.Lerp(displayedValue, currentValue, Time.deltaTime * fillSpeed);
        gaugeFillImage.fillAmount = displayedValue;
    }

    // Call this when the player lands a perfect parry.
    public void OnParrySuccess()
    {
        currentValue = Mathf.Clamp01(currentValue + parryFillAmount);
    }

    // Spends the gauge. Returns true if there was enough, false if not.
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