using System.Collections.Generic;
using KikosMayhem.Core;

namespace KikosMayhem;

public static class ModuleList
{
    public static IEnumerable<MayhemModule> Create() => new MayhemModule[]
    {
        new Gambling.GamblingModule(),
        new Gambling.FacilityModule(),
        new Scps.Scp173Module(),
        new Scps.Scp096Module(),
        new Scps.Scp106Module(),
        new Scps.Scp939Module(),
        new Scps.Scp049Module(),
        new Scps.Scp966Module(),
        new Scps.ScpPowers(),
        new Scp079.PuppetManager(),
        new Ascension.AscensionQuest(),
        new Events.EventsModule(),
    };
}
