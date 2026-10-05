using System.Security.Cryptography;
using System.Text;

namespace NepalHub.Api.Data.Seeding;

internal static class ReferenceDataIds
{
    private static readonly byte[] NamespaceBytes =
        Guid.Parse("9c1a4b2e-7d3f-4f58-8a6b-0d5e1f2c3a4b").ToByteArray();

    internal static Guid Province(string code) => Derive($"province:{code}");

    internal static Guid District(string code) => Derive($"district:{code}");

    internal static Guid Role(string name) => Derive($"role:{name}");

    internal static Guid Calculator(string slug) => Derive($"calculator:{slug}");

    internal static Guid Question(string code) => Derive($"question:{code}");

    internal static Guid QuestionOption(string code, int position) => Derive($"question-option:{code}:{position}");

    internal static Guid Resource(string code) => Derive($"resource:{code}");

    internal static Guid Job(string slug) => Derive($"job:{slug}");

    internal static Guid Business(string slug) => Derive($"business:{slug}");

    internal static Guid Deal(string slug) => Derive($"deal:{slug}");

    internal static Guid Event(string slug) => Derive($"event:{slug}");

    internal static Guid Badge(string code) => Derive($"badge:{code}");

    private static Guid Derive(string name)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var buffer = new byte[NamespaceBytes.Length + nameBytes.Length];
        NamespaceBytes.CopyTo(buffer, 0);
        nameBytes.CopyTo(buffer, NamespaceBytes.Length);

        var hash = SHA1.HashData(buffer);
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
