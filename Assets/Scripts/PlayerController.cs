using UnityEngine;
using UnityEngine.InputSystem;

// "Руки игрока": читает клавиатуру и отдаёт команды своему Fighter.
// Управление: A/D или стрелки — шаг, J — правая лапа, U — левая, K — нога,
//             H (держать) — блок, S (держать) — пригнуться, L — суперудар (при полной шкале).
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
        if (kb.hKey.isPressed) fighter.HoldStance(Fighter.Stance.Block);
        else if (kb.sKey.isPressed) fighter.HoldStance(Fighter.Stance.Crouch);
        else fighter.HoldStance(Fighter.Stance.Stand);

        // Удары — по одному нажатию
        if (kb.jKey.wasPressedThisFrame) fighter.Attack("punch_right");
        else if (kb.uKey.wasPressedThisFrame) fighter.Attack("punch_left");
        else if (kb.kKey.wasPressedThisFrame) fighter.Attack("kick");
        else if (kb.lKey.wasPressedThisFrame) fighter.TrySuper();
    }
}
