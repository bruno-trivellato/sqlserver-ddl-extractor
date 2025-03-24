using System.Configuration;

namespace DDLExtractor.Properties
{
    internal sealed partial class Settings : ApplicationSettingsBase
    {
        private static readonly Settings defaultInstance =
            (Settings)Synchronized(new Settings());

        public static Settings Default => defaultInstance;

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string DefaultConnection
        {
            get => (string)this[nameof(DefaultConnection)];
            set => this[nameof(DefaultConnection)] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string TableSchema
        {
            get => (string)this[nameof(TableSchema)];
            set => this[nameof(TableSchema)] = value;
        }
    }
}
