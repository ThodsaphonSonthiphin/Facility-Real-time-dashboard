using System.Net;
using JanitorialManagementDashboard.Application.Auth;

namespace JanitorialManagementDashboard.UnitTests;

/// <summary>facility-0104 decision 7: what the Login Lock counts as one place.</summary>
public class LoginPlaceTests
{
    private static string PlaceOf(string address) => LoginPlace.Of(IPAddress.Parse(address));

    [Fact]
    public void An_ipv4_address_counts_as_itself()
    {
        Assert.Equal("203.0.113.7", PlaceOf("203.0.113.7"));
        Assert.NotEqual(PlaceOf("203.0.113.7"), PlaceOf("203.0.113.8"));
    }

    [Fact]
    public void Ipv6_addresses_count_by_their_64_bit_network()
    {
        Assert.Equal("2001:db8:1:2::/64", PlaceOf("2001:db8:1:2:aaaa:bbbb:cccc:1"));
        Assert.Equal(PlaceOf("2001:db8:1:2:aaaa:bbbb:cccc:1"), PlaceOf("2001:db8:1:2::9"));
        Assert.NotEqual(PlaceOf("2001:db8:1:2::1"), PlaceOf("2001:db8:1:3::1"));
    }

    [Fact]
    public void An_ipv4_address_carried_in_ipv6_counts_as_the_ipv4_address()
    {
        Assert.Equal("203.0.113.7", PlaceOf("::ffff:203.0.113.7"));
    }

    [Fact]
    public void A_request_with_no_address_counts_as_one_place()
    {
        Assert.Equal("unknown", LoginPlace.Of(null));
    }
}
