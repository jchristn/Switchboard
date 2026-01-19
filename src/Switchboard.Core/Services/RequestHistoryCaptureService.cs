#nullable enable

namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Switchboard.Core.Client;
    using Switchboard.Core.Models;
    using Switchboard.Core.Settings;

    /// <summary>
    /// Service for capturing request history.
    /// Captures request/response data and manages retention policies.
    /// </summary>
    public class RequestHistoryCaptureService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Event raised when a request is captured.
        /// </summary>
        public event EventHandler<RequestHistory>? RequestCaptured;

        /// <summary>
        /// Logging module.
        /// </summary>
        public LoggingModule Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        #endregion

        #region Private-Members

        private readonly RequestHistorySettings _Settings;
        private readonly SwitchboardClient _Client;
        private LoggingModule _Logging;
        private readonly string _Header = "[RequestHistoryCaptureService] ";
        private readonly CancellationTokenSource _TokenSource;
        private readonly Task _CleanupTask;
        private bool _Disposed = false;

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate request history capture service.
        /// </summary>
        /// <param name="settings">Request history settings.</param>
        /// <param name="client">Switchboard client.</param>
        /// <param name="logging">Logging module.</param>
        public RequestHistoryCaptureService(
            RequestHistorySettings settings,
            SwitchboardClient client,
            LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            _TokenSource = new CancellationTokenSource();

            if (_Settings.Enable)
            {
                _Logging.Info(_Header + "starting cleanup task with interval " + _Settings.CleanupIntervalSeconds + " seconds");
                _CleanupTask = Task.Run(() => CleanupLoopAsync(_TokenSource.Token));
            }
            else
            {
                _Logging.Info(_Header + "request history capture is disabled");
                _CleanupTask = Task.CompletedTask;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new request capture context.
        /// Call this when a request is received.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <returns>Request capture context.</returns>
        public RequestCaptureContext BeginCapture(Guid requestId)
        {
            return new RequestCaptureContext(requestId);
        }

        /// <summary>
        /// Complete request capture and store the record.
        /// Per-endpoint and per-origin capture settings are read from the context.
        /// </summary>
        /// <param name="context">Request capture context with endpoint and origin configs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Captured request history record.</returns>
        public async Task<RequestHistory?> EndCaptureAsync(
            RequestCaptureContext context,
            CancellationToken token = default)
        {
            if (!_Settings.Enable)
                return null;

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                context.Complete();

                // Load endpoint and origin configs from database to get current capture settings
                Models.ApiEndpointConfig? endpointConfig = null;
                Models.OriginServerConfig? originConfig = null;

                if (!String.IsNullOrEmpty(context.EndpointIdentifier))
                {
                    try
                    {
                        endpointConfig = await _Client.ApiEndpoints.GetByIdentifierAsync(context.EndpointIdentifier, token).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore errors loading endpoint config
                    }
                }

                if (!String.IsNullOrEmpty(context.OriginIdentifier))
                {
                    try
                    {
                        originConfig = await _Client.OriginServers.GetByIdentifierAsync(context.OriginIdentifier, token).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore errors loading origin config
                    }
                }

                // OR logic: if global OR endpoint OR origin has it enabled, capture
                bool captureRequestHeaders = _Settings.CaptureRequestHeaders
                    || (endpointConfig?.CaptureRequestHeaders == true)
                    || (originConfig?.CaptureRequestHeaders == true);
                bool captureResponseHeaders = _Settings.CaptureResponseHeaders
                    || (endpointConfig?.CaptureResponseHeaders == true)
                    || (originConfig?.CaptureResponseHeaders == true);
                bool captureRequestBody = _Settings.CaptureRequestBody
                    || (endpointConfig?.CaptureRequestBody == true)
                    || (originConfig?.CaptureRequestBody == true);
                bool captureResponseBody = _Settings.CaptureResponseBody
                    || (endpointConfig?.CaptureResponseBody == true)
                    || (originConfig?.CaptureResponseBody == true);

                // Max size: take the largest configured value
                int maxRequestBodySize = Math.Max(
                    _Settings.MaxRequestBodySize,
                    Math.Max(endpointConfig?.MaxCaptureRequestBodySize ?? 0, originConfig?.MaxCaptureRequestBodySize ?? 0));
                int maxResponseBodySize = Math.Max(
                    _Settings.MaxResponseBodySize,
                    Math.Max(endpointConfig?.MaxCaptureResponseBodySize ?? 0, originConfig?.MaxCaptureResponseBodySize ?? 0));

                RequestHistory history = new RequestHistory(context.RequestId)
                {
                    TimestampUtc = context.StartTimeUtc,
                    HttpMethod = context.HttpMethod ?? "GET",
                    RequestPath = context.RequestPath ?? "/",
                    QueryString = context.QueryString,
                    EndpointIdentifier = context.EndpointIdentifier,
                    EndpointGUID = endpointConfig?.GUID,
                    OriginIdentifier = context.OriginIdentifier,
                    OriginGUID = originConfig?.GUID,
                    ClientIp = context.ClientIp,
                    RequestBodySize = context.RequestBodySize,
                    StatusCode = context.StatusCode,
                    ResponseBodySize = context.ResponseBodySize,
                    DurationMs = context.DurationMs,
                    WasAuthenticated = context.WasAuthenticated,
                    ErrorMessage = context.ErrorMessage,
                    Success = context.StatusCode >= 200 && context.StatusCode < 400
                };

                // Capture request headers if enabled
                if (captureRequestHeaders && context.RequestHeaders != null)
                {
                    history.RequestHeaders = JsonSerializer.Serialize(context.RequestHeaders, _JsonOptions);
                }

                // Capture response headers if enabled
                if (captureResponseHeaders && context.ResponseHeaders != null)
                {
                    history.ResponseHeaders = JsonSerializer.Serialize(context.ResponseHeaders, _JsonOptions);
                }

                // Capture request body if enabled and within size limit
                if (captureRequestBody &&
                    context.RequestBody != null &&
                    context.RequestBodySize <= maxRequestBodySize)
                {
                    history.RequestBody = context.RequestBody;
                }

                // Capture response body if enabled and within size limit
                if (captureResponseBody &&
                    context.ResponseBody != null &&
                    context.ResponseBodySize <= maxResponseBodySize)
                {
                    history.ResponseBody = context.ResponseBody;
                }

                RequestHistory saved = await _Client.RequestHistory.CreateAsync(history, token).ConfigureAwait(false);

                RequestCaptured?.Invoke(this, saved);

                return saved;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to capture request: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Run retention cleanup immediately.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of records deleted.</returns>
        public async Task<int> RunCleanupAsync(CancellationToken token = default)
        {
            int totalDeleted = 0;

            try
            {
                // Delete records older than retention period
                if (_Settings.RetentionDays > 0)
                {
                    DateTime cutoff = DateTime.UtcNow.AddDays(-_Settings.RetentionDays);
                    int deleted = await _Client.RequestHistory.DeleteOlderThanAsync(cutoff, token).ConfigureAwait(false);
                    totalDeleted += deleted;

                    if (deleted > 0)
                    {
                        _Logging.Info(_Header + "deleted " + deleted + " records older than " + _Settings.RetentionDays + " days");
                    }
                }

                // Delete excess records if over max
                if (_Settings.MaxRecords > 0)
                {
                    long currentCount = await _Client.RequestHistory.CountAsync(token).ConfigureAwait(false);
                    if (currentCount > _Settings.MaxRecords)
                    {
                        int toDelete = (int)(currentCount - _Settings.MaxRecords);

                        // Get oldest records and delete them
                        List<RequestHistory> oldest = await _Client.RequestHistory.GetAllAsync(
                            _Settings.MaxRecords,
                            toDelete,
                            token).ConfigureAwait(false);

                        foreach (RequestHistory record in oldest)
                        {
                            await _Client.RequestHistory.DeleteByIdAsync(record.Id, token).ConfigureAwait(false);
                            totalDeleted++;
                        }

                        if (toDelete > 0)
                        {
                            _Logging.Info(_Header + "deleted " + toDelete + " excess records (max: " + _Settings.MaxRecords + ")");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "cleanup failed: " + ex.Message);
            }

            return totalDeleted;
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

        private async Task CleanupLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_Settings.CleanupIntervalSeconds), token).ConfigureAwait(false);
                    await RunCleanupAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "cleanup loop error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    _TokenSource.Cancel();
                    try
                    {
                        _CleanupTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        // Ignore errors on dispose
                    }
                    _TokenSource.Dispose();
                }

                _Disposed = true;
            }
        }

        #endregion
    }
}
