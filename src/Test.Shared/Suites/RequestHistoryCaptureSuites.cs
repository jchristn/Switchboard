namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Switchboard.Core.Models;
    using Switchboard.Core.Services;
    using Switchboard.Core.Settings;
    using SyslogLogging;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// Network-free suite exercising <see cref="RequestHistoryCaptureService"/> capture and retention.
    /// </summary>
    public static class RequestHistoryCaptureSuites
    {
        private static readonly LoggingModule _Logging = CreateQuietLogging();

        private static LoggingModule CreateQuietLogging()
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            logging.Settings.MinimumSeverity = Severity.Alert;
            return logging;
        }

        /// <summary>
        /// All request-history capture suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the request-history capture suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "RequestHistoryCapture",
                displayName: "Request History Capture",
                cases: new List<TestCaseDescriptor>
                {
                    Case("CapturePersists", "Enabled capture persists a request history row", async db =>
                    {
                        RequestHistorySettings settings = new RequestHistorySettings { Enable = true };
                        using (RequestHistoryCaptureService svc = new RequestHistoryCaptureService(settings, db.Client, _Logging))
                        {
                            RequestCaptureContext context = svc.BeginCapture(Guid.NewGuid());
                            context.HttpMethod = "GET";
                            context.RequestPath = "/api/captured";
                            context.StatusCode = 200;
                            context.WasAuthenticated = true;

                            RequestHistory? row = await svc.EndCaptureAsync(context);
                            Check.True(row != null, "capture returns a row");
                            Check.Equal(1L, await db.Client.RequestHistory.CountAsync(), "row persisted");
                            List<RequestHistory> recent = await db.Client.RequestHistory.GetRecentAsync(10);
                            Check.True(recent.Exists(r => r.RequestPath == "/api/captured"), "captured path present");
                        }
                    }),

                    Case("CaptureDisabled", "Disabled capture persists nothing", async db =>
                    {
                        RequestHistorySettings settings = new RequestHistorySettings { Enable = false };
                        using (RequestHistoryCaptureService svc = new RequestHistoryCaptureService(settings, db.Client, _Logging))
                        {
                            RequestCaptureContext context = svc.BeginCapture(Guid.NewGuid());
                            context.HttpMethod = "GET";
                            context.RequestPath = "/api/ignored";
                            context.StatusCode = 200;

                            RequestHistory? row = await svc.EndCaptureAsync(context);
                            Check.True(row == null, "disabled capture returns null");
                            Check.Equal(0L, await db.Client.RequestHistory.CountAsync(), "nothing persisted");
                        }
                    }),

                    Case("Retention", "Cleanup removes rows older than the retention window", async db =>
                    {
                        await db.Client.RequestHistory.CreateAsync(new RequestHistory { HttpMethod = "GET", RequestPath = "/recent", StatusCode = 200, Success = true });
                        // CreateAsync stamps TimestampUtc = now, so insert the aged row through the driver to simulate an old record.
                        await db.Driver.InsertAsync(new RequestHistory { GUID = Guid.NewGuid(), HttpMethod = "GET", RequestPath = "/old", StatusCode = 200, Success = true, TimestampUtc = DateTime.UtcNow.AddDays(-30) });

                        RequestHistorySettings settings = new RequestHistorySettings { Enable = true, RetentionDays = 7 };
                        using (RequestHistoryCaptureService svc = new RequestHistoryCaptureService(settings, db.Client, _Logging))
                        {
                            int deleted = await svc.RunCleanupAsync();
                            Check.True(deleted >= 1, "at least the old row deleted");
                            Check.Equal(1L, await db.Client.RequestHistory.CountAsync(), "recent row retained");
                        }
                    })
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<TempDatabase, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: "RequestHistoryCapture",
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    TempDatabase db = await TempDatabase.CreateAsync(ct).ConfigureAwait(false);
                    try
                    {
                        await body(db).ConfigureAwait(false);
                    }
                    finally
                    {
                        await db.DisposeAsync().ConfigureAwait(false);
                    }
                });
        }
    }
}
