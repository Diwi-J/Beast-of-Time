using UnityEngine;
using UnityEngine.UI;

public class Gauge : MonoBehaviour
{
    [Header("Bar Setup")]
    [SerializeField] private Canvas targetCanvas; // drag your scene's Canvas in here
    [SerializeField] private Vector2 barSize = new Vector2(300f, 80f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 20f); // offset up from bottom edge
    [SerializeField] private Color backgroundColor = Color.gray;
    [SerializeField] private Color fillColor = Color.blue;

    // How much a successful parry fills the gauge (0 to 1).
    [SerializeField, Range(0.01f, 1f)] private float parryFillAmount = 0.34f;

    // How much the gauge drains per second while time dilation is active.
    [SerializeField] private float drainRatePerSecond = 0.25f;

    // How fast the bar visually catches up when the value changes.
    [SerializeField] private float fillSpeed = 6f;

    // The real gauge value, and the smoothed value shown on screen.
    private float currentValue = 0f;
    private float displayedValue = 0f;

    // Whether time dilation is currently active and draining the gauge.
    private bool isDraining = false;

    // Built at runtime, so nothing needs to exist in the scene beforehand.
    private RectTransform fillRect;
    private float maxFillWidth;

    private void Awake()
    {
        // Background bar — the empty frame, built and positioned entirely in code.
        GameObject bg = new GameObject("GaugeBackground", typeof(Image));
        bg.transform.SetParent(targetCanvas.transform, false);

        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.sizeDelta = barSize;
        bgRect.anchorMin = new Vector2(0.5f, 0f); // bottom-middle of the canvas
        bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);      // pivot bottom-middle of the bar, so it centers on that anchor
        bgRect.anchoredPosition = anchoredPosition;

        bg.GetComponent<Image>().color = backgroundColor;

        // Fill bar — child of the background, so it inherits its position automatically.
        GameObject fill = new GameObject("GaugeFill", typeof(Image));
        fill.transform.SetParent(bg.transform, false);

        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f); // stretch to match background's height
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;

        maxFillWidth = barSize.x;
        fillRect.sizeDelta = new Vector2(maxFillWidth, 0f); // height comes from the stretch anchors above

        fill.GetComponent<Image>().color = fillColor;
    }

    private void Update()
    {
        // Drain the gauge over time while the ability is active.
        if (isDraining)
        {
            currentValue -= drainRatePerSecond * Time.deltaTime;

            // Auto-stop once the gauge runs out, so the ability can't go negative.
            if (currentValue <= 0f)
            {
                currentValue = 0f;
                StopTimeDilation();
            }
        }

        // Move the displayed value closer to the real value each frame so the bar animates.
        displayedValue = Mathf.Lerp(displayedValue, currentValue, Time.deltaTime * fillSpeed);

        // Resize the fill's width directly — no Image.fillAmount needed.
        Vector2 size = fillRect.sizeDelta;
        size.x = maxFillWidth * displayedValue;
        fillRect.sizeDelta = size;
    }

    // Call this when the player lands a perfect parry.
    public void OnParrySuccess()
    {
        currentValue = Mathf.Clamp01(currentValue + parryFillAmount);
        Debug.Log($"Gauge filled to: {currentValue}");
    }

    // Call this when the player starts using time dilation. Returns false if the gauge is empty.
    public bool StartTimeDilation()
    {
        if (currentValue <= 0f)
        {
            return false;
        }

        isDraining = true;
        return true;
    }

    // Call this when the player releases/cancels time dilation.
    public void StopTimeDilation()
    {
        isDraining = false;
    }

    // True while time dilation is actively draining the gauge.
    public bool IsDraining => isDraining;

    // Empties the gauge, e.g. when the player dies or a fight ends.
    public void ResetGauge()
    {
        currentValue = 0f;
        isDraining = false;
    }
}