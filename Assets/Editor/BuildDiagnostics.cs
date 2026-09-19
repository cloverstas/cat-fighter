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

    // Пробная сборка с Crunch на атласах котов (только DXT) — для сравнения "до/после".
    // Качество — из переменной окружения CF_CRUNCH_Q (0-100). Настройки атласов потом возвращаются.
    public static void BuildCrunchPreview()
    {
        int q = int.Parse(System.Environment.GetEnvironmentVariable("CF_CRUNCH_Q") ?? "80");
        string[] atlases = { "Assets/Art/Atlases/Murzik.spriteatlasv2", "Assets/Art/Atlases/Belchik.spriteatlasv2" };
        SetCrunch(atlases, true, q);
        try
        {
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
            AssetDatabase.Refresh();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = $"Builds/Crunch{q}",
                target = BuildTarget.WebGL,
                subtarget = (int)WebGLTextureSubtarget.DXT,
            });
            Log($"Crunch {q}: {report.summary.result}, {report.summary.totalSize / (1024f * 1024f):F1} МБ");
        }
        finally { SetCrunch(atlases, false, 50); }
    }

    // Образцы для сравнения Crunch: одни и те же кадры из атласа котов в вариантах
    // "без Crunch", "Crunch 100", "Crunch 80" — декодированные видеокартой (нужен запуск без -nographics).
    public static void ExportCrunchSamples()
    {
        string outDir = System.Environment.GetEnvironmentVariable("CF_SAMPLES_DIR");
        string[] atlasPaths = { "Assets/Art/Atlases/Murzik.spriteatlasv2", "Assets/Art/Atlases/Belchik.spriteatlasv2" };
        string[] names = { "murzik_idle_01", "murzik_super_05", "belchik_idle_01", "belchik_super_05" };
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
        var variants = new (string label, bool on, int q)[] { ("off", false, 50), ("c100", true, 100), ("c80", true, 80) };
        try
        {
            foreach (var v in variants)
            {
                SetCrunch(atlasPaths, v.on, v.q);
                var atlases = atlasPaths.Select(AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>).ToArray();
                UnityEditor.U2D.SpriteAtlasUtility.PackAtlases(atlases, BuildTarget.WebGL, false);
                foreach (var atlas in atlases)
                {
                    var sprites = new Sprite[atlas.spriteCount];
                    atlas.GetSprites(sprites);
                    foreach (var sp in sprites)
                    {
                        string n = sp.name.Replace("(Clone)", "");
                        if (!names.Contains(n)) continue;
                        var page = UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(sp, true);
                        var uv = UnityEditor.Sprites.SpriteUtility.GetSpriteUVs(sp, true);
                        float x0 = uv.Min(u => u.x), x1 = uv.Max(u => u.x), y0 = uv.Min(u => u.y), y1 = uv.Max(u => u.y);
                        var rt = RenderTexture.GetTemporary(page.width, page.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                        Graphics.Blit(page, rt);
                        RenderTexture.active = rt;
                        int px = Mathf.FloorToInt(x0 * page.width), py = Mathf.FloorToInt(y0 * page.height);
                        int w = Mathf.CeilToInt(x1 * page.width) - px, h = Mathf.CeilToInt(y1 * page.height) - py;
                        var crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        crop.ReadPixels(new Rect(px, py, w, h), 0, 0);
                        RenderTexture.active = null;
                        RenderTexture.ReleaseTemporary(rt);
                        System.IO.File.WriteAllBytes($"{outDir}/{n}_{v.label}.png", crop.EncodeToPNG());
                        Log($"{n} {v.label}: страница {page.width}x{page.height} {page.format}, кусок {w}x{h}");
                    }
                }
            }
        }
        finally { SetCrunch(atlasPaths, false, 50); }
    }

    static void SetCrunch(string[] paths, bool on, int quality)
    {
        foreach (string path in paths)
        {
            var importer = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            var ps = importer.GetPlatformSettings("DefaultTexturePlatform");
            ps.crunchedCompression = on;
            ps.compressionQuality = quality;
            importer.SetPlatformSettings(ps);
            importer.SaveAndReimport();
        }
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
