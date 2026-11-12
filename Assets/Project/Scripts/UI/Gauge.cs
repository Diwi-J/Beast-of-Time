using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the current Overdrive charge and its existing temporary UI bar.
/// OverdriveController owns application/removal of the speed effects.
/// HitDetector can continue calling OnParrySuccess() without changes.
/// </summary>
public class Gauge : MonoBehaviour
{
    [Header("Existing UI setup - preserved")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Vector2 barSize = new Vector2(300f, 80f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 20f);
    [SerializeField] private Color backgroundColor = Color.gray;
    [SerializeField] private Color fillColor = Color.blue;

    [Header("Legacy fallback tuning (used only until configured)")]
    [SerializeField, Range(0.01f, 1f)] private float parryFillAmount = 0.34f;
    [SerializeField] private float drainRatePerSecond = 0.02f;
    [Header("UI smoothing")]
    [SerializeField] private float fillSpeed = 2f;

    [Header("Existing SFX reference")]
    public PlayerSwordSound playerSwordSound;

    public event Action OnGaugeEmpty;

    private OverdriveConfig configuration;
    private float currentValue;
    private float displayedValue;
    private bool isDraining;
    private RectTransform fillRect;
    private float maxFillWidth;

    public float CurrentCharge => currentValue;
    public float MaximumCharge => configuration != null ? configuration.MaximumCharge : 1f;
    public float Normalized => Mathf.Clamp01(currentValue / MaximumCharge);
    public bool IsDraining => isDraining;

    private float ChargePerParry => configuration != null ? configuration.ChargePerParry : parryFillAmount;
    private float DrainPerRealSecond => configuration != null ? configuration.DrainPerRealSecond : Mathf.Max(0.0001f, drainRatePerSecond);

    public void Configure(OverdriveConfig source)
    {
        configuration = source;
        currentValue = Mathf.Clamp(currentValue, 0f, MaximumCharge);
        displayedValue = Mathf.Clamp(displayedValue, 0f, MaximumCharge);
        UpdateFillImmediately();
    }

    private void Awake()
    {
        // Preserve the existing runtime-created bar. The UI owner can replace
        // this view later without taking over gauge gameplay state.
        if (targetCanvas == null)
        {
            Debug.LogWarning("[Gauge] Target Canvas is unassigned; gauge still functions but has no built-in bar.", this);
            return;
        }

        GameObject background = new GameObject("GaugeBackground", typeof(Image));
        background.transform.SetParent(targetCanvas.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = barSize;
        backgroundRect.anchorMin = new Vector2(0.5f, 0f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0f);
        backgroundRect.anchoredPosition = anchoredPosition;
        background.GetComponent<Image>().color = backgroundColor;

        GameObject fill = new GameObject("GaugeFill", typeof(Image));
        fill.transform.SetParent(background.transform, false);
        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        maxFillWidth = barSize.x;
        fillRect.sizeDelta = new Vector2(0f, 0f);
        fill.GetComponent<Image>().color = fillColor;
    }

    private void Update()
    {
        if (isDraining)
        {
            currentValue = Mathf.Max(0f, currentValue - DrainPerRealSecond * Time.unscaledDeltaTime);
            if (currentValue <= 0f)
            {
                isDraining = false;
                OnGaugeEmpty?.Invoke();
            }
        }

        displayedValue = Mathf.Lerp(displayedValue, currentValue, Time.unscaledDeltaTime * fillSpeed);
        UpdateFillImmediately();
    }

    public void OnParrySuccess()
    {
        if (isDraining) return;

        currentValue = Mathf.Min(MaximumCharge, currentValue + ChargePerParry);
        if (playerSwordSound != null)
            playerSwordSound.PlayClash();
    }

    /// <summary>
    /// Called by OverdriveController ONLY. Retained original public name for
    /// compatibility with any current UnityEvents or external callers.
    /// </summary>
    public bool StartTimeDilation()
    {
        return StartTimeDilation(0f);
    }

    public bool StartTimeDilation(float minimumCharge)
    {
        if (isDraining || currentValue <= 0f ||
            currentValue + 0.00001f < Mathf.Max(0f, minimumCharge))
            return false;

        isDraining = true;
        if (playerSwordSound != null)
            playerSwordSound.PlayCharge();
        return true;
    }

    public void StopTimeDilation()
    {
        isDraining = false;
    }

    public void ResetGauge()
    {
        isDraining = false;
        currentValue = 0f;
        displayedValue = 0f;
        UpdateFillImmediately();
    }

    private void UpdateFillImmediately()
    {
        if (fillRect == null) return;
        Vector2 size = fillRect.sizeDelta;
        size.x = maxFillWidth * Mathf.Clamp01(displayedValue / MaximumCharge);
        fillRect.sizeDelta = size;
    }
}
