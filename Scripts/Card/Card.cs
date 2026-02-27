using Godot;
using System;
using System.Collections.Generic;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Card.Effects;

namespace OdysseyCards.Card;

public partial class Card : Node
{
    public CardData Data { get; private set; }
    public int CurrentCost { get; private set; }
    public bool IsUpgraded { get; private set; }
    
    private List<CardEffect> _effects = new();

    public static Card Create(CardData data)
    {
        var card = new Card
        {
            Data = data,
            CurrentCost = data.Cost,
            IsUpgraded = data.Upgraded
        };
        
        card.LoadEffectsFromData();
        return card;
    }

    private void LoadEffectsFromData()
    {
        if (Data?.Effects == null) return;
        
        foreach (var effectData in Data.Effects)
        {
            if (effectData != null)
            {
                var effect = effectData.CreateEffect();
                if (effect != null)
                {
                    _effects.Add(effect);
                }
            }
        }
    }

    public bool CanPlay(Character.Character caster, Character.Character target = null)
    {
        if (caster.CurrentEnergy < CurrentCost)
            return false;

        return Data.Target switch
        {
            CardTarget.None => true,
            CardTarget.Self => true,
            CardTarget.SingleEnemy => target != null && target != caster,
            CardTarget.AllEnemies => true,
            CardTarget.Everyone => true,
            _ => false
        };
    }

    public void Play(Character.Character caster, Character.Character target = null)
    {
        if (!CanPlay(caster, target))
            return;

        caster.SpendEnergy(CurrentCost);
        ExecuteEffects(caster, target);
        
        if (Data.Exhausts)
            OnExhaust();
    }

    protected virtual void ExecuteEffects(Character.Character caster, Character.Character target)
    {
        foreach (var effect in _effects)
        {
            effect?.Execute(caster, target);
        }
    }

    public virtual void Upgrade()
    {
        if (IsUpgraded)
            return;
        
        IsUpgraded = true;
        OnUpgrade();
    }

    protected virtual void OnUpgrade()
    {
    }

    protected virtual void OnExhaust()
    {
    }

    public Card Clone()
    {
        return Create(Data);
    }
}
