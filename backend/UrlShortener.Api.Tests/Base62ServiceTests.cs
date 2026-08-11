using UrlShortener.Api.Services;

namespace UrlShortener.Api.Tests;

public class Base62ServiceTests
{
    private readonly Base62Service _base62 = new();

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(10, "A")]
    [InlineData(35, "Z")]
    [InlineData(36, "a")]
    [InlineData(61, "z")]
    [InlineData(62, "10")]
    [InlineData(12345, "3D7")]
    public void Encode_ProducesExpectedBase62String(long value, string expected)
    {
        Assert.Equal(expected, _base62.Encode(value));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("A", 10)]
    [InlineData("Z", 35)]
    [InlineData("a", 36)]
    [InlineData("z", 61)]
    [InlineData("10", 62)]
    [InlineData("3D7", 12345)]
    public void TryDecode_ProducesExpectedValue(string code, long expected)
    {
        var success = _base62.TryDecode(code, out var value);

        Assert.True(success);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("has spaces")]
    [InlineData("!!!")]
    [InlineData("café")]
    public void TryDecode_RejectsInvalidInput(string code)
    {
        var success = _base62.TryDecode(code, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryDecode_RejectsNull()
    {
        var success = _base62.TryDecode(null!, out var value);

        Assert.False(success);
        Assert.Equal(0, value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(61)]
    [InlineData(62)]
    [InlineData(999999)]
    public void EncodeThenDecode_RoundTrips(long value)
    {
        var encoded = _base62.Encode(value);

        var success = _base62.TryDecode(encoded, out var decoded);

        Assert.True(success);
        Assert.Equal(value, decoded);
    }
}
