using System;
using System.Security.Cryptography;

namespace Ironwall.Dotnet.Framework.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/18/2025 6:29:17 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
// ——————————————
// 1) 해시/검증 헬퍼
// ——————————————
public static class PasswordHelper
{
    // 솔트 길이
    private const int SaltSize = 16;
    // 해시 길이
    private const int HashSize = 32;
    // 반복 횟수
    private const int Iterations = 100_000;

    // 평문 → “salt|hash” Base64 문자열
    public static string HashPassword(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] salt = new byte[SaltSize];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        byte[] hash = pbkdf2.GetBytes(HashSize);

        // salt + hash 합치기
        byte[] buf = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, buf, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, buf, SaltSize, HashSize);

        return Convert.ToBase64String(buf);
    }

    // “salt|hash” 문자열 + 평문 → 일치 여부
    public static bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        byte[] buf = Convert.FromBase64String(hashedPassword);
        if (buf.Length != SaltSize + HashSize) return false;

        byte[] salt = new byte[SaltSize];
        Buffer.BlockCopy(buf, 0, salt, 0, SaltSize);

        byte[] storedHash = new byte[HashSize];
        Buffer.BlockCopy(buf, SaltSize, storedHash, 0, HashSize);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            providedPassword,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        byte[] computed = pbkdf2.GetBytes(HashSize);
        return CryptographicOperations.FixedTimeEquals(storedHash, computed);
    }
}