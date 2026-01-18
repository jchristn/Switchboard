namespace Test.UrlRewrite
{
    using Switchboard.Core;

    public static class Program
    {
        private static List<string> _TestUrls = new List<string>
        {
            "/foo",
            "/bar",
            "/v1.0/users",
            "/v1.0/users/foo",
            "/v1.0/people",
            "/v1.0/people/helloworld",
        };

        public static void Main(string[] args)
        {
            ApiEndpoint endpoint = new ApiEndpoint
            {
                Identifier = "testendpoint",
                Name = "Test Endpoint",
                LoadBalancing = LoadBalancingMode.RoundRobin,
                Unauthenticated = new ApiEndpointGroup
                {
                    ParameterizedUrls = new Dictionary<string, List<string>>
                    {
                        { "GET", new List<string> { "/test" } }
                    }
                },
                OriginServers = new List<string>
                {
                    "server1",
                    "server2",
                    "server3"
                },
                RewriteUrls = new Dictionary<string, Dictionary<string, string>>
                {
                    { "GET", new Dictionary<string, string>
                        {
                            { "/v1.0/users/{UserGuid}", "/rewritten-users/{UserGuid}" },
                            { "/v1.0/people/{UnusedGuid}", "/rewritten-people/unused" }
                        } 
                    }
                }
            };

            foreach (string url in _TestUrls)
            {
                Console.WriteLine("Original  : " + url);
                Console.WriteLine("Rewritten : " + UrlTools.RewriteUrl("GET", url, endpoint));
            }
        }
    }
}