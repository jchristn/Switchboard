namespace Switchboard.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UrlMatcher;

    /// <summary>
    /// URL tools.
    /// </summary>
    public static class UrlTools
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        /// <summary>
        /// Rewrite a URL based on URL rewrite rules from an API endpoint.
        /// Method-specific rules take precedence over any-method rules (empty method key). Within each set,
        /// rules without a catch-all are tried first in order, then the most specific matching catch-all.
        /// Values captured from the URL replace {name} (or {*name}) placeholders in the target.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="url">Original URL.</param>
        /// <param name="endpoint">API endpoint.</param>
        /// <returns>Rewritten URL or original URL if no change.</returns>
        public static string RewriteUrl(string method, string url, ApiEndpoint endpoint)
        {
            if (String.IsNullOrEmpty(method)) return url;
            if (String.IsNullOrEmpty(url)) return url;
            if (endpoint == null || endpoint.RewriteUrls == null || endpoint.RewriteUrls.Count == 0) return url;

            Matcher matcher = new Matcher(url);

            // Method-specific rewrites take precedence over any-method (empty key) rewrites.
            foreach (string key in new string[] { method, String.Empty })
            {
                if (!endpoint.RewriteUrls.TryGetValue(key, out Dictionary<string, string> rules)) continue;

                if (RouteMatcher.TrySelectRewrite(matcher, rules, out string target, out NameValueCollection nvc))
                {
                    return ReplaceParameters(target, nvc);
                }
            }

            return url;
        }

        #endregion

        #region Private-Methods

        private static string ReplaceParameters(string url, NameValueCollection nvc)
        {
            if (String.IsNullOrEmpty(url)) return url;
            if (nvc == null || nvc.AllKeys == null || nvc.AllKeys.Count() == 0) return url;

            foreach (string key in nvc.AllKeys)
            {
                if (url.Contains("{" + key + "}"))
                {
                    url = url.Replace("{" + key + "}", nvc[key]);
                }

                if (url.Contains("{*" + key + "}"))
                {
                    url = url.Replace("{*" + key + "}", nvc[key]);
                }
            }

            return url;
        }

        #endregion
    }
}
