namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    /// <summary>
    /// YAML serializer.
    /// </summary>
    public static class YamlSerializer
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize to YAML.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ToYaml(object obj)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            return serializer.Serialize(obj);
        }

        /// <summary>
        /// Deserialize from YAML.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="yaml">YAML.</param>
        /// <returns>Instance.</returns>
        public static T FromYaml<T>(string yaml)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            return deserializer.Deserialize<T>(yaml);
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
