using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// "Судья" боя: раунды, таймер, счёт, нокаут, конец матча.
// Сам ничего не рисует — всё показывает через BattleHUD.
//
// Ход матча:  ROUND N → FIGHT! → бой (таймер) → K.O.! / TIME! → +лапка победителю →
//             следующий раунд … пока кто-то не наберёт roundsToWin побед → экран итогов.
[RequireComponent(typeof(BattleHUD))]
public class RoundManager : MonoBehaviour
{
    [SerializeField] private Fighter player;   // если пусто — найдём сами (тот, у кого PlayerController)
    [SerializeField] private Fighter enemy;

    [Header("Правила")]
    [Tooltip("Сколько побед нужно для победы в матче. 2 — игра (до двух побед), 1 — короткая версия для рекламы")]
    [Min(1)] [SerializeField] private int roundsToWin = 2;
    [SerializeField] private float roundTime = 99f; // секунд на раунд

    [Header("Темп")]
    [SerializeField] private float roundBannerTime = 1.3f;   // сколько висит "ROUND N"
    [SerializeField] private float fightBannerTime = 0.8f;   // сколько висит "FIGHT!"
    [SerializeField] private float slowMotionScale = 0.3f;   // замедление при нокауте
    [SerializeField] private float slowMotionDuration = 1.2f;
    [SerializeField] private float betweenRoundsDelay = 1.2f;

    [Header("Тексты")]
    [SerializeField] private string winTitle = "ПОБЕДА!";
    [SerializeField] private string loseTitle = "ПОРАЖЕНИЕ";
    [SerializeField] private string retryText = "ЕЩЁ РАЗ";

    private BattleHUD hud;
    private SoundManager sound; // может и не быть — тогда просто без звука
    private int round;
    private int playerWins, enemyWins;
    private float timer;
    private bool fighting;              // идёт ли сейчас бой (между FIGHT! и K.O.)
    private float playerStartX, enemyStartX;

    void Start()
    {
        Time.timeScale = 1f; // на случай, если прошлый матч закончился посреди замедления

        FindFightersIfEmpty();
        if (player == null || enemy == null)
        {
            Debug.LogWarning("RoundManager: не нашёл двух бойцов");
            return;
        }

        // Запоминаем стартовые места — в каждом раунде коты возвращаются сюда
        playerStartX = player.transform.position.x;
        enemyStartX = enemy.transform.position.x;

        player.KnockedOut += OnKnockedOut;
        enemy.KnockedOut += OnKnockedOut;

        sound = GetComponent<SoundManager>();
        hud = GetComponent<BattleHUD>();
        if (hud == null) hud = gameObject.AddComponent<BattleHUD>(); // старый RoundManager из сцены — без HUD
        hud.Build(player, enemy, roundsToWin);
        hud.SetWins(0, 0);

        StartCoroutine(RoundIntro());
    }

    void OnDestroy()
    {
        if (player != null) player.KnockedOut -= OnKnockedOut;
        if (enemy != null) enemy.KnockedOut -= OnKnockedOut;
        Time.timeScale = 1f;
    }

    void FindFightersIfEmpty()
    {
        if (player != null && enemy != null) return;
        foreach (Fighter f in FindObjectsByType<Fighter>())
        {
            if (f.GetComponent<PlayerController>() != null) player = f;
            else enemy = f;
        }
    }

    void Update()
    {
        if (!fighting) return;

        timer -= Time.deltaTime;
        hud.SetTimer(Mathf.Max(0, Mathf.CeilToInt(timer))); // CeilToInt — округление вверх: 0.3 сек → показываем 1
        if (timer <= 0f) OnTimeUp();
    }

    // ---------- Начало раунда ----------

    IEnumerator RoundIntro()
    {
        round++;
        player.ResetForRound(playerStartX);
        enemy.ResetForRound(enemyStartX);
        timer = roundTime;
        hud.SetTimer(Mathf.CeilToInt(roundTime));
        hud.SetRound(round);

        // Решающий раунд — если обоим осталось по одной победе (и матч длиннее одного раунда)
        bool isFinal = roundsToWin > 1 && playerWins == roundsToWin - 1 && enemyWins == roundsToWin - 1;
        hud.ShowBanner(hud.RoundBanner(round, isFinal), isFinal ? "FINAL ROUND" : $"ROUND {round}");
        PlaySound(SoundManager.Cue.Round);
        yield return new WaitForSeconds(roundBannerTime);

        hud.ShowBanner(hud.FightLogo, "FIGHT!");
        PlaySound(SoundManager.Cue.Fight);
        yield return new WaitForSeconds(fightBannerTime);

        hud.HideBanner();
        player.Unfreeze();
        enemy.Unfreeze();
        fighting = true;
    }

    // ---------- Конец раунда ----------

    void OnKnockedOut(Fighter loser)
    {
        if (!fighting) return; // двойной нокаут или удар после конца раунда — не считаем
        EndRound(loser == enemy ? player : enemy, knockout: true);
    }

    void OnTimeUp()
    {
        // Время вышло — побеждает тот, у кого больше здоровья. Поровну — ничья, раунд переигрывается.
        Fighter winner = null;
        if (player.Health > enemy.Health) winner = player;
        else if (enemy.Health > player.Health) winner = enemy;
        EndRound(winner, knockout: false);
    }

    void EndRound(Fighter winner, bool knockout)
    {
        fighting = false;
        player.Freeze();
        enemy.Freeze();
        StartCoroutine(EndRoundSequence(winner, knockout));
    }

    IEnumerator EndRoundSequence(Fighter winner, bool knockout)
    {
        if (knockout)
        {
            // Замедление + "K.O.!" — самый "сочный" момент боя.
            // Time.timeScale замедляет всё, что зависит от времени: анимации, движение, WaitForSeconds.
            hud.ShowBanner(hud.KoLogo, "K.O.!");
            PlaySound(SoundManager.Cue.KO);
            Time.timeScale = slowMotionScale;
            // WaitForSecondsRealtime — реальные секунды, замедление на них не действует
            yield return new WaitForSecondsRealtime(slowMotionDuration);
            Time.timeScale = 1f;
        }
        else
        {
            hud.ShowBanner(null, winner == null ? "НИЧЬЯ" : "TIME!");
            yield return new WaitForSeconds(1f);
        }

        if (winner == player) playerWins++;
        else if (winner == enemy) enemyWins++;
        hud.SetWins(playerWins, enemyWins);
        hud.HideBanner();

        // "?." — вызвать, только если winner не null (при ничьей победителя нет)
        winner?.Celebrate();

        yield return new WaitForSeconds(betweenRoundsDelay);

        if (playerWins >= roundsToWin || enemyWins >= roundsToWin)
            EndMatch(playerWins >= roundsToWin);
        else
            StartCoroutine(RoundIntro());
    }

    // ---------- Конец матча ----------

    void EndMatch(bool playerWon)
    {
        if (sound != null) sound.StopMusic();
        PlaySound(playerWon ? SoundManager.Cue.Win : SoundManager.Cue.Lose);

        Fighter winner = playerWon ? player : enemy;
        hud.ShowEndScreen(
            playerWon,
            winner,
            $"{playerWins} : {enemyWins}",
            playerWon ? winTitle : loseTitle,
            retryText,
            Restart);
    }

    void PlaySound(SoundManager.Cue cue)
    {
        if (sound != null) sound.Play(cue);
    }

    void Restart()
    {
        Time.timeScale = 1f;
        // Загрузить текущую сцену заново — самый простой способ "начать с нуля".
        // В рекламной версии здесь будет переход в магазин приложений.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
