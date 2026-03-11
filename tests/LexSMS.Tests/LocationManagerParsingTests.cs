using System.Reflection;
using LexSMS.Features;
using LexSMS.Models;
using Xunit;

namespace LexSMS.Tests;

public class LocationManagerParsingTests
{
    private static readonly MethodInfo s_parseClbsResponse = typeof(LocationManager)
        .GetMethod("ParseClbsResponse", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_parseClbsAddressResponse = typeof(LocationManager)
        .GetMethod("ParseClbsAddressResponse", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void ParseClbsResponse_SetsIsValid_WhenCoordinatesAreReturned()
    {
        var location = new CellLocation();

        s_parseClbsResponse.Invoke(null, ["0,39.904200,116.407400,150", location]);

        Assert.True(location.IsValid);
    }

    [Fact]
    public void ParseClbsResponse_ParsesLatitude_FromClbsMode1()
    {
        var location = new CellLocation();

        s_parseClbsResponse.Invoke(null, ["0,39.904200,116.407400,150", location]);

        Assert.Equal(39.9042d, location.Latitude, 4);
    }

    [Fact]
    public void ParseClbsAddressResponse_ParsesAddress_WhenCodeIsZero()
    {
        var location = new CellLocation();

        s_parseClbsAddressResponse.Invoke(null, ["0,\"北京市东城区东华门街道\"", location]);

        Assert.Equal("北京市东城区东华门街道", location.Address);
    }

    [Fact]
    public void ParseClbsAddressResponse_SetsIsValid_WhenAddressExists()
    {
        var location = new CellLocation();

        s_parseClbsAddressResponse.Invoke(null, ["0,\"北京市东城区东华门街道\"", location]);

        Assert.True(location.IsValid);
    }

    [Fact]
    public void ParseClbsAddressResponse_DecodesUcs2_WhenHexString()
    {
        var location = new CellLocation();

        // "北京市" 的 UCS2 编码: 5317 4EAC 5E02
        s_parseClbsAddressResponse.Invoke(null, ["0,\"53174EAC5E02\"", location]);

        Assert.Equal("北京市", location.Address);
    }

    [Fact]
    public void ParseClbsAddressResponse_KeepsOriginal_WhenNotHexString()
    {
        var location = new CellLocation();

        s_parseClbsAddressResponse.Invoke(null, ["0,\"Beijing City\"", location]);

        Assert.Equal("Beijing City", location.Address);
    }

    [Fact]
    public void ParseClbsAddressResponse_KeepsOriginal_WhenUcs2DecodingFails()
    {
        var location = new CellLocation();

        // 奇数长度的十六进制字符串（不是有效的 UCS2）
        s_parseClbsAddressResponse.Invoke(null, ["0,\"ABC\"", location]);

        Assert.Equal("ABC", location.Address);
    }
}
