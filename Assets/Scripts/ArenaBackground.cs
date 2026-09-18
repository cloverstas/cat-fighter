using UnityEngine;

// Фон арены: растягивает картинку так, чтобы она закрывала весь экран камеры
// (без пустых полос при любом соотношении сторон — 16:9, 4:3, телефон...).
// [ExecuteAlways] — работает и в редакторе без Play, чтобы сразу видеть результат в окне Scene/Game.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaBackground : MonoBehaviour
{
    private SpriteRenderer sr;

    void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = -100; // всегда позади котов
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || sr.sprite == null || !cam.orthographic) return;

        // Видимая область ортографической камеры: высота = 2 × size, ширина = высота × aspect
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;

        // Размер картинки при масштабе 1 (в единицах сцены)
        Vector3 spriteSize = sr.sprite.bounds.size;

        // Берём БОЛЬШИЙ из масштабов — картинка закроет экран целиком (лишнее уйдёт за края)
        float scale = Mathf.Max(viewW / spriteSize.x, viewH / spriteSize.y);
        transform.localScale = new Vector3(scale, scale, 1f);

        // Ставим центр картинки в центр камеры (с учётом pivot спрайта)
        Vector3 centerOffset = sr.sprite.bounds.center * scale;
        Vector3 camPos = cam.transform.position;
        transform.position = new Vector3(camPos.x - centerOffset.x, camPos.y - centerOffset.y, 10f);
    }
}
