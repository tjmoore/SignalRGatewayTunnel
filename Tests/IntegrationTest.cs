using Microsoft.Extensions.Logging;

namespace Tests
{
    public class IntegrationTest
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        [Test]
        public async Task GetWebResourceRootReturnsOkStatusCode()
        {
            // Arrange
            using var cts = new CancellationTokenSource(DefaultTimeout);
            var cancellationToken = cts.Token;
            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();
            appHost.Services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                // Override the logging filters from the app's configuration
                logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
                logging.AddFilter("Aspire.", LogLevel.Debug);
            });
            appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
            {
                clientBuilder.AddStandardResilienceHandler();
            });

            await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
            await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

            // Act
            using var httpClient = app.CreateHttpClient("frontend");
            await app.ResourceNotifications.WaitForResourceHealthyAsync("frontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
            await app.ResourceNotifications.WaitForResourceHealthyAsync("backend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
            using var response = await httpClient.GetAsync("/", cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(content, Does.Contain("This is the backend destination web app"));
            }
        }
    }
}
