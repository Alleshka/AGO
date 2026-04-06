namespace Ago.Core
{
    public static class SHaredHttpClient
    {
        private static readonly Lazy<HttpClient> _lazyHttpClient = new(() =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // Set a long timeout for LLM interactions
            };
            return client;
        });

        public static HttpClient Instance => _lazyHttpClient.Value;
    }
}
