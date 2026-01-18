#nullable enable

namespace Switchboard.Core.Database
{
    /// <summary>
    /// Database type enumeration.
    /// </summary>
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// SQLite database.
        /// </summary>
        Sqlite,

        /// <summary>
        /// MySQL database.
        /// </summary>
        Mysql,

        /// <summary>
        /// PostgreSQL database.
        /// </summary>
        Postgres,

        /// <summary>
        /// SQL Server database.
        /// </summary>
        SqlServer
    }
}
