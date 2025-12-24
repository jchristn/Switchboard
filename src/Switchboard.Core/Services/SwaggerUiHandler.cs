namespace Switchboard.Core.Services
{
    using System;

    /// <summary>
    /// Generates Swagger UI HTML content.
    /// </summary>
    public static class SwaggerUiHandler
    {
        #region Public-Methods

        /// <summary>
        /// Generate the Swagger UI HTML page.
        /// </summary>
        /// <param name="openApiPath">Path to the OpenAPI JSON document.</param>
        /// <param name="title">Page title.</param>
        /// <returns>HTML string.</returns>
        public static string GenerateHtml(string openApiPath, string title = "API Documentation")
        {
            if (String.IsNullOrEmpty(openApiPath))
                openApiPath = "/openapi.json";

            if (String.IsNullOrEmpty(title))
                title = "API Documentation";

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(title)}</title>
    <link rel=""stylesheet"" href=""https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui.css"" />
    <style>
        html {{
            box-sizing: border-box;
            overflow-y: scroll;
        }}
        *, *:before, *:after {{
            box-sizing: inherit;
        }}
        body {{
            margin: 0;
            background: #fafafa;
        }}
        .swagger-ui .topbar {{
            background-color: #1b1b1b;
        }}
        #swagger-ui {{
            max-width: 1460px;
            margin: 0 auto;
            padding: 20px;
        }}
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {{
            const ui = SwaggerUIBundle({{
                url: '{EscapeJs(openApiPath)}',
                dom_id: '#swagger-ui',
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: 'StandaloneLayout',
                validatorUrl: null,
                supportedSubmitMethods: ['get', 'post', 'put', 'delete', 'patch', 'head', 'options'],
                defaultModelsExpandDepth: 1,
                defaultModelExpandDepth: 1,
                displayRequestDuration: true,
                docExpansion: 'list',
                filter: true,
                showExtensions: true,
                showCommonExtensions: true,
                syntaxHighlight: {{
                    activate: true,
                    theme: 'monokai'
                }}
            }});
            window.ui = ui;
        }};
    </script>
</body>
</html>";
        }

        #endregion

        #region Private-Methods

        private static string EscapeHtml(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private static string EscapeJs(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            return text
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        #endregion
    }
}
