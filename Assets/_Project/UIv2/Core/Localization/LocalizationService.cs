using System;

namespace ProjectC.UIv2.Core.Localization
{
    public class LocalizationService : ProjectC.UIv2.Core.UIFramework.ILocalizationService
    {
        public string CurrentLocale { get; private set; } = "en";
        public event Action<string> LocaleChanged;

        public void SetLocale(string locale)
        {
            CurrentLocale = locale;
            LocaleChanged?.Invoke(locale);
        }

        public string Get(string table, string key)
        {
            // Placeholder: Unity Localization integration in M1.x
            return key;
        }
    }
}
