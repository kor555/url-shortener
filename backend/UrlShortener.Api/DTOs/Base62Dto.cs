namespace UrlShortener.Api.DTOs;

public interface IBase62Service
{
    string Encode(long value);
    bool TryDecode(string code, out long value);
}
