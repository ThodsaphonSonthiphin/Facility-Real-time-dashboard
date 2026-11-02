using System.Net;
using System.Net.Sockets;

namespace FacilityRealtime.Application.Auth;

/// <summary>
/// facility-0104 decision 7: the place the Login Lock counts. An IPv4 address counts as itself. An IPv6 address counts by
/// its /64, the block a network hands one phone or one home, so an attacker cannot change address on every request.
/// </summary>
public static class LoginPlace
{
    public static string Of(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }
}
