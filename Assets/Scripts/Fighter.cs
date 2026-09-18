using UnityEngine;

// Боец — "тело" кота: здоровье, движение, атаки, стойки, получение урона.
// Сам клавиатуру НЕ читает. Командует им кто-то снаружи:
//   PlayerController — игрок с клавиатуры,
//   FighterAI        — компьютер.
// Оба вызывают одни и те же методы: Move, Attack, HoldStance.
[RequireComponent(typeof(SpriteFramePlayer))]
public class Fighter : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;

    [Header("Движение")] // Header рисует подзаголовок в Inspector — для порядка
    [SerializeField] private float moveSpeed = 3f;       // единиц в секунду
    [SerializeField] private float hopHeight = 0.08f;    // высота подскока на каждом шаге
    [SerializeField] private float stepsPerSecond = 4f;  // сколько подшагов в секунду
    [SerializeField] private float minX = -7f;           // границы арены, чтобы не уйти за экран
    [SerializeField] private float maxX = 7f;

    [Header("Бой")]
    [SerializeField] private Fighter opponent;           // противник — перетащи сюда другого кота
    [SerializeField] private int punchDamage = 8;
    [SerializeField] private int kickDamage = 15;
    [SerializeField] private float punchReach = 3.2f;    // дальность удара лапой (от центра кота)
    [SerializeField] private float kickReach = 3.7f;     // нога длиннее лапы
    [SerializeField] private float minDistance = 2.4f;   // ближе не подойти — коты не проходят сквозь друг друга
    [SerializeField] private bool punchCanBeDucked = false; // можно ли пригнуться от моего удара лапой
    [SerializeField] private bool kickCanBeDucked = true;   // ...и от удара ногой
    [SerializeField] private bool punchKnocksDown = false;  // сбивает ли с ног удар лапой (нет — противник только морщится)
    [SerializeField] private bool kickKnocksDown = true;    // ...и удар ногой
    [SerializeField] private float lightHitPushback = 0.15f; // лёгкий удар чуть отталкивает назад

    [Header("Блок")]
    [SerializeField] private Sprite blockSprite;                  // поза блока — block_01
    [Range(0f, 1f)]                                                // Range рисует в Inspector ползунок от 0 до 1
    [SerializeField] private float blockDamageMultiplier = 0.2f;  // какая доля урона проходит сквозь блок
    [SerializeField] private float blockPushback = 0.3f;          // насколько отъезжаем назад от удара по блоку

    [Header("Присед")]
    [SerializeField] private Sprite crouchSprite;                 // поза приседа — crouch_01

    [Header("Суперудар")]
    [SerializeField] private string superName = "СУПЕРУДАР!";   // название приёма — крупно на экране
    [SerializeField] private int superDamage = 30;
    [SerializeField] private float superReach = 4.3f;           // бьёт дальше обычного — с рывком
    [SerializeField] private float superLunge = 1.5f;           // насколько рывок несёт вперёд
    [SerializeField] private float meterMax = 100f;
    [SerializeField] private float meterPerDamageDealt = 1.2f;  // шкала растёт, когда бьёшь...
    [SerializeField] private float meterPerDamageTaken = 0.8f;  // ...и когда получаешь (шанс отыграться)

    [Header("Интерфейс")]
    [SerializeField] private Sprite portrait;       // портрет в рамке HUD
    [SerializeField] private Sprite portraitHit;    // портрет "получил удар" — показывается на миг
    [SerializeField] private Sprite namePlate;      // табличка с именем
    [SerializeField] private Sprite winsPlate;      // надпись "<ИМЯ> ПОБЕЖДАЕТ!" для экрана итогов

    [Header("Звук")]
    [SerializeField] private AudioClip hurtVoice;   // свой голос кота, когда ему больно

    // enum — свой тип с фиксированным набором вариантов. Кот всегда ровно в одной стойке.
    // public — потому что стойку теперь выбирают снаружи (игрок или ИИ).
    public enum Stance { Stand, Block, Crouch }

    // "Карточка удара": всё, что нужно знать тому, по кому бьют.
    // struct — маленький набор данных, упакованных вместе под одним именем.
    public struct HitInfo
    {
        public int damage;
        public bool canBeDucked; // можно ли пригнуться
        public bool knockdown;   // сбивает ли с ног
        public bool unblockable; // пробивает блок (суперудар)
    }

    private int health;
    private Stance stance = Stance.Stand;
    private Stance wantedStance = Stance.Stand; // какую стойку просит тот, кто управляет
    private float moveInput;                    // куда просят идти: -1, 0, +1
    private SpriteFramePlayer anim;
    private SpriteRenderer spriteRenderer;
    private float groundY;     // высота "пола" — запоминаем на старте
    private float stepPhase;   // где мы сейчас в цикле подскока
    private bool frozen;       // раунд окончен — команды больше не принимаем
    private float meter;       // шкала суперудара
    private bool superActive;  // сейчас идёт суперудар
    private float lungeLeft;   // сколько ещё пролететь рывком

    // ---------- Сведения для тех, кто управляет (особенно для ИИ) ----------
    // "=>" — короткая запись свойства "только для чтения"
    public Fighter Opponent => opponent;
    public float PunchReach => punchReach;
    public float KickReach => kickReach;
    public int Health => health;
    public int MaxHealth => maxHealth;
    public Sprite Portrait => portrait;
    public Sprite PortraitHit => portraitHit;
    public Sprite NamePlate => namePlate;
    public Sprite WinsPlate => winsPlate;
    public AudioClip HurtVoice => hurtVoice;
    public bool IsBusy => anim.IsPlaying;                 // бьёт или получает — команды не принимает
    public string CurrentAction => anim.CurrentClip;      // "kick", "punch_left", "hit"... или null
    public bool IsAttacking => CurrentAction == "kick" || CurrentAction == "super" ||
                               (CurrentAction != null && CurrentAction.StartsWith("punch"));
    public int AttackCount { get; private set; }          // сколько атак начато — ИИ по нему замечает новую атаку
    public bool IsKO => health <= 0;                       // нокаут
    public float SuperMeter01 => meter / meterMax;         // заполненность шкалы 0..1
    public bool SuperReady => meter >= meterMax;
    public float SuperReach => superReach;
    public string SuperName => superName;
    // Пока нет своих кадров суперудара — бьём обычным пинком, но со всеми эффектами и силой супера
    private string SuperClip => anim.HasClip("super") ? "super" : "kick";
    public bool IsFrozen => frozen;

    // Событие "меня нокаутировали". На него подписан RoundManager — он и решает, что бой окончен.
    public event System.Action<Fighter> KnockedOut;

    // Событие "мне больно" (пропустил удар). HUD по нему меняет портрет на морщащийся.
    public event System.Action<Fighter> Hurt;

    // События для звука (и чего угодно ещё): начал атаку, принял удар в блок, увернулся
    public event System.Action<Fighter> AttackStarted;
    public event System.Action<Fighter> Blocked;
    public event System.Action<Fighter> Dodged;

    public bool LastHitWasHeavy { get; private set; } // последний пропущенный удар — тяжёлый (сбивает с ног)?

    // Суперудар: начался (для кино-эффектов и звука) и попал (атакующий, по кому)
    public event System.Action<Fighter> SuperStarted;
    public event System.Action<Fighter, Fighter> SuperLanded;

    // Куда смотрит кот: +1 вправо, -1 влево. Все кадры нарисованы лицом вправо,
    // поэтому если стоит галочка Flip X — кот смотрит влево.
    public int Facing => spriteRenderer.flipX ? -1 : 1;

    // Awake вызывается раньше всех Start — к началу раунда боец уже готов
    void Awake()
    {
        health = maxHealth;
        anim = GetComponent<SpriteFramePlayer>(); // берём проигрыватель, висящий на этом же объекте
        spriteRenderer = GetComponent<SpriteRenderer>();
        groundY = transform.position.y;

        // Лёгкая реакция на удар: если такого клипа не завели в Inspector — собираем сами
        // из кадров "hit": 01 (получил) → 02 (морщится) ×2, чтобы задержался → 01.
        if (!anim.HasClip("hit_light"))
            anim.AddClipFromFrames("hit", "hit_light", new[] { 0, 1, 1, 0 }, 10f);

        // Нокаут: первая половина "hit" — удар, отлёт, падение, лежит (кадры 01–05). Подъёма нет.
        if (!anim.HasClip("ko"))
            anim.AddClipFromFrames("hit", "ko", new[] { 0, 1, 2, 3, 4 }, 10f);

        // Подписка на событие: "когда проигрыватель дойдёт до кадра попадания — вызови мой OnHitFrame"
        anim.HitFrame += OnHitFrame;
    }

    void OnDestroy()
    {
        // Хорошая привычка: отписываться, когда объект уничтожается
        if (anim != null) anim.HitFrame -= OnHitFrame;
    }

    // ---------- Команды ----------

    // Идти: -1 влево, +1 вправо, 0 стоять. Держится, пока не придёт новая команда.
    public void Move(float direction)
    {
        if (frozen) return;
        moveInput = Mathf.Clamp(direction, -1f, 1f);
    }

    // Встать в стойку (блок / присед) или вернуться в обычную
    public void HoldStance(Stance newStance)
    {
        if (frozen) return;
        wantedStance = newStance;
    }

    // Ударить. Возвращает false, если сейчас нельзя (уже бьём, падаем, в блоке...)
    public bool Attack(string clipName)
    {
        if (frozen || anim.IsPlaying || stance != Stance.Stand) return false;
        anim.Play(clipName);
        AttackCount++;
        AttackStarted?.Invoke(this);
        return true;
    }

    // Суперудар: только при полной шкале. Шкала обнуляется, кот делает рывок и бьёт.
    public bool TrySuper()
    {
        if (!SuperReady || frozen || anim.IsPlaying || stance != Stance.Stand) return false;
        meter = 0f;
        superActive = true;
        lungeLeft = superLunge;
        anim.Play(SuperClip);
        AttackCount++;
        AttackStarted?.Invoke(this);
        SuperStarted?.Invoke(this);
        return true;
    }

    // Раунд окончен: стоп, никаких команд. Победитель доигрывает удар и встаёт в стойку.
    public void Freeze()
    {
        frozen = true;
        moveInput = 0f;
        wantedStance = Stance.Stand;
    }

    // Снова в бой (после паузы между раундами)
    public void Unfreeze()
    {
        frozen = false;
    }

    // Подготовка к новому раунду: полное здоровье, на стартовую позицию, в стойку, замороженным до "FIGHT!"
    public void ResetForRound(float startX)
    {
        health = maxHealth;
        Freeze();
        stance = Stance.Stand;
        superActive = false;
        lungeLeft = 0f;
        anim.Stop();                       // прервать любую анимацию (например, лежит в нокауте) и встать
        transform.position = new Vector3(startX, groundY, transform.position.z);
    }

    // Победная поза: проиграть "win" (если такой клип есть) и замереть в последней позе
    public void Celebrate()
    {
        if (IsKO || !anim.HasClip("win")) return;
        anim.Play("win", holdLastFrame: true);
    }

    // ---------- Каждый кадр ----------

    void Update()
    {
        // Кто бьёт — тот рисуется поверх противника, чтобы лапа/нога ложилась НА него, а не пряталась за ним.
        // sortingOrder: чем больше число, тем "ближе к зрителю".
        spriteRenderer.sortingOrder = IsAttacking ? 1 : 0;

        // Суперудар закончился — снимаем флаг; пока идёт — рывок вперёд к противнику
        if (superActive && !anim.IsPlaying) superActive = false;
        if (superActive && lungeLeft > 0f) Lunge();

        if (frozen)
        {
            // Лежачего не трогаем. Победитель — как доиграет удар, выходит из блока/приседа в стойку.
            if (!IsKO && !anim.IsPlaying) SetStance(Stance.Stand);
            anim.StopLoop(); // перестать шагать
            Land();
            return;
        }

        // Пока идёт удар или падение — стоим на полу и ничего не делаем
        if (anim.IsPlaying)
        {
            Land();
            return;
        }

        SetStance(wantedStance);

        // В блоке и в приседе не ходим
        if (stance != Stance.Stand)
        {
            Land();
            return;
        }

        ApplyMovement();
    }

    void SetStance(Stance newStance)
    {
        if (newStance == stance) return; // ничего не поменялось — ничего не делаем
        stance = newStance;

        // switch — выбор по вариантам; удобнее цепочки if/else, когда сравниваем одно значение
        switch (stance)
        {
            case Stance.Block:
                if (blockSprite != null) anim.Hold(blockSprite);
                break;
            case Stance.Crouch:
                if (crouchSprite != null) anim.Hold(crouchSprite);
                break;
            default: // Stance.Stand
                anim.Release();
                break;
        }
    }

    void ApplyMovement()
    {
        if (moveInput == 0f)
        {
            anim.StopLoop(); // остановились — из ходьбы в стойку
            Land();
            return;
        }

        // Есть кадры ходьбы — шагаем ими (назад — те же кадры задом наперёд), без подскока из кода.
        // Нет кадров — старый способ: стойка + подскок.
        bool hasWalk = anim.HasClip("walk");
        if (hasWalk)
        {
            bool backward = moveInput * Facing < 0; // идём против направления взгляда
            anim.PlayLoop("walk", backward);
        }

        Vector3 pos = transform.position;

        // Time.deltaTime — сколько секунд прошло с прошлого кадра.
        // Умножаем на него, чтобы скорость не зависела от FPS: на 30 и на 144 кадрах кот идёт одинаково.
        pos.x += moveInput * moveSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, minX, maxX); // Clamp держит число в границах [minX, maxX]

        // Не подходим к противнику ближе minDistance
        if (opponent != null)
        {
            float oppX = opponent.transform.position.x;
            if (Facing > 0) pos.x = Mathf.Min(pos.x, oppX - minDistance); // противник справа
            else            pos.x = Mathf.Max(pos.x, oppX + minDistance); // противник слева
        }

        // Подскок: |sin| даёт "горбики" 0 → 1 → 0 → 1..., как шаги
        stepPhase += Time.deltaTime * stepsPerSecond;
        pos.y = hasWalk ? groundY : groundY + Mathf.Abs(Mathf.Sin(stepPhase * Mathf.PI)) * hopHeight;

        transform.position = pos;
    }

    // Рывок суперудара: быстро вперёд, но не сквозь противника
    void Lunge()
    {
        float step = Mathf.Min(lungeLeft, 12f * Time.deltaTime);
        lungeLeft -= step;
        Vector3 pos = transform.position;
        pos.x += Facing * step;
        if (opponent != null)
        {
            float oppX = opponent.transform.position.x;
            if (Facing > 0) pos.x = Mathf.Min(pos.x, oppX - minDistance);
            else            pos.x = Mathf.Max(pos.x, oppX + minDistance);
        }
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        transform.position = pos;
    }

    // Вернуть кота на пол и сбросить цикл шагов
    void Land()
    {
        stepPhase = 0f;
        Vector3 pos = transform.position;
        pos.y = groundY;
        transform.position = pos;
    }

    // ---------- Удары ----------

    // Вызывается проигрывателем в момент удара (кадр hitFrame клипа)
    void OnHitFrame(string clipName)
    {
        if (opponent == null) return;

        bool isKick = clipName == "kick";
        float reach = superActive ? superReach : isKick ? kickReach : punchReach;

        // Заполняем карточку удара. Суперудар: мощный, сбивает с ног, не блокируется и не уворачивается.
        HitInfo hit = superActive
            ? new HitInfo { damage = superDamage, knockdown = true, unblockable = true }
            : new HitInfo
            {
                damage = isKick ? kickDamage : punchDamage,
                canBeDucked = isKick ? kickCanBeDucked : punchCanBeDucked,
                knockdown = isKick ? kickKnocksDown : punchKnocksDown,
            };

        // dx — расстояние до противника со знаком. Умножаем на Facing:
        // если результат положительный — противник впереди, отрицательный — за спиной.
        float dx = (opponent.transform.position.x - transform.position.x) * Facing;

        if (dx > 0 && dx <= reach)
        {
            int dealt = opponent.TakeHit(hit);
            if (superActive)
            {
                if (dealt > 0) SuperLanded?.Invoke(this, opponent);
            }
            else
            {
                AddMeter(dealt * meterPerDamageDealt); // попал — шкала растёт
            }
        }
    }

    // Получить удар. Возвращает, сколько урона реально прошло (0 — промах/уворот).
    public int TakeHit(HitInfo hit)
    {
        if (IsKO || frozen) return 0; // лежачего не бьют, и после конца раунда удары не считаются

        if (stance == Stance.Crouch && hit.canBeDucked)
        {
            // Пригнулся — удар прошёл над головой, урона нет
            Debug.Log($"{name}: увернулся!");
            Dodged?.Invoke(this);
            return 0;
        }

        bool blocked = stance == Stance.Block && !hit.unblockable;
        int damage = blocked
            ? Mathf.Max(1, Mathf.RoundToInt(hit.damage * blockDamageMultiplier)) // в блок — малая доля, но хотя бы 1
            : hit.damage;

        // Mathf.Max не даёт здоровью уйти ниже нуля
        health = Mathf.Max(health - damage, 0);
        Debug.Log($"{name}: {(blocked ? "блок! " : "")}здоровье {health}");
        AddMeter(damage * meterPerDamageTaken); // получил — тоже копишь на ответку

        if (blocked)
        {
            Blocked?.Invoke(this);
        }
        else
        {
            LastHitWasHeavy = hit.knockdown || IsKO;
            Hurt?.Invoke(this);
        }

        if (IsKO)
        {
            // Нокаут! Падаем и остаёмся лежать (holdLastFrame), сообщаем всем желающим
            SetStance(Stance.Stand);
            Freeze(); // лежащий кот больше не слушает команд
            anim.Play("ko", holdLastFrame: true);
            KnockedOut?.Invoke(this);
            return damage;
        }

        if (blocked)
        {
            PushBack(blockPushback); // не падаем — только отъезжаем назад
            return damage;
        }

        // Пропущенный удар выбивает из любой стойки. Сбрасываем стойку заранее,
        // иначе после реакции кот "думал" бы, что всё ещё сидит в приседе.
        SetStance(Stance.Stand);

        if (hit.knockdown)
        {
            anim.Play("hit");        // тяжёлый удар: падение и подъём
        }
        else
        {
            anim.Play("hit_light");  // лёгкий: поморщился и снова в стойке
            PushBack(lightHitPushback);
        }
        return damage;
    }

    void AddMeter(float amount)
    {
        meter = Mathf.Min(meter + amount, meterMax);
    }

    // Отъехать назад — против направления взгляда
    void PushBack(float distance)
    {
        Vector3 pos = transform.position;
        pos.x -= Facing * distance;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        transform.position = pos;
    }

    // Рисует в окне Scene (не в игре!) дальность ударов, когда кот выделен.
    // Красная линия — лапа, жёлтая — нога. Удобно подбирать Reach на глаз.
    void OnDrawGizmosSelected()
    {
        var sr = GetComponent<SpriteRenderer>();
        int facing = sr != null && sr.flipX ? -1 : 1;
        Vector3 p = transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(p + Vector3.up * 2.5f, p + new Vector3(punchReach * facing, 2.5f, 0));
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p + Vector3.up * 1.5f, p + new Vector3(kickReach * facing, 1.5f, 0));
    }
}
