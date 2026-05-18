using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using Polly.Timeout;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class GlobalExceptionPolicyTests
{
    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(SocketException))]
    [InlineData(typeof(WebSocketException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(OperationCanceledException))]
    public void IsRecoverableNetworkException_ReturnsTrueForNetworkAndTimeoutTypes(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        var result = GlobalExceptionPolicy.IsRecoverableNetworkException(exception);

        Assert.True(result);
    }

    [Fact]
    public void IsRecoverableNetworkException_ReturnsTrueForPollyTimeoutRejectedException()
    {
        var exception = new TimeoutRejectedException("timeout");

        var result = GlobalExceptionPolicy.IsRecoverableNetworkException(exception);

        Assert.True(result);
    }

    [Fact]
    public void IsRecoverableNetworkException_ReturnsTrueForNestedNetworkExceptions()
    {
        var exception = new AggregateException(new InvalidOperationException("outer"), new HttpRequestException("inner", new SocketException()));

        var result = GlobalExceptionPolicy.IsRecoverableNetworkException(exception);

        Assert.True(result);
    }

    [Fact]
    public void IsRecoverableNetworkException_ReturnsFalseForNonNetworkExceptions()
    {
        var exception = new InvalidOperationException("boom");

        var result = GlobalExceptionPolicy.IsRecoverableNetworkException(exception);

        Assert.False(result);
    }
}
