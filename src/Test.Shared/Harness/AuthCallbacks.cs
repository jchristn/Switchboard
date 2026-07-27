namespace Test.Shared.Harness
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Switchboard.Core;
    using WatsonWebserver.Core;

    using AuthenticationResultEnum = Switchboard.Core.AuthenticationResultEnum;
    using AuthorizationResultEnum = Switchboard.Core.AuthorizationResultEnum;

    /// <summary>
    /// Authentication/authorization callbacks used by the integration harness. A request is
    /// considered authenticated and authorized when it carries any non-empty
    /// <c>Authorization</c> header; otherwise it is denied.
    /// </summary>
    public static class AuthCallbacks
    {
        /// <summary>
        /// Authenticate and authorize based on presence of an Authorization header.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>An <see cref="AuthContext"/> describing the outcome.</returns>
        public static Task<AuthContext> AuthenticateAndAuthorize(HttpContextBase ctx)
        {
            string authHeader = ctx.Request.RetrieveHeaderValue("Authorization");

            if (!String.IsNullOrEmpty(authHeader))
            {
                AuthContext success = new AuthContext();
                success.Authentication.Result = AuthenticationResultEnum.Success;
                success.Authentication.Metadata = new Dictionary<string, object> { { "Authenticated", "true" } };
                success.Authorization.Result = AuthorizationResultEnum.Success;
                success.Authorization.Metadata = new Dictionary<string, object> { { "Authorized", "true" } };
                success.Metadata = new Dictionary<string, object> { { "Allow", "true" } };
                return Task.FromResult(success);
            }

            AuthContext denied = new AuthContext();
            denied.Authentication.Result = AuthenticationResultEnum.Denied;
            denied.Authentication.Metadata = new Dictionary<string, object> { { "Authenticated", "false" } };
            denied.Authorization.Result = AuthorizationResultEnum.Denied;
            denied.Authorization.Metadata = new Dictionary<string, object> { { "Authorized", "false" } };
            denied.Metadata = new Dictionary<string, object> { { "Allow", "false" } };
            denied.FailureMessage = "Supply an Authorization header in your request";
            return Task.FromResult(denied);
        }
    }
}
