using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OpenEmrNext.PatientManagement.Api.OpenApi;

/// <summary>
/// Reflects <see cref="StringLengthAttribute"/>/<see cref="RangeAttribute"/>/<see cref="EmailAddressAttribute"/>
/// into the generated schema for a request-body property. ASP.NET Core's own OpenAPI schema generation
/// (<c>JsonSchemaExporter</c>) only reflects a type's JSON/nullability shape for a body property, not its
/// <see cref="ValidationAttribute"/>s -- confirmed empirically against the real generated document, which
/// showed no <c>minLength</c>/<c>maximum</c>/<c>format</c> for <c>RegisterPatientRequest</c> despite its
/// attributes. This only decorates the schema for documentation/client-generation purposes; it changes
/// nothing about what's actually enforced -- <c>AddValidation()</c> and the Domain factories remain the
/// real enforcement, unaffected by this transformer.
/// </summary>
internal sealed class ValidationAttributesSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var propertyInfo = context.JsonPropertyInfo;
        if (propertyInfo is null)
        {
            return Task.CompletedTask;
        }

        // A positional record's attributes land on the primary constructor's parameter, not the
        // auto-generated property, unless the declaration uses an explicit [property: ...] target --
        // none of these DTOs do. JsonPropertyInfo.AttributeProvider only ever reflects the property
        // itself, so the parameter's own attributes (where every one of these actually lives) must be
        // read separately via AssociatedParameter. Confirmed empirically: without this, the generated
        // schema showed no minLength/maximum/format at all despite every DTO carrying these attributes.
        var attributes = (propertyInfo.AttributeProvider?.GetCustomAttributes(inherit: true) ?? [])
            .Concat(propertyInfo.AssociatedParameter?.AttributeProvider?.GetCustomAttributes(inherit: true) ?? []);

        foreach (var attribute in attributes)
        {
            switch (attribute)
            {
                case RangeAttribute range:
                    schema.Minimum = range.Minimum?.ToString();
                    if (IsRealUpperBound(range.Maximum))
                    {
                        schema.Maximum = range.Maximum!.ToString();
                    }
                    break;
                case StringLengthAttribute stringLength:
                    schema.MaxLength = stringLength.MaximumLength;
                    if (stringLength.MinimumLength > 0)
                    {
                        schema.MinLength = stringLength.MinimumLength;
                    }
                    break;
                case EmailAddressAttribute:
                    schema.Format = "email";
                    break;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Every <c>[Range]</c> in these DTOs uses <c>long.MaxValue</c> as a "must be positive" idiom, not
    /// a real upper bound. <see cref="RangeAttribute"/> has no <c>(long, long)</c> constructor, so
    /// <c>[Range(1, long.MaxValue)]</c> actually resolves to <c>RangeAttribute(double, double)</c> --
    /// <see cref="RangeAttribute.Maximum"/> is already a lossy double (9223372036854775808.0, not the
    /// true 9223372036854775807) by the time this code ever sees it, confirmed by reflecting the
    /// attribute directly. Writing that through produces a wrong, misleading schema bound; omitting a
    /// non-real one is more honest.
    /// </summary>
    private static bool IsRealUpperBound(object? maximum) =>
        maximum switch
        {
            null => false,
            double d => d < long.MaxValue,
            _ => true
        };
}
