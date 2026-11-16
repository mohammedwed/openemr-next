using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.UnitTests.Persistence;

public class Rfc4122GuidConverterTests
{
    // Guid.ToByteArray() is .NET's mixed-endian layout. The layout legacy stores (PHP Ramsey\Uuid getBytes()) is
    // the string's hex pairs in order, left to right. Hand-verified, not just "converts back to itself".
    private static readonly Guid KnownGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    private static readonly byte[] KnownRfc4122Bytes =
    [
        0x00, 0x11, 0x22, 0x33,
        0x44, 0x55,
        0x66, 0x77,
        0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff
    ];

    [Fact]
    public void ToRfc4122Bytes_matches_the_known_byte_layout() =>
        Assert.Equal(KnownRfc4122Bytes, Rfc4122GuidConverter.ToRfc4122Bytes(KnownGuid));

    [Fact]
    public void FromRfc4122Bytes_recovers_the_known_guid() =>
        Assert.Equal(KnownGuid, Rfc4122GuidConverter.FromRfc4122Bytes(KnownRfc4122Bytes));

    [Fact]
    public void A_version_7_guid_round_trips_and_keeps_its_timestamp_first()
    {
        var original = Guid.CreateVersion7();

        var bytes = Rfc4122GuidConverter.ToRfc4122Bytes(original);

        Assert.Equal(original, Rfc4122GuidConverter.FromRfc4122Bytes(bytes));
        // Stored bytes read left to right as the text form, so the leading bytes are the v7 timestamp and rows
        // sort roughly by time in the unique index, as legacy's own COMB uuids do.
        Assert.Equal(original.ToString("N")[..8], Convert.ToHexString(bytes[..4]).ToLowerInvariant());
    }
}
