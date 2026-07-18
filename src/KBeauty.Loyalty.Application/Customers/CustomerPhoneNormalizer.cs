namespace KBeauty.Loyalty.Application.Customers;

public static class CustomerPhoneNormalizer
{
    public static string Normalize(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Trim();
    }
}
