using KikosMayhem.Items;
using LabApi.Features.Wrappers;

namespace KikosMayhem.Effects;

/// <summary>Applies a Mayhem effect by name (admin command).</summary>
public static class EffectApplier
{
    public static bool TryApply(Player p, string name, float seconds)
    {
        switch ((name ?? "").ToLowerInvariant())
        {
            case "manic": EffectManager.Apply<Manic>(p, seconds); return true;
            case "radiance": EffectManager.Apply<Radiance>(p, seconds); return true;
            case "herd": EffectManager.Apply<Herd>(p, seconds); return true;
            case "turbodrunk": EffectManager.Apply<TurboDrunk>(p, seconds); return true;
            case "tazed": EffectManager.Apply<Tazed>(p, seconds); return true;
            case "irradiated": EffectManager.Apply<Irradiated>(p, seconds, 5); return true;
            case "hunted": EffectManager.Apply<Hunted>(p, seconds); return true;
            case "burning": EffectManager.Apply<Burning>(p, 0, 5); return true;
            case "eyesofodin": EffectManager.Apply<EyesOfOdin>(p, seconds); return true;
            case "skinwalker":
            case "skinwalkereffect": EffectManager.Apply<SkinwalkerEffect>(p, seconds); return true;
            case "invincible": EffectManager.Apply<Invincible>(p, seconds); return true;
            case "vampirism": EffectManager.Apply<VampirismEffect>(p, seconds); return true;
            case "gasgasgas": GasGasGas.Start(p); return true;
            default: return false;
        }
    }
}
