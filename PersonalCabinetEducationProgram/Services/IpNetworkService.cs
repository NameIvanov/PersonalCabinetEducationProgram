using System.Net;
using System.Net.Sockets;

namespace PersonalCabinetEducationProgram.Services;

public sealed record IpNetworkInfo(
    IPAddress Address,
    string IpAddress,
    string NetworkAddress,
    int PrefixLength,
    bool IsLocal)
{
    public string Cidr => $"{NetworkAddress}/{PrefixLength}";
}

public interface IIpNetworkService
{
    bool TryGetNetwork(IPAddress? address, out IpNetworkInfo network);
}

public sealed class IpNetworkService : IIpNetworkService
{
    public bool TryGetNetwork(IPAddress? address, out IpNetworkInfo network)
    {
        network = null!;
        if (address == null)
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var prefixLength = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => 24,
            AddressFamily.InterNetworkV6 => 64,
            _ => 0
        };
        if (prefixLength == 0)
            return false;

        var bytes = address.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        if (remainingBits > 0)
            bytes[fullBytes] &= (byte)(0xff << (8 - remainingBits));
        for (var index = fullBytes + (remainingBits > 0 ? 1 : 0); index < bytes.Length; index++)
            bytes[index] = 0;

        var networkAddress = new IPAddress(bytes);
        network = new IpNetworkInfo(
            address,
            address.ToString(),
            networkAddress.ToString(),
            prefixLength,
            !IpGeolocationService.IsPublicAddress(address));
        return true;
    }
}
