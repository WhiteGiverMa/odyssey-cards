using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.Character;

public partial class Enemy : Character
{
    [Export] public string EnemyType { get; set; }
    
    private List<EnemyAction> _actionPool = new();
    private Queue<EnemyAction> _actionQueue = new();
    private EnemyAction _intendedAction;

    public EnemyAction IntendedAction => _intendedAction;

    public event Action<EnemyAction> OnActionDecided;

    public void Initialize(List<EnemyAction> actions)
    {
        _actionPool = actions;
        DecideNextAction();
    }

    public void DecideNextAction()
    {
        if (_actionQueue.Count == 0)
        {
            var pattern = GenerateActionPattern();
            foreach (var action in pattern)
            {
                _actionQueue.Enqueue(action);
            }
        }

        if (_actionQueue.Count > 0)
        {
            _intendedAction = _actionQueue.Dequeue();
            OnActionDecided?.Invoke(_intendedAction);
        }
    }

    public void ExecuteAction(Character target)
    {
        if (_intendedAction == null || target == null)
            return;

        switch (_intendedAction.Type)
        {
            case ActionType.Attack:
                target.TakeDamage(_intendedAction.Value);
                break;
            case ActionType.Defend:
                GainBlock(_intendedAction.Value);
                break;
            case ActionType.Buff:
                ApplySelfBuff(_intendedAction);
                break;
            case ActionType.Debuff:
                ApplyTargetDebuff(target, _intendedAction);
                break;
            case ActionType.Special:
                ExecuteSpecialAction(_intendedAction);
                break;
        }

        DecideNextAction();
    }

    protected virtual List<EnemyAction> GenerateActionPattern()
    {
        var pattern = new List<EnemyAction>();
        var random = new RandomNumberGenerator();
        random.Randomize();

        for (int i = 0; i < 3; i++)
        {
            if (_actionPool.Count > 0)
            {
                int index = random.RandiRange(0, _actionPool.Count - 1);
                pattern.Add(_actionPool[index]);
            }
        }

        return pattern;
    }

    protected virtual void ApplySelfBuff(EnemyAction action)
    {
    }

    protected virtual void ApplyTargetDebuff(Character target, EnemyAction action)
    {
    }

    protected virtual void ExecuteSpecialAction(EnemyAction action)
    {
    }
}

public enum ActionType
{
    Attack,
    Defend,
    Buff,
    Debuff,
    Special
}

public partial class EnemyAction : Resource
{
    [Export] public ActionType Type { get; set; }
    [Export] public int Value { get; set; }
    [Export] public string Description { get; set; }
    [Export] public int Hits { get; set; } = 1;
}
