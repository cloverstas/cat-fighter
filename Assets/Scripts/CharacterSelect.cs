using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Экран "ВЫБЕРИ БОЙЦА" перед матчем.
// Выбранный кот получает "руки игрока" (PlayerController) и встаёт слева лицом вправо,
// второй получает "мозг" (FighterAI) и встаёт справа. Сам Fighter при этом не меняется —
// в этом и смысл разделения на тело / руки / мозг.
//
// RoundManager ждёт, пока выбор не закончится (yield return select.Run()), и только потом начинает бой.
public class CharacterSelect : MonoBehaviour
{
    // static — живёт между перезапусками сцены: после "ЕЩЁ РАЗ" курсор стоит на прошлом выборе
    private static int lastChoice;

    public Fighter Player { get; private set; }
    public Fighter Enemy { get; private set; }

    private Fighter[] cats;          // [0] — левая карточка, [1] — правая
    private int choice;
    private bool confirmed;
    private RectTransform[] cards;
    private Image[] faces;
    private Font font;
    private GameObject canvasGO;

    public IEnumerator Run()
    {
        cats = FindCats();
        if (cats == null) yield break; // котов не двое — выбирать нечего

        foreach (Fighter f in cats) f.Freeze(); // пока выбираем — никто не двигается
        choice = Mathf.Clamp(lastChoice, 0, 1);
        BuildUI();

        // Ждём подтверждения: каждый кадр читаем клавиатуру и обновляем подсветку карточек
        while (!confirmed)
        {
            ReadKeyboard();
            AnimateCards();
            yield return null;
        }

        // Короткая пауза, чтобы увидеть "выбрал!"
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            AnimateCards();
            yield return null;
        }

        lastChoice = choice;
        Destroy(canvasGO);
        AssignRoles(cats[choice], cats[1 - choice]);
    }

    // Коты: слева тот, кто стоит левее в сцене (Мурзик), справа — второй
    Fighter[] FindCats()
    {
        Fighter[] all = FindObjectsByType<Fighter>();
        if (all.Length != 2) return null;
        return all[0].transform.position.x <= all[1].transform.position.x
            ? new[] { all[0], all[1] }
            : new[] { all[1], all[0] };
    }

    void AssignRoles(Fighter player, Fighter enemy)
    {
        Player = player;
        Enemy = enemy;

        // Места: игрок — на левой стартовой позиции, противник — на правой
        float leftX = Mathf.Min(cats[0].transform.position.x, cats[1].transform.position.x);
        float rightX = Mathf.Max(cats[0].transform.position.x, cats[1].transform.position.x);
        Place(player, leftX, faceRight: true);
        Place(enemy, rightX, faceRight: false);

        // Роли: у каждого ровно один "управляющий"
        SetController<PlayerController, FighterAI>(player);
        SetController<FighterAI, PlayerController>(enemy);
    }

    static void Place(Fighter f, float x, bool faceRight)
    {
        Vector3 p = f.transform.position;
        f.transform.position = new Vector3(x, p.y, p.z);
        f.GetComponent<SpriteRenderer>().flipX = !faceRight; // все кадры смотрят вправо — влево разворачиваем
    }

    static void SetController<TKeep, TRemove>(Fighter f) where TKeep : Component where TRemove : Component
    {
        var extra = f.GetComponent<TRemove>();
        if (extra != null) Destroy(extra);
        if (f.GetComponent<TKeep>() == null) f.gameObject.AddComponent<TKeep>();
    }

    // ---------- Ввод ----------

    void ReadKeyboard()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) choice = 0;
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) choice = 1;
        if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            confirmed = true;
    }

    void Pick(int index) // клик мышкой (или тап) по карточке
    {
        choice = index;
        confirmed = true;
    }

    // ---------- Анимация ----------

    void AnimateCards()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < 2; i++)
        {
            bool selected = i == choice;
            // Выбранная карточка крупнее и "дышит", вторая — меньше и приглушена
            float pulse = selected && !confirmed ? 0.03f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            float target = (selected ? (confirmed ? 1.2f : 1.1f) : 0.88f) + pulse;
            cards[i].localScale = Vector3.Lerp(cards[i].localScale, Vector3.one * target, dt * 12f);
            faces[i].color = Color.Lerp(faces[i].color, selected ? Color.white : new Color(0.45f, 0.45f, 0.5f), dt * 12f);
        }
    }

    // ---------- Интерфейс ----------

    void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var hud = GetComponent<BattleHUD>();

        canvasGO = new GameObject("CharacterSelect_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; // поверх боевого интерфейса
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGO.transform;

        var dim = NewImage(root, "Dim", null, new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform);

        Label(root, "ВЫБЕРИ БОЙЦА", 110, new Color(1f, 0.75f, 0.2f), new Vector2(0, 390), new Vector2(1600, 160));
        Label(root, "VS", 140, Color.white, new Vector2(0, 40), new Vector2(300, 200));
        Label(root, "A / D — ВЫБОР        ПРОБЕЛ / ENTER — В БОЙ!        или кликни по коту", 38, Color.white,
              new Vector2(0, -430), new Vector2(1600, 70));

        cards = new RectTransform[2];
        faces = new Image[2];
        for (int i = 0; i < 2; i++)
            BuildCard(root, i, i == 0 ? -420f : 420f, hud);

        BattleHUD.EnsureEventSystem();
    }

    void BuildCard(RectTransform root, int index, float x, BattleHUD hud)
    {
        Fighter cat = cats[index];

        var card = Box(root, "Card", new Vector2(x, 40), new Vector2(470, 560));
        cards[index] = card;

        // Кликабельна вся карточка: прозрачная картинка ловит клик, Button вызывает Pick
        var hit = NewImage(card, "Click", null, new Color(0, 0, 0, 0));
        Stretch(hit.rectTransform);
        hit.raycastTarget = true;
        var button = hit.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => Pick(index));

        // Портрет в рамке (та же рамка и маска, что в HUD)
        var portrait = Box(card, "Portrait", new Vector2(0, 70), new Vector2(460, 420));
        var mask = NewImage(portrait, "Mask", hud != null ? hud.PortraitMask : null, Color.white);
        Stretch(mask.rectTransform);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Stretch(NewImage(mask.rectTransform, "Back", null, new Color(0.16f, 0.16f, 0.2f)).rectTransform);
        var face = NewImage(mask.rectTransform, "Face", cat.Portrait, Color.white);
        face.rectTransform.anchorMin = new Vector2(0.17f, 0.16f);
        face.rectTransform.anchorMax = new Vector2(0.845f, 0.83f);
        face.rectTransform.offsetMin = face.rectTransform.offsetMax = Vector2.zero;
        face.preserveAspect = true;
        // Карточки смотрят друг на друга: левая — вправо, правая — влево
        bool facesRight = cat.PortraitFacesRight;
        bool wantRight = index == 0;
        face.rectTransform.localScale = new Vector3(facesRight == wantRight ? 1 : -1, 1, 1);
        faces[index] = face;
        var frame = NewImage(portrait, "Frame", hud != null ? hud.PortraitFrame : null, Color.white);
        Stretch(frame.rectTransform);

        // Табличка с именем
        var plate = Box(card, "Name", new Vector2(0, -215), new Vector2(430, 140));
        if (cat.NamePlate != null)
        {
            var img = NewImage(plate, "Plate", cat.NamePlate, Color.white);
            Stretch(img.rectTransform);
            img.preserveAspect = true;
        }
        else
        {
            Label(plate, cat.name.ToUpper(), 64, Color.white, Vector2.zero, new Vector2(430, 140));
        }
    }

    // ---------- Помощники ----------

    static RectTransform Box(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static Image NewImage(RectTransform parent, string name, Sprite sprite, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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
        outline.effectDistance = new Vector2(4, -4);
        return text;
    }
}
