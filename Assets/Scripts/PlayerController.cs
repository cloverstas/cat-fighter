using UnityEngine;
using UnityEngine.InputSystem;

// "Руки игрока": читает клавиатуру и экранные кнопки (TouchControls) и отдаёт команды своему Fighter.
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
        Keyboard kb = Keyboard.current;        // может не быть (телефон) — тогда null
        TouchControls t = TouchControls.Instance; // может не быть (ПК) — тогда null

        // Ходьба: -1 влево, +1 вправо, 0 стоим. Клавиша ИЛИ экранная кнопка.
        float direction = 0f;
        if ((kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed)) || (t != null && t.Left.Held)) direction -= 1f;
        if ((kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed)) || (t != null && t.Right.Held)) direction += 1f;
        fighter.Move(direction);

        // Стойки держатся, пока зажата кнопка. Если зажаты обе — блок важнее.
        bool block = (kb != null && kb.spaceKey.isPressed) || (t != null && t.Block.Held);
        bool crouch = (kb != null && kb.sKey.isPressed) || (t != null && t.Crouch.Held);
        if (block) fighter.HoldStance(Fighter.Stance.Block);
        else if (crouch) fighter.HoldStance(Fighter.Stance.Crouch);
        else fighter.HoldStance(Fighter.Stance.Stand);

        // Удары — по одному нажатию. Consume() "съедает" тап, чтобы один тап = один удар.
        if (Pressed(kb?.iKey, t?.PunchLeft)) fighter.Attack("punch_left");
        else if (Pressed(kb?.oKey, t?.PunchRight)) fighter.Attack("punch_right");
        else if (Pressed(kb?.jKey, t?.Kick)) fighter.Attack("kick");
        else if (Pressed(kb?.kKey, t?.Super)) fighter.TrySuper();
    }

    // Нажата ли клавиша в этом кадре или был тап по экранной кнопке
    static bool Pressed(UnityEngine.InputSystem.Controls.KeyControl key, TouchButton button)
    {
        bool tapped = button != null && button.Consume();
        return tapped || (key != null && key.wasPressedThisFrame);
    }
}
