namespace Switchboard.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using SerializationHelper;
    using Switchboard.Core;
    using Switchboard.Core.Services;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Switchboard server.
    /// </summary>
    public static class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private static string _SoftwareVersion = "v1.0.0";
        private static SwitchboardSettings _Settings = null;
        private static SwitchboardDaemon _Switchboard = null;
        private static Serializer _Serializer = new Serializer();

        #endregion

        #region Entrypoint

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Task.</returns>
        public static async Task Main(string[] args)
        {
            Welcome();
            ParseArguments(args);
            InitializeSettings();

            using (_Switchboard = new SwitchboardDaemon(_Settings))
            {
                EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                AssemblyLoadContext.Default.Unloading += (ctx) => waitHandle.Set();
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    waitHandle.Set();
                    eventArgs.Cancel = true;
                };

                bool waitHandleSignal = false;
                do
                {
                    waitHandleSignal = waitHandle.WaitOne(1000);
                }
                while (!waitHandleSignal);
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private static void Welcome()
        {
            Console.WriteLine("");
            Console.WriteLine(Constants.Logo);
            Console.WriteLine("");
            Console.WriteLine("Switchboard Server " + _SoftwareVersion);
            Console.WriteLine("");
        }

        private static void ParseArguments(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].StartsWith("--settings="))
                    {
                        Constants.SettingsFile = args[i].Substring(11);
                    }
                }
            }
        }

        private static void InitializeSettings()
        {
            if (!File.Exists(Constants.SettingsFile))
            {
                Console.WriteLine("Settings file " + Constants.SettingsFile + " does not exist, creating");
                _Settings = new SwitchboardSettings();

                _Settings.Webserver.Port = 8000;
                _Settings.Webserver.Ssl.Enable = false;

                _Settings.Database.Type = Core.Database.DatabaseTypeEnum.Sqlite;
                _Settings.Database.Filename = "sb.db";

                File.WriteAllText(Constants.SettingsFile, _Serializer.SerializeJson(_Settings, true));
                Console.WriteLine("Created default settings file " + Constants.SettingsFile);
            }
            else
            {
                Console.WriteLine("Loading from settings file " + Constants.SettingsFile);
                _Settings = _Serializer.DeserializeJson<SwitchboardSettings>(File.ReadAllText(Constants.SettingsFile));
            }
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}