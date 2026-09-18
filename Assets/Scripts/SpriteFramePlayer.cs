using System.Collections;
using UnityEngine;

// Простой проигрыватель покадровой анимации — замена окну Animation.
// В Inspector заводишь клипы (имя + список картинок + скорость), а из кода вызываешь Play("punch").
// Когда клип доиграл, кот возвращается к картинке стойки.
[RequireComponent(typeof(SpriteRenderer))] // без SpriteRenderer скрипт не повесится — нечего будет рисовать
public class SpriteFramePlayer : MonoBehaviour
{
    // [System.Serializable] — чтобы Unity показала этот класс в Inspector как раскрывающийся блок
    [System.Serializable]
    public class Clip
    {
        public string name;               // имя, по которому зовём клип из кода: "punch", "kick"...
        public Sprite[] frames;           // кадры по порядку
        public float framesPerSecond = 14;
        [Tooltip("Номер кадра (с 1), на котором удар попадает. 0 — у клипа нет попадания")]
        public int hitFrame;
    }

    [SerializeField] private Clip[] clips;

    // Событие "дошли до кадра попадания". На него подписывается Fighter.
    // Проигрыватель не знает ничего про урон — он только сообщает: "в клипе такой-то сейчас момент удара".
    public event System.Action<string> HitFrame;

    private SpriteRenderer spriteRenderer;
    private Sprite idleSprite;            // картинка стойки — то, что стоит в Sprite Renderer на старте
    private Coroutine current;            // клип, который играет сейчас (чтобы можно было прервать)

    // Играет ли сейчас какой-нибудь клип. Fighter спрашивает это, чтобы не бить посреди удара.
    public bool IsPlaying => current != null;

    // Имя клипа, который играет сейчас (null — ничего не играет). ИИ смотрит сюда, чтобы понять, что делает противник.
    public string CurrentClip { get; private set; }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        idleSprite = spriteRenderer.sprite;
    }

    // holdLastFrame = true — после конца клипа НЕ возвращаться в стойку, а замереть на последнем кадре
    // (например, кот упал в нокаут и лежит)
    public void Play(string clipName, bool holdLastFrame = false)
    {
        Clip clip = System.Array.Find(clips, c => c.name == clipName);
        if (clip == null)
        {
            Debug.LogWarning($"Нет клипа '{clipName}' на {name}");
            return;
        }

        if (current != null) StopCoroutine(current); // новый клип прерывает старый (например, удар прервали попаданием)
        CurrentClip = clip.name;
        current = StartCoroutine(PlayRoutine(clip, holdLastFrame));
    }

    public bool HasClip(string clipName) =>
        clips != null && System.Array.Exists(clips, c => c.name == clipName);

    // Собрать новый клип из кадров существующего — прямо в коде, без Inspector.
    // Например: из "hit" взять кадры 0, 1, 1, 0 и назвать это "hit_light".
    public void AddClipFromFrames(string sourceClip, string newName, int[] frameIndices, float fps)
    {
        Clip source = clips == null ? null : System.Array.Find(clips, c => c.name == sourceClip);
        if (source == null)
        {
            Debug.LogWarning($"Не из чего собрать '{newName}': нет клипа '{sourceClip}' на {name}");
            return;
        }

        var frames = new System.Collections.Generic.List<Sprite>();
        foreach (int i in frameIndices)
        {
            if (i >= 0 && i < source.frames.Length) frames.Add(source.frames[i]);
        }

        var clip = new Clip { name = newName, frames = frames.ToArray(), framesPerSecond = fps };

        // Массив нельзя "удлинить" — создаём новый на 1 больше и копируем старые клипы
        var list = new System.Collections.Generic.List<Clip>(clips) { clip };
        clips = list.ToArray();
    }

    // Показать одну картинку и держать её, пока не вызовут Release (например, поза блока)
    public void Hold(Sprite sprite)
    {
        if (current != null) StopCoroutine(current);
        current = null;
        CurrentClip = null;
        spriteRenderer.sprite = sprite;
    }

    // Прервать всё и встать в стойку
    public void Stop()
    {
        if (current != null) StopCoroutine(current);
        current = null;
        CurrentClip = null;
        spriteRenderer.sprite = idleSprite;
    }

    // Вернуться к стойке
    public void Release()
    {
        spriteRenderer.sprite = idleSprite;
    }

    // Корутина — метод, который умеет "ставиться на паузу" через yield и продолжаться в следующих кадрах игры.
    // Идеально для анимации: показали кадр → подождали → показали следующий.
    private IEnumerator PlayRoutine(Clip clip, bool holdLastFrame)
    {
        // Защита от нуля: 1 / 0 = бесконечность, и корутина "уснула" бы навсегда
        float fps = clip.framesPerSecond > 0 ? clip.framesPerSecond : 12f;
        if (clip.framesPerSecond <= 0)
            Debug.LogWarning($"У клипа '{clip.name}' Frames Per Second = 0, играю на 12");
        float delay = 1f / fps;
        for (int i = 0; i < clip.frames.Length; i++)
        {
            spriteRenderer.sprite = clip.frames[i];

            // i считается с 0, а hitFrame в Inspector — с 1, поэтому i + 1
            // "?." — вызвать, только если кто-то подписан (иначе была бы ошибка)
            if (i + 1 == clip.hitFrame) HitFrame?.Invoke(clip.name);

            yield return new WaitForSeconds(delay);
        }
        if (!holdLastFrame) spriteRenderer.sprite = idleSprite;
        current = null;
        CurrentClip = null;
    }
}
