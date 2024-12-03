using System;

namespace Switchboard.Core
{
    /// <summary>
    /// Constants.
    /// </summary>
    public static class Constants
    {
        #region Logo

        /// <summary>
        /// Logo.
        /// See patorjk.com font Small.
        /// </summary>
        public static string Logo = @"
            _ _      _    _                      _ 
  ____ __ _(_) |_ __| |_ | |__  ___  __ _ _ _ __| |
 (_-< V  V / |  _/ _| ' \| '_ \/ _ \/ _` | '_/ _` |
 /__/\_/\_/|_|\__\__|_||_|_.__/\___/\__,_|_| \__,_|
                                                    ";

        #endregion

        #region Settings

        /// <summary>
        /// Settings file.
        /// </summary>
        public static string SettingsFile = "./sb.json";

        #endregion

        #region Default-Homepage

        /// <summary>
        /// Default HTML homepage.
        /// </summary>
        public static string HtmlHomepage =
            @"<html>" + Environment.NewLine +
            @"  <head>" + Environment.NewLine +
            @"    <title>Switchboard Server</title>" + Environment.NewLine +
            @"    <link rel='icon' type='image/x-icon' href='favicon.ico'>" + Environment.NewLine +
            @"    <style>" + Environment.NewLine +
            @"        pre {" + Environment.NewLine +
            @"          background-color: #f5f5f5;" + Environment.NewLine +
            @"          padding: 10px;" + Environment.NewLine +
            @"        }" + Environment.NewLine +
            @"     </style>" + Environment.NewLine +
            @"  </head>" + Environment.NewLine +
            @"  <body>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"      <pre>" + Environment.NewLine +
            Logo + Environment.NewLine +
            @"      </pre>" + Environment.NewLine +
            @"    </div>" + Environment.NewLine +
            @"    <div style='font-family: Arial, sans-serif;'>" + Environment.NewLine +
            @"      <h3>Your node is operational</h2>" + Environment.NewLine +
            @"      <p>Congratulations, your node is operational.  Please refer to the documentation for use.</p>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"  </body>" + Environment.NewLine +
            @"</html>" + Environment.NewLine;

        #endregion

        #region Logging

        /// <summary>
        /// Log filename.
        /// </summary>
        public static string LogFilename = "./switchboard.log";

        /// <summary>
        /// Log directory.
        /// </summary>
        public static string LogDirectory = "./logs/";

        #endregion

        #region Content-Types

        /// <summary>
        /// Binary content type.
        /// </summary>
        public static string BinaryContentType = "application/octet-stream";

        /// <summary>
        /// JSON content type.
        /// </summary>
        public static string JsonContentType = "application/json";

        /// <summary>
        /// HTML content type.
        /// </summary>
        public static string HtmlContentType = "text/html";

        /// <summary>
        /// PNG content type.
        /// </summary>
        public static string PngContentType = "image/png";

        /// <summary>
        /// Text content type.
        /// </summary>
        public static string TextContentType = "text/plain";

        /// <summary>
        /// Favicon filename.
        /// </summary>
        public static string FaviconFilename = "assets/favicon.png";

        /// <summary>
        /// Favicon content type.
        /// </summary>
        public static string FaviconContentType = "image/png";

        /// <summary>
        /// Default GUID.
        /// </summary>
        public static string DefaultGUID = default(Guid).ToString();

        #endregion

        #region Headers

        /// <summary>
        /// Forwarded for header, generally x-forwarded-for.
        /// </summary>
        public static string ForwardedForHeader = "x-forwarded-for";

        /// <summary>
        /// Request ID header, generally x-request-id.
        /// </summary>
        public static string RequestIdHeader = "x-request-id";

        #endregion
    }
}
