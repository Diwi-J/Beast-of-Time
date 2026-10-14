using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("Bar Setup")]
    [SerializeField] private Canvas targetCanvas; // drag your scene's Canvas in here
    [SerializeField] private Vector2 barSize = new Vector2(300f, 40f);
    // Top-left corner of the screen, offset inward a little so it isn't touching the edge.
    [SerializeField] private Vector2 anchoredPosition = new Vector2(20f, -20f);
    [SerializeField] private Color backgroundColor = Color.gray;

    [Header("Health Colors")]
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color cautionColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;

    [Header("Color Thresholds (0 to 1)")]
    // At or below this value the bar turns yellow (60%).
    [SerializeField, Range(0f, 1f)] private float yellowThreshold = 0.6f;
    // At or below this value the bar turns red (30%).
    [SerializeField, Range(0f, 1f)] private float redThreshold = 0.3f;

    [Header("Damage")]
    // How much of the bar one critical hit removes (0.1 = 10%).
    [SerializeField, Range(0.01f, 1f)] private float criticalHitDamage = 0.1f;

    // How fast the bar visually catches up when the value changes.
    [SerializeField] private float fillSpeed = 4f;

    // The real health value (1 = full, 0 = dead), and the smoothed value shown on screen.
    private float currentValue = 1f;
    private float displayedValue = 1f;

    // Built at runtime, so nothing needs to exist in the scene beforehand.
    private RectTransform fillRect;
    private Image fillImage;
    private float maxFillWidth;

    // Floats can be slightly off after repeated subtraction (e.g. 0.6 might become 0.59999996
    // or 0.6000001). This small tolerance makes sure the threshold checks still behave correctly.
    private const float Epsilon = 0.001f;

    // Fired once when health reaches zero. Hook your death / game over logic to this.
    public event Action OnPlayerDied;

    // Lets other scripts read the current health (0 to 1) and check if the player is dead.
    public float CurrentHealth => currentValue;
    public bool IsDead => currentValue <= 0f;

    [SerializeField] private Health health;

    private void Awake()
    {
        // Background bar — the empty frame, built and positioned entirely in code.
        GameObject bg = new GameObject("HealthBackground", typeof(Image));
        bg.transform.SetParent(targetCanvas.transform, false);

        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.sizeDelta = barSize;
        bgRect.anchorMin = new Vector2(0f, 1f); // top-left of the canvas
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot = new Vector2(0f, 1f);      // pivot top-left of the bar, so it sits flush with that anchor
        bgRect.anchoredPosition = anchoredPosition;

        bg.GetComponent<Image>().color = backgroundColor;

        // Fill bar — child of the background, so it inherits its position automatically.
        GameObject fill = new GameObject("HealthFill", typeof(Image));
        fill.transform.SetParent(bg.transform, false);

        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f); // stretch to match background's height
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;

        maxFillWidth = barSize.x;
        fillRect.sizeDelta = new Vector2(maxFillWidth, 0f); // height comes from the stretch anchors above

        fillImage = fill.GetComponent<Image>();
        fillImage.color = healthyColor;
    }

    private void Update()
    {
        // Smoothly move the shown value toward the real value.
        // unscaledDeltaTime keeps this working even if time dilation slows the game down.
        displayedValue = Mathf.Lerp(displayedValue, currentValue, Time.unscaledDeltaTime * fillSpeed);

        // Resize the fill bar to match the displayed value.
        Vector2 size = fillRect.sizeDelta;
        size.x = maxFillWidth * displayedValue;
        fillRect.sizeDelta = size;

        // Pick the color based on the real health, not the smoothed one,
        // so the color changes the instant the threshold is crossed.
        fillImage.color = GetColorForHealth(currentValue);
    }

    // Call this whenever the player gets hit by a critical hit.
    public void OnCriticalHit()
    {
        if (health == null || health.IsDead) return;
        health.TakeDamage(health.Max * criticalHitDamage); // 0.1 = 10% of max health
    }

    // Works out which color the bar should be for a given health value.
    private Color GetColorForHealth(float value)
    {
        if (value <= redThreshold + Epsilon) return dangerColor;   // 30% or below
        if (value <= yellowThreshold + Epsilon) return cautionColor; // 60% or below
        return healthyColor;                                         // above 60%
    }

    // Refills the bar to full, e.g. on respawn or at the start of a new fight.
    public void ResetHealth()
    {
        currentValue = 1f;
        displayedValue = 1f;
    }
    private void OnEnable()
    {
        if (health == null) return;
        health.OnHealthChanged += HandleHealthChanged;
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health == null) return;
        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDeath -= HandleDeath;
    }
    // Health sends current and max. We convert them to the 0-1 value the bar uses.
    private void HandleHealthChanged(float current, float max)
    {
        currentValue = max > 0f ? current / max : 0f;
    }

    // Re-fire your existing event so anything listening to OnPlayerDied still works.
    private void HandleDeath()
    {
        OnPlayerDied?.Invoke();
    }
}