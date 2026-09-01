using LoyaltyCloud.Application.Customers;
using Xunit;

namespace LoyaltyCloud.Tests.Application;

public sealed class CustomerNormalizationTests
{
    [Theory]
    [InlineData("6461234567", "6461234567")]
    [InlineData("646 123 4567", "6461234567")]
    [InlineData("646-123-4567", "6461234567")]
    [InlineData("(646) 123-4567", "6461234567")]
    public void Phone_normalizer_compares_common_local_formats(string input, string expected)
    {
        Assert.Equal(expected, CustomerPhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("Daniel Chavez", "Daniel", "Chavez")]
    [InlineData("daniel chavez", "Daniel", "Chavez")]
    [InlineData("DANIEL CHAVEZ", "Daniel", "Chavez")]
    [InlineData(" Daniel   Chavez ", "Daniel", "Chavez")]
    [InlineData("José García", "Jose", "Garcia")]
    public void Name_normalizer_matches_case_spaces_and_accents(
        string existingFullName,
        string firstName,
        string lastName)
    {
        Assert.True(CustomerNameNormalizer.Matches(existingFullName, firstName, lastName));
    }

    [Theory]
    [InlineData("Daniel Chavez", "Danny", "Chavez")]
    [InlineData("Daniel Chavez", "Daniel", "Chaves")]
    public void Name_normalizer_does_not_do_fuzzy_matching(
        string existingFullName,
        string firstName,
        string lastName)
    {
        Assert.False(CustomerNameNormalizer.Matches(existingFullName, firstName, lastName));
    }
}
