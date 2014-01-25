package com.upokecenter.util;
/*
Written in 2013 by Peter O.
Any copyright is dedicated to the Public Domain.
http://creativecommons.org/publicdomain/zero/1.0/
If you like this, you should donate to Peter O.
at: http://peteroupc.github.io/CBOR/
 */

// import java.math.*;

  final class DigitShiftAccumulator implements IShiftAccumulator {
    private int bitLeftmost;

    /**
     * Gets a value indicating whether the last discarded digit was set.
     */
    public int getLastDiscardedDigit() {
        return this.bitLeftmost;
      }

    private int bitsAfterLeftmost;

    /**
     * Gets a value indicating whether any of the discarded digits to the
     * right of the last one was set.
     */
    public int getOlderDiscardedDigits() {
        return this.bitsAfterLeftmost;
      }

    private BigInteger shiftedBigInt;
    private FastInteger knownBitLength;

    /**
     * Not documented yet.
     * @return A FastInteger object.
     */
    public FastInteger GetDigitLength() {
      if (this.knownBitLength == null) {
        this.knownBitLength = this.CalcKnownDigitLength();
      }
      return FastInteger.Copy(this.knownBitLength);
    }

    private int shiftedSmall;
    private boolean isSmall;

    private FastInteger discardedBitCount;

    /**
     * Gets a value not documented yet.
     */
    public FastInteger getDiscardedDigitCount() {
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        return this.discardedBitCount;
      }

    private static BigInteger valueTen = BigInteger.TEN;

    /**
     * Gets the current integer after shifting.
     */
    public BigInteger getShiftedInt() {
        if (this.isSmall) {
          return BigInteger.valueOf(this.shiftedSmall);
        } else {
          return this.shiftedBigInt;
        }
      }

    public DigitShiftAccumulator (
      BigInteger bigint,
      int lastDiscarded,
      int olderDiscarded) {
      if (bigint.canFitInInt()) {
        this.shiftedSmall = bigint.intValue();
        if (this.shiftedSmall < 0) {
          throw new IllegalArgumentException("bigint is negative");
        }
        this.isSmall = true;
      } else {
        this.shiftedBigInt = bigint;
        this.isSmall = false;
      }
      this.bitsAfterLeftmost = (olderDiscarded != 0) ? 1 : 0;
      this.bitLeftmost = lastDiscarded;
    }

    private static int FastParseLong(String str, int offset, int length) {
      // Assumes the String is length 9 or less and contains
      // only the digits '0' through '9'
      if (length > 9) {
        throw new IllegalArgumentException(
          "length" + " not less or equal to " + "9" + " ("+(length)+")");
      }
      int ret = 0;
      for (int i = 0; i < length; ++i) {
        int digit = (int)(str.charAt(offset + i) - '0');
        ret *= 10;
        ret += digit;
      }
      return ret;
    }

    /**
     * Gets a value not documented yet.
     */
    public FastInteger getShiftedIntFast() {
        if (this.isSmall) {
          return new FastInteger(this.shiftedSmall);
        } else {
          return FastInteger.FromBig(this.shiftedBigInt);
        }
      }

    /**
     * Not documented yet.
     * @param fastint A FastInteger object.
     */
    public void ShiftRight(FastInteger fastint) {
      if (fastint == null) {
        throw new NullPointerException("fastint");
      }
      if (fastint.signum() <= 0) {
        return;
      }
      if (fastint.CanFitInInt32()) {
        this.ShiftRightInt(fastint.AsInt32());
      } else {
        BigInteger bi = fastint.AsBigInteger();
        while (bi.signum() > 0) {
          int count = 1000000;
          if (bi.compareTo(BigInteger.valueOf(1000000)) < 0) {
            count = bi.intValue();
          }
          this.ShiftRightInt(count);
          bi=bi.subtract(BigInteger.valueOf(count));
          if (this.isSmall ? this.shiftedSmall == 0 : this.shiftedBigInt.signum()==0) {
            break;
          }
        }
      }
    }

    private void ShiftRightBig(int digits) {
      if (digits <= 0) {
        return;
      }
      if (this.shiftedBigInt.signum()==0) {
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.AddInt(digits);
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = 0;
        this.knownBitLength = new FastInteger(1);
        return;
      }
      // System.out.println("digits={0}",digits);
      if (digits == 1) {
        BigInteger bigrem;
        BigInteger bigquo;
{
BigInteger[] divrem=(this.shiftedBigInt).divideAndRemainder(BigInteger.TEN);
bigquo=divrem[0];
bigrem=divrem[1]; }
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = bigrem.intValue();
        this.shiftedBigInt = bigquo;
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.AddInt(digits);
        if (this.knownBitLength != null) {
          if (bigquo.signum()==0) {
            this.knownBitLength.SetInt(0);
          } else {
            this.knownBitLength.Decrement();
          }
        }
        return;
      }
      int startCount = Math.min(4, digits - 1);
      if (startCount > 0) {
        BigInteger bigrem;
        BigInteger radixPower = DecimalUtility.FindPowerOfTen(startCount);
        BigInteger bigquo;
{
BigInteger[] divrem=(this.shiftedBigInt).divideAndRemainder(radixPower);
bigquo=divrem[0];
bigrem=divrem[1]; }
        if (bigrem.signum()!=0) {
          this.bitsAfterLeftmost |= 1;
        }
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.shiftedBigInt = bigquo;
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.AddInt(startCount);
        digits -= startCount;
        if (this.shiftedBigInt.signum()==0) {
          // Shifted all the way to 0
          this.isSmall = true;
          this.shiftedSmall = 0;
          this.knownBitLength = new FastInteger(1);
          this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
          this.bitLeftmost = 0;
          return;
        }
      }
      if (digits == 1) {
        BigInteger bigrem;
        BigInteger bigquo;
{
BigInteger[] divrem=(this.shiftedBigInt).divideAndRemainder(valueTen);
bigquo=divrem[0];
bigrem=divrem[1]; }
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = bigrem.intValue();
        this.shiftedBigInt = bigquo;
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.Increment();
        if (this.knownBitLength == null) {
          this.knownBitLength = this.GetDigitLength();
        } else {
          this.knownBitLength.Decrement();
        }
        this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
        return;
      }
      if (this.knownBitLength == null) {
        this.knownBitLength = this.GetDigitLength();
      }
      if (new FastInteger(digits).Decrement().compareTo(this.knownBitLength) >= 0) {
        // Shifting more bits than available
        this.bitsAfterLeftmost |= this.shiftedBigInt.signum()==0 ? 0 : 1;
        this.isSmall = true;
        this.shiftedSmall = 0;
        this.knownBitLength = new FastInteger(1);
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.AddInt(digits);
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = 0;
        return;
      }
      if (this.shiftedBigInt.canFitInInt()) {
        this.isSmall = true;
        this.shiftedSmall = this.shiftedBigInt.intValue();
        this.ShiftRightSmall(digits);
        return;
      }
      String str = this.shiftedBigInt.toString();
      // NOTE: Will be 1 if the value is 0
      int digitLength = str.length();
      int bitDiff = 0;
      if (digits > digitLength) {
        bitDiff = digits - digitLength;
      }
      if (this.discardedBitCount == null) {
        this.discardedBitCount = new FastInteger(0);
      }
      this.discardedBitCount.AddInt(digits);
      this.bitsAfterLeftmost |= this.bitLeftmost;
      int digitShift = Math.min(digitLength, digits);
      if (digits >= digitLength) {
        this.isSmall = true;
        this.shiftedSmall = 0;
        this.knownBitLength = new FastInteger(1);
      } else {
        int newLength = (int)(digitLength - digitShift);
        this.knownBitLength = new FastInteger(newLength);
        if (newLength <= 9) {
          // Fits in a small number
          this.isSmall = true;
          this.shiftedSmall = FastParseLong(str, 0, newLength);
        } else {
          this.shiftedBigInt = BigInteger.fromSubstring(str, 0, newLength);
        }
      }
      for (int i = str.length() - 1; i >= 0; --i) {
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = (int)(str.charAt(i) - '0');
        digitShift--;
        if (digitShift <= 0) {
          break;
        }
      }
      this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
      if (bitDiff > 0) {
        // Shifted more digits than the digit length
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = 0;
      }
    }

    private void ShiftToBitsBig(int digits) {
      // Shifts a number until it reaches the given number of digits,
      // gathering information on whether the last digit discarded is set
      // and whether the discarded digits to the right of that digit are set.
      // Assumes that the big integer being shifted is positive.
      if (this.knownBitLength != null) {
        if (this.knownBitLength.CompareToInt(digits) <= 0) {
          return;
        }
      }
      String str;
      if (this.knownBitLength == null) {
        this.knownBitLength = this.GetDigitLength();
      }
      if (this.knownBitLength.CompareToInt(digits) <= 0) {
        return;
      }
      FastInteger digitDiff = FastInteger.Copy(this.knownBitLength).SubtractInt(digits);
      if (digitDiff.CompareToInt(1) == 0) {
        BigInteger bigrem;
        BigInteger bigquo;
{
BigInteger[] divrem=(this.shiftedBigInt).divideAndRemainder(valueTen);
bigquo=divrem[0];
bigrem=divrem[1]; }
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = bigrem.intValue();
        this.shiftedBigInt = bigquo;
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.Add(digitDiff);
        this.knownBitLength.Subtract(digitDiff);
        this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
        return;
      } else if (digitDiff.CompareToInt(9) <= 0) {
        BigInteger bigrem;
        int diffInt = digitDiff.AsInt32();
        BigInteger radixPower = DecimalUtility.FindPowerOfTen(diffInt);
        BigInteger bigquo;
{
BigInteger[] divrem=(this.shiftedBigInt).divideAndRemainder(radixPower);
bigquo=divrem[0];
bigrem=divrem[1]; }
        int rem = bigrem.intValue();
        this.bitsAfterLeftmost |= this.bitLeftmost;
        for (int i = 0; i < diffInt; ++i) {
          if (i == diffInt - 1) {
            this.bitLeftmost = rem % 10;
          } else {
            this.bitsAfterLeftmost |= rem % 10;
            rem /= 10;
          }
        }
        this.shiftedBigInt = bigquo;
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.Add(digitDiff);
        this.knownBitLength.Subtract(digitDiff);
        this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
        return;
      } else if (digitDiff.CompareToInt(Integer.MAX_VALUE) <= 0) {
        BigInteger bigrem;
        BigInteger radixPower = DecimalUtility.FindPowerOfTen(digitDiff.AsInt32() - 1);
        BigInteger bigquo;
{
BigInteger[] divrem=(this.shiftedBigInt).divideAndRemainder(radixPower);
bigquo=divrem[0];
bigrem=divrem[1]; }
        this.bitsAfterLeftmost |= this.bitLeftmost;
        if (bigrem.signum()!=0) {
          this.bitsAfterLeftmost |= 1;
        }
        {
          BigInteger bigquo2;
{
BigInteger[] divrem=(bigquo).divideAndRemainder(valueTen);
bigquo2=divrem[0];
bigrem=divrem[1]; }
          this.bitLeftmost = bigrem.intValue();
          this.shiftedBigInt = bigquo2;
        }
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.Add(digitDiff);
        this.knownBitLength.Subtract(digitDiff);
        this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
        return;
      }
      str = this.shiftedBigInt.toString();
      // NOTE: Will be 1 if the value is 0
      int digitLength = str.length();
      this.knownBitLength = new FastInteger(digitLength);
      // Shift by the difference in digit length
      if (digitLength > digits) {
        int digitShift = digitLength - digits;
        this.knownBitLength.SubtractInt(digitShift);
        int newLength = (int)(digitLength - digitShift);
        // System.out.println("dlen={0} dshift={1} newlen={2}",digitLength,
        // digitShift, newLength);
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        if (digitShift <= Integer.MAX_VALUE) {
          this.discardedBitCount.AddInt((int)digitShift);
        } else {
          this.discardedBitCount.AddBig(BigInteger.valueOf(digitShift));
        }
        for (int i = str.length() - 1; i >= 0; --i) {
          this.bitsAfterLeftmost |= this.bitLeftmost;
          this.bitLeftmost = (int)(str.charAt(i) - '0');
          digitShift--;
          if (digitShift <= 0) {
            break;
          }
        }
        if (newLength <= 9) {
          this.isSmall = true;
          this.shiftedSmall = FastParseLong(str, 0, newLength);
        } else {
          this.shiftedBigInt = BigInteger.fromSubstring(str, 0, newLength);
        }
        this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
      }
    }

    /**
     * Shifts a number to the right, gathering information on whether the
     * last digit discarded is set and whether the discarded digits to the
     * right of that digit are set. Assumes that the big integer being shifted
     * is positive.
     * @param digits A 32-bit signed integer.
     */
    public void ShiftRightInt(int digits) {
      if (this.isSmall) {
        this.ShiftRightSmall(digits);
      } else {
        this.ShiftRightBig(digits);
      }
    }

    private void ShiftRightSmall(int digits) {
      if (digits <= 0) {
        return;
      }
      if (this.shiftedSmall == 0) {
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(0);
        }
        this.discardedBitCount.AddInt(digits);
        this.bitsAfterLeftmost |= this.bitLeftmost;
        this.bitLeftmost = 0;
        this.knownBitLength = new FastInteger(1);
        return;
      }

      int kb = 0;
      int tmp = this.shiftedSmall;
      while (tmp > 0) {
        kb++;
        tmp /= 10;
      }
      // Make sure digit length is 1 if value is 0
      if (kb == 0) {
        kb++;
      }
      this.knownBitLength = new FastInteger(kb);
      if (this.discardedBitCount == null) {
        this.discardedBitCount = new FastInteger(0);
      }
      this.discardedBitCount.AddInt(digits);
      while (digits > 0) {
        if (this.shiftedSmall == 0) {
          this.bitsAfterLeftmost |= this.bitLeftmost;
          this.bitLeftmost = 0;
          this.knownBitLength = new FastInteger(0);
          break;
        } else {
          int digit = (int)(this.shiftedSmall % 10);
          this.bitsAfterLeftmost |= this.bitLeftmost;
          this.bitLeftmost = digit;
          digits--;
          this.shiftedSmall /= 10;
          this.knownBitLength.Decrement();
        }
      }
      this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
    }

    /**
     * Not documented yet.
     * @param bits A FastInteger object.
     */
    public void ShiftToDigits(FastInteger bits) {
      if (bits.CanFitInInt32()) {
        int intval = bits.AsInt32();
        if (intval < 0) {
          throw new IllegalArgumentException("bits is negative");
        }
        this.ShiftToDigitsInt(intval);
      } else {
        if (bits.signum() < 0) {
          throw new IllegalArgumentException("bits is negative");
        }
        this.knownBitLength = this.CalcKnownDigitLength();
        BigInteger bigintDiff = this.knownBitLength.AsBigInteger();
        BigInteger bitsBig = bits.AsBigInteger();
        bigintDiff=bigintDiff.subtract(bitsBig);
        if (bigintDiff.signum() > 0) {
          // current length is greater than the
          // desired bit length
          this.ShiftRight(FastInteger.FromBig(bigintDiff));
        }
      }
    }

    /**
     * Shifts a number until it reaches the given number of digits, gathering
     * information on whether the last digit discarded is set and whether
     * the discarded digits to the right of that digit are set. Assumes that
     * the big integer being shifted is positive.
     * @param digits A 64-bit signed integer.
     */
    public void ShiftToDigitsInt(int digits) {
      if (this.isSmall) {
        this.ShiftToBitsSmall(digits);
      } else {
        this.ShiftToBitsBig(digits);
      }
    }

    private FastInteger CalcKnownDigitLength() {
      if (this.isSmall) {
        int kb = 0;
        int v2 = this.shiftedSmall;
        if (v2 >= 1000000000) {
          kb = 10;
        } else if (v2 >= 100000000) {
          kb = 9;
        } else if (v2 >= 10000000) {
          kb = 8;
        } else if (v2 >= 1000000) {
          kb = 7;
        } else if (v2 >= 100000) {
          kb = 6;
        } else if (v2 >= 10000) {
          kb = 5;
        } else if (v2 >= 1000) {
          kb = 4;
        } else if (v2 >= 100) {
          kb = 3;
        } else if (v2 >= 10) {
          kb = 2;
        } else {
          kb = 1;
        }
        return new FastInteger(kb);
      } else {
        return new FastInteger(this.shiftedBigInt.getDigitCount());
      }
    }

    private void ShiftToBitsSmall(int digits) {
      int kb = 0;
      int v2 = this.shiftedSmall;
      if (v2 >= 1000000000) {
        kb = 10;
      } else if (v2 >= 100000000) {
        kb = 9;
      } else if (v2 >= 10000000) {
        kb = 8;
      } else if (v2 >= 1000000) {
        kb = 7;
      } else if (v2 >= 100000) {
        kb = 6;
      } else if (v2 >= 10000) {
        kb = 5;
      } else if (v2 >= 1000) {
        kb = 4;
      } else if (v2 >= 100) {
        kb = 3;
      } else if (v2 >= 10) {
        kb = 2;
      } else {
        kb = 1;
      }
      this.knownBitLength = new FastInteger(kb);
      if (kb > digits) {
        int digitShift = (int)(kb - digits);
        int newLength = (int)(kb - digitShift);
        this.knownBitLength = new FastInteger(Math.max(1, newLength));
        if (this.discardedBitCount == null) {
          this.discardedBitCount = new FastInteger(digitShift);
        } else {
          this.discardedBitCount.AddInt(digitShift);
        }
        for (int i = 0; i < digitShift; ++i) {
          int digit = (int)(this.shiftedSmall % 10);
          this.shiftedSmall /= 10;
          this.bitsAfterLeftmost |= this.bitLeftmost;
          this.bitLeftmost = digit;
        }
        this.bitsAfterLeftmost = (this.bitsAfterLeftmost != 0) ? 1 : 0;
      }
    }
  }
