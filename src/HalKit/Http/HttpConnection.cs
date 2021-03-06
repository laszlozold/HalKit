﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HalKit.Json;

namespace HalKit.Http
{
    public class HttpConnection : IHttpConnection
    {
        private readonly HttpClient _httpClient;
        private readonly IHalKitConfiguration _configuration;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApiResponseFactory _responseFactory;

        public HttpConnection(IEnumerable<DelegatingHandler> handlers,
                              IHalKitConfiguration configuration)
            : this(handlers,
                   configuration,
                   new DefaultJsonSerializer())
        {
        }

        public HttpConnection(
            IEnumerable<DelegatingHandler> handlers,
            IHalKitConfiguration configuration,
            IJsonSerializer jsonSerializer)
            : this(handlers,
                   configuration,
                   new HttpClientFactory(),
                   jsonSerializer,
                   new ApiResponseFactory(jsonSerializer, configuration))
        {
        }

        public HttpConnection(IEnumerable<DelegatingHandler> handlers,
                              IHalKitConfiguration configuration,
                              IHttpClientFactory httpClientFactory,
                              IJsonSerializer jsonSerializer,
                              IApiResponseFactory responseFactory)
        {
            Requires.ArgumentNotNull(handlers, nameof(handlers));
            Requires.ArgumentNotNull(configuration, nameof(configuration));
            Requires.ArgumentNotNull(httpClientFactory, nameof(httpClientFactory));
            Requires.ArgumentNotNull(jsonSerializer, nameof(jsonSerializer));
            Requires.ArgumentNotNull(responseFactory, nameof(responseFactory));

            _httpClient = httpClientFactory.CreateClient(handlers);
            _configuration = configuration;
            _jsonSerializer = jsonSerializer;
            _responseFactory = responseFactory;
        }

        public async Task<IApiResponse<T>> SendRequestAsync<T>(
            Uri uri,
            HttpMethod method,
            object body,
            IDictionary<string, IEnumerable<string>> headers,
            CancellationToken cancellationToken)
        {
            Requires.ArgumentNotNull(uri, nameof(uri));
            Requires.ArgumentNotNull(method, nameof(method));

            using (var request = new HttpRequestMessage {RequestUri = uri, Method = method})
            {
                var contentType = "application/hal+json";
                headers = headers ?? new Dictionary<string, IEnumerable<string>>();
                foreach (var header in headers)
                {
                    if (header.Key == "Content-Type")
                    {
                        contentType = header.Value.FirstOrDefault();
                        continue;
                    }

                    request.Headers.Add(header.Key, header.Value);
                }
                request.Content = GetRequestContent(method, body, contentType);

                var responseMessage = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(Configuration);
                return await _responseFactory.CreateApiResponseAsync<T>(responseMessage).ConfigureAwait(Configuration);
            }
        }

        public Task<IApiResponse<T>> SendRequestAsync<T>(IApiRequest apiRequest, CancellationToken cancellationToken)
        {
            return SendRequestAsync<T>(apiRequest.Uri, apiRequest.Method, apiRequest.Body, apiRequest.Headers, cancellationToken);
        }

        private HttpContent GetRequestContent(
            HttpMethod method,
            object body,
            string contentType)
        {
            if (method == HttpMethod.Get || body == null)
            {
                return null;
            }

            var bodyContent = body as HttpContent;
            if (bodyContent != null)
            {
                return bodyContent;
            }

            var bodyString = body as string;
            if (bodyString != null)
            {
                return new StringContent(bodyString, Encoding.UTF8, contentType);
            }

            var bodyStream = body as Stream;
            if (bodyStream != null)
            {
                var streamContent = new StreamContent(bodyStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                return streamContent;
            }

            // Anything else gets serialized to JSON
            var bodyJson = _jsonSerializer.Serialize(body);
            return new StringContent(bodyJson, Encoding.UTF8, contentType);
        }

        public IHalKitConfiguration Configuration => _configuration;
        public HttpClient Client => _httpClient;
    }
}