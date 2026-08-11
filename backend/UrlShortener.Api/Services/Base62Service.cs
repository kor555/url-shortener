using UrlShortener.Api.DTOs;

namespace UrlShortener.Api.Services;

public class Base62Service : IBase62Service
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public string Encode(long value)
    {
        if (value == 0) return Alphabet[0].ToString();

        var chars = new Stack<char>();
        while (value > 0)
        {
            chars.Push(Alphabet[(int)(value % 62)]);
            value /= 62;
        }
        return new string(chars.ToArray());
    }

    // Rejects unknown characters instead of silently mapping them, so bad codes 404 cleanly.
    public bool TryDecode(string code, out long value)
    {
        value = 0;
        if (string.IsNullOrEmpty(code)) return false;

        foreach (var c in code)
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0) return false;
            value = value * 62 + index;
        }
        return true;
    }
}
