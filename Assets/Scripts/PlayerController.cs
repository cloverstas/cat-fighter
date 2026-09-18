using UnityEngine;
using UnityEngine.InputSystem;

// "Руки игрока": читает клавиатуру и отдаёт команды своему Fighter.
// Управление: A/D или стрелки — шаг, I — левая лапа, O — правая лапа, J — нога,
//             K — суперудар (при полной шкале), ПРОБЕЛ (держать) — блок, S (держать) — пригнуться.
[RequireComponent(typeof(Fighter))]
public class PlayerController : MonoBehaviour
{
    private Fighter fighter;

    void Awake()
    {
        fighter = GetComponent<Fighter>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return; // клавиатуры нет (например, на телефоне) — ничего не делаем

        // Ходьба: -1 влево, +1 вправо, 0 стоим
        float direction = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) direction -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) direction += 1f;
        fighter.Move(direction);

        // Стойки держатся, пока зажата кнопка. Если зажаты обе — блок важнее.
        if (kb.spaceKey.isPressed) fighter.HoldStance(Fighter.Stance.Block);
        else if (kb.sKey.isPressed) fighter.HoldStance(Fighter.Stance.Crouch);
        else fighter.HoldStance(Fighter.Stance.Stand);

        // Удары — по одному нажатию
        if (kb.iKey.wasPressedThisFrame) fighter.Attack("punch_left");
        else if (kb.oKey.wasPressedThisFrame) fighter.Attack("punch_right");
        else if (kb.jKey.wasPressedThisFrame) fighter.Attack("kick");
        else if (kb.kKey.wasPressedThisFrame) fighter.TrySuper();
    }
}
