﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using HalKit.Http;
using HalKit.Json;
using Moq;
using Xunit;

namespace HalKit.Tests.Http
{
    public class ApiResponseFactoryTests
    {
        private static ApiResponseFactory CreateFactory(
            IJsonSerializer serializer = null,
            IHalKitConfiguration config = null)
        {
            return new ApiResponseFactory(
                serializer ?? new Mock<IJsonSerializer>(MockBehavior.Loose).Object,
                config ?? new HalKitConfiguration(new Uri("http://foo.api.com")));
        }

        private class Foo
        {
        }

        public class TheCreateApiResponseAsyncMethod
        {
            public static readonly List<object[]> JsonContentTypes = new List<object[]>
            {
                new object[] {"application/hal+json"},
                new object[] {"application/json"},
                new object[] {"text/json"}
            };

            [Fact]
            public async void ShouldReturnApiResponseWithStatusCodeSetToTheResponseStatusCode()
            {
                var expectedStatusCode = HttpStatusCode.PartialContent;
                var factory = CreateFactory();

                var actualResponse = await factory.CreateApiResponseAsync<Foo>(new HttpResponseMessage { StatusCode = expectedStatusCode });

                Assert.Equal(expectedStatusCode, actualResponse.StatusCode);
            }

            [Fact]
            public async void ShouldReturnApiResponseWithNullBody_WhenResponseHasNoContent()
            {
                var factory = CreateFactory();

                var actualResponse = await factory.CreateApiResponseAsync<Foo>(new HttpResponseMessage());

                Assert.Null(actualResponse.Body);
                Assert.Null(actualResponse.BodyAsObject);
            }

            [Fact]
            public async void ShouldReturnApiResponseWithNullBodyAsObject_WhenResponseStatusCodeIsNotSuccess()
            {
                var responseContent = new StringContent("{ \"id\": 1}");
                var factory = CreateFactory();

                var actualResponse = await factory
                    .CreateApiResponseAsync<Foo>(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.NotFound,
                        Content = responseContent
                    });

                Assert.NotNull(actualResponse.Body);
                Assert.Null(actualResponse.BodyAsObject);
            }

            [Fact]
            public async void ShouldReturnApiResponseWithNullBody_WhenResponseIsByteArray()
            {
                var responseContent = new ByteArrayContent(Encoding.UTF8.GetBytes("abcdefg"));
                var factory = CreateFactory();

                var actualResponse = await factory.CreateApiResponseAsync<byte[]>(new HttpResponseMessage() { Content = responseContent });

                Assert.Null(actualResponse.Body);
            }

            [Fact]
            public async void ShouldReturnApiResponseWithBodyAsObjectSetToTheResponseContentBytes_WhenResponseIsByteArray()
            {
                var expectedBodyAsObject = Encoding.UTF8.GetBytes("expectedBytes");
                var responseContent = new ByteArrayContent(expectedBodyAsObject);
                var factory = CreateFactory();

                var actualResponse = await factory.CreateApiResponseAsync<byte[]>(new HttpResponseMessage() { Content = responseContent });

                Assert.Equal(expectedBodyAsObject, actualResponse.BodyAsObject);
            }

            [Fact]
            public async void ShouldReturnApiResponseWithBodySetToResponseContentString_WhenResponseIsNotByteArray()
            {
                var expectedBody = "{\"some_prop\":71}";
                var responseContent = new StringContent(expectedBody);
                var factory = CreateFactory();

                var actualResponse = await factory.CreateApiResponseAsync<Foo>(new HttpResponseMessage() { Content = responseContent });

                Assert.Equal(expectedBody, actualResponse.Body);
            }

            [Fact]
            public async void ShouldReturnApiResponseWithBodyAsObjectSetToNull_WhenResponseIsNotByteArray_AndContentTypeIsNotJson()
            {
                var responseContent = new StringContent("{}", Encoding.UTF8, "application/xml");
                var factory = CreateFactory();

                var actualResponse = await factory.CreateApiResponseAsync<Foo>(new HttpResponseMessage() { Content = responseContent });

                Assert.Null(actualResponse.BodyAsObject);
            }

            [Fact]
            public async void ShouldNotCallSerializer_WhenResponseIsNotByteArray_AndContentTypeIsNotJson()
            {
                var mockSerializer = new Mock<IJsonSerializer>(MockBehavior.Loose);
                var responseContent = new StringContent("{}", Encoding.UTF8, "application/xml");
                var factory = CreateFactory(serializer: mockSerializer.Object);

                await factory.CreateApiResponseAsync<Foo>(new HttpResponseMessage() { Content = responseContent });

                mockSerializer.Verify(j => j.Deserialize<Foo>(It.IsAny<string>()), Times.Never());
            }

            [Theory, MemberData(nameof(JsonContentTypes))]
            public async void ShouldPassResponseContentStringToJsonSerializer_WhenResponseIsNotByteArray_AndContentTypeIsJson(
                string jsonContentType)
            {
                var expectedJsonText = "{\"id\": 7}";
                var responseContent = new StringContent(expectedJsonText, Encoding.UTF8, jsonContentType);
                var mockSerializer = new Mock<IJsonSerializer>(MockBehavior.Loose);
                mockSerializer.Setup(j => j.Deserialize<Foo>(expectedJsonText))
                              .Returns(new Foo())
                              .Verifiable();
                var factory = CreateFactory(serializer: mockSerializer.Object);

                await factory.CreateApiResponseAsync<Foo>(new HttpResponseMessage() { Content = responseContent });

                mockSerializer.Verify();
            }

            [Theory, MemberData(nameof(JsonContentTypes))]
            public async void ShouldReturnApiResponseWithBodyAsObjectSetToResultDeserializedByTheJsonSerializer_WhenResponseIsNotByteArray_AndContentTypeIsJson(
                string jsonContentType)
            {
                var expectedBodyAsObject = new Foo();
                var responseContent = new StringContent("{}", Encoding.UTF8, jsonContentType);
                var mockSerializer = new Mock<IJsonSerializer>(MockBehavior.Loose);
                mockSerializer.Setup(j => j.Deserialize<Foo>(It.IsAny<string>())).Returns(expectedBodyAsObject);
                var factory = CreateFactory(serializer: mockSerializer.Object);

                var actualResponse = await factory.CreateApiResponseAsync<Foo>(new HttpResponseMessage() { Content = responseContent });

                Assert.Same(expectedBodyAsObject, actualResponse.BodyAsObject);
            }
        }
    }
}
