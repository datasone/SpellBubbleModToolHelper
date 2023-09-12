using System;
using System.Text;

namespace SpellBubbleModToolHelper;

public class MD4
{
    private uint A;
    private uint B;
    private uint C;
    private uint D;

    private uint F(uint X, uint Y, uint Z)
    {
        return (X & Y) | (~X & Z);
    }

    private uint G(uint X, uint Y, uint Z)
    {
        return (X & Y) | (X & Z) | (Y & Z);
    }

    private uint H(uint X, uint Y, uint Z)
    {
        return X ^ Y ^ Z;
    }

    private uint LShift(uint X, int s)
    {
        X &= 0xFFFFFFFF;
        return ((X << s) & 0xFFFFFFFF) | (X >> (32 - s));
    }

    private void FF(ref uint X, uint Y, uint Z, uint O, uint P, int S)
    {
        X = LShift(X + F(Y, Z, O) + P, S);
    }

    private void HH(ref uint X, uint Y, uint Z, uint O, uint P, int S)
    {
        X = LShift(X + G(Y, Z, O) + P + 0x5A827999, S);
    }

    private void GG(ref uint X, uint Y, uint Z, uint O, uint P, int S)
    {
        X = LShift(X + H(Y, Z, O) + P + 0x6ED9EBA1, S);
    }

    private void MdFour64(ref uint[] M)
    {
        uint AA, BB, CC, DD;
        var X = new uint[16];

        for (var j = 0; j < 16; j++) X[j] = M[j];

        AA = A;
        BB = B;
        CC = C;
        DD = D;

        FF(ref A, B, C, D, X[0], 3);
        FF(ref D, A, B, C, X[1], 7);
        FF(ref C, D, A, B, X[2], 11);
        FF(ref B, C, D, A, X[3], 19);
        FF(ref A, B, C, D, X[4], 3);
        FF(ref D, A, B, C, X[5], 7);
        FF(ref C, D, A, B, X[6], 11);
        FF(ref B, C, D, A, X[7], 19);
        FF(ref A, B, C, D, X[8], 3);
        FF(ref D, A, B, C, X[9], 7);
        FF(ref C, D, A, B, X[10], 11);
        FF(ref B, C, D, A, X[11], 19);
        FF(ref A, B, C, D, X[12], 3);
        FF(ref D, A, B, C, X[13], 7);
        FF(ref C, D, A, B, X[14], 11);
        FF(ref B, C, D, A, X[15], 19);

        HH(ref A, B, C, D, X[0], 3);
        HH(ref D, A, B, C, X[4], 5);
        HH(ref C, D, A, B, X[8], 9);
        HH(ref B, C, D, A, X[12], 13);
        HH(ref A, B, C, D, X[1], 3);
        HH(ref D, A, B, C, X[5], 5);
        HH(ref C, D, A, B, X[9], 9);
        HH(ref B, C, D, A, X[13], 13);
        HH(ref A, B, C, D, X[2], 3);
        HH(ref D, A, B, C, X[6], 5);
        HH(ref C, D, A, B, X[10], 9);
        HH(ref B, C, D, A, X[14], 13);
        HH(ref A, B, C, D, X[3], 3);
        HH(ref D, A, B, C, X[7], 5);
        HH(ref C, D, A, B, X[11], 9);
        HH(ref B, C, D, A, X[15], 13);

        GG(ref A, B, C, D, X[0], 3);
        GG(ref D, A, B, C, X[8], 9);
        GG(ref C, D, A, B, X[4], 11);
        GG(ref B, C, D, A, X[12], 15);
        GG(ref A, B, C, D, X[2], 3);
        GG(ref D, A, B, C, X[10], 9);
        GG(ref C, D, A, B, X[6], 11);
        GG(ref B, C, D, A, X[14], 15);
        GG(ref A, B, C, D, X[1], 3);
        GG(ref D, A, B, C, X[9], 9);
        GG(ref C, D, A, B, X[5], 11);
        GG(ref B, C, D, A, X[13], 15);
        GG(ref A, B, C, D, X[3], 3);
        GG(ref D, A, B, C, X[11], 9);
        GG(ref C, D, A, B, X[7], 11);
        GG(ref B, C, D, A, X[15], 15);

        A += AA;
        B += BB;
        C += CC;
        D += DD;

        A &= 0xFFFFFFFF;
        B &= 0xFFFFFFFF;
        C &= 0xFFFFFFFF;
        D &= 0xFFFFFFFF;

        for (var j = 0; j < 16; j++) X[j] = 0;
    }

    private void Copy64(ref uint[] M, byte[] input, int left)
    {
        for (var i = 0; i < 16; i++)
            M[i] = (uint) ((input[i * 4 + left + 3] << 24) | (input[i * 4 + left + 2] << 16) |
                           (input[i * 4 + left + 1] << 8) | (input[i * 4 + left + 0] << 0));
    }

    private void Copy4(ref byte[] output, int left, uint x)
    {
        output[left + 0] = (byte) (x & 0xFF);
        output[left + 1] = (byte) ((x >> 8) & 0xFF);
        output[left + 2] = (byte) ((x >> 16) & 0xFF);
        output[left + 3] = (byte) ((x >> 24) & 0xFF);
    }

    private byte[] MdFour(byte[] input, int n)
    {
        var output = new byte[16];
        var buf = new byte[128];
        var M = new uint[16];
        var b = (uint) (n * 8);

        A = 0x67452301;
        B = 0xefcdab89;
        C = 0x98badcfe;
        D = 0x10325476;

        var j = 0;
        while (n > 64)
        {
            Copy64(ref M, input, 0);
            MdFour64(ref M);
            j += 64;
            n -= 64;
        }

        for (var i = 0; i < 128; i++) buf[i] = 0;

        for (var i = 0; i < n; i++) buf[i] = input[j + i];

        buf[n] = 0x80;

        if (n <= 55)
        {
            Copy4(ref buf, 56, b);
            Copy64(ref M, buf, 0);
            MdFour64(ref M);
        }
        else
        {
            Copy4(ref buf, 120, b);
            Copy64(ref M, buf, 0);
            MdFour64(ref M);
            Copy64(ref M, buf, 64);
            MdFour64(ref M);
        }

        for (var i = 0; i < 128; i++) buf[i] = 0;

        Copy64(ref M, buf, 0);
        Copy4(ref output, 0, A);
        Copy4(ref output, 4, B);
        Copy4(ref output, 8, C);
        Copy4(ref output, 12, D);
        A = B = C = D = 0;
        return output;
    }

    /// <summary>
    ///     MD4加密
    /// </summary>
    /// <param name="str">输入字符串</param>
    /// <returns>加密的字符串</returns>
    public string Encode(string str)
    {
        return Encode(str, true, 32);
    }

    /// <summary>
    ///     MD4加密
    /// </summary>
    /// <param name="str">输入字符串</param>
    /// <param name="isUpper">是否大写</param>
    /// <returns>加密的字符串</returns>
    public string Encode(string str, bool isUpper)
    {
        return Encode(str, isUpper, 32);
    }

    /// <summary>
    ///     MD4加密
    /// </summary>
    /// <param name="str">输入字符串</param>
    /// <param name="isHex">16、32位输出</param>
    /// <returns>加密的字符串</returns>
    public string Encode(string str, int isHex)
    {
        return Encode(str, true, isHex);
    }

    /// <summary>
    ///     MD4加密
    /// </summary>
    /// <param name="str">输入字符串</param>
    /// <param name="isUpper">是否大写</param>
    /// <param name="isHex">16、32位输出</param>
    /// <returns>加密的字符串</returns>
    public string Encode(string str, bool isUpper, int isHex)
    {
        var returnString = string.Empty;
        var t = Encoding.ASCII.GetBytes(str);
        var temp = MdFour(t, t.Length);

        if (isHex == 16) returnString = BitConverter.ToString(temp, 4, 8).Replace("-", "");

        if (isHex == 32) returnString = BitConverter.ToString(temp).Replace("-", "");

        if (isUpper)
            returnString = returnString.ToUpper();
        else
            returnString = returnString.ToLower();

        return returnString;
    }
}