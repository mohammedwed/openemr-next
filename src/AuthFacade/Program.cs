using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AuthFacade.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. OPENEMR CLIENT
// ==========================================
var openEmrBase = builder.Configuration["OpenEmr:BaseUrl"]
    ?? throw new InvalidOperationException("Missing 'OpenEmr:BaseUrl'.");

builder.Services.AddHttpClient<OpenEmrAuthClient>(client =>
{
    client.BaseAddress = new Uri(openEmrBase.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
    {
        // OpenEMR's dev certificate is self-signed. Development only.
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

// ==========================================
// 2. JWT VALIDATION (this service's own tokens, used by /me)
// ==========================================
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Missing 'Jwt:Key'.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
const string issuer = "openemr-next";
const string audience = "openemr-next-api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// ==========================================
// 3. PIPELINE
// ==========================================
app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// 4. ENDPOINTS
// ==========================================
app.MapGet("/hello", () => Results.Ok(new { message = "Hello from AuthFacade" }))
   .AllowAnonymous();

app.MapPost("/connect/token", async (HttpRequest request, OpenEmrAuthClient openEmr, CancellationToken ct) =>
{
    var form = await request.ReadFormAsync(ct);
    if (form["grant_type"].ToString() != "password")
        return Results.BadRequest(new { error = "unsupported_grant_type" });

    var username = form["username"].ToString();
    var password = form["password"].ToString();
    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        return Results.BadRequest(new { error = "invalid_request" });

    try
    {
        var (status, body) = await openEmr.PasswordGrantAsync(username, password, ct);
        if (status != 200)
            return Results.Content(body, "application/json", statusCode: status);

        // OpenEMR accepted the login. Read the subject from its token (received over TLS),
        // then issue this service's own short-lived token. OpenEMR's token is not returned.
        using var doc = JsonDocument.Parse(body);
        var openEmrToken = doc.RootElement.GetProperty("access_token").GetString()!;
        var subject = new JwtSecurityTokenHandler().ReadJwtToken(openEmrToken).Subject;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(JwtRegisteredClaimNames.Name, username)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return Results.Ok(new
        {
            access_token = new JwtSecurityTokenHandler().WriteToken(token),
            token_type = "Bearer",
            expires_in = 1800
        });
    }
    catch (HttpRequestException)
    {
        return Results.Json(new
        {
            error = "server_error",
            error_description = "OpenEMR is unreachable."
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous().DisableAntiforgery();

app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
{
    Subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
    Name = user.FindFirst(JwtRegisteredClaimNames.Name)?.Value
})).RequireAuthorization();

app.Run();