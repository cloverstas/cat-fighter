using System.Collections;
using UnityEngine;

// Кино-эффекты суперудара — то, что делает момент "сочным":
//   начало приёма: время замирает, фон темнеет, крупно название приёма;
//   попадание:     белая вспышка, тряска камеры и короткий стоп-кадр ("hitstop").
// Коты про эффекты ничего не знают — скрипт просто подписан на их события.
public class SuperEffects : MonoBehaviour
{
    [SerializeField] private float startFreeze = 0.45f;   // сколько реальных секунд время стоит в начале приёма
    [SerializeField] private float hitStop = 0.12f;       // стоп-кадр в момент попадания
    [SerializeField] private float shakeTime = 0.35f;
    [SerializeField] private float shakeStrength = 0.25f; // в единицах сцены
    [SerializeField] private Color darkBackground = new Color(0.3f, 0.3f, 0.35f);

    private BattleHUD hud;
    private SpriteRenderer background;
    private Camera cam;
    private Vector3 camBase;
    private bool busy; // эффект уже идёт — второй поверх не запускаем

    void Start()
    {
        hud = GetComponent<BattleHUD>();
        cam = Camera.main;
        if (cam != null) camBase = cam.transform.position;
        var arena = FindAnyObjectByType<ArenaBackground>();
        if (arena != null) background = arena.GetComponent<SpriteRenderer>();

        foreach (Fighter f in FindObjectsByType<Fighter>())
        {
            f.SuperStarted += OnSuperStarted;
            f.SuperLanded += OnSuperLanded;
        }
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void OnSuperStarted(Fighter f)
    {
        if (!busy) StartCoroutine(StartSequence(f));
    }

    IEnumerator StartSequence(Fighter f)
    {
        busy = true;
        if (background != null) background.color = darkBackground; // фон темнеет, коты остаются яркими
        if (hud != null) hud.ShowBanner(null, f.SuperName);

        // Стоп-кадр: timeScale = 0 — всё, что зависит от времени, замирает.
        // Ждём через WaitForSecondsRealtime — на реальные секунды пауза не действует.
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(startFreeze);
        Time.timeScale = 1f;

        if (hud != null) hud.HideBanner();

        // Фон плавно возвращает цвет
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 3f)
        {
            if (background != null) background.color = Color.Lerp(darkBackground, Color.white, t);
            yield return null; // подождать один кадр
        }
        if (background != null) background.color = Color.white;
        busy = false;
    }

    void OnSuperLanded(Fighter attacker, Fighter victim)
    {
        if (hud != null) hud.Flash(0.7f);
        StartCoroutine(Shake());
        // Если супер нокаутировал — стоп-кадр не нужен: там своё замедление от судьи
        if (!victim.IsKO) StartCoroutine(HitStop());
    }

    IEnumerator HitStop()
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(hitStop);
        Time.timeScale = 1f;
    }

    IEnumerator Shake()
    {
        if (cam == null) yield break;
        for (float t = 0f; t < shakeTime; t += Time.unscaledDeltaTime)
        {
            // Случайный сдвиг, который затухает к концу тряски
            float power = shakeStrength * (1f - t / shakeTime);
            cam.transform.position = camBase + (Vector3)(Random.insideUnitCircle * power);
            yield return null;
        }
        cam.transform.position = camBase;
    }
}
