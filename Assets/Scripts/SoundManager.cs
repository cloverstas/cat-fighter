using System;
using UnityEngine;

// Звук боя: фоновая музыка + короткие эффекты (удары, взмахи, блок, нокаут, раунды).
//
// Эффекты называются "сигналами" (Cue): где-то в игре происходит событие — звучит сигнал.
// На каждый сигнал можно дать несколько вариантов звука: играем случайный и чуть меняем высоту тона,
// чтобы серия одинаковых ударов не звучала как "пулемёт".
//
// Клипы заполняет кнопка Cat Fighter → Настроить бойцов из папки Assets/Audio (по началу имени файла).
public class SoundManager : MonoBehaviour
{
    public enum Cue { Whoosh, HitLight, HitHeavy, Block, Dodge, CatHurt, KO, Round, Fight, Win, Lose, Super }

    [Serializable]
    public class CueClips
    {
        public Cue cue;
        public AudioClip[] clips;           // варианты — берём случайный
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("Музыка")]
    [SerializeField] private AudioClip music;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.35f;

    [Header("Эффекты")]
    [SerializeField] private CueClips[] cues;
    [Range(0f, 0.3f)] [SerializeField] private float pitchVariation = 0.08f; // ±8% высоты тона
    [Range(0f, 1f)] [SerializeField] private float catHurtChance = 0.5f;     // кот вякает не на каждый удар

    private AudioSource musicSource;
    private AudioSource[] sfxSources;   // несколько "динамиков" — чтобы звуки не обрывали друг друга
    private int nextSource;

    void Awake()
    {
        // AudioSource — "динамик" на объекте. Музыке — свой, эффектам — небольшой пул из 6 штук.
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = musicVolume;

        sfxSources = new AudioSource[6];
        for (int i = 0; i < sfxSources.Length; i++)
            sfxSources[i] = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        if (music != null)
        {
            musicSource.clip = music;
            musicSource.Play();
        }

        // Подписываемся на события всех котов — сами коты про звук ничего не знают
        foreach (Fighter f in FindObjectsByType<Fighter>())
        {
            f.AttackStarted += _ => Play(Cue.Whoosh);
            f.Blocked += _ => Play(Cue.Block);
            f.Dodged += _ => Play(Cue.Dodge);
            f.Hurt += OnHurt;
            f.SuperStarted += _ => Play(Cue.Super);
        }
    }

    void OnHurt(Fighter f)
    {
        Play(f.LastHitWasHeavy ? Cue.HitHeavy : Cue.HitLight);

        // Голос: от тяжёлого удара кот вскрикивает всегда, от лёгкого — через раз
        if (!f.LastHitWasHeavy && UnityEngine.Random.value > catHurtChance) return;
        if (f.HurtVoice != null) PlayClip(f.HurtVoice, 1f); // у кота свой голос
        else Play(Cue.CatHurt);                               // иначе — общий звук
    }

    // Сыграть сигнал: случайный вариант, случайная высота тона, свободный "динамик"
    public void Play(Cue cue)
    {
        CueClips entry = cues == null ? null : Array.Find(cues, c => c.cue == cue);
        if (entry == null || entry.clips == null || entry.clips.Length == 0) return; // звука нет — просто тишина

        AudioClip clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
        if (clip == null) return;
        PlayClip(clip, entry.volume);
    }

    // Сыграть конкретный звук на свободном "динамике" со случайной высотой тона
    void PlayClip(AudioClip clip, float volume)
    {
        AudioSource source = sfxSources[nextSource];
        nextSource = (nextSource + 1) % sfxSources.Length; // по кругу: 0,1,2,3,4,5,0,1...
        source.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, volume);
    }

    public void StopMusic() => musicSource.Stop();
}
