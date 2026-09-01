using System.Globalization;
using System.Text;

namespace LoyaltyCloud.Application.Customers;

public static class CustomerNameNormalizer
{
    public static string NormalizeFullName(string firstName, string lastName) =>
        Normalize($"{firstName} {lastName}");

    public static bool Matches(string existingFullName, string firstName, string lastName) =>
        string.Equals(
            Normalize(existingFullName),
            NormalizeFullName(firstName, lastName),
            StringComparison.Ordinal);

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            builder.Append(char.ToUpperInvariant(ch));
            previousWasSpace = false;
        }

        return builder.ToString().TrimEnd().Normalize(NormalizationForm.FormC);
    }
}
