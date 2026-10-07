using System;
using System.Linq;
using LabApi.Features;
using LabApi.Features.Extensions;
using LabApi.Loader;
using PlayerRoles;
using ScpProximityChat.Enums;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace ScpProximityChat
{
    public class Plugin : LabApi.Loader.Features.Plugins.Plugin
    {
        public override string Name => "EnhancedScpProximityChat";
        public override string Description => "Proximity voice chat for SCPs.";
        public override string Author => "Bolton (Reworked by Morkamo)";
        public override Version Version => new(1, 0, 3);
        public override Version RequiredApiVersion { get; } = new(LabApiProperties.CompiledVersion);

        public Config Config { get; set; }

        private EventHandlers _eventHandlers;
        private ServerSpecificSettingBase[] _settings;

        public override void Enable()
        {
            Config = this.LoadConfig<Config>("Config");
            if (Config != null && !Config.IsEnabled)
                return;

            if (Config != null)
            {
                Config.ScpRoles.RemoveWhere(role => !role.IsScp() || role == RoleTypeId.Scp079);

                _eventHandlers = new EventHandlers(Config);
                _eventHandlers.RegisterEvents();

                if (Config.ActivationType == ActivationType.ServerSpecificSettings)
                {
                    _settings = new ServerSpecificSettingBase[]
                    {
                        new SSGroupHeader(Config.KeybindId, Config.SettingHeaderLabel),
                        new SSKeybindSetting(Config.KeybindId, Config.KeybindLabel, KeyCode.None,
                            hint: Config.KeybindHint),
                    };

                    ServerSpecificSettingsSync.DefinedSettings =
                        (ServerSpecificSettingsSync.DefinedSettings ?? Array.Empty<ServerSpecificSettingBase>())
                        .Concat(_settings).ToArray();
                    ServerSpecificSettingsSync.SendToAll();
                }
            }
        }

        public override void Disable()
        {
            _eventHandlers?.UnregisterEvents();
            _eventHandlers = null;

            if (_settings != null)
            {
                ServerSpecificSettingsSync.DefinedSettings =
                    (ServerSpecificSettingsSync.DefinedSettings ?? Array.Empty<ServerSpecificSettingBase>())
                    .Except(_settings).ToArray();
                ServerSpecificSettingsSync.SendToAll();
                _settings = null;
            }

            Config = null;
        }
    }
}
