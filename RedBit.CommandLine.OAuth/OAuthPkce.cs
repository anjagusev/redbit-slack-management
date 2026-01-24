using System.Security.Cryptography;
using System.Text;

namespace RedBit.CommandLine.OAuth;

/// <summary>
/// Static utilities for OAuth 2.0 PKCE (Proof Key for Code Exchange) and CSRF protection.
/// </summary>
public static class OAuthPkce
{
    /// <summary>
    /// Generates a cryptographically secure random state parameter for CSRF protection.
    /// </summary>
    /// <returns>A URL-safe base64-encoded 32-byte random string.</returns>
    public static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Generates a PKCE code verifier (random string between 43-128 characters).
    /// </summary>
    /// <returns>A URL-safe base64-encoded 64-byte random string.</returns>
    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Generates a PKCE code challenge from the verifier using S256 method.
    /// </summary>
    /// <param name="codeVerifier">The code verifier to hash.</param>
    /// <returns>A URL-safe base64-encoded SHA-256 hash of the verifier.</returns>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(challengeBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
