#nullable enable

namespace Switchboard.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Switchboard.Core;

    /// <summary>
    /// Point-in-time health status for an origin server, exposed via the management API.
    /// Combines the origin's runtime health state with computed values (current-period uptime and
    /// downtime, uptime percentage). This is a read-only snapshot; the authoritative runtime state is
    /// maintained on the <see cref="OriginServer"/> instance by the health check service.
    /// </summary>
    public class OriginServerHealthStatus
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier of the origin server.
        /// </summary>
        public string Identifier { get; set; } = null!;

        /// <summary>
        /// Unique GUID of the origin server, derived deterministically from the identifier so it is
        /// stable across reads and matches the GUID used to address the origin's configuration.
        /// </summary>
        public Guid GUID { get; set; } = Guid.Empty;

        /// <summary>
        /// Display name of the origin server. May be null.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Hostname of the origin server.
        /// </summary>
        public string? Hostname { get; set; } = null;

        /// <summary>
        /// TCP port of the origin server.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// True if the origin server is currently considered healthy.
        /// </summary>
        public bool IsHealthy { get; set; } = false;

        /// <summary>
        /// Timestamp, in UTC, of the first health check performed against this origin since startup.
        /// Null if no check has been performed yet.
        /// </summary>
        public DateTime? FirstCheckUtc { get; set; } = null;

        /// <summary>
        /// Timestamp, in UTC, of the most recent health check. Null if no check has been performed yet.
        /// </summary>
        public DateTime? LastCheckUtc { get; set; } = null;

        /// <summary>
        /// Timestamp, in UTC, of the most recent transition to the healthy state. Null if never healthy.
        /// </summary>
        public DateTime? LastHealthyUtc { get; set; } = null;

        /// <summary>
        /// Timestamp, in UTC, of the most recent transition to the unhealthy state. Null if never unhealthy.
        /// </summary>
        public DateTime? LastUnhealthyUtc { get; set; } = null;

        /// <summary>
        /// Timestamp, in UTC, of the most recent transition in either direction. Null if no transition
        /// has occurred.
        /// </summary>
        public DateTime? LastStateChangeUtc { get; set; } = null;

        /// <summary>
        /// Cumulative time, in milliseconds, the origin has spent healthy, including the current period.
        /// </summary>
        public long TotalUptimeMs { get; set; } = 0;

        /// <summary>
        /// Cumulative time, in milliseconds, the origin has spent unhealthy, including the current period.
        /// </summary>
        public long TotalDowntimeMs { get; set; } = 0;

        /// <summary>
        /// Uptime percentage (0-100) computed from total uptime and downtime.
        /// </summary>
        public double UptimePercentage { get; set; } = 0.0;

        /// <summary>
        /// Number of consecutive successful health checks.
        /// </summary>
        public int ConsecutiveSuccesses { get; set; } = 0;

        /// <summary>
        /// Number of consecutive failed health checks.
        /// </summary>
        public int ConsecutiveFailures { get; set; } = 0;

        /// <summary>
        /// Error message from the most recent failed health check. Null when the last check succeeded.
        /// </summary>
        public string? LastError { get; set; } = null;

        /// <summary>
        /// Rolling window of individual check results, retained for up to 24 hours.
        /// </summary>
        public List<HealthCheckRecord> History { get; set; } = new List<HealthCheckRecord>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OriginServerHealthStatus()
        {
        }

        /// <summary>
        /// Create a health status snapshot from an origin server's runtime state.
        /// Reads the origin's state under its lock and computes the current in-progress uptime/downtime
        /// period on top of the banked totals.
        /// </summary>
        /// <param name="origin">Origin server. Cannot be null.</param>
        /// <returns>Health status snapshot.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="origin"/> is null.</exception>
        public static OriginServerHealthStatus FromOrigin(OriginServer origin)
        {
            if (origin == null) throw new ArgumentNullException(nameof(origin));

            OriginServerHealthStatus status = new OriginServerHealthStatus
            {
                Identifier = origin.Identifier,
                GUID = DeterministicGuid.FromString(origin.Identifier),
                Name = origin.Name,
                Hostname = origin.Hostname,
                Port = origin.Port
            };

            lock (origin.Lock)
            {
                status.IsHealthy = origin.Healthy;
                status.FirstCheckUtc = origin.FirstCheckUtc;
                status.LastCheckUtc = origin.LastCheckUtc;
                status.LastHealthyUtc = origin.LastHealthyUtc;
                status.LastUnhealthyUtc = origin.LastUnhealthyUtc;
                status.LastStateChangeUtc = origin.LastStateChangeUtc;
                status.ConsecutiveSuccesses = origin.HealthCheckSuccess;
                status.ConsecutiveFailures = origin.HealthCheckFailure;
                status.LastError = origin.LastError;

                long uptimeMs = origin.TotalUptimeMs;
                long downtimeMs = origin.TotalDowntimeMs;

                if (origin.LastStateChangeUtc.HasValue)
                {
                    long currentPeriodMs = (long)(DateTime.UtcNow - origin.LastStateChangeUtc.Value).TotalMilliseconds;
                    if (currentPeriodMs < 0) currentPeriodMs = 0;

                    if (origin.Healthy) uptimeMs += currentPeriodMs;
                    else downtimeMs += currentPeriodMs;
                }

                status.TotalUptimeMs = uptimeMs;
                status.TotalDowntimeMs = downtimeMs;

                long totalMs = uptimeMs + downtimeMs;
                status.UptimePercentage = totalMs > 0 ? (double)uptimeMs / totalMs * 100.0 : 0.0;

                status.History = new List<HealthCheckRecord>(origin.CheckHistory);
            }

            return status;
        }

        #endregion
    }
}
