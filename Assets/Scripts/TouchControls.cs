using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Экранные кнопки для телефона: слева — движение и присед, справа — удары, блок и СУПЕР.
// Показываются только на сенсорных устройствах (или всегда, если включить "Always Show" для проверки в редакторе).
//
// Сами кнопки ничего не делают с котом — их читает PlayerController, как и клавиатуру.
// То есть "руки игрока" теперь умеют и клавиатуру, и тачскрин.
public class TouchControls : MonoBehaviour
{
    [SerializeField] private bool alwaysShow = false; // включи, чтобы проверить кнопки мышкой в редакторе

    public static TouchControls Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.visible;

    public TouchButton Left, Right, Crouch, Block, PunchLeft, PunchRight, Kick, Super;

    private Fighter player;       // чья шкала супера подсвечивает кнопку СУПЕР
    private Image superImage;
    private GameObject rotateHint;
    private bool visible;
    private bool pausedForRotate;
    private Font font;
    private Sprite circle;
    private Sprite triangle;

    // Телефон/планшет или экран с тачем
    public static bool IsTouchDevice => Application.isMobilePlatform || Touchscreen.current != null;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (pausedForRotate) Time.timeScale = 1f;
    }

    public void Show(Fighter playerFighter)
    {
        player = playerFighter;
        visible = alwaysShow || IsTouchDevice;
        if (!visible) return;
        Build();
    }

    void Update()
    {
        if (!visible) return;

        // Кнопка СУПЕР: тусклая, пока шкала не полна; полная — яркая и пульсирует
        if (player != null && superImage != null)
        {
            bool ready = player.SuperReady;
            float pulse = ready ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            superImage.color = ready
                ? Color.Lerp(new Color(1f, 0.55f, 0.1f, 0.9f), new Color(1f, 0.9f, 0.4f, 1f), pulse)
                : new Color(0.3f, 0.3f, 0.35f, 0.55f);
            superImage.rectTransform.localScale = Vector3.one * (1f + 0.08f * pulse);
        }

        // Телефон держат вертикально — просим повернуть и ставим бой на паузу
        bool portrait = Screen.height > Screen.width;
        rotateHint.SetActive(portrait);
        if (portrait && !pausedForRotate && Time.timeScale > 0f)
        {
            pausedForRotate = true;
            Time.timeScale = 0f;
        }
        else if (!portrait && pausedForRotate)
        {
            pausedForRotate = false;
            Time.timeScale = 1f;
        }
    }

    // ---------- Построение ----------

    void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        circle = MakeCircleSprite(128);
        triangle = MakeTriangleSprite(64);

        var go = new GameObject("TouchControls_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15; // поверх HUD, под экраном итогов
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)go.transform;

        // Левая рука: ◀ ▶ и ▼ (присед)
        Vector2 bl = new Vector2(0, 0);
        Left = Button(root, "", bl, new Vector2(150, 330), 190, arrowAngle: 180f);
        Right = Button(root, "", bl, new Vector2(390, 330), 190, arrowAngle: 0f);
        Crouch = Button(root, "", bl, new Vector2(270, 170), 140, arrowAngle: -90f);

        // Правая рука: лапы сверху, блок и нога снизу, СУПЕР — большая кнопка сбоку
        Vector2 br = new Vector2(1, 0);
        PunchLeft = Button(root, "ЛАПА", br, new Vector2(-470, 440), 170);
        PunchRight = Button(root, "ЛАПА", br, new Vector2(-270, 480), 170);
        Block = Button(root, "БЛОК", br, new Vector2(-470, 250), 170);
        Kick = Button(root, "НОГА", br, new Vector2(-270, 290), 170);
        Super = Button(root, "СУПЕР", br, new Vector2(-120, 640), 200);
        superImage = Super.GetComponent<Image>();

        // Подсказка "поверни телефон"
        var hint = new GameObject("RotateHint", typeof(RectTransform), typeof(Image));
        var hintRect = (RectTransform)hint.transform;
        hintRect.SetParent(root, false);
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = Vector2.one;
        hintRect.offsetMin = hintRect.offsetMax = Vector2.zero;
        hint.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
        Label(hintRect, "ПОВЕРНИ ТЕЛЕФОН\nГОРИЗОНТАЛЬНО", 90, Vector2.zero, new Vector2(1800, 600));
        rotateHint = hint;
        rotateHint.SetActive(false);

        BattleHUD.EnsureEventSystem();
    }

    // arrowAngle — если задан, вместо надписи рисуем стрелку, повёрнутую на этот угол (0 — вправо)
    TouchButton Button(RectTransform root, string label, Vector2 anchor, Vector2 position, float size,
                       float? arrowAngle = null)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(TouchButton));
        var rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();
        img.sprite = circle;
        img.color = new Color(0.1f, 0.1f, 0.12f, 0.55f);

        if (arrowAngle.HasValue)
        {
            var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            var ar = (RectTransform)arrow.transform;
            ar.SetParent(rect, false);
            ar.sizeDelta = new Vector2(size * 0.42f, size * 0.42f);
            ar.localEulerAngles = new Vector3(0, 0, arrowAngle.Value);
            var ai = arrow.GetComponent<Image>();
            ai.sprite = triangle;
            ai.raycastTarget = false; // нажатие ловит круг кнопки
        }
        else
        {
            Label(rect, label, (int)(size * 0.2f), Vector2.zero, new Vector2(size, size));
        }
        return go.GetComponent<TouchButton>();
    }

    void Label(RectTransform parent, string content, int size, Vector2 position, Vector2 box)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false; // нажатия ловит круг, а не текст
        go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
    }

    // Круглая картинка для кнопок — рисуем прямо в коде, без файла.
    // Каждый пиксель: внутри круга — непрозрачный, по краю — плавно прозрачнеет, снаружи — прозрачный.
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
            float fill = Mathf.Clamp01(r - d);                            // сам круг с мягким краем
            float ring = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 4f)) / 2f); // светлая обводка по краю
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(fill * 0.85f, ring)));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // Треугольник-стрелка "вправо" (для других направлений поворачиваем картинку)
    static Sprite MakeTriangleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Точка внутри, если её отклонение от середины по высоте меньше, чем "осталось" до острия справа
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float halfHeight = 0.5f * (1f - u);
            float inside = Mathf.Clamp01((halfHeight - Mathf.Abs(v - 0.5f)) * size);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
