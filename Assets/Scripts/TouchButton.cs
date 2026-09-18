using UnityEngine;
using UnityEngine.EventSystems;

// Одна экранная кнопка: помнит, зажата ли она (Held), и было ли новое нажатие (Consume).
// IPointerDownHandler и т.п. — "интерфейсы" UI: Unity сама вызывает эти методы при касании или клике.
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool Held { get; private set; }
    private bool pressed;

    public void OnPointerDown(PointerEventData e) { Held = true; pressed = true; }
    public void OnPointerUp(PointerEventData e) { Held = false; }
    public void OnPointerExit(PointerEventData e) { Held = false; } // палец соскользнул с кнопки

    // Было ли нажатие с прошлой проверки (и "съесть" его, чтобы один тап = один удар)
    public bool Consume()
    {
        bool p = pressed;
        pressed = false;
        return p;
    }
}
