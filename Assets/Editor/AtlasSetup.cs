using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

// Меню Cat Fighter → "Создать атласы".
//
// Атлас — одна большая картинка, в которую плотно уложены много маленьких спрайтов.
// Зачем нам:
//  1) у кадров котов ~60% холста 640x512 — пустота; в атласе её нет (Tight-упаковка по контуру);
//  2) страницы атласа — со сторонами 2^n, и WebGL может их сжать (DXT/ASTC) вместе с mip-картами —
//     отдельные кадры 640x512 он оставлял несжатыми;
//  3) меньше переключений текстур при отрисовке.
// Ссылки на спрайты не меняются: FighterSetup, сцена и код работают с теми же Sprite,
// Unity сама подставляет кусок атласа при сборке (и в редакторе, режим Sprite Atlas V2).
public static class AtlasSetup
{
    const string Folder = "Assets/Art/Atlases";

    [MenuItem("Cat Fighter/Создать атласы")]
    public static void CreateAll()
    {
        // Режим упаковщика "Sprite Atlas V2 — Enabled": атласы работают и в редакторе, и в сборке
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "Atlases");

        // Коты: SpriteRenderer рисует сетку по контуру спрайта, поэтому можно укладывать
        // вплотную по форме (Tight) — соседние кадры в картинку не попадут
        Create("Murzik", tight: true, 2048, AssetDatabase.LoadAssetAtPath<Object>("Assets/Art/Murzik"));
        Create("Belchik", tight: true, 2048, AssetDatabase.LoadAssetAtPath<Object>("Assets/Art/Belchik"));

        // Мелкий UI: Image рисует ПРЯМОУГОЛЬНИК спрайта, поэтому только прямоугольная упаковка —
        // иначе в прямоугольник попадут куски соседей. Крупный UI (фон, логотипы) — не в атлас:
        // он и так почти без пустоты и сжимается сам (см. ArtImportSettings).
        // Страницы 1024: мелкого UI всего ~1.5 Мпикс, на странице 2048 (4.2 Мпикс) больше половины пустовало бы
        Create("UI", tight: false, 1024, SmallUISprites());

        AssetDatabase.SaveAssets();
        Debug.Log("Cat Fighter: атласы созданы в " + Folder);
    }

    static Object[] SmallUISprites()
    {
        var result = new List<Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || ArtImportSettings.IsLargeUI(importer)) continue;
            result.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        Debug.Log("Cat Fighter: в UI-атлас — " + string.Join(", ", result.Select(o => o.name)));
        return result.ToArray();
    }

    static void Create(string name, bool tight, int pageSize, params Object[] packables)
    {
        string path = $"{Folder}/{name}.spriteatlasv2";
        AssetDatabase.DeleteAsset(path); // пересоздаём с нуля — всегда одинаковый результат

        var atlas = new SpriteAtlasAsset();
        atlas.Add(packables);
        SpriteAtlasAsset.Save(atlas, path);
        AssetDatabase.ImportAsset(path);

        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
        importer.includeInBuild = true;
        importer.packingSettings = new SpriteAtlasPackingSettings
        {
            enableTightPacking = tight,
            enableRotation = false,   // поворот кадров — лишний риск, выигрыш мал
            enableAlphaDilation = true, // края прозрачных пикселей тянут цвет соседей — без тёмной каймы
            padding = 4,              // зазор между спрайтами: при mip-картах соседи не "протекают"
            blockOffset = 1,
        };
        importer.textureSettings = new SpriteAtlasTextureSettings
        {
            generateMipMaps = true,   // как у исходных кадров — без ряби при уменьшении
            filterMode = FilterMode.Trilinear,
            sRGB = true,
            readable = false,
            anisoLevel = 1,
        };
        importer.SetPlatformSettings(new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            maxTextureSize = pageSize, // максимальный размер страницы; не влезло — Unity заведёт ещё страницу
            format = TextureImporterFormat.Automatic,
            textureCompression = TextureImporterCompression.Compressed, // DXT / ASTC по сборке
            crunchedCompression = false, // Crunch — только после одобрения (шаг 8)
        });
        importer.SaveAndReimport();
    }
}
