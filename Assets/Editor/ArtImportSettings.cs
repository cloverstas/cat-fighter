using UnityEditor;
using UnityEngine;

// Автонастройка импорта: любая картинка, попавшая в Assets/Art, сразу становится
// спрайтом с нашими настройками (Single, pivot между лап, 100 px на единицу).
// Больше не нужно выставлять это руками в Inspector для каждого нового кадра.
//
// AssetPostprocessor — класс, методы которого Unity сама вызывает при импорте файлов.
public class ArtImportSettings : AssetPostprocessor
{
    // Версия настроек. Если поменять число — Unity сама переимпортирует все картинки из Assets/Art.
    public override uint GetVersion() => 3;

    private static readonly Vector2 FeetPivot = new Vector2(0.5f, 0.03f); // 16 px от низа холста 512

    // Крупный интерфейс (фон, логотипы, баннеры, полоски здоровья) показывается почти в своём размере —
    // уменьшенные копии (mip-карты) ему не нужны.
    private const int LargeUISize = 1024;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Art/")) return;
        Apply((TextureImporter)assetImporter);
    }

    // Возвращает true, если что-то пришлось поменять
    public static bool Apply(TextureImporter importer)
    {
        if (importer == null) return false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool mips = !IsLargeUI(importer);

        bool alreadyOk =
            importer.textureType == TextureImporterType.Sprite &&
            settings.spriteMode == (int)SpriteImportMode.Single &&
            settings.spriteAlignment == (int)SpriteAlignment.Custom &&
            settings.spritePivot == FeetPivot &&
            Mathf.Approximately(settings.spritePixelsPerUnit, 100f) &&
            !settings.spriteGenerateFallbackPhysicsShape &&
            importer.mipmapEnabled == mips &&
            importer.filterMode == FilterMode.Trilinear &&
            importer.textureCompression == TextureImporterCompression.Compressed &&
            !importer.crunchedCompression;
        if (alreadyOk) return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.ReadTextureSettings(settings); // перечитываем после смены типа
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = FeetPivot;
        settings.spritePixelsPerUnit = 100f;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        // Mip-карты — заранее уменьшенные копии картинки (1/2, 1/4, 1/8...).
        // Когда картинка на экране меньше оригинала, видеокарта берёт подходящую копию —
        // без них мелкие детали (шерсть, брызги кисти) "рябят" и мерцают.
        //
        // Важно для WebGL: если у картинки есть mip-карты, а стороны не степень двойки (640x512),
        // Unity НЕ сжимает её (остаётся RGBA32, 4 байта на пиксель): у маленьких копий (5x4, 10x8...)
        // стороны не кратны 4, а WebGL требует этого для сжатых форматов. Поэтому:
        //  - коты и мелкий UI — с mip-картами; сжимаются в атласах (у страниц атласа стороны 2^n);
        //  - крупный UI — без mip-карт, тогда сжимается и сам по себе.
        importer.mipmapEnabled = mips;
        importer.filterMode = FilterMode.Trilinear; // плавный переход между копиями

        // Сжатие для видеокарты (DXT на ПК, ASTC на телефонах): в 4-8 раз меньше, чем RGBA32.
        // Crunch (ещё меньше, но с потерями) — пока выключен.
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = false;
        return true;
    }

    static bool IsLargeUI(TextureImporter importer)
    {
        if (!importer.assetPath.StartsWith("Assets/Art/UI/")) return false;
        importer.GetSourceTextureWidthAndHeight(out int w, out int h);
        return Mathf.Max(w, h) >= LargeUISize;
    }
}
