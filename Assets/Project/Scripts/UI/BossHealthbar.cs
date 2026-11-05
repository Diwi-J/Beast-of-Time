using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BossHealthBar : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Canvas targetCanvas;   // drag your scene's Canvas in here
    [SerializeField] private Health bossHealth;     // drag the boss's Health component in here

    [Header("Bar Layout")]
    [SerializeField] private Vector2 barSize = new Vector2(400f, 36f);
    // X is negative because "inward" from the right edge means going LEFT.
    // Y is positive because "up" from the bottom edge is +Y.
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-20f, 60f);

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] private Color fillColor = new Color(0.7f, 0.1f, 0.1f); // deep red suits a boss

    [Header("Label")]
    [SerializeField] private string labelText = "Final Boss";
    [SerializeField] private float labelFontSize = 26f;
    [SerializeField] private Color labelColor = Color.white;

    [Header("Behaviour")]
    [SerializeField] private float fillSpeed = 4f;  // how fast the bar catches up visually
    // CHANGED: the key that shows/hides the bar when focusing on the boss.
    [SerializeField] private KeyCode focusKey = KeyCode.F;

    // Real value (1 = full, 0 = dead) vs the smoothed value shown on screen.
    private float currentValue = 1f;
    private float displayedValue = 1f;

    private GameObject barRoot;
    private RectTransform fillRect;
    private float maxFillWidth;

    // Other scripts (e.g. a victory screen) can listen to this.
    public event Action OnBossDied;

    private void Awake()
    {
        // Background: the empty frame, anchored to the bottom-right corner.
        barRoot = new GameObject("BossHealthBackground", typeof(Image));
        barRoot.transform.SetParent(targetCanvas.transform, false);

        RectTransform bgRect = barRoot.GetComponent<RectTransform>();
        bgRect.sizeDelta = barSize;
        // Anchor AND pivot both at (1, 0) = bottom-right, so the bar's own
        // bottom-right corner sits exactly on the canvas's bottom-right corner.
        bgRect.anchorMin = new Vector2(1f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0f);
        bgRect.pivot = new Vector2(1f, 0f);
        bgRect.anchoredPosition = anchoredPosition;

        Image bgImage = barRoot.GetComponent<Image>();
        bgImage.color = backgroundColor;
        bgImage.raycastTarget = false; // never block clicks on other UI

        // Fill: child of the background so it moves with it.
        GameObject fill = new GameObject("BossHealthFill", typeof(Image));
        fill.transform.SetParent(barRoot.transform, false);

        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f); // stretch to the background's height
        fillRect.pivot = new Vector2(0f, 0.5f);   // shrink toward the left edge
        fillRect.anchoredPosition = Vector2.zero;

        maxFillWidth = barSize.x;
        fillRect.sizeDelta = new Vector2(maxFillWidth, 0f); // height comes from the anchors

        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        // Label: child of the background, sitting just above the bar.
        GameObject labelObj = new GameObject("BossHealthLabel", typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(barRoot.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        // Stretch across the top edge so the text box is as wide as the bar.
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0f, 0f);    // sits ON TOP of the bar, not over it
        labelRect.sizeDelta = new Vector2(0f, 34f);
        labelRect.anchoredPosition = new Vector2(0f, 4f);

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.fontSize = labelFontSize;
        label.color = labelColor;
        label.alignment = TextAlignmentOptions.BottomRight; // hugs the screen corner
        label.raycastTarget = false;

        // CHANGED: start hidden. The bar only appears once the player focuses on the boss.
        barRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (bossHealth == null) return;
        bossHealth.OnHealthChanged += HandleHealthChanged;
        bossHealth.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (bossHealth == null) return;
        bossHealth.OnHealthChanged -= HandleHealthChanged;
        bossHealth.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        // CHANGED: press F to show the bar, press F again to hide it.
        // This script sits on a different object from barRoot, so Update keeps
        // running even while the bar itself is switched off.
        if (Input.GetKeyDown(focusKey))
        {
            barRoot.SetActive(!barRoot.activeSelf);
        }

        // unscaledDeltaTime keeps the animation smooth even during slow-motion.
        displayedValue = Mathf.Lerp(displayedValue, currentValue, Time.unscaledDeltaTime * fillSpeed);

        Vector2 size = fillRect.sizeDelta;
        size.x = maxFillWidth * displayedValue;
        fillRect.sizeDelta = size;
    }

    // Health gives us current and max; the bar only needs a 0-1 value.
    private void HandleHealthChanged(float current, float max)
    {
        currentValue = max > 0f ? current / max : 0f;
    }

    private void HandleDeath()
    {
        OnBossDied?.Invoke();
    }

    // Other scripts (e.g. a lock-on system) can call these instead of using F.
    public void Show() { barRoot.SetActive(true); }
    public void Hide() { barRoot.SetActive(false); }
}