using OpenEmrNext.PatientManagement.Api.Persistence;

namespace OpenEmrNext.PatientManagement.UnitTests.Persistence;

public class Rfc4122GuidConverterTests
{
    // Guid.ToByteArray() for this value is [33,22,11,00, 55,44, 77,66, 88,99,aa,bb,cc,dd,ee,ff] --
    // .NET's own mixed-endian layout. The RFC 4122 layout (what PHP's Ramsey\Uuid::getBytes() returns,
    // and what the uuid column must actually contain) is just the string's hex pairs in order, left to
    // right -- hand-verified below, not just asserting the conversion is its own inverse.
    private static readonly Guid KnownGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    private static readonly byte[] KnownRfc4122Bytes =
    [
        0x00, 0x11, 0x22, 0x33,
        0x44, 0x55,
        0x66, 0x77,
        0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff
    ];

    [Fact]
    public void ToRfc4122Bytes_matches_the_known_byte_layout()
    {
        var bytes = Rfc4122GuidConverter.ToRfc4122Bytes(KnownGuid);

        Assert.Equal(KnownRfc4122Bytes, bytes);
    }

    [Fact]
    public void FromRfc4122Bytes_recovers_the_known_guid()
    {
        var guid = Rfc4122GuidConverter.FromRfc4122Bytes(KnownRfc4122Bytes);

        Assert.Equal(KnownGuid, guid);
    }

    [Fact]
    public void Round_trips_a_freshly_generated_guid()
    {
        var original = Guid.NewGuid();

        var roundTripped = Rfc4122GuidConverter.FromRfc4122Bytes(Rfc4122GuidConverter.ToRfc4122Bytes(original));

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Rfc4122_bytes_differ_from_Guid_ToByteArray_for_a_mixed_value()
    {
        // Confirms the bug this converter exists to avoid: the two layouts are NOT the same for a guid
        // whose first three fields aren't byte-palindromic.
        Assert.NotEqual(KnownGuid.ToByteArray(), Rfc4122GuidConverter.ToRfc4122Bytes(KnownGuid));
    }
}
