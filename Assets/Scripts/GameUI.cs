using UnityEngine;
using UnityEngine.UI;

// Общие настройки интерфейса для всех экранов игры.
public static class GameUI
{
    private static Font font;

    // Шрифт с кириллицей. Встроенный шрифт Unity её не содержит: в редакторе Windows
    // подставляет русские буквы из системных шрифтов, а в браузере — неоткуда, и текст пропадает.
    // Шрифт лежит в папке Resources — всё оттуда можно загрузить по имени через Resources.Load.
    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.Load<Font>("Fonts/RussoOne-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // запасной вариант
            return font;
        }
    }

    // Настроить масштаб холста интерфейса под любой экран
    public static void SetupScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        if (scaler.GetComponent<AdaptiveScaler>() == null) scaler.gameObject.AddComponent<AdaptiveScaler>();
    }
}
