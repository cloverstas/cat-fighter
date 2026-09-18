using UnityEngine;

// Фон арены + кадрирование камеры.
//
// Фон растягивается так, чтобы закрыть весь экран при любых пропорциях (16:9, 4:3, вытянутый телефон),
// и при этом ПОЛ НА КАРТИНКЕ всегда совпадает с полом, на котором стоят коты (y = floorY).
// Раньше фон просто центрировался: на широком телефоне он растягивался, ковёр уезжал вниз,
// и коты "висели в воздухе".
//
// На телефоне камера подъезжает ближе — коты крупнее.
// [ExecuteAlways] — фон подгоняется и в редакторе без Play, чтобы сразу видеть результат.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaBackground : MonoBehaviour
{
    // Лёгкое затемнение комнаты: коты на её фоне читаются лучше. 1 — без затемнения.
    [Range(0.5f, 1f)] [SerializeField] private float brightness = 0.85f;

    [Header("Пол")]
    [SerializeField] private float floorY = 0f;                         // где стоят лапы котов (в сцене)
    [Range(0f, 0.5f)] [SerializeField] private float floorOnPicture = 0.22f; // та же линия на картинке: доля высоты от низа

    [Header("Камера")]
    [SerializeField] private float pcCameraSize = 4.5f;      // половина видимой высоты на ПК
    [SerializeField] private float mobileCameraSize = 3.8f;  // на телефоне меньше — коты крупнее
    [Range(0f, 0.5f)] [SerializeField] private float floorOnScreen = 0.22f; // пол — на такой доле высоты экрана от низа

    private SpriteRenderer sr;
    private Vector3? baseCamPos; // "?" — может быть пустым: ещё не запомнили

    public SpriteRenderer Renderer => sr;
    public Color Tint => new Color(brightness, brightness, brightness); // обычный цвет фона

    void Awake()
    {
        if (Application.isPlaying) FrameCamera();
    }

    void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = -100; // всегда позади котов
        sr.color = Tint;
    }

    // Камера: размер зависит от устройства, высота — так, чтобы пол был на floorOnScreen от низа экрана
    void FrameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float size = Application.isMobilePlatform ? mobileCameraSize : pcCameraSize;
        cam.orthographicSize = size;
        Vector3 p = cam.transform.position;
        // Низ экрана = camY - size; хотим, чтобы пол был выше низа на floorOnScreen × (2 × size)
        cam.transform.position = new Vector3(p.x, floorY - floorOnScreen * 2f * size + size, p.z);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || sr.sprite == null || !cam.orthographic) return;

        // В игре держимся за стартовую позицию камеры — чтобы при тряске камеры фон трясся вместе с котами
        Vector3 camPos;
        if (Application.isPlaying)
        {
            if (baseCamPos == null) baseCamPos = cam.transform.position;
            camPos = baseCamPos.Value;
        }
        else
        {
            camPos = cam.transform.position;
        }

        // Видимая область ортографической камеры: высота = 2 × size, ширина = высота × aspect
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        float camBottom = camPos.y - viewH / 2f;
        float camTop = camPos.y + viewH / 2f;

        // Масштаб: берём БОЛЬШИЙ — картинка закроет экран целиком (лишнее уйдёт за края)
        Bounds b = sr.sprite.bounds; // размер картинки при масштабе 1
        float scale = Mathf.Max(viewW / b.size.x, viewH / b.size.y);
        transform.localScale = new Vector3(scale, scale, 1f);
        float picH = b.size.y * scale;

        // Где должен быть низ картинки, чтобы её пол совпал с полом котов...
        float bottom = floorY - floorOnPicture * picH;
        // ...но без пустых полос: низ не выше низа экрана, верх не ниже верха экрана
        bottom = Mathf.Clamp(bottom, camTop - picH, camBottom);

        // Переводим "низ картинки" и "центр по X" в позицию объекта (у спрайта свой pivot)
        float x = camPos.x - b.center.x * scale;
        float y = bottom - b.min.y * scale;
        transform.position = new Vector3(x, y, 10f);
    }
}
