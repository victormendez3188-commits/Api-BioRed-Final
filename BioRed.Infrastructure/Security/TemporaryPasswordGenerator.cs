using System.Security.Cryptography;

namespace BioRed.Infrastructure.Security;

internal sealed class TemporaryPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Specials = "@$!%*?&#+-";
    private const int PasswordLength = 16;

    public string Generate()
    {
        var allowed = Uppercase + Lowercase + Digits + Specials;
        var characters = new List<char>(PasswordLength)
        {
            Pick(Uppercase),
            Pick(Lowercase),
            Pick(Digits),
            Pick(Specials)
        };

        while (characters.Count < PasswordLength)
        {
            characters.Add(Pick(allowed));
        }

        for (var index = characters.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) =
                (characters[swapIndex], characters[index]);
        }

        return new string(characters.ToArray());
    }

    private static char Pick(string characters) =>
        characters[RandomNumberGenerator.GetInt32(characters.Length)];
}
