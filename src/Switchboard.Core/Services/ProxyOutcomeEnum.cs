namespace Switchboard.Core.Services
{
    /// <summary>
    /// Result of a single attempt to proxy a request to one origin server. Used by the gateway's
    /// retry/failover loop to decide whether another origin may be tried.
    /// </summary>
    public enum ProxyOutcome
    {
        /// <summary>
        /// The response was fully handled and written to the client (a success, or a failure surfaced to
        /// the client because no further retry was permitted). The request is finished.
        /// </summary>
        Completed,
        /// <summary>
        /// The attempt failed before any bytes were written to the client (a transport error, or a
        /// retryable upstream status), so the request may safely be retried against another origin.
        /// </summary>
        Failed,
        /// <summary>
        /// The attempt began streaming a response to the client (server-sent events or chunked transfer)
        /// and then failed. Bytes were already sent, so the request cannot be retried and must be aborted.
        /// </summary>
        FailedResponseStarted
    }
}
