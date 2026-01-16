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
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="url">Original URL.</param>
        /// <param name="endpoint">API endpoint.</param>
        /// <returns>Rewritten URL or original URL if no change.</returns>
        public static string RewriteUrl(string method, string url, ApiEndpoint endpoint)
        {
            if (String.IsNullOrEmpty(method)) return method;
            if (String.IsNullOrEmpty(url)) return url;
            if (endpoint == null || endpoint.RewriteUrls == null || endpoint.RewriteUrls.Count == 0) return url;
            if (!endpoint.RewriteUrls.Keys.Contains(method)) return url;

            NameValueCollection nvc = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

            Matcher matcher = new Matcher(url);

            foreach (KeyValuePair<string, Dictionary<string, string>> kvpOuter in endpoint.RewriteUrls)
            {
                if (String.IsNullOrEmpty(kvpOuter.Key)) continue;
                if (kvpOuter.Value == null || kvpOuter.Value.Count == 0) continue;

                if (kvpOuter.Key.Equals(method))
                {
                    foreach (KeyValuePair<string, string> kvpInner in kvpOuter.Value)
                    {
                        if (String.IsNullOrEmpty(kvpInner.Key)) continue;
                        if (String.IsNullOrEmpty(kvpInner.Value)) continue;

                        if (matcher.Match(kvpInner.Key, out nvc))
                        {
                            return ReplaceParameters(kvpInner.Value, nvc);
                        }
                    }
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
            }

            return url;
        }

        #endregion
    }
}
