using System;
using System.IO.IsolatedStorage;
using Microsoft.Phone.BackgroundTransfer;

namespace TransferManager
{
    public class TransferSettings
    {
        #region Singleton-related stuff
        private static TransferSettings _Instance = null;

        public static TransferSettings I
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = new TransferSettings();
                }
                return _Instance;
            }
        }
        #endregion

        public TransferSettings()
        {
            _Settings = IsolatedStorageSettings.ApplicationSettings;   
        }

        /// <summary>
        /// The isolated storage settings.
        /// </summary>
        IsolatedStorageSettings _Settings;

        #region The isolated storage key names of the settings
        const string TransferPreferencesSettingKeyName = "TransferPreferences";
        #endregion

        #region The default value of the settings
        const TransferPreferences TransferPreferencesSettingDefault = TransferPreferences.AllowCellularAndBattery;
        #endregion

        #region Update, set and save
        /// <summary>
        /// Update a setting value for our application. If the setting does not
        /// exist, then add the setting.
        /// </summary>
        /// <param name="Key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool AddOrUpdateValue(string Key, Object value)
        {
            bool valueChanged = false;

            // If the key exists
            if (_Settings.Contains(Key))
            {
                // If the value has changed
                if (_Settings[Key] != value)
                {
                    // Store the new value
                    _Settings[Key] = value;
                    valueChanged = true;
                }
            }
            // Otherwise create the key.
            else
            {
                _Settings.Add(Key, value);
                valueChanged = true;
            }
            return valueChanged;
        }

        /// <summary>
        /// Get the current value of the setting, or if it is not found, set the 
        /// setting to the default setting.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T GetValueOrDefault<T>(string Key, T defaultValue)
        {
            T value;

            // If the key exists, retrieve the value.
            if (_Settings.Contains(Key))
            {
                value = (T)_Settings[Key];
            }
            // Otherwise, use the default value.
            else
            {
                value = defaultValue;
            }
            return value;
        }

        /// <summary>
        /// Save the settings.
        /// </summary>
        public void Save()
        {
            _Settings.Save();
        }
        #endregion

        /// <summary>
        /// Property to get and set a TransferPreferences Setting Key.
        /// </summary>
        public TransferPreferences TransferPreferences
        {
            get
            {
                return GetValueOrDefault<TransferPreferences>(TransferPreferencesSettingKeyName, TransferPreferencesSettingDefault);
            }
            set
            {
                if (AddOrUpdateValue(TransferPreferencesSettingKeyName, value))
                {
                    Save();
                }
            }
        }
    }
}
