using System.Collections.Generic;
using CustomPlayerEffects;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace KikosMayhem.Items;

/// <summary>Addictionater overdose: queued painkillers / adrenaline / SCP-500 all resolve at once when it ends.</summary>
public sealed class Overdose : CustomEffect
{
    public readonly List<ItemType> Queue = new();
    public override string Name => Queue.Count > 0 ? $"Overdose ({Queue.Count} queued)" : "Overdose";
    public override EffectKind Kind => EffectKind.Mixed;

    public override void OnRemoved()
    {
        if (!Owner.Valid() || !Owner.IsAlive) return;
        Hud.Message(Owner, $"<color=#FF66CC>Overdose! {Queue.Count} doses kick in...</color>", 3f);
        foreach (var type in Queue)
        {
            if (!Owner.IsAlive) break;
            Gambling.GamblingModule.Resolve(Owner, type);
        }
        Queue.Clear();
    }
}

/// <summary>Vampirism: kills give speed, crits and AHP and extend the timer. At 0 you drop to 1 HP.</summary>
public sealed class VampirismEffect : CustomEffect
{
    public int Kills;
    public override string Name => $"Vampirism ({Kills} kills)";
    public override EffectKind Kind => EffectKind.Mixed;

    public void OnKill()
    {
        Kills++;
        var s = Stats.Of(Owner);
        s.AddSpeed("vampirism", 4f);
        s.AddCrit("vampirism", 5f, 0f);
        Owner.CreateAhpProcess(20f, 75f, 0f, 1f, 0f, true);
        Expires += MayhemPlugin.Cfg.Items.VampirismSecondsPerKill;
    }

    public override void OnRemoved()
    {
        var s = Stats.Of(Owner);
        s.RemoveSpeed("vampirism");
        s.RemoveCrit("vampirism");
        if (Owner.IsAlive && Remaining <= 0.01f) Owner.Health = 1f;
    }
}

/// <summary>Paladin's Elixir: 87.5% damage reduction. For Puppet it weakens 2.5% per 150 HP lost.</summary>
public sealed class PaladinEffect : CustomEffect
{
    private float _damageTaken;
    public float Strength = 87.5f;
    public override string Name => $"Paladin ({Strength:0.#}% DR)";
    public override EffectKind Kind => EffectKind.Positive;

    public override void OnApplied() => Stats.Of(Owner).SetDamageReduction("paladin", Strength);
    public override void OnRemoved() => Stats.Of(Owner).RemoveDamageReduction("paladin");

    public override void OnOwnerHurting(Player attacker, PlayerHurtingEventArgs ev)
    {
        if (!Owner.IsPuppet()) return;
        _damageTaken += Combat.GetDamage(ev) * (1f - Strength / 100f);
        while (_damageTaken >= 150f && Strength > 0)
        {
            _damageTaken -= 150f;
            Strength = Mathf.Max(0, Strength - 2.5f);
        }
        Stats.Of(Owner).SetDamageReduction("paladin", Strength);
    }
}

/// <summary>Hell's Reckoning: kill an SCP before the timer runs out.</summary>
public sealed class ReckoningEffect : CustomEffect
{
    public ushort Serial;
    public override string Name => "HELL'S RECKONING";
    public override EffectKind Kind => EffectKind.Mixed;

    public override void OnApplied()
    {
        var s = Stats.Of(Owner);
        s.SetCrit("reckoning", 100f, 5f); // 6x crit damage
        s.SetSpeed("reckoning", 100f);
        s.SetDamageReduction("reckoning", 85f);
        Owner.EnableEffect<Scp207>(3);
        Owner.IsBypassEnabled = true;
        Audio.Music.PlayOn(Owner, "hells_reckoning", true);
    }

    public override void OnTick()
    {
        // Speed ramps from +100% to +200%.
        var s = Stats.Of(Owner);
        float t = Time.time - AppliedAt;
        s.SetSpeed("reckoning", Mathf.Min(200f, 100f + t));
    }

    public override void OnRemoved()
    {
        var s = Stats.Of(Owner);
        s.RemoveCrit("reckoning");
        s.RemoveSpeed("reckoning");
        s.RemoveDamageReduction("reckoning");
        Owner.IsBypassEnabled = false;
        Audio.Music.StopOn(Owner);
        if (Owner.IsAlive && Remaining <= 0.01f && !Succeeded)
        {
            var item = Item.Get(Serial);
            if (item != null) { ItemEngine.Unregister(Serial); Owner.RemoveItem(item); }
            Owner.Kill("Consumed by Hell's Reckoning.");
        }
    }

    public bool Succeeded;
}

/// <summary>Dubstep Gun overvolt stacks: drains health faster; 60 stacks = huge explosion.</summary>
public sealed class Overvolt : CustomEffect
{
    public override string Name => "Overvolt";
    public override EffectKind Kind => EffectKind.Mixed;
    public override bool Stacks => true;
    public override bool ShowIntensity => true;
    public override float MaxIntensity => 60;
}
