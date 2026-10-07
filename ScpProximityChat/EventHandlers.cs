using System.Collections.Generic;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using PlayerRoles.Voice;
using PlayerRoles.FirstPersonControl;
using RueI.API;
using RueI.API.Elements;
using ScpProximityChat.Enums;
using UnityEngine;
using UserSettings.ServerSpecific;
using VoiceChat;
using VoiceChat.Networking;

namespace ScpProximityChat
{
    internal class EventHandlers
    {
        private readonly Config _config;
        private readonly Dictionary<Player, SpeakerToy> _toggledPlayers = new();

        internal EventHandlers(Config config)
        {
            _config = config;
        }

        internal void RegisterEvents()
        {
            ServerEvents.RoundRestarted += ClearPlayers;
            PlayerEvents.SendingVoiceMessage += OnSendingVoiceMessage;
            PlayerEvents.ChangedRole += OnChangedRole;
            PlayerEvents.Left += OnLeft;

            switch (_config.ActivationType)
            {
                case ActivationType.ServerSpecificSettings:
                    ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingValueReceived;
                    PlayerEvents.Joined += OnJoined;
                    break;
                case ActivationType.NoClip:
                    PlayerEvents.TogglingNoclip += OnTogglingNoclip;
                    break;
            }
        }

        internal void UnregisterEvents()
        {
            ServerEvents.RoundRestarted -= ClearPlayers;
            PlayerEvents.SendingVoiceMessage -= OnSendingVoiceMessage;
            PlayerEvents.ChangedRole -= OnChangedRole;
            PlayerEvents.Left -= OnLeft;

            switch (_config.ActivationType)
            {
                case ActivationType.ServerSpecificSettings:
                    ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingValueReceived;
                    PlayerEvents.Joined -= OnJoined;
                    break;
                case ActivationType.NoClip:
                    PlayerEvents.TogglingNoclip -= OnTogglingNoclip;
                    break;
            }

            ClearPlayers();
        }

        private void ClearPlayers()
        {
            foreach (KeyValuePair<Player, SpeakerToy> entry in _toggledPlayers)
            {
                if (entry.Value.Base != null)
                    entry.Value.Destroy();
                OpusHandler.Remove(entry.Key);
            }

            _toggledPlayers.Clear();
        }

        private void OnSendingVoiceMessage(PlayerSendingVoiceMessageEventArgs ev)
        {
            Player player = ev.Player;
            if (!ev.IsAllowed || ev.Message.Channel != VoiceChatChannel.ScpChat ||
                !_toggledPlayers.TryGetValue(player, out SpeakerToy speaker))
                return;

            OpusHandler opusHandler = OpusHandler.Get(player);
            float[] decodedBuffer = new float[480];
            opusHandler.Decoder.Decode(ev.Message.Data, ev.Message.DataLength, decodedBuffer);

            for (int i = 0; i < decodedBuffer.Length; i++)
                decodedBuffer[i] *= _config.Volume;

            byte[] encodedData = new byte[512];
            int dataLen = opusHandler.Encoder.Encode(decodedBuffer, encodedData);
            AudioMessage audioMessage = new AudioMessage(speaker.ControllerId, encodedData, dataLen);

            foreach (Player target in Player.List)
            {
                if (target.RoleBase is not IVoiceRole voiceRole ||
                    voiceRole.VoiceModule.ValidateReceive(player.ReferenceHub, VoiceChatChannel.Proximity) == VoiceChatChannel.None)
                    continue;

                if (_config.UseDefaultScpChat && target.IsSCP)
                    continue;

                target.ReferenceHub.connectionToClient.Send(audioMessage);

                if (Vector3.Distance(player.Position, target.Position) <= _config.MaxDistance)
                {
                    target.ReferenceHub.connectionToClient.Send(new VoiceMessage
                    {
                        Speaker = player.ReferenceHub,
                        Channel = VoiceChatChannel.Proximity,
                        Data = encodedData,
                        DataLength = 0,
                    });
                }
            }

            ev.IsAllowed = _config.UseDefaultScpChat;
        }

        private void OnChangedRole(PlayerChangedRoleEventArgs ev)
        {
            RemovePlayer(ev.Player);

            if (_config.ScpRoles.Contains(ev.Player.Role))
            {
                Message message = _config.ProximityChatRole;
                switch (message.Type)
                {
                    case MessageType.Broadcast when message.Show:
                        ev.Player.SendBroadcast(message.Content, message.Duration);
                        break;
                    case MessageType.Hint when message.Show:
                        RueDisplay.Get(ev.Player).Show(new Tag(), new BasicElement(500, message.Content), message.Duration);
                        break;
                }
            }
        }

        private void OnLeft(PlayerLeftEventArgs ev)
        {
            RemovePlayer(ev.Player);
        }

        private void OnJoined(PlayerJoinedEventArgs ev)
        {
            ServerSpecificSettingsSync.SendToPlayer(ev.Player.ReferenceHub);
        }

        private void OnSettingValueReceived(ReferenceHub hub, ServerSpecificSettingBase setting)
        {
            if (setting is not SSKeybindSetting keybind || keybind.SettingId != _config.KeybindId || !keybind.SyncIsPressed)
                return;

            Player player = Player.Get(hub);
            if (player != null && _config.ScpRoles.Contains(player.Role))
                ToggleProximity(player);
        }

        private void OnTogglingNoclip(PlayerTogglingNoclipEventArgs ev)
        {
            if (FpcNoclip.IsPermitted(ev.Player.ReferenceHub) || !_config.ScpRoles.Contains(ev.Player.Role))
                return;

            ToggleProximity(ev.Player);
            ev.IsAllowed = false;
        }

        private void RemovePlayer(Player player)
        {
            if (!_toggledPlayers.TryGetValue(player, out SpeakerToy speaker))
                return;

            if (speaker.Base != null)
                speaker.Destroy();
            _toggledPlayers.Remove(player);
            OpusHandler.Remove(player);
        }

        private void ToggleProximity(Player player)
        {
            Message message;
            if (_toggledPlayers.ContainsKey(player))
            {
                RemovePlayer(player);
                message = _config.ProximityChatDisabled;
            }
            else
            {
                SpeakerToy speaker = SpeakerToy.Create(player.Position, player.ReferenceHub.transform, false);
                speaker.ControllerId = (byte)player.PlayerId;
                speaker.MinDistance = _config.MinDistance;
                speaker.MaxDistance = _config.MaxDistance;
                speaker.IsStatic = false;
                speaker.Spawn();
                _toggledPlayers.Add(player, speaker);
                message = _config.ProximityChatEnabled;
            }

            switch (message.Type)
            {
                case MessageType.Broadcast when message.Show:
                    player.SendBroadcast(message.Content, message.Duration);
                    break;
                case MessageType.Hint when message.Show:
                    player.SendHint(message.Content, message.Duration);
                    break;
            }
        }
    }
}
