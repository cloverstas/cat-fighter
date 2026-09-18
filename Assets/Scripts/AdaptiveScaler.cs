using UnityEngine;
using UnityEngine.UI;

// Подстраивает интерфейс под пропорции экрана, в том числе при повороте телефона:
//   экран УЖЕ 16:9 (планшет, 4:3)      → масштаб по ширине — ничего не вылезет за края по бокам;
//   экран ШИРЕ 16:9 (современный телефон) → масштаб по высоте — ничего не налезет сверху и снизу.
[RequireComponent(typeof(CanvasScaler))]
public class AdaptiveScaler : MonoBehaviour
{
    private CanvasScaler scaler;

    void Awake() => scaler = GetComponent<CanvasScaler>();

    void Update()
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        scaler.matchWidthOrHeight = aspect < 16f / 9f ? 0f : 1f;
    }
}
