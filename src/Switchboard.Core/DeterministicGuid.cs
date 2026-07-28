namespace Switchboard.Core
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Produces stable, deterministic GUIDs from string identifiers.
    /// Origin servers and API endpoints are keyed by their string identifier in the database and do
    /// not persist a GUID column; deriving the GUID from the identifier keeps it stable across reads
    /// so callers can address these resources by GUID without a persisted value.
    /// </summary>
    internal static class DeterministicGuid
    {
        /// <summary>
        /// Compute a stable GUID for the supplied identifier.
        /// Returns <see cref="Guid.Empty"/> when the value is null or empty.
        /// </summary>
        /// <param name="value">Identifier.</param>
        /// <returns>Deterministic GUID.</returns>
        internal static Guid FromString(string value)
        {
            if (String.IsNullOrEmpty(value)) return Guid.Empty;

            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
                return new Guid(hash);
            }
        }
    }
}
