namespace Switchboard.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using Switchboard.Core.Settings;

    /// <summary>
    /// Generates OpenAPI 3.0.3 specification documents from Switchboard settings.
    /// </summary>
    public class SwitchboardOpenApiDocumentGenerator
    {
        #region Public-Members

        /// <summary>
        /// JSON serializer options used for generating the OpenAPI document.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Private-Members

        private static readonly Regex _ParameterRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SwitchboardOpenApiDocumentGenerator()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate an OpenAPI JSON document from the Switchboard settings.
        /// </summary>
        /// <param name="settings">Switchboard settings.</param>
        /// <returns>OpenAPI JSON string.</returns>
        public string Generate(SwitchboardSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Dictionary<string, object> document = new Dictionary<string, object>
            {
                ["openapi"] = "3.0.3",
                ["info"] = BuildInfo(settings.OpenApi),
                ["paths"] = BuildPaths(settings)
            };

            if (settings.OpenApi.Servers != null && settings.OpenApi.Servers.Count > 0)
            {
                document["servers"] = BuildServers(settings.OpenApi);
            }

            if (settings.OpenApi.Tags != null && settings.OpenApi.Tags.Count > 0)
            {
                document["tags"] = BuildTags(settings.OpenApi);
            }

            Dictionary<string, object> components = BuildComponents(settings.OpenApi);
            if (components.Count > 0)
            {
                document["components"] = components;
            }

            return JsonSerializer.Serialize(document, SerializerOptions);
        }

        #endregion

        #region Private-Methods

        private Dictionary<string, object> BuildInfo(OpenApiDocumentSettings settings)
        {
            Dictionary<string, object> info = new Dictionary<string, object>
            {
                ["title"] = settings.Title ?? "Switchboard API",
                ["version"] = settings.Version ?? "1.0.0"
            };

            if (!String.IsNullOrEmpty(settings.Description))
                info["description"] = settings.Description;

            if (!String.IsNullOrEmpty(settings.TermsOfService))
                info["termsOfService"] = settings.TermsOfService;

            if (settings.Contact != null)
            {
                Dictionary<string, object> contact = new Dictionary<string, object>();
                if (!String.IsNullOrEmpty(settings.Contact.Name))
                    contact["name"] = settings.Contact.Name;
                if (!String.IsNullOrEmpty(settings.Contact.Email))
                    contact["email"] = settings.Contact.Email;
                if (!String.IsNullOrEmpty(settings.Contact.Url))
                    contact["url"] = settings.Contact.Url;
                if (contact.Count > 0)
                    info["contact"] = contact;
            }

            if (settings.License != null)
            {
                Dictionary<string, object> license = new Dictionary<string, object>();
                if (!String.IsNullOrEmpty(settings.License.Name))
                    license["name"] = settings.License.Name;
                if (!String.IsNullOrEmpty(settings.License.Url))
                    license["url"] = settings.License.Url;
                if (license.Count > 0)
                    info["license"] = license;
            }

            return info;
        }

        private List<object> BuildServers(OpenApiDocumentSettings settings)
        {
            List<object> servers = new List<object>();
            foreach (OpenApiServerSettings server in settings.Servers)
            {
                Dictionary<string, object> serverObj = new Dictionary<string, object>
                {
                    ["url"] = server.Url
                };
                if (!String.IsNullOrEmpty(server.Description))
                    serverObj["description"] = server.Description;
                servers.Add(serverObj);
            }
            return servers;
        }

        private List<object> BuildTags(OpenApiDocumentSettings settings)
        {
            List<object> tags = new List<object>();
            foreach (OpenApiTagSettings tag in settings.Tags)
            {
                Dictionary<string, object> tagObj = new Dictionary<string, object>
                {
                    ["name"] = tag.Name
                };
                if (!String.IsNullOrEmpty(tag.Description))
                    tagObj["description"] = tag.Description;
                tags.Add(tagObj);
            }
            return tags;
        }

        private Dictionary<string, object> BuildComponents(OpenApiDocumentSettings settings)
        {
            Dictionary<string, object> components = new Dictionary<string, object>();

            if (settings.SecuritySchemes != null && settings.SecuritySchemes.Count > 0)
            {
                Dictionary<string, object> schemes = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiSecuritySchemeSettings> kvp in settings.SecuritySchemes)
                {
                    Dictionary<string, object> scheme = new Dictionary<string, object>
                    {
                        ["type"] = kvp.Value.Type ?? "http"
                    };

                    if (!String.IsNullOrEmpty(kvp.Value.Description))
                        scheme["description"] = kvp.Value.Description;

                    if (kvp.Value.Type == "apiKey")
                    {
                        if (!String.IsNullOrEmpty(kvp.Value.Name))
                            scheme["name"] = kvp.Value.Name;
                        if (!String.IsNullOrEmpty(kvp.Value.In))
                            scheme["in"] = kvp.Value.In;
                    }
                    else if (kvp.Value.Type == "http")
                    {
                        if (!String.IsNullOrEmpty(kvp.Value.Scheme))
                            scheme["scheme"] = kvp.Value.Scheme;
                        if (!String.IsNullOrEmpty(kvp.Value.BearerFormat))
                            scheme["bearerFormat"] = kvp.Value.BearerFormat;
                    }

                    schemes[kvp.Key] = scheme;
                }
                components["securitySchemes"] = schemes;
            }

            return components;
        }

        private Dictionary<string, object> BuildPaths(SwitchboardSettings settings)
        {
            Dictionary<string, Dictionary<string, object>> paths = new Dictionary<string, Dictionary<string, object>>();

            foreach (ApiEndpoint endpoint in settings.Endpoints)
            {
                // Process unauthenticated routes
                if (endpoint.Unauthenticated?.ParameterizedUrls != null)
                {
                    foreach (KeyValuePair<string, List<string>> methodUrls in endpoint.Unauthenticated.ParameterizedUrls)
                    {
                        string httpMethod = methodUrls.Key.ToLowerInvariant();
                        foreach (string urlPattern in methodUrls.Value)
                        {
                            string path = NormalizePath(urlPattern);
                            List<string> pathParams = ExtractPathParameters(urlPattern);

                            if (!paths.ContainsKey(path))
                                paths[path] = new Dictionary<string, object>();

                            OpenApiRouteDocumentation doc = endpoint.OpenApiDocumentation?.GetDocumentation(methodUrls.Key, urlPattern);
                            paths[path][httpMethod] = BuildOperation(endpoint, doc, httpMethod, path, pathParams, requiresAuth: false, settings.OpenApi);
                        }
                    }
                }

                // Process authenticated routes
                if (endpoint.Authenticated?.ParameterizedUrls != null)
                {
                    foreach (KeyValuePair<string, List<string>> methodUrls in endpoint.Authenticated.ParameterizedUrls)
                    {
                        string httpMethod = methodUrls.Key.ToLowerInvariant();
                        foreach (string urlPattern in methodUrls.Value)
                        {
                            string path = NormalizePath(urlPattern);
                            List<string> pathParams = ExtractPathParameters(urlPattern);

                            if (!paths.ContainsKey(path))
                                paths[path] = new Dictionary<string, object>();

                            OpenApiRouteDocumentation doc = endpoint.OpenApiDocumentation?.GetDocumentation(methodUrls.Key, urlPattern);
                            paths[path][httpMethod] = BuildOperation(endpoint, doc, httpMethod, path, pathParams, requiresAuth: true, settings.OpenApi);
                        }
                    }
                }
            }

            // Convert to Dictionary<string, object> for JSON serialization
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (KeyValuePair<string, Dictionary<string, object>> kvp in paths)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        private Dictionary<string, object> BuildOperation(
            ApiEndpoint endpoint,
            OpenApiRouteDocumentation doc,
            string method,
            string path,
            List<string> pathParams,
            bool requiresAuth,
            OpenApiDocumentSettings openApiSettings)
        {
            Dictionary<string, object> operation = new Dictionary<string, object>();

            if (doc != null)
            {
                // Use provided documentation
                if (!String.IsNullOrEmpty(doc.OperationId))
                    operation["operationId"] = doc.OperationId;

                if (!String.IsNullOrEmpty(doc.Summary))
                    operation["summary"] = doc.Summary;

                if (!String.IsNullOrEmpty(doc.Description))
                    operation["description"] = doc.Description;

                if (doc.Tags != null && doc.Tags.Count > 0)
                    operation["tags"] = doc.Tags;
                else if (!String.IsNullOrEmpty(endpoint.Name))
                    operation["tags"] = new List<string> { endpoint.Name };

                if (doc.Deprecated)
                    operation["deprecated"] = true;

                // Build parameters
                List<object> parameters = new List<object>();
                HashSet<string> definedParams = new HashSet<string>();

                if (doc.Parameters != null)
                {
                    foreach (OpenApiParameterDocumentation param in doc.Parameters)
                    {
                        parameters.Add(BuildParameter(param));
                        definedParams.Add(param.Name);
                    }
                }

                // Auto-add missing path parameters
                foreach (string paramName in pathParams)
                {
                    if (!definedParams.Contains(paramName))
                    {
                        parameters.Add(new Dictionary<string, object>
                        {
                            ["name"] = paramName,
                            ["in"] = "path",
                            ["required"] = true,
                            ["schema"] = new Dictionary<string, object> { ["type"] = "string" }
                        });
                    }
                }

                if (parameters.Count > 0)
                    operation["parameters"] = parameters;

                // Build request body
                if (doc.RequestBody != null)
                {
                    operation["requestBody"] = BuildRequestBody(doc.RequestBody);
                }

                // Build responses
                if (doc.Responses != null && doc.Responses.Count > 0)
                {
                    Dictionary<string, object> responses = new Dictionary<string, object>();
                    foreach (KeyValuePair<string, OpenApiResponseDocumentation> kvp in doc.Responses)
                    {
                        responses[kvp.Key] = BuildResponse(kvp.Value);
                    }
                    operation["responses"] = responses;
                }
                else
                {
                    operation["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object> { ["description"] = "Successful response" }
                    };
                }

                // Handle security - use doc.Security if provided, otherwise auto-add for authenticated routes
                if (doc.Security != null && doc.Security.Count > 0)
                {
                    operation["security"] = BuildSecurityRequirement(doc.Security);
                }
                else if (requiresAuth)
                {
                    operation["security"] = BuildDefaultSecurityRequirement(openApiSettings);
                }
            }
            else
            {
                // Auto-generate minimal documentation
                operation["summary"] = $"{method.ToUpper()} {path}";

                if (!String.IsNullOrEmpty(endpoint.Name))
                    operation["tags"] = new List<string> { endpoint.Name };
                else if (!String.IsNullOrEmpty(endpoint.Identifier))
                    operation["tags"] = new List<string> { endpoint.Identifier };

                operation["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object> { ["description"] = "Successful response" }
                };

                // Auto-add path parameters
                if (pathParams.Count > 0)
                {
                    List<object> parameters = new List<object>();
                    foreach (string paramName in pathParams)
                    {
                        parameters.Add(new Dictionary<string, object>
                        {
                            ["name"] = paramName,
                            ["in"] = "path",
                            ["required"] = true,
                            ["schema"] = new Dictionary<string, object> { ["type"] = "string" }
                        });
                    }
                    operation["parameters"] = parameters;
                }

                // Auto-add security for authenticated routes
                if (requiresAuth)
                {
                    operation["security"] = BuildDefaultSecurityRequirement(openApiSettings);
                }
            }

            return operation;
        }

        private List<object> BuildSecurityRequirement(List<string> schemeNames)
        {
            List<object> security = new List<object>();
            foreach (string scheme in schemeNames)
            {
                security.Add(new Dictionary<string, List<string>> { [scheme] = new List<string>() });
            }
            return security;
        }

        private List<object> BuildDefaultSecurityRequirement(OpenApiDocumentSettings settings)
        {
            List<object> security = new List<object>();

            if (settings.SecuritySchemes != null && settings.SecuritySchemes.Count > 0)
            {
                // Use the first defined security scheme
                foreach (string schemeName in settings.SecuritySchemes.Keys)
                {
                    security.Add(new Dictionary<string, List<string>> { [schemeName] = new List<string>() });
                    break;
                }
            }
            else
            {
                // Default to bearerAuth
                security.Add(new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() });
            }

            return security;
        }

        private Dictionary<string, object> BuildParameter(OpenApiParameterDocumentation param)
        {
            Dictionary<string, object> parameter = new Dictionary<string, object>
            {
                ["name"] = param.Name,
                ["in"] = param.In ?? "query"
            };

            if (!String.IsNullOrEmpty(param.Description))
                parameter["description"] = param.Description;

            if (param.Required || (param.In != null && param.In.Equals("path", StringComparison.OrdinalIgnoreCase)))
                parameter["required"] = true;

            if (param.Deprecated)
                parameter["deprecated"] = true;

            Dictionary<string, object> schema = new Dictionary<string, object>
            {
                ["type"] = param.SchemaType ?? "string"
            };
            if (!String.IsNullOrEmpty(param.SchemaFormat))
                schema["format"] = param.SchemaFormat;
            parameter["schema"] = schema;

            if (param.Example != null)
                parameter["example"] = param.Example;

            return parameter;
        }

        private Dictionary<string, object> BuildRequestBody(OpenApiRequestBodyDocumentation requestBody)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();

            if (!String.IsNullOrEmpty(requestBody.Description))
                body["description"] = requestBody.Description;

            if (requestBody.Required)
                body["required"] = true;

            if (requestBody.Content != null && requestBody.Content.Count > 0)
            {
                Dictionary<string, object> content = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiMediaTypeDocumentation> kvp in requestBody.Content)
                {
                    Dictionary<string, object> mediaType = new Dictionary<string, object>();
                    Dictionary<string, object> schema = new Dictionary<string, object>
                    {
                        ["type"] = kvp.Value.SchemaType ?? "object"
                    };
                    if (!String.IsNullOrEmpty(kvp.Value.SchemaFormat))
                        schema["format"] = kvp.Value.SchemaFormat;
                    mediaType["schema"] = schema;

                    if (kvp.Value.Example != null)
                        mediaType["example"] = kvp.Value.Example;

                    content[kvp.Key] = mediaType;
                }
                body["content"] = content;
            }

            return body;
        }

        private Dictionary<string, object> BuildResponse(OpenApiResponseDocumentation response)
        {
            Dictionary<string, object> resp = new Dictionary<string, object>
            {
                ["description"] = response.Description ?? "Response"
            };

            if (response.Content != null && response.Content.Count > 0)
            {
                Dictionary<string, object> content = new Dictionary<string, object>();
                foreach (KeyValuePair<string, OpenApiMediaTypeDocumentation> kvp in response.Content)
                {
                    Dictionary<string, object> mediaType = new Dictionary<string, object>();
                    Dictionary<string, object> schema = new Dictionary<string, object>
                    {
                        ["type"] = kvp.Value.SchemaType ?? "object"
                    };
                    if (!String.IsNullOrEmpty(kvp.Value.SchemaFormat))
                        schema["format"] = kvp.Value.SchemaFormat;
                    mediaType["schema"] = schema;

                    if (kvp.Value.Example != null)
                        mediaType["example"] = kvp.Value.Example;

                    content[kvp.Key] = mediaType;
                }
                resp["content"] = content;
            }

            return resp;
        }

        private string NormalizePath(string path)
        {
            if (String.IsNullOrEmpty(path)) return "/";

            // Remove trailing slash for OpenAPI (except root)
            if (path.Length > 1 && path.EndsWith("/"))
                path = path.TrimEnd('/');

            // Ensure leading slash
            if (!path.StartsWith("/"))
                path = "/" + path;

            return path;
        }

        private List<string> ExtractPathParameters(string path)
        {
            List<string> parameters = new List<string>();
            MatchCollection matches = _ParameterRegex.Matches(path);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                    parameters.Add(match.Groups[1].Value);
            }
            return parameters;
        }

        #endregion
    }
}
