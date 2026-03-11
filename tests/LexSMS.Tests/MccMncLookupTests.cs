using LexSMS.Helpers;
using Xunit;

namespace LexSMS.Tests;

public class MccMncLookupTests
{
    [Theory]
    [InlineData(460, "中国")]
    [InlineData(440, "日本")]
    [InlineData(450, "韩国")]
    [InlineData(310, "美国")]
    [InlineData(234, "英国")]
    [InlineData(262, "德国")]
    public void GetCountryName_ReturnsCorrectCountry(int mcc, string expectedCountry)
    {
        var country = MccMncLookup.GetCountryName(mcc);

        Assert.Equal(expectedCountry, country);
    }

    [Fact]
    public void GetCountryName_ReturnsUnknown_ForInvalidMcc()
    {
        var country = MccMncLookup.GetCountryName(999);

        Assert.Equal("未知", country);
    }

    [Theory]
    [InlineData(460, 0, "中国移动")]
    [InlineData(460, 1, "中国联通")]
    [InlineData(460, 3, "中国电信")]
    [InlineData(460, 15, "中国广电")]
    [InlineData(440, 0, "NTT DoCoMo")]
    [InlineData(450, 0, "SK Telecom")]
    public void GetOperatorName_ReturnsCorrectOperator(int mcc, int mnc, string expectedOperator)
    {
        var operatorName = MccMncLookup.GetOperatorName(mcc, mnc);

        Assert.Equal(expectedOperator, operatorName);
    }

    [Fact]
    public void GetOperatorName_ReturnsUnknown_ForUnknownMccMnc()
    {
        var operatorName = MccMncLookup.GetOperatorName(999, 99);

        Assert.Equal("未知运营商", operatorName);
    }

    [Fact]
    public void GetCellInfoDescription_ReturnsFormattedString()
    {
        var description = MccMncLookup.GetCellInfoDescription(460, 0, 12345, 67890);

        Assert.Contains("中国", description);
        Assert.Contains("中国移动", description);
    }

    [Theory]
    [InlineData(460, 0, "中国", "中国移动")]
    [InlineData(460, 1, "中国", "中国联通")]
    [InlineData(460, 3, "中国", "中国电信")]
    public void GetCellInfoDescription_ContainsCountryAndOperator(int mcc, int mnc, string expectedCountry, string expectedOperator)
    {
        var description = MccMncLookup.GetCellInfoDescription(mcc, mnc, 0, 0);

        Assert.Contains(expectedCountry, description);
        Assert.Contains(expectedOperator, description);
    }
}
