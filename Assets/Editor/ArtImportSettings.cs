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
    public override uint GetVersion() => 2;

    private static readonly Vector2 FeetPivot = new Vector2(0.5f, 0.03f); // 16 px от низа холста 512

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

        bool alreadyOk =
            importer.textureType == TextureImporterType.Sprite &&
            settings.spriteMode == (int)SpriteImportMode.Single &&
            settings.spriteAlignment == (int)SpriteAlignment.Custom &&
            settings.spritePivot == FeetPivot &&
            Mathf.Approximately(settings.spritePixelsPerUnit, 100f) &&
            !settings.spriteGenerateFallbackPhysicsShape &&
            importer.mipmapEnabled &&
            importer.filterMode == FilterMode.Trilinear;
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
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear; // плавный переход между копиями
        return true;
    }
}
