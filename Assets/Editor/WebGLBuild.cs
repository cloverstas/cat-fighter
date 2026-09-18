using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Меню Cat Fighter → "Собрать WebGL": игра превращается в веб-страницу,
// которую можно открыть в браузере на ПК и телефоне (GitHub Pages, свой сервер, itch.io).
public static class WebGLBuild
{
    public const string OutputFolder = "Builds/WebGL"; // папка Builds не хранится в git (.gitignore)

    [MenuItem("Cat Fighter/Собрать WebGL")]
    public static void Build() => Build(OutputFolder, BuildOptions.None);

    // Отладочная сборка: в ошибках браузера видны настоящие имена функций, а не wasm-function[1518]
    public static void BuildDev() => Build("Builds/WebGLDev", BuildOptions.Development);

    static void Build(string outputFolder, BuildOptions buildOptions)
    {
        // Сжатие Gzip + Decompression Fallback: браузер сам распакует файлы.
        // Так игра работает на любом хостинге, даже без настройки сервера (например, GitHub Pages).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;               // повторный заход — быстрее, файлы в кэше браузера
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = outputFolder,
            target = BuildTarget.WebGL,
            options = buildOptions,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            // Пустой файл .nojekyll — чтобы GitHub Pages отдавал все файлы как есть
            File.WriteAllText(Path.Combine(outputFolder, ".nojekyll"), "");
            float mb = report.summary.totalSize / (1024f * 1024f);
            Debug.Log($"Cat Fighter: WebGL собран → {Path.GetFullPath(outputFolder)}  ({mb:F1} МБ)");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(outputFolder);
        }
        else
        {
            Debug.LogError($"Cat Fighter: сборка не удалась ({report.summary.result}), ошибок: {report.summary.totalErrors}");
        }
    }
}
