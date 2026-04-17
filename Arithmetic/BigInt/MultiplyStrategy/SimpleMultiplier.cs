using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        bool isNeg = a.IsNegative ^ b.IsNegative;
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        uint[] res = new uint[aDigits.Length + bDigits.Length];

        for (int i = 0; i < aDigits.Length; i++)
        {
            uint carry = 0;
            uint aVal = aDigits[i];
            uint a0 = aVal & 0xFFFF, a1 = aVal >> 16;

            for (int j = 0; j < bDigits.Length; j++)
            {
                uint bVal = bDigits[j];
                uint b0 = bVal & 0xFFFF, b1 = bVal >> 16;

                uint p00 = b0 * a0;
                uint p11 = a1 * b1;
                uint p01 = a0 * b1;
                uint p10 = a1 * b0;

                uint c0 = p00 & 0xFFFF;
                uint c1 = (p00 >> 16) + (p01 & 0xFFFF) + (p10 & 0xFFFF);
                uint c2 = (p01 >> 16) + (p10 >> 16) + (p11 & 0xFFFF) + (c1 >> 16);
                uint c3 = (p11 >> 16) + (c2 >> 16);

                uint low  = c0 | (c1 << 16);
                uint high = (c2 & 0xFFFF) | (c3 << 16);

                uint cur = res[i + j];
                uint sum = cur + low;
                uint carry1 = (sum < cur) ? 1u : 0u;

                uint total = sum + carry;
                uint carry2 = (total < sum) ? 1u : 0u;

                res[i + j] = total;

                carry = high + carry1 + carry2; 
            }
            res[i + bDigits.Length] = carry;
        }

        return new BetterBigInteger(res, isNeg);
    }
}