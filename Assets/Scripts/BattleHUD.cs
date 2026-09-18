using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Интерфейс боя как на заставке: портреты в рамках, полоски здоровья, таблички имён,
// таймер, лапки-счёт раундов, баннеры ROUND / FIGHT! / K.O.! и экран итогов.
//
// Весь интерфейс строится из кода по картинкам из полей ниже (их заполняет кнопка
// Cat Fighter → Настроить бойцов). Логики боя здесь нет — ей командует RoundManager.
public class BattleHUD : MonoBehaviour
{
    [Header("Рамки и полоски")]
    [SerializeField] private Sprite portraitFrame;
    [SerializeField] private Sprite portraitMask;    // форма "окна" рамки — портрет не вылезает за края
    [SerializeField] private Sprite healthbarFrame;
    [SerializeField] private Sprite healthbarFill;   // белая заливка по форме рамки — цвет задаём кодом
    [SerializeField] private Sprite timerSplash;
    [SerializeField] private Sprite pawEmpty;
    [SerializeField] private Sprite pawFilled;

    [Header("Баннеры")]
    [SerializeField] private Sprite fightLogo;
    [SerializeField] private Sprite koLogo;
    [SerializeField] private Sprite[] roundBanners;  // ROUND 1, ROUND 2, ROUND 3 (если нет — напишем текстом)
    [SerializeField] private Sprite finalRoundBanner;

    [Header("Экран итогов")]
    [SerializeField] private Sprite endWinTitle;     // "ПОБЕДА!"
    [SerializeField] private Sprite endLoseTitle;    // "ПОРАЖЕНИЕ!"
    [SerializeField] private Sprite endButtonSprite; // кнопка "ЕЩЁ РАЗ" (текст уже нарисован на картинке)

    [Header("Цвета")]
    [SerializeField] private Color healthColor = new Color(0.45f, 0.9f, 0.2f);
    [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.35f); // "шлейф" только что потерянного здоровья
    [SerializeField] private Color portraitBack = new Color(0.16f, 0.16f, 0.2f);

    private const string ControlsHint =
        "A / D — ШАГ     I / O — ЛАПЫ     J — НОГА     K — СУПЕР     ПРОБЕЛ — БЛОК     S — ПРИСЕД";

    public Sprite FightLogo => fightLogo;
    public Sprite PortraitFrame => portraitFrame;
    public Sprite PortraitMask => portraitMask;
    public Sprite KoLogo => koLogo;

    // Всё, что относится к одной стороне экрана (одному коту)
    private class Side
    {
        public Fighter fighter;
        public Image portrait;
        public RectTransform portraitBox;
        public Image healthFill;
        public Image trailFill;
        public Image[] paws;
        public Image superFill;    // шкала суперудара
        public RectTransform superBar;
        public Text superReadyText;
        public Color superColor;
        public float hurtTimer;    // сколько ещё показывать морщащийся портрет
        public float trailDelay;   // пауза перед тем, как шлейф начнёт догонять
    }

    private Side left, right;
    private Font font;
    private RectTransform canvasRoot;
    private Image banner;
    private Text bannerText;
    private Image flash;        // вспышка на весь экран (суперудар попал)
    private Text timerText;
    private Text roundText;
    private GameObject endPanel;
    private Image endTitleImg, endSubtitleImg, endButtonImg;
    private Text endTitle, endSubtitle, endScore, endButtonText;
    private Button endButton;
    private float endShownAt;   // когда показали экран — для анимации появления

    // ---------- Команды от RoundManager ----------

    public void Build(Fighter leftFighter, Fighter rightFighter, int roundsToWin)
    {
        font = GameUI.Font; // шрифт с кириллицей
        canvasRoot = CreateCanvas();

        left = BuildSide(leftFighter, false, roundsToWin);
        right = BuildSide(rightFighter, true, roundsToWin);
        BuildTimer();
        BuildControlsHint();
        BuildBanner();
        BuildFlash();
        BuildEndPanel();
        EnsureEventSystem();
    }

    public void SetTimer(int seconds) => timerText.text = seconds.ToString();
    public void SetRound(int round) => roundText.text = $"ROUND {round}";

    public void SetWins(int leftWins, int rightWins)
    {
        for (int i = 0; i < left.paws.Length; i++) left.paws[i].sprite = i < leftWins ? pawFilled : pawEmpty;
        for (int i = 0; i < right.paws.Length; i++) right.paws[i].sprite = i < rightWins ? pawFilled : pawEmpty;
    }

    // Картинка баннера для раунда: своя, "финальный раунд" или null (тогда покажем текст)
    public Sprite RoundBanner(int round, bool isFinal)
    {
        if (isFinal && finalRoundBanner != null) return finalRoundBanner;
        if (roundBanners != null && round - 1 < roundBanners.Length) return roundBanners[round - 1];
        return null;
    }

    // Показать баннер по центру: картинку, а если её нет — крупный текст
    public void ShowBanner(Sprite sprite, string fallbackText)
    {
        banner.sprite = sprite;
        banner.gameObject.SetActive(sprite != null);
        bannerText.text = fallbackText;
        bannerText.gameObject.SetActive(sprite == null);
        banner.rectTransform.localScale = Vector3.one * 1.4f; // "влёт": начинаем крупнее и сжимаемся в Update
    }

    // Белая вспышка на весь экран (strength 0..1)
    public void Flash(float strength)
    {
        flash.color = new Color(1f, 1f, 1f, strength);
    }

    public void HideBanner()
    {
        banner.gameObject.SetActive(false);
        bannerText.gameObject.SetActive(false);
    }

    // Экран итогов. Для каждой надписи: есть картинка — показываем её, нет — пишем текстом.
    public void ShowEndScreen(bool playerWon, Fighter winner, string score, string fallbackTitle,
                              string buttonLabel, Action onClick)
    {
        HideBanner();

        Sprite title = playerWon ? endWinTitle : endLoseTitle;
        SetImageOrText(endTitleImg, title, endTitle, fallbackTitle);
        endTitle.color = playerWon ? new Color(1f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);

        SetImageOrText(endSubtitleImg, winner.WinsPlate, endSubtitle, $"{winner.name.ToUpper()} ПОБЕЖДАЕТ!");

        endScore.text = score;

        // Кнопка: если есть картинка (текст уже нарисован на ней) — свою надпись прячем
        bool hasButtonArt = endButtonSprite != null;
        endButtonImg.sprite = endButtonSprite;
        endButtonImg.color = hasButtonArt ? Color.white : new Color(0.95f, 0.45f, 0.1f);
        endButtonImg.preserveAspect = hasButtonArt;
        endButtonText.text = buttonLabel;
        endButtonText.gameObject.SetActive(!hasButtonArt);

        endButton.onClick.RemoveAllListeners();
        endButton.onClick.AddListener(() => onClick());

        endShownAt = Time.unscaledTime;
        endPanel.SetActive(true);
    }

    static void SetImageOrText(Image img, Sprite sprite, Text text, string fallback)
    {
        img.sprite = sprite;
        img.gameObject.SetActive(sprite != null);
        text.text = fallback;
        text.gameObject.SetActive(sprite == null);
    }

    // ---------- Каждый кадр: оживляем интерфейс ----------

    void Update()
    {
        if (left == null) return;
        // unscaledDeltaTime — интерфейс живёт в реальном времени, замедление нокаута на него не действует
        float dt = Time.unscaledDeltaTime;
        UpdateSide(left, dt);
        UpdateSide(right, dt);

        // Баннер плавно "приземляется" с масштаба 1.4 до 1
        var b = banner.rectTransform;
        b.localScale = Vector3.Lerp(b.localScale, Vector3.one, dt * 10f);
        bannerText.rectTransform.localScale = b.localScale;

        if (endPanel.activeSelf) AnimateEndScreen();

        // Вспышка гаснет сама
        if (flash.color.a > 0f)
            flash.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(flash.color.a, 0f, dt * 3f));
    }

    // Экран итогов: заголовок "влетает" с пружинкой, кнопка "дышит" — зовёт нажать.
    // Пульсирующая кнопка — стандарт финала playable ad (CTA — call to action, "призыв к действию").
    void AnimateEndScreen()
    {
        float t = Time.unscaledTime - endShownAt;

        // Первые 0.4 сек: масштаб 2.2 → 1 с небольшим "перелётом" (пружина)
        float k = Mathf.Clamp01(t / 0.4f);
        float pop = 1f + 1.2f * (1f - k) * Mathf.Cos(k * Mathf.PI * 1.5f);
        endTitleImg.rectTransform.localScale = Vector3.one * pop;
        endTitle.rectTransform.localScale = Vector3.one * pop;

        // Кнопка появляется чуть позже и пульсирует
        var btn = (RectTransform)endButton.transform;
        btn.gameObject.SetActive(t > 0.6f);
        btn.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 6f));
    }

    void UpdateSide(Side s, float dt)
    {
        float target = (float)s.fighter.Health / s.fighter.MaxHealth;

        // Зелёная полоска — почти сразу; жёлтый шлейф — с задержкой догоняет (видно, сколько сняли)
        s.healthFill.fillAmount = Mathf.MoveTowards(s.healthFill.fillAmount, target, dt * 2f);
        if (s.trailFill.fillAmount > target)
        {
            s.trailDelay -= dt;
            if (s.trailDelay <= 0f)
                s.trailFill.fillAmount = Mathf.MoveTowards(s.trailFill.fillAmount, target, dt * 0.6f);
        }
        else
        {
            s.trailFill.fillAmount = target; // здоровье восстановили (новый раунд) — шлейф сразу на месте
        }

        // Портрет: морщится, пока идёт таймер, "вздрагивает" масштабом и вспыхивает красным
        if (s.hurtTimer > 0f) s.hurtTimer -= dt;
        Sprite want = s.hurtTimer > 0f && s.fighter.PortraitHit != null ? s.fighter.PortraitHit : s.fighter.Portrait;
        if (s.portrait.sprite != want) s.portrait.sprite = want;
        s.portrait.color = Color.Lerp(s.portrait.color, Color.white, dt * 6f);
        s.portraitBox.localScale = Vector3.Lerp(s.portraitBox.localScale, Vector3.one, dt * 12f);

        // Шкала супера: плавно догоняет значение; полная — мигает, "дышит" и пишет "СУПЕР ГОТОВ!"
        s.superFill.fillAmount = Mathf.MoveTowards(s.superFill.fillAmount, s.fighter.SuperMeter01, dt * 1.5f);
        bool ready = s.fighter.SuperReady;
        s.superReadyText.gameObject.SetActive(ready);
        float pulse = ready ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f) : 0f;
        s.superFill.color = Color.Lerp(s.superColor, Color.white, pulse * 0.6f);
        s.superBar.localScale = Vector3.one * (1f + pulse * 0.04f);
    }

    void OnHurt(Side s)
    {
        s.hurtTimer = 0.8f;
        s.trailDelay = 0.4f;
        s.portraitBox.localScale = Vector3.one * 1.15f;       // вздрогнул
        s.portrait.color = new Color(1f, 0.45f, 0.45f);       // красная вспышка, дальше плавно гаснет в Update
    }

    // ---------- Построение ----------

    Side BuildSide(Fighter fighter, bool isRight, int paws)
    {
        var s = new Side { fighter = fighter };
        fighter.Hurt += _ => OnHurt(s); // "=>" здесь — короткая функция: при событии Hurt вызвать OnHurt

        // Правая сторона — зеркало левой: якорь в правом верхнем углу, X с минусом
        Vector2 anchor = isRight ? new Vector2(1, 1) : new Vector2(0, 1);
        float dir = isRight ? -1f : 1f;

        // Портрет: [маска-окно → тёмная подложка + портрет] + рамка поверх
        s.portraitBox = Box(canvasRoot, "Portrait", anchor, new Vector2(24 * dir, -16), new Vector2(214, 196));
        var mirror = Box(s.portraitBox, "Mirror", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);
        mirror.localScale = new Vector3(dir, 1, 1); // рамку справа зеркалим
        var maskImg = Img(mirror, "Mask", portraitMask, Color.white);
        maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false; // Mask обрезает детей по форме картинки
        Img(maskImg.rectTransform, "Back", null, portraitBack); // тёмная подложка — белый Бельчик не сольётся
        s.portrait = Img(maskImg.rectTransform, "Face", fighter.Portrait, Color.white);
        var face = s.portrait.rectTransform;
        face.anchorMin = new Vector2(0.17f, 0.16f); // ровно по "окну" рамки (измерено по portrait_mask)
        face.anchorMax = new Vector2(0.845f, 0.83f);
        // Морда должна смотреть к центру экрана. Рамка уже отзеркалена (dir), поэтому
        // итоговое направление = dir × (куда смотрит картинка) × этот масштаб. Нужно, чтобы вышло dir.
        face.localScale = new Vector3(fighter.PortraitFacesRight ? 1 : -1, 1, 1);
        s.portrait.preserveAspect = true;
        Img(mirror, "Frame", portraitFrame, Color.white);

        // Полоска здоровья: тёмный фон → жёлтый шлейф → зелёная заливка → рамка
        var barBox = Box(canvasRoot, "HealthBar", anchor, new Vector2(226 * dir, -34), new Vector2(740, 111));
        // Зеркалим не саму полоску (она бы перевернулась вокруг своего угла и уехала за экран),
        // а растянутый внутри неё слой — он переворачивается вокруг центра и остаётся на месте.
        var bar = Box(barBox, "Mirror", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);
        bar.localScale = new Vector3(dir, 1, 1); // справа — зеркало: убывает к центру экрана
        Img(bar, "Back", healthbarFill, new Color(0f, 0f, 0f, 0.65f));
        s.trailFill = FillImg(bar, "Trail", trailColor);
        s.healthFill = FillImg(bar, "Health", healthColor);
        Img(bar, "Frame", healthbarFrame, Color.white);

        // Табличка с именем под полоской
        var plate = Box(canvasRoot, "NamePlate", anchor, new Vector2(250 * dir, -128), new Vector2(330, 108));
        var plateImg = Img(plate, "Plate", fighter.NamePlate, Color.white);
        plateImg.preserveAspect = true;
        if (fighter.NamePlate == null) plateImg.gameObject.SetActive(false);

        // Лапки-счёт у внутреннего края полоски (ближе к таймеру)
        s.paws = new Image[paws];
        for (int i = 0; i < paws; i++)
        {
            var paw = Box(canvasRoot, "Paw", anchor, new Vector2((226 + 740 - 60 - i * 62) * dir, -150), new Vector2(54, 49));
            s.paws[i] = Img(paw, "Icon", pawEmpty, Color.white);
            s.paws[i].preserveAspect = true;
        }
        BuildSuperBar(s, isRight);
        return s;
    }

    // Шкала суперудара внизу экрана — как синяя и красная полоски на заставке
    void BuildSuperBar(Side s, bool isRight)
    {
        Vector2 anchor = isRight ? new Vector2(1, 0) : new Vector2(0, 0);
        float dir = isRight ? -1f : 1f;
        s.superColor = isRight ? new Color(1f, 0.28f, 0.2f) : new Color(0.25f, 0.6f, 1f);

        var paw = Box(canvasRoot, "SuperPaw", anchor, new Vector2(22 * dir, 26), new Vector2(66, 60));
        Img(paw, "Icon", pawFilled, Color.white).preserveAspect = true;

        s.superBar = Box(canvasRoot, "SuperBar", anchor, new Vector2(96 * dir, 30), new Vector2(520, 78));
        var mirror = Box(s.superBar, "Mirror", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);
        mirror.localScale = new Vector3(dir, 1, 1);
        Img(mirror, "Back", healthbarFill, new Color(0f, 0f, 0f, 0.65f));
        s.superFill = FillImg(mirror, "Fill", s.superColor);
        s.superFill.fillAmount = 0f;
        Img(mirror, "Frame", healthbarFrame, Color.white);

        s.superReadyText = Label(s.superBar, "СУПЕР ГОТОВ!", 30, Color.white, new Vector2(0, 54), new Vector2(520, 40));
        s.superReadyText.gameObject.SetActive(false);
    }

    void BuildFlash()
    {
        flash = Img(canvasRoot, "Flash", null, new Color(1f, 1f, 1f, 0f));
    }

    void BuildTimer()
    {
        var box = Box(canvasRoot, "Timer", new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(300, 185));
        Img(box, "Splash", timerSplash, Color.white).preserveAspect = true;
        timerText = Label(box, "99", 96, Color.white, new Vector2(0, 16), new Vector2(300, 110));
        roundText = Label(box, "ROUND 1", 28, Color.white, new Vector2(0, -52), new Vector2(300, 40));
    }

    void BuildControlsHint()
    {
        if (TouchControls.IsTouchDevice) return; // на телефоне клавиатуры нет — вместо подсказки экранные кнопки
        var box = Box(canvasRoot, "Controls", new Vector2(0.5f, 0), new Vector2(0, 118), new Vector2(1250, 56));
        Img(box, "Back", null, new Color(0f, 0f, 0f, 0.55f));
        Label(box, ControlsHint, 26, Color.white, Vector2.zero, new Vector2(1250, 56));
    }

    void BuildBanner()
    {
        var box = Box(canvasRoot, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1000, 370));
        banner = Img(box, "Image", null, Color.white);
        banner.preserveAspect = true;
        bannerText = Label(box, "", 150, new Color(1f, 0.45f, 0.1f), Vector2.zero, new Vector2(1400, 300));
        HideBanner();
    }

    void BuildEndPanel()
    {
        endPanel = new GameObject("EndPanel", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)endPanel.transform;
        rect.SetParent(canvasRoot, false);
        Stretch(rect);
        endPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // Заголовок ПОБЕДА! / ПОРАЖЕНИЕ! (картинка или текст)
        var title = Box(rect, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1050, 340));
        endTitleImg = Img(title, "Image", null, Color.white);
        endTitleImg.preserveAspect = true;
        endTitle = Label(title, "", 150, Color.white, Vector2.zero, new Vector2(1400, 220));

        // "<ИМЯ> ПОБЕЖДАЕТ!"
        var sub = Box(rect, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0, 65), new Vector2(760, 95));
        endSubtitleImg = Img(sub, "Image", null, Color.white);
        endSubtitleImg.preserveAspect = true;
        endSubtitle = Label(sub, "", 60, Color.white, Vector2.zero, new Vector2(1400, 100));

        // Счёт — текстом, в стиле табло (белые жирные цифры с толстой чёрной обводкой):
        // он бывает разный (2:0, 2:1, 1:2...), картинкой его не нарисуешь заранее
        endScore = Label(rect, "", 140, Color.white, new Vector2(0, -75), new Vector2(600, 170));
        endScore.GetComponent<Outline>().effectDistance = new Vector2(6, -6);

        // Кнопка
        var btn = Box(rect, "Button", new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(560, 125));
        endButtonImg = btn.gameObject.AddComponent<Image>();
        endButton = btn.gameObject.AddComponent<Button>();
        endButtonText = Label(btn, "", 64, Color.white, Vector2.zero, new Vector2(560, 125));

        endPanel.SetActive(false);
    }

    // ---------- Маленькие помощники, чтобы не повторять одно и то же ----------

    RectTransform CreateCanvas()
    {
        var go = new GameObject("BattleHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        GameUI.SetupScaler(scaler); // одинаково на любом разрешении и пропорциях
        return (RectTransform)go.transform;
    }

    // Прямоугольник, привязанный к углу/центру родителя (anchor) и сдвинутый на position
    RectTransform Box(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, bool stretch = false)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        if (stretch) { Stretch(rect); return rect; }
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    // Картинка на весь родительский прямоугольник
    Image Img(RectTransform parent, string name, Sprite sprite, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        Stretch(img.rectTransform);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false; // на картинки не кликают — не мешаем кнопкам
        return img;
    }

    // Заливка полоски: Image типа Filled — показывает только долю fillAmount (0..1)
    Image FillImg(RectTransform parent, string name, Color color)
    {
        var img = Img(parent, name, healthbarFill, color);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left; // полная сторона — у портрета
        img.fillAmount = 1f;
        return img;
    }

    Text Label(RectTransform parent, string content, int size, Color color, Vector2 position, Vector2 boxSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3, -3);
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Чтобы кнопки нажимались, в сцене нужен EventSystem (для новой Input System — свой модуль ввода)
    public static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
