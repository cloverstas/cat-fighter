using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Диагностика настроек сборки: какие форматы текстур и оптимизации кода реально выберет Unity.
// Запуск из batchmode: -executeMethod BuildDiagnostics.Run  (результат — в логе, строки "[Diag]").
public static class BuildDiagnostics
{
    static readonly string[] Samples =
    {
        "Assets/Art/Murzik/murzik_idle_01.png",
        "Assets/Art/Murzik/murzik_super_05.png",
        "Assets/Art/UI/Portraits/murzik_portrait_neutral.png",
        "Assets/Art/UI/Arena/arena_background.jpg",
    };

    // То же, но с набором текстур ASTC (как во второй сборке для телефонов)
    public static void RunAstc()
    {
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        Log($"после установки webGLBuildSubtarget = {EditorUserBuildSettings.webGLBuildSubtarget}");
        AssetDatabase.Refresh();
        Run();
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
    }

    // Проверка "Настроить бойцов" из batchmode: открыть сцену, настроить, сохранить.
    // Если после этого git diff сцены пуст — все ссылки на спрайты остались прежними.
    public static void RunFighterSetup()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        FighterSetup.SetupAll();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Cat Fighter/Диагностика сборки")]
    public static void Run()
    {
        Log($"activeBuildTarget = {EditorUserBuildSettings.activeBuildTarget}");
        Log($"overrideTextureCompression = {EditorUserBuildSettings.overrideTextureCompression}");
        Log($"webGLBuildSubtarget = {EditorUserBuildSettings.webGLBuildSubtarget}");

        // Список форматов сжатия для WebGL из Player Settings — API внутренний, читаем через рефлексию
        foreach (var m in typeof(PlayerSettings).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(m => m.Name.Contains("TextureCompressionFormat")))
            Log($"PlayerSettings.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) : {m.ReturnType.Name}");
        TryLogFormats();

        // Сводка: какой формат получила каждая картинка из Assets/Art (для активной платформы)
        foreach (var group in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .GroupBy(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p).format))
            Log($"формат {group.Key}: {group.Count()} шт. — {string.Join(", ", group.Take(6).Select(System.IO.Path.GetFileName))}{(group.Count() > 6 ? ", ..." : "")}");

        // Страницы атласов: размер и формат (должны быть 2^n и сжаты)
        var atlases = AssetDatabase.FindAssets("t:SpriteAtlas").Select(g => AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        UnityEditor.U2D.SpriteAtlasUtility.PackAtlases(atlases, EditorUserBuildSettings.activeBuildTarget, false);
        foreach (var atlas in atlases)
        {
            var sprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(sprites);
            var pages = sprites.Select(sp => UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(sp, true)).Where(t => t != null).Distinct().ToArray();
            Log($"атлас {atlas.name}: {atlas.spriteCount} спрайтов, страниц {pages.Length}: " +
                string.Join("; ", pages.Select(t => $"{t.width}x{t.height} {t.format} mips={t.mipmapCount}")));
        }

        foreach (string path in Samples)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Log($"{path}: нет импортера"); continue; }
            var ws = importer.GetPlatformTextureSettings("WebGL");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Log($"{path}: compression={importer.textureCompression}, WebGL override={ws.overridden} fmt={ws.format}, " +
                $"auto(WebGL)={importer.GetAutomaticFormat("WebGL")}, imported={tex?.format} {tex?.width}x{tex?.height}, npot={importer.npotScale}");
        }
    }

    static void TryLogFormats()
    {
        var get = typeof(PlayerSettings).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "GetTextureCompressionFormatsImpl" && m.GetParameters().Length == 1);
        if (get == null) { Log("GetTextureCompressionFormats не найден"); return; }
        var pt = get.GetParameters()[0].ParameterType;
        object arg = pt == typeof(BuildTargetGroup) ? BuildTargetGroup.WebGL :
                     pt == typeof(BuildTarget) ? (object)BuildTarget.WebGL : null;
        if (arg == null) { Log($"неизвестный тип параметра {pt}"); return; }
        var result = get.Invoke(null, new[] { arg }) as System.Array;
        Log($"WebGL texture formats = [{string.Join(", ", result?.Cast<object>() ?? new object[0])}]");
    }

    static void Log(string s) => Debug.Log("[Diag] " + s);
}
