using MeridianVoteTracker.Core.Ingestion;

namespace MeridianVoteTracker.Core.Tests;

public class CsvLineParserTests
{
    [Fact]
    public void Splits_a_plain_line()
    {
        Assert.Equal(["a", "b", "c"], CsvLineParser.Parse("a,b,c"));
    }

    [Fact]
    public void A_quoted_field_may_contain_commas()
    {
        var fields = CsvLineParser.Parse("MA/2024/001,79,2024-01-12,\"Desertification, Including Capacity Building\",AFG");
        Assert.Equal(5, fields.Length);
        Assert.Equal("Desertification, Including Capacity Building", fields[3]);
    }

    [Fact]
    public void Doubled_quotes_inside_quotes_are_one_literal_quote()
    {
        Assert.Equal(["say \"hi\"", "x"], CsvLineParser.Parse("\"say \"\"hi\"\"\",x"));
    }

    [Fact]
    public void An_unquoted_comma_in_a_title_shifts_the_columns()
    {
        var fields = CsvLineParser.Parse("MA/2020/001,75,2020-01-01,Peace, Security and Trade,FRA,France,Y,2020-01-03T09:00:00Z");
        Assert.Equal(9, fields.Length);
    }
}

public class RowValidatorTests
{
    private static RowValidationResult Validate(string line) =>
        new RowValidator(TestData.Members()).Validate(TestData.Raw(2, line));

    [Fact]
    public void A_correct_row_is_valid_and_keeps_its_values_exactly()
    {
        var result = Validate(TestData.Line("MA/2024/101", "BRA", "Y"));

        Assert.True(result.IsValid);
        Assert.Equal("MA/2024/101", result.Row!.VoteId);
        Assert.Equal("BRA", result.Row.CountryCode);
        Assert.Equal(new DateOnly(2024, 1, 10), result.Row.VoteDate);
        Assert.Equal("Maritime Safety", result.Row.Title);
    }

    [Fact]
    public void A_row_with_the_wrong_number_of_columns_is_invalid()
    {
        var result = Validate("MA/2024/022,AGO;N");

        Assert.False(result.IsValid);
        Assert.Contains("Expected 8 columns but found 2", result.Error);
    }

    [Theory]
    [InlineData("Yes")]
    [InlineData("YES")]
    [InlineData("y")]
    [InlineData("0")]
    [InlineData("?")]
    [InlineData("")]
    public void A_ballot_that_is_not_exactly_Y_N_A_or_X_is_invalid(string ballot)
    {
        var result = Validate(TestData.Line("MA/2024/101", "BRA", ballot));

        Assert.False(result.IsValid);
        Assert.Contains("ballot", result.Error);
    }

    [Fact]
    public void A_lower_case_country_code_is_rejected_and_not_normalised()
    {
        var result = Validate(TestData.Line("MA/2024/101", "deu", "Y"));

        Assert.False(result.IsValid);
        Assert.Contains("'deu' is not in the member-state list", result.Error);
    }

    [Fact]
    public void An_unknown_country_code_is_invalid()
    {
        Assert.False(Validate(TestData.Line("MA/2024/104", "FRN", "X")).IsValid);
        Assert.False(Validate(TestData.Line("MA/2024/104", "XXX", "X")).IsValid);
    }

    [Theory]
    [InlineData("21/02/2024")]
    [InlineData("2024-13-01")]
    [InlineData("2024-02-30")]
    [InlineData("2024-2-1")]
    [InlineData("")]
    public void A_date_that_is_not_yyyy_mm_dd_is_invalid(string date)
    {
        var result = Validate(TestData.Line("MA/2024/027", "BRA", "Y", date: date));

        Assert.False(result.IsValid);
        Assert.Contains("vote_date", result.Error);
    }

    [Fact]
    public void A_missing_title_is_invalid()
    {
        Assert.False(Validate(TestData.Line("MA/2024/101", "BRA", "Y", title: "")).IsValid);
    }

    [Fact]
    public void A_malformed_vote_id_is_invalid()
    {
        Assert.False(Validate(TestData.Line("101", "BRA", "Y")).IsValid);
    }

    [Theory]
    [InlineData("2021-06-30", true)]   // the last day of membership still counts
    [InlineData("2021-07-01", false)]  // the day after, New Zealand is no longer a member
    public void Membership_ends_on_the_last_member_day(string date, bool expectedValid)
    {
        Assert.Equal(expectedValid, Validate(TestData.Line("MA/2021/001", "NZL", "Y", date: date)).IsValid);
    }

    [Theory]
    [InlineData("2019-12-31", true)]
    [InlineData("2020-06-01", false)]  // Iceland's gap
    [InlineData("2021-12-31", false)]
    [InlineData("2022-01-01", true)]   // Iceland's second membership
    public void A_country_with_two_membership_periods_is_checked_against_both(string date, bool expectedValid)
    {
        Assert.Equal(expectedValid, Validate(TestData.Line("MA/2020/001", "ISL", "Y", date: date)).IsValid);
    }

    [Fact]
    public void A_country_that_joined_later_is_invalid_before_it_joined()
    {
        Assert.False(Validate(TestData.Line("MA/2022/001", "TLS", "Y", date: "2022-12-31")).IsValid);
        Assert.True(Validate(TestData.Line("MA/2023/001", "TLS", "Y", date: "2023-01-01")).IsValid);
    }

    [Fact]
    public void All_problems_of_a_row_are_reported_together()
    {
        var result = Validate(TestData.Line("MA/2024/101", "FRN", "Yes", date: "21/02/2024"));

        Assert.False(result.IsValid);
        Assert.Contains("vote_date", result.Error);
        Assert.Contains("country_code", result.Error);
        Assert.Contains("ballot", result.Error);
    }

    [Fact]
    public void A_malformed_published_at_is_invalid()
    {
        var result = Validate("MA/2024/101,79,2024-01-10,Maritime Safety,BRA,BRA,Y,Y");

        Assert.False(result.IsValid);
        Assert.Contains("published_at", result.Error);
    }
}
