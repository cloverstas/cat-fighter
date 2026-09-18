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
    public static void Build()
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
            locationPathName = OutputFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            // Пустой файл .nojekyll — чтобы GitHub Pages отдавал все файлы как есть
            File.WriteAllText(Path.Combine(OutputFolder, ".nojekyll"), "");
            float mb = report.summary.totalSize / (1024f * 1024f);
            Debug.Log($"Cat Fighter: WebGL собран → {Path.GetFullPath(OutputFolder)}  ({mb:F1} МБ)");
            EditorUtility.RevealInFinder(OutputFolder);
        }
        else
        {
            Debug.LogError($"Cat Fighter: сборка не удалась ({report.summary.result}), ошибок: {report.summary.totalErrors}");
        }
    }
}
