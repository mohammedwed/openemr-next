namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// Converts between <see cref="Guid"/> and the 16-byte layout OpenEMR's own PHP code actually stores in
/// a <c>binary(16)</c> <c>uuid</c> column: RFC 4122's byte order (the same order you'd read off the
/// UUID string left to right), via Ramsey\Uuid's <c>getBytes()</c>/<c>fromBytes()</c>
/// (<c>UuidRegistry::uuidToBytes</c>/<c>uuidToString</c> in the legacy source).
///
/// <see cref="Guid.ToByteArray()"/>/<see cref="Guid(byte[])"/> do NOT use that order -- for historical
/// COM reasons, .NET reverses the byte order within the first three fields (the 4-byte, then two
/// 2-byte groups). Storing a <see cref="Guid"/>'s raw <see cref="Guid.ToByteArray()"/> output directly
/// would silently store a different value than the same UUID string would produce in PHP: anything that
/// later reads this column expecting Ramsey's convention -- including OpenEMR's own FHIR API, the whole
/// reason this aggregate uses <c>uuid</c> as its public identity (docs/m4-domain-notes.md) -- would
/// compute the wrong UUID string back out.
/// </summary>
internal static class Rfc4122GuidConverter
{
    public static byte[] ToRfc4122Bytes(Guid guid)
    {
        var bytes = guid.ToByteArray();
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
        return bytes;
    }

    public static Guid FromRfc4122Bytes(byte[] rfc4122Bytes)
    {
        var bytes = (byte[])rfc4122Bytes.Clone();
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
        return new Guid(bytes);
    }
}
