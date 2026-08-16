namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Switchboard.Core.Models;
    using Test.Shared.Harness;
    using Touchstone.Core;

    /// <summary>
    /// Network-free suites exercising the <see cref="Switchboard.Core.Client.SwitchboardClient"/>
    /// entity CRUD surface directly against a temporary SQLite database: users, credentials, origin
    /// and endpoint configs, and request history.
    /// </summary>
    public static class ClientCrudSuites
    {
        /// <summary>
        /// All client CRUD suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new List<TestSuiteDescriptor> { Suite() }; }
        }

        /// <summary>
        /// Build the client CRUD suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ClientCrud",
                displayName: "Client Entity CRUD",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Users", "User create/read/update/delete round-trip", async db =>
                    {
                        UserMaster created = await db.Client.Users.CreateAsync(new UserMaster("alice") { Email = "alice@example.com", IsAdmin = false });
                        Check.True(created.GUID != Guid.Empty, "user GUID assigned");

                        UserMaster? byName = await db.Client.Users.GetByUsernameAsync("alice");
                        Check.True(byName != null, "get by username");
                        Check.Equal("alice@example.com", byName!.Email, "email persisted");
                        Check.True(await db.Client.Users.ExistsByUsernameAsync("alice"), "exists by username");
                        Check.Equal(1, await db.Client.Users.CountAsync(), "user count");

                        byName.Email = "alice2@example.com";
                        await db.Client.Users.UpdateAsync(byName);
                        UserMaster? updated = await db.Client.Users.GetByGuidAsync(created.GUID);
                        Check.Equal("alice2@example.com", updated!.Email, "email updated");

                        await db.Client.Users.DeleteByGuidAsync(created.GUID);
                        Check.False(await db.Client.Users.ExistsByUsernameAsync("alice"), "deleted");
                    }),

                    Case("Credentials", "Credential validation and regeneration", async db =>
                    {
                        UserMaster user = await db.Client.Users.CreateAsync(new UserMaster("bob"));
                        Credential cred = new Credential(user.GUID);
                        string originalToken = cred.BearerToken;
                        await db.Client.Credentials.CreateAsync(cred);

                        Credential? validated = await db.Client.Credentials.ValidateBearerTokenAsync(originalToken);
                        Check.True(validated != null, "token validates");
                        UserMaster? resolved = await db.Client.Credentials.GetUserByBearerTokenAsync(originalToken);
                        Check.True(resolved != null && resolved.GUID == user.GUID, "token resolves to user");

                        Credential regenerated = await db.Client.Credentials.RegenerateBearerTokenAsync(cred.GUID);
                        Check.True(regenerated.BearerToken != originalToken, "token changed on regenerate");
                        Check.True(await db.Client.Credentials.ValidateBearerTokenAsync(regenerated.BearerToken) != null, "new token validates");

                        await db.Client.Credentials.DeleteByGuidAsync(cred.GUID);
                        Check.Equal(0, await db.Client.Credentials.CountAsync(), "credential deleted");
                    }),

                    Case("Origins", "Origin config create/update/delete", async db =>
                    {
                        await db.Client.OriginServers.CreateAsync(new OriginServerConfig("origin-x") { Hostname = "localhost", Port = 8001 });
                        OriginServerConfig? read = await db.Client.OriginServers.GetByIdentifierAsync("origin-x");
                        Check.True(read != null, "origin present");
                        Check.Equal(8001, read!.Port, "origin port");

                        read.Port = 8090;
                        await db.Client.OriginServers.UpdateAsync(read);
                        OriginServerConfig? updated = await db.Client.OriginServers.GetByIdentifierAsync("origin-x");
                        Check.Equal(8090, updated!.Port, "origin port updated");

                        Check.Equal(1, await db.Client.OriginServers.CountAsync(), "origin count");
                        await db.Client.OriginServers.DeleteByIdentifierAsync("origin-x");
                        Check.False(await db.Client.OriginServers.ExistsByIdentifierAsync("origin-x"), "origin deleted");
                    }),

                    Case("Endpoints", "Endpoint config create/update/delete", async db =>
                    {
                        await db.Client.ApiEndpoints.CreateAsync(new ApiEndpointConfig("endpoint-x") { TimeoutMs = 5000 });
                        ApiEndpointConfig? read = await db.Client.ApiEndpoints.GetByIdentifierAsync("endpoint-x");
                        Check.True(read != null, "endpoint present");
                        Check.Equal(5000, read!.TimeoutMs, "endpoint timeout");

                        read.TimeoutMs = 7500;
                        await db.Client.ApiEndpoints.UpdateAsync(read);
                        ApiEndpointConfig? updated = await db.Client.ApiEndpoints.GetByIdentifierAsync("endpoint-x");
                        Check.Equal(7500, updated!.TimeoutMs, "endpoint timeout updated");

                        await db.Client.ApiEndpoints.DeleteByIdentifierAsync("endpoint-x");
                        Check.False(await db.Client.ApiEndpoints.ExistsByIdentifierAsync("endpoint-x"), "endpoint deleted");
                    }),

                    Case("RequestHistory", "Request history create/query/retention", async db =>
                    {
                        RequestHistory recent = new RequestHistory { HttpMethod = "GET", RequestPath = "/api/recent", StatusCode = 200, Success = true };
                        await db.Client.RequestHistory.CreateAsync(recent);
                        // CreateAsync stamps TimestampUtc = now, so insert the aged row through the driver to simulate an old record.
                        RequestHistory old = new RequestHistory { GUID = Guid.NewGuid(), HttpMethod = "GET", RequestPath = "/api/old", StatusCode = 500, Success = false, TimestampUtc = DateTime.UtcNow.AddDays(-30) };
                        await db.Driver.InsertAsync(old);

                        Check.Equal(2L, await db.Client.RequestHistory.CountAsync(), "history count");
                        List<RequestHistory> recentRows = await db.Client.RequestHistory.GetRecentAsync(10);
                        Check.True(recentRows.Count == 2, "recent rows returned");
                        Check.Equal(1L, await db.Client.RequestHistory.CountFailedAsync(), "failed count");

                        int deleted = await db.Client.RequestHistory.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-7));
                        Check.Equal(1, deleted, "one old row deleted");
                        Check.Equal(1L, await db.Client.RequestHistory.CountAsync(), "history count after retention");
                    }),

                    Case("BlockedHeaders", "Blocked header create/query/delete", async db =>
                    {
                        await db.Client.BlockedHeaders.CreateAsync(new BlockedHeader("X-Secret-Header"));
                        Check.True(await db.Client.BlockedHeaders.IsBlockedAsync("x-secret-header"), "blocked (lowercased)");
                        Check.Equal(1, await db.Client.BlockedHeaders.CountAsync(), "blocked header count");
                        await db.Client.BlockedHeaders.DeleteByNameAsync("x-secret-header");
                        Check.False(await db.Client.BlockedHeaders.IsBlockedAsync("x-secret-header"), "unblocked after delete");
                    }),

                    Case("UsersMissing", "User lookups/deletes against absent rows and invalid input", async db =>
                    {
                        // SELECT/EXISTS returning empty must yield null/false, not throw, on the SQLite driver.
                        Check.True(await db.Client.Users.GetByGuidAsync(Guid.NewGuid()) == null, "missing user by GUID is null");
                        Check.True(await db.Client.Users.GetByUsernameAsync("ghost") == null, "missing user by username is null");
                        Check.False(await db.Client.Users.ExistsByUsernameAsync("ghost"), "missing user does not exist by username");
                        Check.False(await db.Client.Users.ExistsByGuidAsync(Guid.NewGuid()), "missing user does not exist by GUID");

                        // Deleting an absent row is a guarded no-op.
                        await db.Client.Users.DeleteByGuidAsync(Guid.NewGuid());
                        await db.Client.Users.DeleteByUsernameAsync("ghost");
                        Check.Equal(0, await db.Client.Users.CountAsync(), "count unchanged after no-op deletes");

                        // Guard clauses reject invalid input.
                        await Check.ThrowsAsync<ArgumentNullException>(() => db.Client.Users.CreateAsync(null!), "create null user throws");
                        await Check.ThrowsAsync<ArgumentException>(() => db.Client.Users.CreateAsync(new UserMaster("   ")), "create whitespace-username user throws");
                        await Check.ThrowsAsync<ArgumentNullException>(() => db.Client.Users.GetByUsernameAsync(""), "get by empty username throws");
                    }),

                    Case("CredentialsInvalid", "Credential validation rejects bad tokens and missing GUIDs", async db =>
                    {
                        // Unknown / empty tokens validate to null rather than throwing.
                        Check.True(await db.Client.Credentials.ValidateBearerTokenAsync("not-a-real-token") == null, "unknown token does not validate");
                        Check.True(await db.Client.Credentials.ValidateBearerTokenAsync("") == null, "empty token does not validate");
                        Check.True(await db.Client.Credentials.GetUserByBearerTokenAsync("not-a-real-token") == null, "unknown token resolves to no user");

                        // Regenerating a token for an absent credential is a hard error.
                        await Check.ThrowsAsync<KeyNotFoundException>(() => db.Client.Credentials.RegenerateBearerTokenAsync(Guid.NewGuid()), "regenerate missing credential throws");

                        // A credential with no owning user is rejected up front.
                        await Check.ThrowsAsync<ArgumentException>(() => db.Client.Credentials.CreateAsync(new Credential(Guid.Empty)), "create credential without user GUID throws");
                    }),

                    Case("OriginsAndEndpointsMissing", "Origin/endpoint lookups and deletes against absent identifiers", async db =>
                    {
                        Check.True(await db.Client.OriginServers.GetByIdentifierAsync("no-such-origin") == null, "missing origin is null");
                        Check.False(await db.Client.OriginServers.ExistsByIdentifierAsync("no-such-origin"), "missing origin does not exist");
                        await db.Client.OriginServers.DeleteByIdentifierAsync("no-such-origin");
                        Check.Equal(0, await db.Client.OriginServers.CountAsync(), "origin count still zero after no-op delete");

                        Check.True(await db.Client.ApiEndpoints.GetByIdentifierAsync("no-such-endpoint") == null, "missing endpoint is null");
                        Check.False(await db.Client.ApiEndpoints.ExistsByIdentifierAsync("no-such-endpoint"), "missing endpoint does not exist");
                        await db.Client.ApiEndpoints.DeleteByIdentifierAsync("no-such-endpoint");
                        Check.Equal(0, await db.Client.ApiEndpoints.CountAsync(), "endpoint count still zero after no-op delete");
                    }),

                    Case("EmptyHistoryQueries", "Request-history aggregates over an empty table", async db =>
                    {
                        Check.Equal(0L, await db.Client.RequestHistory.CountAsync(), "empty history count");
                        Check.Equal(0L, await db.Client.RequestHistory.CountFailedAsync(), "empty failed count");
                        List<RequestHistory> recent = await db.Client.RequestHistory.GetRecentAsync(10);
                        Check.True(recent.Count == 0, "no recent rows on empty history");
                        Check.False(await db.Client.BlockedHeaders.IsBlockedAsync("x-absent"), "absent header is not blocked");
                        Check.False(await db.Client.BlockedHeaders.IsBlockedAsync(""), "empty header name is not blocked");
                    })
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<TempDatabase, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: "ClientCrud",
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
