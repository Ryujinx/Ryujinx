using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace Ryujinx.Ava.Common.Locale
{
    class LocaleManager : BaseModel
    {
        private const string DefaultLanguageCode = "en_US";

        private readonly Dictionary<LocaleKeys, string> _localeStrings;
        private Dictionary<LocaleKeys, string> _localeDefaultStrings;
        private readonly ConcurrentDictionary<LocaleKeys, object[]> _dynamicValues;
        private string _localeLanguageCode;

        public static LocaleManager Instance { get; } = new();
        public event Action LocaleChanged;

        public LocaleManager()
        {
            _localeStrings = new Dictionary<LocaleKeys, string>();
            _localeDefaultStrings = new Dictionary<LocaleKeys, string>();
            _dynamicValues = new ConcurrentDictionary<LocaleKeys, object[]>();

            Load();
        }

        public void Load()
        {
            // Load the system Language Code.
            string localeLanguageCode = CultureInfo.CurrentCulture.Name.Replace('-', '_');

            // If the view is loaded with the UI Previewer detached, then override it with the saved one or default.
            if (Program.PreviewerDetached)
            {
                if (!string.IsNullOrEmpty(ConfigurationState.Instance.UI.LanguageCode.Value))
                {
                    localeLanguageCode = ConfigurationState.Instance.UI.LanguageCode.Value;
                }
                else
                {
                    localeLanguageCode = DefaultLanguageCode;
                }
            }

            // Load en_US as default, if the target language translation is incomplete.
            LoadDefaultLanguage();

            LoadLanguage(localeLanguageCode);
        }

        public string this[LocaleKeys key]
        {
            get
            {
                // Check if the locale contains the key.
                if (_localeStrings.TryGetValue(key, out string value))
                {
                    // Check if the localized string needs to be formatted.
                    if (_dynamicValues.TryGetValue(key, out var dynamicValue))
                    {
                        try
                        {
                            return string.Format(value, dynamicValue);
                        }
                        catch (Exception)
                        {
                            // If formatting failed use the default text instead.
                            if (_localeDefaultStrings.TryGetValue(key, out value))
                            {
                                try
                                {
                                    return string.Format(value, dynamicValue);
                                }
                                catch (Exception)
                                {
                                    // If formatting the default text failed return the key.
                                    return key.ToString();
                                }
                            }
                        }
                    }

                    return value;
                }

                // If the locale doesn't contain the key return the default one.
                if (_localeDefaultStrings.TryGetValue(key, out string defaultValue))
                {
                    return defaultValue;
                }

                // If the locale text doesn't exist return the key.
                return key.ToString();
            }
            set
            {
                _localeStrings[key] = value;

                OnPropertyChanged();
            }
        }

        public bool IsRTL()
        {
            return _localeLanguageCode switch
            {
                "he_IL" => true,
                _ => false
            };
        }

        public string UpdateAndGetDynamicValue(LocaleKeys key, params object[] values)
        {
            _dynamicValues[key] = values;

            OnPropertyChanged("Item");

            return this[key];
        }

        private void LoadDefaultLanguage()
        {
            _localeDefaultStrings = LoadJsonLanguage();
        }

        public void LoadLanguage(string languageCode)
        {
            foreach (var item in LoadJsonLanguage(languageCode))
            {
                this[item.Key] = item.Value;
            }

            _localeLanguageCode = languageCode;
            LocaleChanged?.Invoke();
        }

        private static Dictionary<LocaleKeys, string> LoadJsonLanguage(string languageCode = DefaultLanguageCode)
        {
            var localeStrings = new Dictionary<LocaleKeys, string>();
            string languageJson = EmbeddedResources.ReadAllText($"Ryujinx/Assets/Locales/{languageCode}.json");
            var strings = JsonHelper.Deserialize(languageJson, CommonJsonContext.Default.StringDictionary);

            foreach (var item in strings)
            {
                if (Enum.TryParse<LocaleKeys>(item.Key, out var key))
                {
                    localeStrings[key] = item.Value;
                }
            }

            return localeStrings;
        }
    }
}
