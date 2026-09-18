using UnityEngine;

// "Мозг" компьютерного бойца. Отдаёт своему Fighter те же команды, что и игрок:
// Move, Attack, HoldStance. Характер настраивается ползунками в Inspector.
//
// Логика простая, в два слоя:
//   1. Защита (реакция): противник начал атаку и я в зоне удара → с каким-то шансом ставлю блок или пригибаюсь.
//   2. Нападение (решения): далеко → иду к противнику; близко → раз в полсекунды решаю: бить, отступить или ждать.
[RequireComponent(typeof(Fighter))]
public class FighterAI : MonoBehaviour
{
    [Header("Характер")]
    [Range(0f, 1f)] [SerializeField] private float aggression = 0.6f;  // шанс ударить, когда противник рядом
    [Range(0f, 1f)] [SerializeField] private float blockChance = 0.35f; // шанс поставить блок на атаку
    [Range(0f, 1f)] [SerializeField] private float duckChance = 0.35f;  // шанс пригнуться от пинка
    [Range(0f, 1f)] [SerializeField] private float kickChance = 0.25f;  // доля пинков среди атак
    [Range(0f, 1f)] [SerializeField] private float superChance = 0.5f;  // шанс сразу пустить готовый суперудар

    [Header("Темп")]
    [SerializeField] private Vector2 thinkDelay = new Vector2(0.35f, 0.9f); // пауза между решениями: от..до, секунд
    [SerializeField] private float defenseHoldTime = 0.5f;                  // сколько держать блок/присед
    [SerializeField] private float retreatTime = 0.3f;                      // сколько отступать, если решил отойти
    [SerializeField] private float startDelay = 1f;                         // пауза в начале боя

    private Fighter fighter;
    private float thinkTimer;
    private float defenseTimer;
    private float retreatTimer;
    private int lastSeenAttack; // номер последней атаки противника, на которую уже отреагировали

    void Awake()
    {
        fighter = GetComponent<Fighter>();
        thinkTimer = startDelay;
    }

    void Update()
    {
        Fighter opp = fighter.Opponent;
        if (opp == null || fighter.IsFrozen) return; // раунд окончен — думать не о чем

        float dt = Time.deltaTime;
        float offset = opp.transform.position.x - transform.position.x;
        float distance = Mathf.Abs(offset);
        float toward = Mathf.Sign(offset); // +1 — противник справа, -1 — слева

        // ---------- 1. Защита ----------
        // AttackCount у противника вырос → он только что начал новую атаку
        if (opp.AttackCount != lastSeenAttack)
        {
            lastSeenAttack = opp.AttackCount;
            if (!fighter.IsBusy) TryDefend(opp, distance);
        }

        if (defenseTimer > 0f)
        {
            defenseTimer -= dt;
            if (defenseTimer <= 0f) fighter.HoldStance(Fighter.Stance.Stand);
            return; // пока защищаемся — больше ничего не делаем
        }

        // Бьём или получаем — думать не о чем, ждём
        if (fighter.IsBusy)
        {
            fighter.Move(0f);
            return;
        }

        // ---------- 2. Нападение ----------
        if (retreatTimer > 0f)
        {
            retreatTimer -= dt;
            fighter.Move(-toward); // шаг назад от противника
            return;
        }

        // Далеко — подходим (0.9 — чтобы встать чуть ближе, чем дальность удара, а не на самой границе)
        if (distance > fighter.PunchReach * 0.9f)
        {
            fighter.Move(toward);
            return;
        }

        // Близко — стоим и раз в какое-то время принимаем решение
        fighter.Move(0f);
        thinkTimer -= dt;
        if (thinkTimer > 0f) return;
        thinkTimer = Random.Range(thinkDelay.x, thinkDelay.y); // случайная пауза — чтобы не был предсказуемым роботом

        // Шкала полная и противник в досягаемости — иногда бьём суперударом
        if (fighter.SuperReady && distance <= fighter.SuperReach && Random.value < superChance)
        {
            fighter.TrySuper();
            return;
        }

        float roll = Random.value; // случайное число от 0 до 1 — "бросок кубика"
        if (roll < aggression)
        {
            Attack();
        }
        else if (roll < aggression + 0.15f)
        {
            retreatTimer = retreatTime; // иногда отходим — даёт игроку передышку
        }
        // иначе — просто ждём следующего решения
    }

    void Attack()
    {
        if (Random.value < kickChance)
            fighter.Attack("kick");
        else
            fighter.Attack(Random.value < 0.5f ? "punch_right" : "punch_left");
    }

    void TryDefend(Fighter opp, float distance)
    {
        bool isKick = opp.CurrentAction == "kick";
        float reach = isKick ? opp.KickReach : opp.PunchReach;
        if (distance > reach) return; // удар не достанет — не дёргаемся

        Fighter.Stance defense = Fighter.Stance.Stand;
        if (isKick && Random.value < duckChance) defense = Fighter.Stance.Crouch; // от пинка можно пригнуться
        else if (Random.value < blockChance) defense = Fighter.Stance.Block;

        if (defense == Fighter.Stance.Stand) return; // не повезло — пропустит удар

        fighter.Move(0f);
        fighter.HoldStance(defense);
        defenseTimer = defenseHoldTime;
    }
}
