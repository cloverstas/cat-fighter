using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Меню Cat Fighter → "Собрать WebGL": игра превращается в веб-страницу,
// которую можно открыть в браузере на ПК и телефоне (GitHub Pages, свой сервер, itch.io).
public static class WebGLBuild
{
    public const string OutputFolder = "Builds/WebGL"; // папка Builds не хранится в git (.gitignore)
    const string AstcTempFolder = "Builds/WebGL_ASTC_tmp";
    const string AstcDataName = "WebGL.astc.data.unityweb";

    [MenuItem("Cat Fighter/Собрать WebGL")]
    public static void Build() => Build(OutputFolder, BuildOptions.None, withAstc: true);

    // Отладочная сборка: в ошибках браузера видны настоящие имена функций, а не wasm-function[1518].
    // Только DXT — так быстрее, для поиска ошибок в коде второй набор текстур не нужен.
    public static void BuildDev() => Build("Builds/WebGLDev", BuildOptions.Development, withAstc: false);

    static void Build(string outputFolder, BuildOptions buildOptions, bool withAstc)
    {
        // Сжатие Gzip + Decompression Fallback: браузер сам распакует файлы.
        // Так игра работает на любом хостинге, даже без настройки сервера (например, GitHub Pages).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;               // повторный заход — быстрее, файлы в кэше браузера
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = false;

        // Без заставки "Made with Unity": минус 2.7 МБ (картинка логотипа) и ~2 с до начала игры.
        // С Unity 6 отключается на любой лицензии, включая бесплатную.
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        // Текстуры — в двух GPU-форматах. Видеокарта ПК понимает DXT, телефона — ASTC.
        // Если формат не поддерживается, Unity распакует текстуры на лету в RGBA32 —
        // игра работает, но загрузка дольше и памяти нужно в 4-8 раз больше.
        // Одна сборка Web хранит только один формат, поэтому собираем дважды (так советует
        // документация Unity): ASTC во временную папку, потом DXT в основную, и забираем
        // из первой только файл данных. Код (wasm) у обеих сборок одинаковый.
        if (withAstc)
        {
            if (!BuildOnce(AstcTempFolder, buildOptions, WebGLTextureSubtarget.ASTC, out _)) return;
        }
        if (!BuildOnce(outputFolder, buildOptions, WebGLTextureSubtarget.DXT, out BuildReport report)) return;

        long totalBytes = (long)report.summary.totalSize;
        if (withAstc)
        {
            string astcData = Path.Combine(outputFolder, "Build", AstcDataName);
            File.Copy(Path.Combine(AstcTempFolder, "Build", DataFileName(AstcTempFolder)), astcData, true);
            PatchIndexHtml(outputFolder);
            Directory.Delete(AstcTempFolder, true);
            float astcMb = new FileInfo(astcData).Length / (1024f * 1024f);
            float dxtMb = new FileInfo(Path.Combine(outputFolder, "Build", DataFileName(outputFolder))).Length / (1024f * 1024f);
            Debug.Log($"Cat Fighter: файл данных DXT (ПК) {dxtMb:F1} МБ, ASTC (телефоны) {astcMb:F1} МБ");
        }

        // Пустой файл .nojekyll — чтобы GitHub Pages отдавал все файлы как есть
        File.WriteAllText(Path.Combine(outputFolder, ".nojekyll"), "");
        float mb = totalBytes / (1024f * 1024f);
        Debug.Log($"Cat Fighter: WebGL собран → {Path.GetFullPath(outputFolder)}  ({mb:F1} МБ, скачивает ПК)");
        if (!Application.isBatchMode) EditorUtility.RevealInFinder(outputFolder);
    }

    static bool BuildOnce(string outputFolder, BuildOptions buildOptions, WebGLTextureSubtarget textures, out BuildReport report)
    {
        // Смена формата — Unity перепакует текстуры (первый раз долго, потом из кэша).
        // Без Refresh сборка возьмёт текстуры, импортированные в прошлом формате.
        EditorUserBuildSettings.webGLBuildSubtarget = textures;
        AssetDatabase.Refresh();

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = outputFolder,
            target = BuildTarget.WebGL,
            subtarget = (int)textures, // формат текстур этой сборки (без него Unity 6 берёт Generic = DXT)
            options = buildOptions,
        };
        report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded) return true;

        Debug.LogError($"Cat Fighter: сборка ({textures}) не удалась ({report.summary.result}), ошибок: {report.summary.totalErrors}");
        return false;
    }

    // Имя файла данных зависит от имени папки сборки: Builds/WebGL → WebGL.data.unityweb
    static string DataFileName(string outputFolder) => Path.GetFileName(outputFolder) + ".data.unityweb";

    // Страница выбирает файл данных по видеокарте: есть DXT (s3tc) — берём DXT,
    // иначе есть ASTC — берём ASTC, иначе DXT (Unity распакует сама).
    static void PatchIndexHtml(string outputFolder)
    {
        string path = Path.Combine(outputFolder, "index.html");
        string html = File.ReadAllText(path);
        string dxt = $"buildUrl + \"/{DataFileName(outputFolder)}\"";
        if (!html.Contains("dataUrl: " + dxt))
        {
            Debug.LogWarning("Cat Fighter: не нашёл dataUrl в index.html — ASTC-данные не подключены");
            return;
        }
        const string detect =
            "function catFighterDataUrl(buildUrl) {\n" +
            "        // Выбор набора текстур под видеокарту: DXT (ПК) или ASTC (телефоны)\n" +
            "        // Для проверки можно выбрать вручную: index.html?textures=astc или ?textures=dxt\n" +
            "        var forced = new URLSearchParams(location.search).get(\"textures\");\n" +
            "        if (forced === \"astc\") return buildUrl + \"/{ASTC}\";\n" +
            "        if (forced === \"dxt\") return buildUrl + \"/{DXT}\";\n" +
            "        var gl = document.createElement(\"canvas\").getContext(\"webgl2\");\n" +
            "        if (!gl) return buildUrl + \"/{DXT}\";\n" +
            "        if (gl.getExtension(\"WEBGL_compressed_texture_s3tc\")) return buildUrl + \"/{DXT}\";\n" +
            "        if (gl.getExtension(\"WEBGL_compressed_texture_astc\")) return buildUrl + \"/{ASTC}\";\n" +
            "        return buildUrl + \"/{DXT}\";\n" +
            "      }\n" +
            "      var buildUrl = \"Build\";";
        html = html.Replace("var buildUrl = \"Build\";",
            detect.Replace("{DXT}", DataFileName(outputFolder)).Replace("{ASTC}", AstcDataName));
        html = html.Replace("dataUrl: " + dxt, "dataUrl: catFighterDataUrl(buildUrl)");
        File.WriteAllText(path, html);
    }
}
