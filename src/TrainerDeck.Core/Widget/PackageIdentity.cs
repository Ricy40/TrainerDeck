using System.Security.Cryptography;
using System.Text;

namespace TrainerDeck.Core.Widget;


public static class PackageIdentity
{
    public const string WidgetName = "TrainerDeck.GameBarWidget";
    public const string Publisher = "CN=Trainer Deck";

    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    public static string WidgetFamilyName => FamilyName(WidgetName, Publisher);

    public static string FamilyName(string name, string publisher) => $"{name}_{PublisherId(publisher)}";

    public static string PublisherId(string publisher)
    {
        byte[] hash = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));
        ulong bits = 0;
        for (int i = 0; i < 8; i++)
        {
            bits = (bits << 8) | hash[i];
        }

        var sb = new StringBuilder(13);
        for (int i = 0; i < 13; i++)
        {
            int shift = 64 - 5 * (i + 1);
            int index = shift >= 0 ? (int)((bits >> shift) & 0x1F) : (int)((bits << -shift) & 0x1F);
            sb.Append(Alphabet[index]);
        }

        return sb.ToString();
    }
}
