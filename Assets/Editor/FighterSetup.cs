using UnityEditor;
using UnityEngine;

// Инструмент редактора: меню Cat Fighter → "Настроить бойцов".
// Для каждого Fighter в сцене берёт кадры из папки Assets/Art/<ИмяОбъекта>
// и сам заполняет клипы, позы блока и приседа — вместо ручного перетаскивания.
//
// Скрипты в папке "Editor" работают только внутри Unity и не попадают в сборку игры.
public static class FighterSetup
{
    // Описание клипа: имя, какие кадры взять из файлов "<кот>_<source>_NN", скорость и кадр попадания
    private struct ClipDef
    {
        public string name, source;
        public int from, to;   // номера кадров, включительно (как в именах файлов: 01..08)
        public float fps;
        public int hitFrame;   // с 1, 0 — нет попадания
        public bool optional;  // если кадров нет — клип просто пропускаем (например, победа ещё не нарисована)

        public ClipDef(string name, string source, int from, int to, float fps, int hitFrame, bool optional = false)
        {
            this.name = name; this.source = source; this.from = from; this.to = to;
            this.fps = fps; this.hitFrame = hitFrame; this.optional = optional;
        }
    }

    private static readonly ClipDef[] Clips =
    {
        new ClipDef("punch_right", "punch", 1, 6, 16, 3),
        new ClipDef("punch_left",  "punch", 7, 8, 12, 1),
        new ClipDef("kick",        "kick",  1, 8, 14, 5),
        new ClipDef("hit",         "hit",   1, 8, 10, 0),
        new ClipDef("walk",        "walk",  1, 6, 10, 0, optional: true),
        new ClipDef("super",       "super", 1, 8, 14, 5, optional: true),
        new ClipDef("win",         "win",   1, 4, 8,  0, optional: true),
    };

    [MenuItem("Cat Fighter/Настроить бойцов")]
    public static void SetupAll()
    {
        Fighter[] fighters = Object.FindObjectsByType<Fighter>();
        if (fighters.Length == 0)
        {
            EditorUtility.DisplayDialog("Cat Fighter", "В сцене нет ни одного Fighter.", "Ок");
            return;
        }

        int ok = 0;
        foreach (Fighter f in fighters)
        {
            if (Setup(f)) ok++;
        }

        // Если бойцов ровно двое — связываем их друг с другом
        if (fighters.Length == 2)
        {
            SetOpponent(fighters[0], fighters[1]);
            SetOpponent(fighters[1], fighters[0]);
        }

        SetupCamera();
        SetupArena();
        SetupRoundManager();

        Debug.Log($"Cat Fighter: настроено бойцов — {ok} из {fighters.Length}. Не забудь Ctrl+S.");
    }

    private static bool Setup(Fighter fighter)
    {
        string folder = $"Assets/Art/{fighter.name}";           // Murzik → Assets/Art/Murzik
        string prefix = fighter.name.ToLowerInvariant();          // → murzik_...

        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"Cat Fighter: нет папки {folder} для объекта '{fighter.name}' — пропускаю.");
            return false;
        }

        // SerializedObject — "правильный" способ менять поля компонента из редакторского кода:
        // работает Undo (Ctrl+Z), сцена помечается изменённой, приватные [SerializeField] тоже доступны.
        var player = new SerializedObject(fighter.GetComponent<SpriteFramePlayer>());
        SerializedProperty clips = player.FindProperty("clips");

        // Какие клипы собирать: обязательные — всегда, необязательные — только если есть первый кадр
        var defs = new System.Collections.Generic.List<ClipDef>();
        foreach (ClipDef d in Clips)
        {
            bool exists = AssetDatabase.LoadMainAssetAtPath($"{folder}/{prefix}_{d.source}_{d.from:00}.png") != null;
            if (!d.optional || exists) defs.Add(d);
        }
        clips.arraySize = defs.Count;

        for (int c = 0; c < defs.Count; c++)
        {
            ClipDef def = defs[c];
            SerializedProperty clip = clips.GetArrayElementAtIndex(c);
            clip.FindPropertyRelative("name").stringValue = def.name;
            clip.FindPropertyRelative("framesPerSecond").floatValue = def.fps;
            clip.FindPropertyRelative("hitFrame").intValue = def.hitFrame;

            SerializedProperty frames = clip.FindPropertyRelative("frames");
            frames.arraySize = def.to - def.from + 1;
            for (int n = def.from; n <= def.to; n++)
            {
                frames.GetArrayElementAtIndex(n - def.from).objectReferenceValue =
                    LoadSprite(folder, $"{prefix}_{def.source}_{n:00}");
            }
        }
        player.ApplyModifiedProperties();

        var so = new SerializedObject(fighter);
        so.FindProperty("blockSprite").objectReferenceValue = LoadSprite(folder, $"{prefix}_block_01");
        so.FindProperty("crouchSprite").objectReferenceValue = LoadSprite(folder, $"{prefix}_crouch_01");
        so.FindProperty("portrait").objectReferenceValue = LoadSprite(UI + "/Portraits", $"{prefix}_portrait_neutral");
        so.FindProperty("portraitHit").objectReferenceValue = LoadSprite(UI + "/Portraits", $"{prefix}_portrait_hit");
        so.FindProperty("portraitFacesRight").boolValue = prefix != "belchik"; // портрет Бельчика отзеркален — смотрит влево
        so.FindProperty("namePlate").objectReferenceValue = LoadSprite(UI, $"name_{prefix}");
        so.FindProperty("hurtVoice").objectReferenceValue = FindAudio($"cat_hurt_{prefix}");
        so.FindProperty("superSound").objectReferenceValue = FindAudio($"super_{prefix}");
        if (SuperNames.TryGetValue(prefix, out string superName))
            so.FindProperty("superName").stringValue = superName;
        so.FindProperty("winsPlate").objectReferenceValue = LoadSpriteAtPath($"{UI}/End/wins_{prefix}.png", false);
        so.ApplyModifiedProperties();

        // Кто управляет котом: тот, что смотрит вправо (стоит слева), — игрок,
        // тот, что смотрит влево (Flip X), — компьютер. Лишний "управляющий" убираем.
        bool isAI = fighter.GetComponent<SpriteRenderer>().flipX;
        if (isAI) EnsureOnly<FighterAI, PlayerController>(fighter.gameObject);
        else      EnsureOnly<PlayerController, FighterAI>(fighter.gameObject);

        // Картинку стойки не трогаем, если там уже какой-то кадр idle (ты мог выбрать idle_04 специально).
        // А вот если там пусто или случайно оказался кадр удара/падения — ставим idle_01.
        var sr = fighter.GetComponent<SpriteRenderer>();
        if (sr.sprite == null || !sr.sprite.name.Contains("_idle_"))
        {
            Undo.RecordObject(sr, "Setup idle sprite");
            sr.sprite = LoadSprite(folder, $"{prefix}_idle_01");
        }

        return true;
    }

    // Оставить на объекте компонент TKeep (добавить, если нет) и убрать TRemove.
    // <TKeep, TRemove> — "обобщённый" метод: типы подставляются при вызове.
    private static void EnsureOnly<TKeep, TRemove>(GameObject go)
        where TKeep : Component where TRemove : Component
    {
        foreach (TRemove extra in go.GetComponents<TRemove>())
            Undo.DestroyObjectImmediate(extra);
        if (go.GetComponent<TKeep>() == null)
            Undo.AddComponent<TKeep>(go);
    }

    private static void SetOpponent(Fighter who, Fighter opponent)
    {
        var so = new SerializedObject(who);
        so.FindProperty("opponent").objectReferenceValue = opponent;
        so.ApplyModifiedProperties();
    }

    private const string UI = "Assets/Art/UI";

    // Названия суперударов — крупно на экране в момент приёма
    private static readonly System.Collections.Generic.Dictionary<string, string> SuperNames = new()
    {
        { "murzik", "УШИРО С РАЗВОРОТА!" },
        { "belchik", "БЕЛЫЙ ВИХРЬ!" },
    };

    // Камера: ортографическая (без перспективы — как в 2D-игре) и так, чтобы пол (y = 0)
    // был примерно на 22% от низа экрана, а коты занимали почти половину высоты — как на заставке.
    private static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Undo.RecordObject(cam, "Setup camera");
        Undo.RecordObject(cam.transform, "Setup camera");
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        cam.transform.position = new Vector3(0f, 2.5f, -10f);
        cam.transform.rotation = Quaternion.identity;
    }

    // Фон-комната позади котов
    private static void SetupArena()
    {
        var bg = Object.FindAnyObjectByType<ArenaBackground>();
        if (bg == null)
        {
            var go = new GameObject("ArenaBackground", typeof(SpriteRenderer), typeof(ArenaBackground));
            Undo.RegisterCreatedObjectUndo(go, "Create arena");
            bg = go.GetComponent<ArenaBackground>();
        }
        var sr = bg.GetComponent<SpriteRenderer>();
        Undo.RecordObject(sr, "Setup arena");
        sr.sprite = LoadSpriteAtPath($"{UI}/Arena/arena_background.jpg");
    }

    // Судья боя + интерфейс — один объект на сцену
    private static void SetupRoundManager()
    {
        var rm = Object.FindAnyObjectByType<RoundManager>();
        GameObject go;
        if (rm == null)
        {
            go = new GameObject("RoundManager", typeof(BattleHUD), typeof(RoundManager));
            Undo.RegisterCreatedObjectUndo(go, "Create RoundManager");
        }
        else
        {
            go = rm.gameObject;
            if (go.GetComponent<BattleHUD>() == null) Undo.AddComponent<BattleHUD>(go);
        }

        var hud = new SerializedObject(go.GetComponent<BattleHUD>());
        void Set(string field, string file) => hud.FindProperty(field).objectReferenceValue = LoadSprite(UI, file);
        Set("portraitFrame", "portrait_frame");
        Set("portraitMask", "portrait_mask");
        Set("healthbarFrame", "healthbar_frame");
        Set("healthbarFill", "healthbar_fill");
        Set("timerSplash", "timer_splash");
        Set("pawEmpty", "paw_empty");
        Set("pawFilled", "paw_filled");
        Set("fightLogo", "fight_logo");
        Set("koLogo", "ko_logo");
        hud.FindProperty("endWinTitle").objectReferenceValue = LoadSpriteAtPath($"{UI}/End/title_win.png", false);
        hud.FindProperty("endLoseTitle").objectReferenceValue = LoadSpriteAtPath($"{UI}/End/title_lose.png", false);
        hud.FindProperty("endButtonSprite").objectReferenceValue = LoadSpriteAtPath($"{UI}/End/button_retry.png", false);

        // Баннеры раундов — необязательные: какие есть, те и подключаем
        var banners = hud.FindProperty("roundBanners");
        banners.arraySize = 0;
        for (int i = 1; i <= 3; i++)
        {
            Sprite s = LoadSpriteAtPath($"{UI}/round_{i}.png", warnIfMissing: false);
            if (s == null) break;
            banners.arraySize++;
            banners.GetArrayElementAtIndex(i - 1).objectReferenceValue = s;
        }
        hud.FindProperty("finalRoundBanner").objectReferenceValue =
            LoadSpriteAtPath($"{UI}/final_round.png", warnIfMissing: false);
        hud.ApplyModifiedProperties();

        SetupSound(go);

        // Экранные кнопки: компонент заранее, чтобы в Inspector была галочка Always Show (проверка мышкой)
        if (go.GetComponent<TouchControls>() == null) Undo.AddComponent<TouchControls>(go);
    }

    // Звук: раскладываем файлы из Assets/Audio по сигналам — по началу имени файла.
    // hit_light_01.wav, hit_light_02.wav → сигнал HitLight; music.mp3 → фоновая музыка.
    private static readonly (SoundManager.Cue cue, string prefix)[] SoundPrefixes =
    {
        (SoundManager.Cue.Whoosh, "whoosh"), (SoundManager.Cue.HitLight, "hit_light"),
        (SoundManager.Cue.HitHeavy, "hit_heavy"), (SoundManager.Cue.Block, "block"),
        (SoundManager.Cue.Dodge, "dodge"), (SoundManager.Cue.CatHurt, "cat_hurt"),
        (SoundManager.Cue.KO, "ko"), (SoundManager.Cue.Round, "round"),
        (SoundManager.Cue.Fight, "fight"), (SoundManager.Cue.Win, "win"), (SoundManager.Cue.Lose, "lose"),
        (SoundManager.Cue.Super, "super"),
    };

    // Найти звук в Assets/Audio по имени файла (без расширения — подойдёт .wav, .mp3, .ogg)
    private static AudioClip FindAudio(string fileName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Audio")) return null;
        foreach (string guid in AssetDatabase.FindAssets($"{fileName} t:AudioClip", new[] { "Assets/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant() == fileName)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    private static void SetupSound(GameObject go)
    {
        var manager = go.GetComponent<SoundManager>();
        if (manager == null) manager = Undo.AddComponent<SoundManager>(go);

        // Все звуки из папки Assets/Audio (и подпапок)
        var all = new System.Collections.Generic.List<(string name, AudioClip clip)>();
        if (AssetDatabase.IsValidFolder("Assets/Audio"))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                all.Add((System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant(),
                         AssetDatabase.LoadAssetAtPath<AudioClip>(path)));
            }
        }

        var so = new SerializedObject(manager);
        AudioClip music = all.Find(a => a.name.StartsWith("music")).clip;
        so.FindProperty("music").objectReferenceValue = music;

        SerializedProperty cues = so.FindProperty("cues");
        cues.arraySize = SoundPrefixes.Length;
        int found = 0;
        for (int i = 0; i < SoundPrefixes.Length; i++)
        {
            var (cue, prefix) = SoundPrefixes[i];
            // Личные звуки котов (super_murzik, cat_hurt_belchik…) — не в общий сигнал, они подключаются к самим котам
            var clips = all.FindAll(a => a.name.StartsWith(prefix) &&
                                         !a.name.EndsWith("_murzik") && !a.name.EndsWith("_belchik"));
            SerializedProperty entry = cues.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("cue").enumValueIndex = (int)cue;
            SerializedProperty arr = entry.FindPropertyRelative("clips");
            arr.arraySize = clips.Count;
            for (int c = 0; c < clips.Count; c++) arr.GetArrayElementAtIndex(c).objectReferenceValue = clips[c].clip;
            SerializedProperty volume = entry.FindPropertyRelative("volume");
            if (volume.floatValue <= 0f) volume.floatValue = 1f; // новый элемент списка приходит с нулём
            found += clips.Count;
        }
        so.ApplyModifiedProperties();
        Debug.Log($"Cat Fighter: звуков найдено — {found}, музыка: {(music != null ? music.name : "нет")}");
    }

    private static Sprite LoadSprite(string folder, string fileName) =>
        LoadSpriteAtPath($"{folder}/{fileName}.png");

    // Загружает картинку как спрайт. Если она ещё не настроена как Sprite — настраивает.
    private static Sprite LoadSpriteAtPath(string path, bool warnIfMissing = true)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
        {
            if (warnIfMissing) Debug.LogWarning($"Cat Fighter: нет файла {path}");
            return null;
        }

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ArtImportSettings.Apply(importer))
            importer.SaveAndReimport(); // сохранить новые настройки и переимпортировать картинку

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
