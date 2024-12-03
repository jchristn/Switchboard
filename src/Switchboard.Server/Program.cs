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

        private static string _Header = "[Switchboard] ";
        private static string _SoftwareVersion = "v1.0.0";
        private static int _ProcessId = Environment.ProcessId;
        private static Serializer _Serializer = new Serializer();
        private static SwitchboardSettings _Settings = null;
        private static bool _UseTestData = false;
        private static LoggingModule _Logging = null;

        private static GatewayService _GatewayService = null;
        private static Webserver _Webserver = null;

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
            InitializeGlobals();

            _Logging.Info(_Header + "Switchboard Server started using process ID " + _ProcessId);

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

            _Logging.Info(_Header + "stopping Switchboard Server");
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
                    if (args[i].Equals("--test")) _UseTestData = true;
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

                File.WriteAllText(Constants.SettingsFile, _Serializer.SerializeJson(_Settings, true));
                Console.WriteLine("Created settings file " + Constants.SettingsFile + ", please modify and restart Switchboard");
                Console.WriteLine("");
                Environment.Exit(1);
            }
            else
            {
                Console.WriteLine("Loading from settings file " + Constants.SettingsFile);
                _Settings = _Serializer.DeserializeJson<SwitchboardSettings>(File.ReadAllText(Constants.SettingsFile));
            }

            if (_UseTestData)
            {
                Console.WriteLine("Applying test data to configuration");

                OriginServer githubOrigin = new OriginServer
                {
                    Identifier = "githuborigin",
                    Name = "Github origin server",
                    Method = HttpMethod.GET,
                    Hostname = "github.com",
                    Port = 443,
                    Ssl = true
                };

                ApiEndpoint githubApi = new ApiEndpoint
                {
                    Identifier = "githubapi",
                    Name = "Github API endpoint",
                    Method = HttpMethod.GET,
                    ParameterizedUrl = "/github",
                    TimeoutMs = 60000,
                    RateLimitInterval = RateLimitIntervalEnum.Minutes,
                    RateLimit = 10,
                    LoadBalancing = LoadBalancingMode.RoundRobin,
                    StickySessions = false,
                    EnableRetries = true,
                    RetryCount = 3,
                    MaxRequestBodySize = (512 * 1024 * 1024),
                    OriginServers = new List<string>
                    {
                        "githuborigin"
                    }
                };

                OriginServer nugetOrigin = new OriginServer
                {
                    Identifier = "nugetorigin",
                    Name = "NuGet origin server",
                    Method = HttpMethod.GET,
                    Hostname = "nuget.org",
                    Port = 443,
                    Ssl = true
                };

                ApiEndpoint nugetApi = new ApiEndpoint
                {
                    Identifier = "nugetapi",
                    Name = "NuGet API endpoint",
                    Method = HttpMethod.GET,
                    ParameterizedUrl = "/nuget",
                    TimeoutMs = 60000,
                    RateLimitInterval = RateLimitIntervalEnum.Minutes,
                    RateLimit = 10,
                    LoadBalancing = LoadBalancingMode.RoundRobin,
                    StickySessions = false,
                    EnableRetries = true,
                    RetryCount = 3,
                    MaxRequestBodySize = (512 * 1024 * 1024),
                    OriginServers = new List<string>
                    {
                        "nugetorigin"
                    }
                };

                OriginServer viewOrigin = new OriginServer
                {
                    Identifier = "vieworigin",
                    Name = "View origin server",
                    Method = HttpMethod.GET,
                    Hostname = "localhost",
                    Port = 8321,
                    Ssl = false
                };

                ApiEndpoint viewApi = new ApiEndpoint
                {
                    Identifier = "viewapi",
                    Name = "View API endpoint",
                    Method = HttpMethod.GET,
                    ParameterizedUrl = "/view",
                    TimeoutMs = 60000,
                    RateLimitInterval = RateLimitIntervalEnum.Minutes,
                    RateLimit = 10,
                    LoadBalancing = LoadBalancingMode.RoundRobin,
                    StickySessions = false,
                    EnableRetries = true,
                    RetryCount = 3,
                    MaxRequestBodySize = (512 * 1024 * 1024),
                    OriginServers = new List<string>
                    {
                        "vieworigin"
                    }
                };

                _Settings.Origins.Add(githubOrigin);
                _Settings.Origins.Add(nugetOrigin);
                _Settings.Origins.Add(viewOrigin);
                _Settings.Endpoints.Add(githubApi);
                _Settings.Endpoints.Add(nugetApi);
                _Settings.Endpoints.Add(viewApi);
            }
        }

        private static void InitializeGlobals()
        {
            #region Logging

            Console.WriteLine("Initializing logging");

            List<SyslogServer> syslogServers = new List<SyslogServer>();

            if (_Settings.Logging.Servers != null && _Settings.Logging.Servers.Count > 0)
            {
                foreach (Switchboard.Core.Settings.SyslogServer server in _Settings.Logging.Servers)
                {
                    syslogServers.Add(
                        new SyslogServer
                        {
                            Hostname = server.Hostname,
                            Port = server.Port
                        }
                    );

                    Console.WriteLine("| syslog://" + server.Hostname + ":" + server.Port);
                }
            }

            if (syslogServers.Count > 0)
                _Logging = new LoggingModule(syslogServers);
            else
                _Logging = new LoggingModule();

            _Logging.Settings.EnableConsole = _Settings.Logging.ConsoleLogging;
            _Logging.Settings.EnableColors = _Settings.Logging.EnableColors;

            if (!String.IsNullOrEmpty(_Settings.Logging.LogDirectory))
            {
                if (!Directory.Exists(_Settings.Logging.LogDirectory))
                    Directory.CreateDirectory(_Settings.Logging.LogDirectory);

                _Settings.Logging.LogFilename = _Settings.Logging.LogDirectory + _Settings.Logging.LogFilename;
            }

            if (!String.IsNullOrEmpty(_Settings.Logging.LogFilename))
            {
                _Logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
                _Logging.Settings.LogFilename = _Settings.Logging.LogFilename;
            }

            #endregion

            #region Services

            _GatewayService = new GatewayService(_Settings, _Logging, _Serializer);

            #endregion

            #region Webserver

            _Webserver = new Webserver(_Settings.Webserver, _GatewayService.DefaultRoute);
            _Webserver.Routes.Preflight = _GatewayService.OptionsRoute;
            _Webserver.Routes.PreRouting = _GatewayService.PreRoutingHandler;
            _Webserver.Routes.PostRouting = _GatewayService.PostRoutingHandler;
            _Webserver.Routes.AuthenticateRequest = _GatewayService.AuthenticateRequest;

            _GatewayService.InitializeRoutes(_Webserver);

            _Webserver.Start();

            _Logging.Info(
                _Header + 
                "webserver started on " 
                + (_Settings.Webserver.Ssl.Enable ? "https://" : "http://") 
                + _Settings.Webserver.Hostname 
                + ":" + _Settings.Webserver.Port);

            #endregion
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}