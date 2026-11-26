using System.Collections.Generic;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using PlayerRoles;
using VoiceChat;

namespace KikosMayhem.Scp079;

/// <summary>
/// SCP-079 and its Puppet(s) can always hear each other, anywhere in the facility.
/// SCP-079 talks on its normal SCP chat key; the Puppet just talks normally (proximity or radio).
/// The relayed audio uses the RoundSummary channel, which every client plays as clear, non-positional voice.
/// After the ascension both Puppets hear each other the same way.
/// </summary>
public static class PuppetVoice
{
    // Channel the current speaker actually sent on (the receiving event only sees the per-listener channel).
    private static readonly Dictionary<ReferenceHub, VoiceChatChannel> SentChannel = new();

    public static void Register()
    {
        PlayerEvents.SendingVoiceMessage += OnSending;
        PlayerEvents.ReceivingVoiceMessage += OnReceiving;
    }

    public static void Unregister()
    {
        PlayerEvents.SendingVoiceMessage -= OnSending;
        PlayerEvents.ReceivingVoiceMessage -= OnReceiving;
        SentChannel.Clear();
    }

    private static void OnSending(PlayerSendingVoiceMessageEventArgs ev)
    {
        if (ev.Player != null) SentChannel[ev.Player.ReferenceHub] = ev.Message.Channel;
    }

    private static void OnReceiving(PlayerReceivingVoiceMessageEventArgs ev)
    {
        if (!MayhemPlugin.Cfg.Scp079.PuppetVoiceChat) return;
        var speaker = ev.Sender;
        var listener = ev.Player;
        if (speaker == null || listener == null || speaker == listener) return;
        if (!SentChannel.TryGetValue(speaker.ReferenceHub, out var sent)) return;
        if (!Links(speaker, listener, sent)) return;
        ev.Message.Channel = VoiceChatChannel.RoundSummary;
    }

    private static bool Links(Player speaker, Player listener, VoiceChatChannel sent)
    {
        bool speakerPuppet = PuppetManager.IsPuppet(speaker) && speaker.IsAlive;
        bool listenerPuppet = PuppetManager.IsPuppet(listener) && listener.IsAlive;

        // SCP-079 -> Puppet: only its SCP chat (its speaker ability stays positional like vanilla).
        if (speaker.Role == RoleTypeId.Scp079 && listenerPuppet) return sent == VoiceChatChannel.ScpChat;
        // Puppet -> SCP-079, and Puppet <-> Puppet after the ascension: anything the Puppet says.
        if (speakerPuppet && (listener.Role == RoleTypeId.Scp079 || listenerPuppet))
            return sent is VoiceChatChannel.Proximity or VoiceChatChannel.Radio;
        return false;
    }
}
