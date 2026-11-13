using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar: MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] Vector3 offset = new Vector3(0f, 1f, 0f);   // how far above the enemy's pivot
    [SerializeField] Vector2 size = new Vector2(1.5f, 0.2f);     // bar size in world units
    [SerializeField] Color backColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] Color fillColor = Color.red;

    GameObject barRoot;
    RectTransform fillRect;
    Camera cam;

    void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        cam = Camera.main; // cache it, calling Camera.main every frame is wasteful
        BuildBar();
        //barRoot.SetActive(false); // hidden until the player targets this enemy
    }

    void Start()
    {
        // Start, not Awake: by now Health.Awake has definitely set Current,
        // so the bar starts correct even if the enemy begins below max.
        UpdateBar(health.Current, health.Max);
    }

    void BuildBar()
    {
        // World Space canvas so the bar lives in the 3D scene.
        // Child of the enemy, so it follows the enemy automatically.
        barRoot = new GameObject("HealthBar", typeof(Canvas));
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = offset;

        barRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        barRoot.GetComponent<RectTransform>().sizeDelta = size;

        // Background stretches over the whole canvas (anchors 0 -> 1, no offsets)
        var back = new GameObject("Background", typeof(Image));
        back.transform.SetParent(barRoot.transform, false);
        var backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.offsetMin = backRect.offsetMax = Vector2.zero;
        back.GetComponent<Image>().color = backColor;

        // Fill shrinks by moving its right anchor, because Image.Filled needs a sprite
        // and we have none when building in code.
        var fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(barRoot.transform, false);
        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = fillColor;
    }

    void OnEnable()
    {
        health.OnHealthChanged += UpdateBar;
        health.OnDeath += HideBar;
    }

    void OnDisable()
    {
        // Always mirror every += with a -= so you don't get errors after destroy
        health.OnHealthChanged -= UpdateBar;
        health.OnDeath -= HideBar;
    }

    void UpdateBar(float current, float max)
    {
        fillRect.anchorMax = new Vector2(max > 0f ? current / max : 0f, 1f);
    }

    void HideBar() => SetVisible(false);

    public void SetVisible(bool visible)
    {
        if (visible && health.IsDead) return; // dead enemies never show a bar
        barRoot.SetActive(visible);
    }

    void LateUpdate()
    {
        // Billboard: face the camera. LateUpdate so the camera has already moved this frame.
        if (barRoot.activeSelf)
            barRoot.transform.forward = cam.transform.forward;
    }
}