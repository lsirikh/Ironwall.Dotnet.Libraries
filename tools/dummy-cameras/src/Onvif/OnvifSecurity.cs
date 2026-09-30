using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace DummyCameras.Onvif;

public enum AuthResult { Ok, Missing, BadUser, BadPassword, Stale }

/// <summary>
/// WS-Security UsernameToken check (ONVIF Core §5.12). Our client (SoapSecurityHeader) sends
/// <c>PasswordDigest = Base64(SHA1(nonce + created + password))</c>, elements in the wsse default namespace and
/// <c>Created</c> in wsu - matched by local name so either prefixing style works. PasswordText is accepted too.
/// <c>Created</c> must be within ±<see cref="MaxSkew"/> of the camera clock (the client computes its time shift
/// from GetSystemDateAndTime, which we answer with the real UTC time).
/// </summary>
public static class WsUsernameToken
{
    public static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(5);

    public static AuthResult Verify(XElement? header, string user, string password, DateTime nowUtc)
    {
        var token = header?.Descendants().FirstOrDefault(e => e.Name.LocalName == "UsernameToken");
        if (token is null) return AuthResult.Missing;
        string? U(string local) => token.Elements().FirstOrDefault(e => e.Name.LocalName == local)?.Value;

        var username = U("Username");
        var pwEl = token.Elements().FirstOrDefault(e => e.Name.LocalName == "Password");
        if (username is null || pwEl is null) return AuthResult.Missing;
        if (!string.Equals(username, user, StringComparison.Ordinal)) return AuthResult.BadUser;

        var type = (string?)pwEl.Attribute("Type") ?? string.Empty;
        if (type.EndsWith("#PasswordText", StringComparison.Ordinal) || type.Length == 0)
            return string.Equals(pwEl.Value, password, StringComparison.Ordinal) ? AuthResult.Ok : AuthResult.BadPassword;

        var nonceB64 = U("Nonce");
        var created = U("Created");
        if (nonceB64 is null || created is null) return AuthResult.Missing;
        byte[] nonce;
        try { nonce = Convert.FromBase64String(nonceB64); }
        catch (FormatException) { return AuthResult.BadPassword; }

        if (DateTime.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var createdUtc)
            && (createdUtc - nowUtc).Duration() > MaxSkew)
            return AuthResult.Stale;

        var expected = Digest(nonce, created, password);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(pwEl.Value.Trim()))
            ? AuthResult.Ok : AuthResult.BadPassword;
    }

    public static string Digest(byte[] nonce, string created, string password)
    {
        var tail = Encoding.UTF8.GetBytes(created + password);
        var buffer = new byte[nonce.Length + tail.Length];
        nonce.CopyTo(buffer, 0);
        tail.CopyTo(buffer, nonce.Length);
        return Convert.ToBase64String(SHA1.HashData(buffer));   // ONVIF-mandated algorithm (not a password store)
    }
}

/// <summary>
/// HTTP Digest (RFC 2617, MD5, qop=auth) for cameras that demand it on the transport as well.
/// Nonces are random per challenge and not tracked (loopback test double - no replay protection needed).
/// </summary>
public static class HttpDigestAuth
{
    public const string Realm = "DummyCam";

    public static string Challenge() =>
        $"Digest realm=\"{Realm}\", qop=\"auth\", nonce=\"{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}\", algorithm=MD5";

    /// <summary>true when the Authorization header carries a valid digest response for this request.</summary>
    public static bool Verify(string? authorization, string method, string user, string password)
    {
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase)) return false;
        var p = Parse(authorization[7..]);
        if (!p.TryGetValue("username", out var u) || u != user) return false;
        if (!p.TryGetValue("nonce", out var nonce) || !p.TryGetValue("uri", out var uri) || !p.TryGetValue("response", out var response)) return false;
        var realm = p.TryGetValue("realm", out var r) ? r : Realm;
        var ha1 = Md5Hex($"{user}:{realm}:{password}");
        var ha2 = Md5Hex($"{method}:{uri}");
        string expected = p.TryGetValue("qop", out var qop) && qop.Length > 0
            ? Md5Hex($"{ha1}:{nonce}:{(p.TryGetValue("nc", out var nc) ? nc : "")}:{(p.TryGetValue("cnonce", out var cn) ? cn : "")}:{qop}:{ha2}")
            : Md5Hex($"{ha1}:{nonce}:{ha2}");
        return string.Equals(expected, response, StringComparison.OrdinalIgnoreCase);
    }

    private static string Md5Hex(string s) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();   // RFC 2617 mandates MD5

    private static Dictionary<string, string> Parse(string s)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == ',')) i++;
            int eq = s.IndexOf('=', i);
            if (eq < 0) break;
            var key = s[i..eq].Trim();
            i = eq + 1;
            string val;
            if (i < s.Length && s[i] == '"')
            {
                int end = s.IndexOf('"', i + 1);
                if (end < 0) end = s.Length;
                val = s[(i + 1)..end];
                i = end + 1;
            }
            else
            {
                int end = s.IndexOf(',', i);
                if (end < 0) end = s.Length;
                val = s[i..end].Trim();
                i = end;
            }
            d[key] = val;
        }
        return d;
    }
}
