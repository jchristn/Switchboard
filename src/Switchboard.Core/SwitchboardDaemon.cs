namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using SerializationHelper;
    using Switchboard.Core.Client;
    using Switchboard.Core.Database;
    using Switchboard.Core.Services;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Switchboard.
    /// </summary>
    public class SwitchboardDaemon : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Switchboard callbacks.  Attach handlers to these methods to integrate your application logic into Switchboard.
        /// </summary>
        public SwitchboardCallbacks Callbacks
        {
            get
            {
                return _Callbacks;
            }
            set
            {
                if (value == null) _Callbacks = new SwitchboardCallbacks();
                _Callbacks = value;
            }
        }

        /// <summary>
        /// Webserver.
        /// </summary>
        public Webserver Webserver
        {
            get => _Webserver;
        }

        /// <summary>
        /// Switchboard client for database operations.
        /// Use this to seed or manage data programmatically.
        /// </summary>
        public SwitchboardClient Client
        {
            get => _Client;
        }

        #endregion

        #region Private-Members

        private static string _Header = "[SwitchboardDaemon] ";
        private static int _ProcessId = Environment.ProcessId;
        private SwitchboardSettings _Settings = null;
        private SwitchboardCallbacks _Callbacks = new SwitchboardCallbacks();
        private Serializer _Serializer = new Serializer();
        private LoggingModule _Logging = null;
        private HealthCheckService _HealthCheckService = null;
        private GatewayService _GatewayService = null;
        private OpenApiService _OpenApiService = null;
        private Webserver _Webserver = null;

        private IDatabaseDriver _DatabaseDriver = null;
        private SwitchboardClient _Client = null;
        private ManagementService _ManagementService = null;
        private RequestHistoryCaptureService _RequestHistoryService = null;

        private bool _IsDisposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        public SwitchboardDaemon(SwitchboardSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            InitializeGlobals();

            _Logging.Info(_Header + "Switchboard Server started using process ID " + _ProcessId);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_IsDisposed)
            {
                if (disposing)
                {
                    _ManagementService?.Dispose();
                    _ManagementService = null;

                    _RequestHistoryService?.Dispose();
                    _RequestHistoryService = null;

                    _OpenApiService?.Dispose();
                    _OpenApiService = null;

                    _GatewayService?.Dispose();
                    _GatewayService = null;

                    _Webserver?.Dispose();
                    _Webserver = null;

                    _DatabaseDriver?.Dispose();
                    _DatabaseDriver = null;

                    _Client = null;

                    _Logging?.Dispose();
                    _Logging = null;

                    _Serializer = null;
                    _Settings = null;

                    _IsDisposed = true;
                }
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private void CreateDefaultAdminIfNeeded()
        {
            int userCount = _Client.Users.CountAsync().GetAwaiter().GetResult();
            if (userCount > 0)
                return;

            _Logging.Info(_Header + "first startup detected, creating default administrator account...");

            // Create default admin user
            Models.UserMaster adminUser = new Models.UserMaster("admin", isAdmin: true)
            {
                Email = "admin@switchboard",
                FirstName = "Default",
                LastName = "Administrator",
                Active = true
            };

            adminUser = _Client.Users.CreateAsync(adminUser).GetAwaiter().GetResult();

            // Create credential for admin user with known token
            string bearerToken = "sbadmin";
            Models.Credential adminCredential = new Models.Credential
            {
                UserGUID = adminUser.GUID,
                Name = "Default Admin Credential",
                Description = "Auto-generated credential created on first startup",
                BearerToken = bearerToken,
                Active = true,
                IsReadOnly = true
            };

            adminCredential = _Client.Credentials.CreateAsync(adminCredential).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine("  DEFAULT ADMINISTRATOR ACCOUNT CREATED");
            Console.WriteLine("================================================================================");
            Console.WriteLine();
            Console.WriteLine("  A default administrator account has been created for first-time setup.");
            Console.WriteLine();
            Console.WriteLine("  Username:     " + adminUser.Username);
            Console.WriteLine("  User GUID:    " + adminUser.GUID);
            Console.WriteLine("  Bearer Token: " + bearerToken);
            Console.WriteLine();
            Console.WriteLine("  IMPORTANT: This bearer token will NOT be displayed again.");
            Console.WriteLine("  Please copy and store it securely now.");
            Console.WriteLine();
            Console.WriteLine("  This credential is marked as read-only and cannot be modified or");
            Console.WriteLine("  deleted through the API or dashboard.");
            Console.WriteLine();
            Console.WriteLine("================================================================================");
            Console.WriteLine();

            _Logging.Info(_Header + "default administrator account created successfully");
        }

        private void InitializeGlobals()
        {
            #region Logging

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
                }
            }

            if (syslogServers.Count > 0)
                _Logging = new LoggingModule(syslogServers);
            else
                _Logging = new LoggingModule();

            _Logging.Settings.MinimumSeverity = (Severity)_Settings.Logging.MinimumSeverity;
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

            #region Database

            _Logging.Info(_Header + "initializing database (" + _Settings.Database.Type + ")");

            _DatabaseDriver = DatabaseDriverFactory.Create(_Settings.Database.Type, _Settings.Database.BuildConnectionString());
            _DatabaseDriver.OpenAsync().GetAwaiter().GetResult();
            _DatabaseDriver.InitializeSchemaAsync().GetAwaiter().GetResult();

            _Client = SwitchboardClient.CreateAsync(_DatabaseDriver).GetAwaiter().GetResult();

            _Logging.Info(_Header + "database initialized successfully");

            // Check if this is first startup (no users exist)
            CreateDefaultAdminIfNeeded();

            #endregion

            #region Services

            _HealthCheckService = new HealthCheckService(
                _Settings,
                _Logging,
                _Serializer);

            _GatewayService = new GatewayService(
                _Settings,
                _Callbacks,
                _Logging,
                _Serializer);

            _OpenApiService = new OpenApiService(
                _Settings,
                _Logging);

            if (_Client != null)
            {
                if (_Settings.RequestHistory.Enable)
                {
                    _RequestHistoryService = new RequestHistoryCaptureService(
                        _Settings.RequestHistory,
                        _Client,
                        _Logging);

                    _GatewayService.RequestHistoryService = _RequestHistoryService;

                    _Logging.Info(_Header + "request history capture enabled");
                }

                if (_Settings.Management.Enable)
                {
                    _ManagementService = new ManagementService(
                        _Settings.Management,
                        _Client,
                        _Logging);

                    _Logging.Info(_Header + "management API enabled at " + _Settings.Management.BasePath);
                }
            }

            #endregion

            #region Webserver

            _Webserver = new Webserver(_Settings.Webserver, _GatewayService.DefaultRoute);
            _Webserver.Routes.Preflight = _GatewayService.OptionsRoute;
            _Webserver.Routes.PreRouting = _GatewayService.PreRoutingHandler;
            _Webserver.Routes.PostRouting = _GatewayService.PostRoutingHandler;
            _Webserver.Routes.AuthenticateRequest = _GatewayService.AuthenticateRequest;

            _GatewayService.InitializeRoutes(_Webserver);
            _OpenApiService.InitializeRoutes(_Webserver);

            if (_ManagementService != null)
            {
                string serverUrl = (_Settings.Webserver.Ssl.Enable ? "https://" : "http://")
                    + _Settings.Webserver.Hostname
                    + ":" + _Settings.Webserver.Port;
                _ManagementService.InitializeRoutes(_Webserver, serverUrl);
            }

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
    }
}
