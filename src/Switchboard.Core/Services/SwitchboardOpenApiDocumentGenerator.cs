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

        /// <summary>
        /// Generate an OpenAPI JSON document for the Switchboard Management API.
        /// </summary>
        /// <param name="basePath">Base path for the management API (e.g., "/_sb/v1.0").</param>
        /// <param name="serverUrl">Server URL (e.g., "http://localhost:8000").</param>
        /// <returns>OpenAPI JSON string.</returns>
        public string GenerateManagementApiDocument(string basePath = "/_sb/v1.0", string serverUrl = "")
        {
            if (String.IsNullOrEmpty(basePath)) basePath = "/_sb/v1.0";
            basePath = basePath.TrimEnd('/');

            Dictionary<string, object> document = new Dictionary<string, object>
            {
                ["openapi"] = "3.0.3",
                ["info"] = new Dictionary<string, object>
                {
                    ["title"] = "Switchboard Management API",
                    ["version"] = "4.0.2",
                    ["description"] = "REST API for managing Switchboard configuration including origin servers, API endpoints, routes, mappings, URL rewrites, blocked headers, users, credentials, and request history."
                },
                ["paths"] = BuildManagementPaths(basePath),
                ["tags"] = BuildManagementTags(),
                ["components"] = BuildManagementComponents()
            };

            if (!String.IsNullOrEmpty(serverUrl))
            {
                document["servers"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["url"] = serverUrl,
                        ["description"] = "Switchboard Server"
                    }
                };
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

        private List<object> BuildManagementTags()
        {
            return new List<object>
            {
                new Dictionary<string, object> { ["name"] = "Origins", ["description"] = "Origin server management" },
                new Dictionary<string, object> { ["name"] = "Endpoints", ["description"] = "API endpoint management" },
                new Dictionary<string, object> { ["name"] = "Routes", ["description"] = "Endpoint route management" },
                new Dictionary<string, object> { ["name"] = "Mappings", ["description"] = "Endpoint-origin mapping management" },
                new Dictionary<string, object> { ["name"] = "Rewrites", ["description"] = "URL rewrite rule management" },
                new Dictionary<string, object> { ["name"] = "Headers", ["description"] = "Blocked header management" },
                new Dictionary<string, object> { ["name"] = "Users", ["description"] = "User management" },
                new Dictionary<string, object> { ["name"] = "Credentials", ["description"] = "Credential and bearer token management" },
                new Dictionary<string, object> { ["name"] = "History", ["description"] = "Request history and statistics" },
                new Dictionary<string, object> { ["name"] = "System", ["description"] = "Health check and system information" }
            };
        }

        private Dictionary<string, object> BuildManagementComponents()
        {
            return new Dictionary<string, object>
            {
                ["securitySchemes"] = new Dictionary<string, object>
                {
                    ["bearerAuth"] = new Dictionary<string, object>
                    {
                        ["type"] = "http",
                        ["scheme"] = "bearer",
                        ["bearerFormat"] = "token",
                        ["description"] = "Bearer token authentication. Use the AdminToken from configuration or a user's bearer token from credentials."
                    }
                },
                ["schemas"] = BuildManagementSchemas()
            };
        }

        private Dictionary<string, object> BuildManagementSchemas()
        {
            return new Dictionary<string, object>
            {
                ["OriginServerConfig"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["GUID"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["Identifier"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Hostname"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Port"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["Ssl"] = new Dictionary<string, object> { ["type"] = "boolean" },
                        ["HealthCheckIntervalMs"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["HealthCheckUrl"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["HealthCheckMethod"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["UnhealthyThreshold"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["HealthyThreshold"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["MaxParallelRequests"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["RateLimitRequestsThreshold"] = new Dictionary<string, object> { ["type"] = "integer" }
                    }
                },
                ["ApiEndpointConfig"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["GUID"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["Identifier"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["LoadBalancing"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new List<string> { "RoundRobin", "Random" } },
                        ["TimeoutMs"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["BlockHttp10"] = new Dictionary<string, object> { ["type"] = "boolean" },
                        ["MaxRequestBodySize"] = new Dictionary<string, object> { ["type"] = "integer", ["format"] = "int64" }
                    }
                },
                ["EndpointRoute"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["Id"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["EndpointGuid"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["HttpMethod"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["UrlPattern"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["RequiresAuthentication"] = new Dictionary<string, object> { ["type"] = "boolean" }
                    }
                },
                ["EndpointOriginMapping"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["Id"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["EndpointGuid"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["OriginGuid"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }
                    }
                },
                ["UrlRewrite"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["Id"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["EndpointGuid"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["HttpMethod"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["SourcePattern"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["TargetPattern"] = new Dictionary<string, object> { ["type"] = "string" }
                    }
                },
                ["BlockedHeader"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["Id"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["HeaderName"] = new Dictionary<string, object> { ["type"] = "string" }
                    }
                },
                ["UserMaster"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["GUID"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["Username"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Email"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["FirstName"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["LastName"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["IsAdmin"] = new Dictionary<string, object> { ["type"] = "boolean" },
                        ["Active"] = new Dictionary<string, object> { ["type"] = "boolean" }
                    }
                },
                ["Credential"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["GUID"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["UserGuid"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["Name"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["BearerToken"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Active"] = new Dictionary<string, object> { ["type"] = "boolean" },
                        ["IsReadOnly"] = new Dictionary<string, object> { ["type"] = "boolean" }
                    }
                },
                ["RequestHistory"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["Id"] = new Dictionary<string, object> { ["type"] = "integer", ["format"] = "int64" },
                        ["GUID"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["Timestamp"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" },
                        ["EndpointGuid"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["OriginGuid"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" },
                        ["HttpMethod"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["RequestUrl"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["ResponseStatusCode"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["DurationMs"] = new Dictionary<string, object> { ["type"] = "integer", ["format"] = "int64" },
                        ["Success"] = new Dictionary<string, object> { ["type"] = "boolean" }
                    }
                },
                ["ApiErrorResponse"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["StatusCode"] = new Dictionary<string, object> { ["type"] = "integer" },
                        ["Error"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Description"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["Context"] = new Dictionary<string, object> { ["type"] = "object" }
                    }
                },
                ["HealthResponse"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["status"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["timestamp"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" },
                        ["version"] = new Dictionary<string, object> { ["type"] = "string" }
                    }
                },
                ["HistoryStats"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["totalRequests"] = new Dictionary<string, object> { ["type"] = "integer", ["format"] = "int64" },
                        ["failedRequests"] = new Dictionary<string, object> { ["type"] = "integer", ["format"] = "int64" },
                        ["successRate"] = new Dictionary<string, object> { ["type"] = "number", ["format"] = "double" }
                    }
                }
            };
        }

        private Dictionary<string, object> BuildManagementPaths(string basePath)
        {
            Dictionary<string, object> paths = new Dictionary<string, object>();

            // Origins
            AddCrudPaths(paths, basePath, "/origins", "Origins", "OriginServerConfig", "Origin server", "guid");

            // Endpoints
            AddCrudPaths(paths, basePath, "/endpoints", "Endpoints", "ApiEndpointConfig", "API endpoint", "guid");

            // Routes
            AddCrudPaths(paths, basePath, "/routes", "Routes", "EndpointRoute", "Endpoint route", "id");

            // Mappings
            paths[basePath + "/mappings"] = new Dictionary<string, object>
            {
                ["get"] = BuildListOperation("Mappings", "EndpointOriginMapping", "List all endpoint-origin mappings"),
                ["post"] = BuildCreateOperation("Mappings", "EndpointOriginMapping", "Create a new endpoint-origin mapping")
            };
            paths[basePath + "/mappings/{id}"] = new Dictionary<string, object>
            {
                ["get"] = BuildGetOperation("Mappings", "EndpointOriginMapping", "Get endpoint-origin mapping by ID", "id"),
                ["delete"] = BuildDeleteOperation("Mappings", "Delete endpoint-origin mapping", "id")
            };

            // Rewrites
            AddCrudPaths(paths, basePath, "/rewrites", "Rewrites", "UrlRewrite", "URL rewrite rule", "id");

            // Headers
            paths[basePath + "/headers"] = new Dictionary<string, object>
            {
                ["get"] = BuildListOperation("Headers", "BlockedHeader", "List all blocked headers"),
                ["post"] = BuildCreateOperation("Headers", "BlockedHeader", "Create a new blocked header")
            };
            paths[basePath + "/headers/{id}"] = new Dictionary<string, object>
            {
                ["get"] = BuildGetOperation("Headers", "BlockedHeader", "Get blocked header by ID", "id"),
                ["delete"] = BuildDeleteOperation("Headers", "Delete blocked header", "id")
            };

            // Users
            AddCrudPaths(paths, basePath, "/users", "Users", "UserMaster", "User", "guid");

            // Credentials
            AddCrudPaths(paths, basePath, "/credentials", "Credentials", "Credential", "Credential", "guid");
            paths[basePath + "/credentials/{guid}/regenerate"] = new Dictionary<string, object>
            {
                ["post"] = BuildRegenerateOperation()
            };

            // Request History
            paths[basePath + "/history"] = new Dictionary<string, object>
            {
                ["get"] = BuildHistoryListOperation()
            };
            paths[basePath + "/history/recent"] = new Dictionary<string, object>
            {
                ["get"] = BuildRecentHistoryOperation()
            };
            paths[basePath + "/history/failed"] = new Dictionary<string, object>
            {
                ["get"] = BuildFailedHistoryOperation()
            };
            paths[basePath + "/history/stats"] = new Dictionary<string, object>
            {
                ["get"] = BuildHistoryStatsOperation()
            };
            paths[basePath + "/history/cleanup"] = new Dictionary<string, object>
            {
                ["post"] = BuildHistoryCleanupOperation()
            };
            paths[basePath + "/history/{id}"] = new Dictionary<string, object>
            {
                ["get"] = BuildGetOperation("History", "RequestHistory", "Get request history by ID or GUID", "id"),
                ["delete"] = BuildDeleteOperation("History", "Delete request history record", "id")
            };

            // Health
            paths[basePath + "/health"] = new Dictionary<string, object>
            {
                ["get"] = BuildHealthOperation()
            };

            // Current user
            paths[basePath + "/me"] = new Dictionary<string, object>
            {
                ["get"] = BuildMeOperation()
            };

            return paths;
        }

        private void AddCrudPaths(Dictionary<string, object> paths, string basePath, string resource, string tag, string schema, string displayName, string paramName)
        {
            paths[basePath + resource] = new Dictionary<string, object>
            {
                ["get"] = BuildListOperation(tag, schema, $"List all {displayName.ToLower()}s"),
                ["post"] = BuildCreateOperation(tag, schema, $"Create a new {displayName.ToLower()}")
            };
            paths[basePath + resource + "/{" + paramName + "}"] = new Dictionary<string, object>
            {
                ["get"] = BuildGetOperation(tag, schema, $"Get {displayName.ToLower()} by {paramName.ToUpper()}", paramName),
                ["put"] = BuildUpdateOperation(tag, schema, $"Update {displayName.ToLower()}", paramName),
                ["delete"] = BuildDeleteOperation(tag, $"Delete {displayName.ToLower()}", paramName)
            };
        }

        private Dictionary<string, object> BuildListOperation(string tag, string schema, string summary)
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { tag },
                ["summary"] = summary,
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object> { ["name"] = "search", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "string" }, ["description"] = "Search term for filtering" },
                    new Dictionary<string, object> { ["name"] = "skip", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer", ["default"] = 0 }, ["description"] = "Number of records to skip" },
                    new Dictionary<string, object> { ["name"] = "take", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer" }, ["description"] = "Maximum number of records to return" }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Successful response",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "array",
                                    ["items"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + schema }
                                }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildCreateOperation(string tag, string schema, string summary)
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { tag },
                ["summary"] = summary,
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["requestBody"] = new Dictionary<string, object>
                {
                    ["required"] = true,
                    ["content"] = new Dictionary<string, object>
                    {
                        ["application/json"] = new Dictionary<string, object>
                        {
                            ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + schema }
                        }
                    }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["201"] = new Dictionary<string, object>
                    {
                        ["description"] = "Created successfully",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + schema }
                            }
                        }
                    },
                    ["400"] = new Dictionary<string, object> { ["description"] = "Bad request", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildGetOperation(string tag, string schema, string summary, string paramName)
        {
            string paramType = paramName == "guid" ? "uuid" : "integer";
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { tag },
                ["summary"] = summary,
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = paramName,
                        ["in"] = "path",
                        ["required"] = true,
                        ["schema"] = paramType == "uuid"
                            ? new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }
                            : new Dictionary<string, object> { ["type"] = "integer" }
                    }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Successful response",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + schema }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["404"] = new Dictionary<string, object> { ["description"] = "Not found", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildUpdateOperation(string tag, string schema, string summary, string paramName)
        {
            string paramType = paramName == "guid" ? "uuid" : "integer";
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { tag },
                ["summary"] = summary,
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = paramName,
                        ["in"] = "path",
                        ["required"] = true,
                        ["schema"] = paramType == "uuid"
                            ? new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }
                            : new Dictionary<string, object> { ["type"] = "integer" }
                    }
                },
                ["requestBody"] = new Dictionary<string, object>
                {
                    ["required"] = true,
                    ["content"] = new Dictionary<string, object>
                    {
                        ["application/json"] = new Dictionary<string, object>
                        {
                            ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + schema }
                        }
                    }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Updated successfully",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + schema }
                            }
                        }
                    },
                    ["400"] = new Dictionary<string, object> { ["description"] = "Bad request", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["404"] = new Dictionary<string, object> { ["description"] = "Not found", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildDeleteOperation(string tag, string summary, string paramName)
        {
            string paramType = paramName == "guid" ? "uuid" : "integer";
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { tag },
                ["summary"] = summary,
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = paramName,
                        ["in"] = "path",
                        ["required"] = true,
                        ["schema"] = paramType == "uuid"
                            ? new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }
                            : new Dictionary<string, object> { ["type"] = "integer" }
                    }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["204"] = new Dictionary<string, object> { ["description"] = "Deleted successfully" },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["404"] = new Dictionary<string, object> { ["description"] = "Not found", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildRegenerateOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "Credentials" },
                ["summary"] = "Regenerate bearer token for credential",
                ["description"] = "Generates a new bearer token for the specified credential. The old token will no longer be valid.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = "guid",
                        ["in"] = "path",
                        ["required"] = true,
                        ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }
                    }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Token regenerated successfully",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/Credential" }
                            }
                        }
                    },
                    ["400"] = new Dictionary<string, object> { ["description"] = "Bad request (e.g., credential is read-only)", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["404"] = new Dictionary<string, object> { ["description"] = "Credential not found", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildHistoryListOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "History" },
                ["summary"] = "List request history",
                ["description"] = "Get request history with optional filtering by time range, endpoint, or origin.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object> { ["name"] = "skip", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer", ["default"] = 0 }, ["description"] = "Number of records to skip" },
                    new Dictionary<string, object> { ["name"] = "take", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer" }, ["description"] = "Maximum number of records to return" },
                    new Dictionary<string, object> { ["name"] = "start", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" }, ["description"] = "Start of time range filter" },
                    new Dictionary<string, object> { ["name"] = "end", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "date-time" }, ["description"] = "End of time range filter" },
                    new Dictionary<string, object> { ["name"] = "endpoint", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }, ["description"] = "Filter by endpoint GUID" },
                    new Dictionary<string, object> { ["name"] = "origin", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "string", ["format"] = "uuid" }, ["description"] = "Filter by origin GUID" }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Successful response",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "array",
                                    ["items"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/RequestHistory" }
                                }
                            }
                        }
                    },
                    ["400"] = new Dictionary<string, object> { ["description"] = "Bad request (e.g., invalid date format)", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildRecentHistoryOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "History" },
                ["summary"] = "Get recent request history",
                ["description"] = "Get the most recent request history records.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object> { ["name"] = "count", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer", ["default"] = 100, ["minimum"] = 1, ["maximum"] = 1000 }, ["description"] = "Number of records to return" }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Successful response",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "array",
                                    ["items"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/RequestHistory" }
                                }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildFailedHistoryOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "History" },
                ["summary"] = "Get failed request history",
                ["description"] = "Get request history for failed requests only.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object> { ["name"] = "skip", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer", ["default"] = 0 }, ["description"] = "Number of records to skip" },
                    new Dictionary<string, object> { ["name"] = "take", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer" }, ["description"] = "Maximum number of records to return" }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Successful response",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "array",
                                    ["items"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/RequestHistory" }
                                }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildHistoryStatsOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "History" },
                ["summary"] = "Get request history statistics",
                ["description"] = "Get aggregate statistics about request history including total requests, failed requests, and success rate.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Successful response",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/HistoryStats" }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildHistoryCleanupOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "History" },
                ["summary"] = "Run history cleanup",
                ["description"] = "Delete old request history records based on the specified number of days.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["parameters"] = new List<object>
                {
                    new Dictionary<string, object> { ["name"] = "days", ["in"] = "query", ["schema"] = new Dictionary<string, object> { ["type"] = "integer" }, ["description"] = "Delete records older than this many days" }
                },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Cleanup completed",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "object",
                                    ["properties"] = new Dictionary<string, object>
                                    {
                                        ["deletedRecords"] = new Dictionary<string, object> { ["type"] = "integer" }
                                    }
                                }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildHealthOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "System" },
                ["summary"] = "Health check",
                ["description"] = "Get the health status of the Switchboard server.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Server is healthy",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/HealthResponse" }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        private Dictionary<string, object> BuildMeOperation()
        {
            return new Dictionary<string, object>
            {
                ["tags"] = new List<string> { "System" },
                ["summary"] = "Get current user",
                ["description"] = "Get information about the currently authenticated user based on the bearer token.",
                ["security"] = new List<object> { new Dictionary<string, List<string>> { ["bearerAuth"] = new List<string>() } },
                ["responses"] = new Dictionary<string, object>
                {
                    ["200"] = new Dictionary<string, object>
                    {
                        ["description"] = "Successful response",
                        ["content"] = new Dictionary<string, object>
                        {
                            ["application/json"] = new Dictionary<string, object>
                            {
                                ["schema"] = new Dictionary<string, object>
                                {
                                    ["type"] = "object",
                                    ["properties"] = new Dictionary<string, object>
                                    {
                                        ["guid"] = new Dictionary<string, object> { ["type"] = "string" },
                                        ["username"] = new Dictionary<string, object> { ["type"] = "string" },
                                        ["email"] = new Dictionary<string, object> { ["type"] = "string" },
                                        ["firstName"] = new Dictionary<string, object> { ["type"] = "string" },
                                        ["lastName"] = new Dictionary<string, object> { ["type"] = "string" },
                                        ["isAdmin"] = new Dictionary<string, object> { ["type"] = "boolean" },
                                        ["active"] = new Dictionary<string, object> { ["type"] = "boolean" }
                                    }
                                }
                            }
                        }
                    },
                    ["401"] = new Dictionary<string, object> { ["description"] = "Unauthorized", ["content"] = new Dictionary<string, object> { ["application/json"] = new Dictionary<string, object> { ["schema"] = new Dictionary<string, object> { ["$ref"] = "#/components/schemas/ApiErrorResponse" } } } }
                }
            };
        }

        #endregion
    }
}
