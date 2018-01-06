using System;
using System.Configuration;
using System.Globalization;

namespace WmcTvOrganizer.Common
{
    public class Config
    {
        public static readonly CultureInfo EnUsCulture = CultureInfo.CreateSpecificCulture("en-US");
        public static readonly CultureInfo EnUkCulture = CultureInfo.CreateSpecificCulture("en-UK");
        /// <summary>
        /// Gets the AppSetting config value. If the key is not found or if the value is empty then default(T) is returned.
        /// </summary>
        /// <typeparam name="T">Datatype to config value</typeparam>
        /// <param name="keyName">Name of key you want to read</param>
        /// <returns>value of config entry; default(T) if key is not found or the value is empty</returns>
        public static T Get<T>(string keyName)
        {
            return Get<T>(keyName, default(T));
        }

        /// <summary>
        /// Gets the AppSetting config value. If the key is not found or if the value is empty then defaultValue parameter is returned.
        /// </summary>
        /// <typeparam name="T">Datatype to config value</typeparam>
        /// <param name="keyName">Name of key you want to read</param>
        /// <param name="defaultValue">The value to return if the key is not found or the value is empty</param>
        /// <returns>value of config entry; defaultValue parameter if key is not found or the value is empty</returns>
        public static T Get<T>(string keyName, T defaultValue)
        {
            T retVal = defaultValue;  // set the default

            try
            {
                string value = ConfigurationManager.AppSettings[keyName];

                if (value != null)
                {   // The value will be null when the key doesn't exist so only bother
                    // trying to convert when we have a value
                    retVal = (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception ex)
            {
                //EventManager.LogInternalCoreWarning("Unable to read appSetting config key.[keyName=" + keyName + "; value to be returned=" + retVal + "]", ex);
            }

            return retVal;
        } 
        
    }
}
