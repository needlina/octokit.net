﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NSubstitute;
using Octokit.Http;
using Octokit.Tests.Helpers;
using Xunit;
using Xunit.Extensions;

namespace Octokit.Tests.Http
{
    public class ConnectionTests
    {
        const string ExampleUrl = "http://example.com";
        static readonly Uri ExampleUri = new Uri(ExampleUrl);

        public class TheConstructor
        {
            [Fact]
            public void ThrowsForBadArguments()
            {
                var uri = new Uri("http://whatevs");
                var store = Substitute.For<ICredentialStore>();
                var httpClient = Substitute.For<IHttpClient>();
                var serializer = Substitute.For<IJsonSerializer>();
                // 1 param ctor
                Assert.Throws<ArgumentNullException>(() => new Connection((Uri)null));
                Assert.Throws<ArgumentNullException>(() => new Connection((ICredentialStore)null));

                // 2 param ctor
                Assert.Throws<ArgumentNullException>(() => new Connection(null, store));
                Assert.Throws<ArgumentNullException>(() => new Connection(uri, null));

                // 4 param ctor
                Assert.Throws<ArgumentNullException>(() => new Connection(null, store, httpClient, serializer));
                Assert.Throws<ArgumentNullException>(() => new Connection(uri, null, httpClient, serializer));
                Assert.Throws<ArgumentNullException>(() => new Connection(uri, store, null, serializer));
                Assert.Throws<ArgumentNullException>(() => new Connection(uri, store, httpClient, null));
            }

            [Fact]
            public void EnsuresAbsoluteBaseAddress()
            {
                Assert.Throws<ArgumentException>(() => new Connection(new Uri("/foo", UriKind.Relative)));
                Assert.Throws<ArgumentException>(() => new Connection(new Uri("/foo", UriKind.RelativeOrAbsolute)));
            }

            [Fact]
            public void CreatesConnectionWithBaseAddress()
            {
                var connection = new Connection(new Uri("https://github.com/"));
                Assert.Equal(new Uri("https://github.com/"), connection.BaseAddress);
            }
        }

        public class TheGetAsyncMethod
        {
            [Fact]
            public async Task SendsProperlyFormattedRequest()
            {
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string>();
                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                await connection.GetAsync<string>(new Uri("/endpoint", UriKind.Relative));

                httpClient.Received(1).Send<string>(Arg.Is<IRequest>(req =>
                    req.BaseAddress == ExampleUri &&
                        req.Method == HttpMethod.Get &&
                        req.Endpoint == new Uri("/endpoint", UriKind.Relative)));
            }

            [Fact]
            public async Task CanMakeMutipleRequestsWithSameConnection()
            {
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string>();
                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                await connection.GetAsync<string>(new Uri("/endpoint", UriKind.Relative));
                await connection.GetAsync<string>(new Uri("/endpoint", UriKind.Relative));
                await connection.GetAsync<string>(new Uri("/endpoint", UriKind.Relative));

                httpClient.Received(3).Send<string>(Arg.Is<IRequest>(req =>
                    req.BaseAddress == ExampleUri &&
                        req.Method == HttpMethod.Get &&
                        req.Endpoint == new Uri("/endpoint", UriKind.Relative)));
            }

            [Fact]
            public async Task ParsesApiInfoOnResponse()
            {
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string>
                {
                    Headers =
                    {
                        { "X-Accepted-OAuth-Scopes", "user" },
                    }
                };

                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                var resp = await connection.GetAsync<string>(new Uri("/endpoint", UriKind.Relative));
                Assert.NotNull(resp.ApiInfo);
                Assert.Equal("user", resp.ApiInfo.AcceptedOauthScopes.First());
            }

            [Theory]
            [InlineData(HttpStatusCode.Forbidden)]
            [InlineData(HttpStatusCode.Unauthorized)]
            public async Task ThrowsAuthenticationExceptionExceptionForAppropriateStatusCodes(HttpStatusCode statusCode)
            {
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string> { StatusCode = statusCode};
                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                var exception = await AssertEx.Throws<AuthenticationException>(
                    async () => await connection.GetAsync<string>(new Uri("/endpoint", UriKind.Relative)));

                Assert.Equal("You must be authenticated to call this method. Either supply a login/password or an " +
                             "oauth token.", exception.Message);
            }
        }

        public class TheGetHtmlMethod
        {
            [Fact]
            public async Task SendsProperlyFormattedRequestWithProperAcceptHeader()
            {
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string>();
                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                await connection.GetHtml(new Uri("/endpoint", UriKind.Relative));

                httpClient.Received(1).Send<string>(Arg.Is<IRequest>(req =>
                    req.BaseAddress == ExampleUri &&
                        req.Method == HttpMethod.Get &&
                        req.Headers["Accept"] == "application/vnd.github.html" &&
                        req.Endpoint == new Uri("/endpoint", UriKind.Relative)));
            }
        }

        public class ThePatchAsyncMethod
        {
            [Fact]
            public async Task RunsConfiguredAppWithAppropriateEnv()
            {
                string data = SimpleJson.SerializeObject(new object());
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string>();
                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                await connection.PatchAsync<string>(new Uri("/endpoint", UriKind.Relative), new object());

                httpClient.Received(1).Send<string>(Arg.Is<IRequest>(req =>
                    req.BaseAddress == ExampleUri &&
                        (string)req.Body == data &&
                        req.Method == HttpVerb.Patch &&
                        req.Endpoint == new Uri("/endpoint", UriKind.Relative)));
            }
        }

        public class ThePostAsyncMethod
        {
            [Fact]
            public async Task RunsConfiguredAppWithAppropriateEnv()
            {
                string data = SimpleJson.SerializeObject(new object());
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string>();
                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                await connection.PostAsync<string>(new Uri("/endpoint", UriKind.Relative), new object());

                httpClient.Received(1).Send<string>(Arg.Is<IRequest>(req =>
                    req.BaseAddress == ExampleUri &&
                        (string)req.Body == data &&
                        req.Method == HttpMethod.Post &&
                        req.Endpoint == new Uri("/endpoint", UriKind.Relative)));
            }
        }

        public class TheDeleteAsyncMethod
        {
            [Fact]
            public async Task RunsConfiguredAppWithAppropriateEnv()
            {
                var httpClient = Substitute.For<IHttpClient>();
                IResponse<string> response = new ApiResponse<string>();
                httpClient.Send<string>(Args.Request).Returns(Task.FromResult(response));
                var connection = new Connection(ExampleUri,
                    Substitute.For<ICredentialStore>(),
                    httpClient,
                    Substitute.For<IJsonSerializer>());

                await connection.DeleteAsync<string>(new Uri("/endpoint", UriKind.Relative));

                httpClient.Received(1).Send<string>(Arg.Is<IRequest>(req =>
                    req.BaseAddress == ExampleUri &&
                        req.Method == HttpMethod.Delete &&
                        req.Endpoint == new Uri("/endpoint", UriKind.Relative)));
            }
        }
    }
}
