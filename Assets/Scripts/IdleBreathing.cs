using UnityEngine;

// "Дыхание" в стойке: кот плавно вытягивается вверх и чуть сужается, потом обратно.
// Работает через масштаб объекта, поэтому хватает одного кадра вместо нескольких картинок.
public class IdleBreathing : MonoBehaviour
{
    [SerializeField] private float breathsPerSecond = 0.7f; // сколько вдохов в секунду
    [SerializeField] private float amount = 0.03f;          // сила: 0.03 = вытягивается на 3%

    private Vector3 baseScale; // исходный размер, от него и "дышим"

    void Start()
    {
        // Запоминаем масштаб, который стоит в Inspector, чтобы не потерять его
        baseScale = transform.localScale;
    }

    void Update()
    {
        // Mathf.Sin даёт плавную волну от -1 до 1.
        // Time.time — секунды с начала игры; умножаем на 2π, чтобы 1 = один полный вдох-выдох.
        float wave = Mathf.Sin(Time.time * breathsPerSecond * 2f * Mathf.PI);
        float stretch = wave * amount;

        // Вверх растягиваем, в ширину чуть сжимаем — так выглядит живее, чем просто "раздуваться"
        transform.localScale = new Vector3(
            baseScale.x * (1f - stretch * 0.5f),
            baseScale.y * (1f + stretch),
            baseScale.z);
    }
}
