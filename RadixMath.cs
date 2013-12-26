using System;
using System.Text;
//using System.Numerics;

namespace PeterO {
    /// <summary> Encapsulates radix-independent arithmetic. </summary>
    /// <typeparam name='T'>Data type for a numeric value in a particular
    /// radix.</typeparam>
  class RadixMath<T> {
    
    IRadixMathHelper<T> helper;
    int thisRadix;
    int support;
    
    public RadixMath(IRadixMathHelper<T> helper) {
      this.helper = helper;
      this.support=helper.GetArithmeticSupport();
      this.thisRadix=helper.GetRadix();
    }

    private T ReturnQuietNaNFastIntPrecision(T thisValue, FastInteger precision){
      BigInteger mant=BigInteger.Abs(helper.GetMantissa(thisValue));
      bool mantChanged=false;
      if(!(mant.IsZero) && precision!=null && precision.Sign>0){
        BigInteger limit=helper.MultiplyByRadixPower(
          BigInteger.One,precision);
        if(mant.CompareTo(limit)>=0){
          mant=mant%(BigInteger)limit;
          mantChanged=true;
        }
      }
      int flags=helper.GetFlags(thisValue);
      if(!mantChanged && (flags&BigNumberFlags.FlagQuietNaN)!=0){
        return thisValue;
      }
      flags&=BigNumberFlags.FlagNegative;
      flags|=BigNumberFlags.FlagQuietNaN;
      return helper.CreateNewWithFlags(mant,BigInteger.Zero,flags);
    }

    private T ReturnQuietNaN(T thisValue, PrecisionContext ctx){
      BigInteger mant=BigInteger.Abs(helper.GetMantissa(thisValue));
      bool mantChanged=false;
      if(!(mant.IsZero) && ctx!=null && !((ctx.Precision).IsZero)){
        BigInteger limit=helper.MultiplyByRadixPower(
          BigInteger.One,FastInteger.FromBig(ctx.Precision));
        if(mant.CompareTo(limit)>=0){
          mant=mant%(BigInteger)limit;
          mantChanged=true;
        }
      }
      int flags=helper.GetFlags(thisValue);
      if(!mantChanged && (flags&BigNumberFlags.FlagQuietNaN)!=0){
        return thisValue;
      }
      flags&=BigNumberFlags.FlagNegative;
      flags|=BigNumberFlags.FlagQuietNaN;
      return helper.CreateNewWithFlags(mant,BigInteger.Zero,flags);
    }

    private T DivisionHandleSpecial(T thisValue, T other, PrecisionContext ctx){
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(other);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        T result=HandleNotANumber(thisValue,other,ctx);
        if((Object)result!=(Object)default(T))return result;
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0 && (otherFlags&BigNumberFlags.FlagInfinity)!=0){
          // Attempt to divide infinity by infinity
          return SignalInvalid(ctx);
        }
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          return EnsureSign(thisValue,((thisFlags^otherFlags)&BigNumberFlags.FlagNegative)!=0);
        }
        if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
          // Divisor is infinity, so result will be epsilon
          if(ctx!=null && ctx.HasExponentRange && (ctx.Precision).Sign>0){
            if(ctx.HasFlags){
              ctx.Flags|=PrecisionContext.FlagClamped;
            }
            BigInteger bigexp=ctx.EMin;
            bigexp-=(BigInteger)(ctx.Precision);
            bigexp+=BigInteger.One;
            thisFlags=((thisFlags^otherFlags)&BigNumberFlags.FlagNegative);
            return helper.CreateNewWithFlags(
              BigInteger.Zero,bigexp,thisFlags);
          }
          thisFlags=((thisFlags^otherFlags)&BigNumberFlags.FlagNegative);
          return RoundToPrecision(helper.CreateNewWithFlags(
            BigInteger.Zero,BigInteger.Zero,
            thisFlags),ctx);
        }
      }
      return default(T);
    }
    
    private T RemainderHandleSpecial(T thisValue, T other, PrecisionContext ctx){
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(other);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        T result=HandleNotANumber(thisValue,other,ctx);
        if((Object)result!=(Object)default(T))return result;
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          return SignalInvalid(ctx);
        }
        if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
          return RoundToPrecision(thisValue,ctx);
        }
      }
      if(helper.GetMantissa(other).IsZero){
        return SignalInvalid(ctx);
      }
      return default(T);
    }

    private T MinMaxHandleSpecial(T thisValue, T otherValue, PrecisionContext ctx,
                                  bool isMinOp, bool compareAbs){
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(otherValue);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        // Check this value then the other value for signaling NaN
        if((helper.GetFlags(thisValue)&BigNumberFlags.FlagSignalingNaN)!=0){
          return SignalingNaNInvalid(thisValue,ctx);
        }
        if((helper.GetFlags(otherValue)&BigNumberFlags.FlagSignalingNaN)!=0){
          return SignalingNaNInvalid(otherValue,ctx);
        }
        // Check this value then the other value for quiet NaN
        if((helper.GetFlags(thisValue)&BigNumberFlags.FlagQuietNaN)!=0){
          if((helper.GetFlags(otherValue)&BigNumberFlags.FlagQuietNaN)!=0){
            // both values are quiet NaN
            return ReturnQuietNaN(thisValue,ctx);
          }
          // return "other" for being numeric
          return RoundToPrecision(otherValue,ctx);
        }
        if((helper.GetFlags(otherValue)&BigNumberFlags.FlagQuietNaN)!=0){
          // At this point, "thisValue" can't be NaN,
          // return "thisValue" for being numeric
          return RoundToPrecision(thisValue,ctx);
        }
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          if(compareAbs && (otherFlags&BigNumberFlags.FlagInfinity)==0){
            // treat this as larger
            return (isMinOp) ? RoundToPrecision(otherValue,ctx) : thisValue;
          }
          // This value is infinity
          if(isMinOp){
            return ((thisFlags&BigNumberFlags.FlagNegative)!=0) ?
              thisValue : // if negative, will be less than every other number
              RoundToPrecision(otherValue,ctx); // if positive, will be greater
          } else {
            return ((thisFlags&BigNumberFlags.FlagNegative)==0) ?
              thisValue : // if positive, will be greater than every other number
              RoundToPrecision(otherValue,ctx);
          }
        }
        if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
          if(compareAbs){
            // treat this as larger (the first value
            // won't be infinity at this point
            return (isMinOp) ? RoundToPrecision(thisValue,ctx) : otherValue;
          }
          if(isMinOp){
            return ((otherFlags&BigNumberFlags.FlagNegative)==0) ?
              RoundToPrecision(thisValue,ctx) :
              otherValue;
          } else {
            return ((otherFlags&BigNumberFlags.FlagNegative)!=0) ?
              RoundToPrecision(thisValue,ctx) :
              otherValue;
          }
        }
      }
      return default(T);
    }
    
    private T HandleNotANumber(T thisValue, T other, PrecisionContext ctx){
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(other);
      // Check this value then the other value for signaling NaN
      if((helper.GetFlags(thisValue)&BigNumberFlags.FlagSignalingNaN)!=0){
        return SignalingNaNInvalid(thisValue,ctx);
      }
      if((helper.GetFlags(other)&BigNumberFlags.FlagSignalingNaN)!=0){
        return SignalingNaNInvalid(other,ctx);
      }
      // Check this value then the other value for quiet NaN
      if((helper.GetFlags(thisValue)&BigNumberFlags.FlagQuietNaN)!=0){
        return ReturnQuietNaN(thisValue,ctx);
      }
      if((helper.GetFlags(other)&BigNumberFlags.FlagQuietNaN)!=0){
        return ReturnQuietNaN(other,ctx);
      }
      return default(T);
    }
    
    private T ValueOf(int value, PrecisionContext ctx){
      if(ctx==null || !ctx.HasExponentRange || ctx.ExponentWithinRange(BigInteger.Zero))
        return helper.ValueOf(value);
      return RoundToPrecision(helper.ValueOf(value),ctx);
    }
    private int CompareToHandleSpecialReturnInt(
      T thisValue, T other){
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(other);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        if(((thisFlags|otherFlags)&BigNumberFlags.FlagNaN)!=0){
          throw new ArithmeticException("Either operand is NaN");
        }
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          // thisValue is infinity
          if((thisFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative))==
             (otherFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative)))
            return 0;
          return ((thisFlags&BigNumberFlags.FlagNegative)==0) ? 1 : -1;
        }
        if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
          // the other value is infinity
          if((thisFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative))==
             (otherFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative)))
            return 0;
          return ((otherFlags&BigNumberFlags.FlagNegative)==0) ? -1 : 1;
        }
      }
      return 2;
    }
    
    private T CompareToHandleSpecial(T thisValue, T other, bool treatQuietNansAsSignaling, PrecisionContext ctx){
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(other);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        // Check this value then the other value for signaling NaN
        if((helper.GetFlags(thisValue)&BigNumberFlags.FlagSignalingNaN)!=0){
          return SignalingNaNInvalid(thisValue,ctx);
        }
        if((helper.GetFlags(other)&BigNumberFlags.FlagSignalingNaN)!=0){
          return SignalingNaNInvalid(other,ctx);
        }
        if(treatQuietNansAsSignaling){
          if((helper.GetFlags(thisValue)&BigNumberFlags.FlagQuietNaN)!=0){
            return SignalingNaNInvalid(thisValue,ctx);
          }
          if((helper.GetFlags(other)&BigNumberFlags.FlagQuietNaN)!=0){
            return SignalingNaNInvalid(other,ctx);
          }
        } else {
          // Check this value then the other value for quiet NaN
          if((helper.GetFlags(thisValue)&BigNumberFlags.FlagQuietNaN)!=0){
            return ReturnQuietNaN(thisValue,ctx);
          }
          if((helper.GetFlags(other)&BigNumberFlags.FlagQuietNaN)!=0){
            return ReturnQuietNaN(other,ctx);
          }
        }
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          // thisValue is infinity
          if((thisFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative))==
             (otherFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative)))
            return ValueOf(0,null);
          return ((thisFlags&BigNumberFlags.FlagNegative)==0) ?
            ValueOf(1,null) : ValueOf(-1,null);
        }
        if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
          // the other value is infinity
          if((thisFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative))==
             (otherFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative)))
            return ValueOf(0,null);
          return ((otherFlags&BigNumberFlags.FlagNegative)==0) ?
            ValueOf(-1,null) : ValueOf(1,null);
        }
      }
      return default(T);
    }
    private T SignalingNaNInvalid(T value, PrecisionContext ctx){
      int flags=helper.GetFlags(value);
      if(ctx!=null && ctx.HasFlags){
        ctx.Flags|=PrecisionContext.FlagInvalid;
      }
      return ReturnQuietNaN(value,ctx);
    }
    private T SignalInvalid(PrecisionContext ctx){
      if(support==BigNumberFlags.FiniteOnly)
        throw new ArithmeticException("Invalid operation");
      if(ctx!=null && ctx.HasFlags){
        ctx.Flags|=PrecisionContext.FlagInvalid;
      }
      return helper.CreateNewWithFlags(BigInteger.Zero,BigInteger.Zero,BigNumberFlags.FlagQuietNaN);
    }
    private T SignalInvalidWithMessage(PrecisionContext ctx, String str){
      if(support==BigNumberFlags.FiniteOnly)
        throw new ArithmeticException("Invalid operation");
      if(ctx!=null && ctx.HasFlags){
        ctx.Flags|=PrecisionContext.FlagInvalid;
      }
      return helper.CreateNewWithFlags(BigInteger.Zero,BigInteger.Zero,BigNumberFlags.FlagQuietNaN);
    }
    
    private T SignalOverflow(bool neg){
      return support==BigNumberFlags.FiniteOnly ? default(T) :
        helper.CreateNewWithFlags(
          BigInteger.Zero,BigInteger.Zero,
          (neg ? BigNumberFlags.FlagNegative : 0)|BigNumberFlags.FlagInfinity);
    }
    
    private T SignalDivideByZero(PrecisionContext ctx, bool neg){
      if(support==BigNumberFlags.FiniteOnly)
        throw new DivideByZeroException("Division by zero");
      if(ctx!=null && ctx.HasFlags){
        ctx.Flags|=PrecisionContext.FlagDivideByZero;
      }
      return helper.CreateNewWithFlags(
        BigInteger.Zero,BigInteger.Zero,
        BigNumberFlags.FlagInfinity|(neg ? BigNumberFlags.FlagNegative : 0));
    }

    private bool Round(IShiftAccumulator accum, Rounding rounding,
                       bool neg, FastInteger fastint) {
      bool incremented = false;
      int radix = thisRadix;
      if (rounding == Rounding.HalfUp) {
        if (accum.LastDiscardedDigit >= (radix / 2)) {
          incremented = true;
        }
      } else if (rounding == Rounding.HalfEven) {
        if (accum.LastDiscardedDigit >= (radix / 2)) {
          if ((accum.LastDiscardedDigit > (radix / 2) || accum.OlderDiscardedDigits != 0)) {
            incremented = true;
          } else if (!fastint.IsEvenNumber) {
            incremented = true;
          }
        }
      } else if (rounding == Rounding.Ceiling) {
        if (!neg && (accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
          incremented = true;
        }
      } else if (rounding == Rounding.Floor) {
        if (neg && (accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
          incremented = true;
        }
      } else if (rounding == Rounding.HalfDown) {
        if (accum.LastDiscardedDigit > (radix / 2) ||
            (accum.LastDiscardedDigit == (radix / 2) && accum.OlderDiscardedDigits != 0)) {
          incremented = true;
        }
      } else if (rounding == Rounding.Up) {
        if ((accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
          incremented = true;
        }
      } else if (rounding == Rounding.ZeroFiveUp) {
        if ((accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
          if (radix == 2) {
            incremented = true;
          } else {
            int lastDigit = FastInteger.Copy(fastint).Mod(radix).AsInt32();
            if (lastDigit == 0 || lastDigit == (radix / 2)) {
              incremented = true;
            }
          }
        }
      }
      return incremented;
    }

    private bool RoundGivenDigits(int lastDiscarded, int olderDiscarded, Rounding rounding,
                                  bool neg, BigInteger bigval) {
      bool incremented = false;
      int radix = thisRadix;
      if (rounding == Rounding.HalfUp) {
        if (lastDiscarded >= (radix / 2)) {
          incremented = true;
        }
      } else if (rounding == Rounding.HalfEven) {
        if (lastDiscarded >= (radix / 2)) {
          if ((lastDiscarded > (radix / 2) || olderDiscarded != 0)) {
            incremented = true;
          } else if (!bigval.IsEven) {
            incremented = true;
          }
        }
      } else if (rounding == Rounding.Ceiling) {
        if (!neg && (lastDiscarded | olderDiscarded) != 0) {
          incremented = true;
        }
      } else if (rounding == Rounding.Floor) {
        if (neg && (lastDiscarded | olderDiscarded) != 0) {
          incremented = true;
        }
      } else if (rounding == Rounding.HalfDown) {
        if (lastDiscarded > (radix / 2) ||
            (lastDiscarded == (radix / 2) && olderDiscarded != 0)) {
          incremented = true;
        }
      } else if (rounding == Rounding.Up) {
        if ((lastDiscarded | olderDiscarded) != 0) {
          incremented = true;
        }
      } else if (rounding == Rounding.ZeroFiveUp) {
        if ((lastDiscarded | olderDiscarded) != 0) {
          if (radix == 2) {
            incremented = true;
          } else {
            BigInteger bigdigit = bigval % (BigInteger)radix;
            int lastDigit = (int)bigdigit;
            if (lastDigit == 0 || lastDigit == (radix / 2)) {
              incremented = true;
            }
          }
        }
      }
      return incremented;
    }

    private bool RoundGivenBigInt(IShiftAccumulator accum, Rounding rounding,
                                  bool neg, BigInteger bigval) {
      return RoundGivenDigits(accum.LastDiscardedDigit,accum.OlderDiscardedDigits,rounding,
                              neg,bigval);
    }

    private T EnsureSign(T val, bool negative) {
      if (val == null) return val;
      int flags = helper.GetFlags(val);
      if ((negative && (flags&BigNumberFlags.FlagNegative)==0) ||
          (!negative && (flags&BigNumberFlags.FlagNegative)!=0)) {
        flags&=~BigNumberFlags.FlagNegative;
        flags|=(negative ? BigNumberFlags.FlagNegative : 0);
        return helper.CreateNewWithFlags(
          helper.GetMantissa(val),helper.GetExponent(val),flags);
      }
      return val;
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T DivideToIntegerNaturalScale(
      T thisValue,
      T divisor,
      PrecisionContext ctx
     ) {
      FastInteger desiredScale = FastInteger.FromBig(
        helper.GetExponent(thisValue)).SubtractBig(
        helper.GetExponent(divisor));
      PrecisionContext ctx2=PrecisionContext.ForRounding(Rounding.Down).WithBigPrecision(
        ctx == null ? BigInteger.Zero : ctx.Precision).WithBlankFlags();
      T ret = DivideInternal(thisValue, divisor,
                             ctx2,
                             IntegerModeFixedScale, BigInteger.Zero,
                             null);
      if((ctx2.Flags&(PrecisionContext.FlagInvalid|PrecisionContext.FlagDivideByZero))!=0){
        if(ctx.HasFlags){
          ctx.Flags|=(PrecisionContext.FlagInvalid|PrecisionContext.FlagDivideByZero);
        }
        return ret;
      }
      bool neg = (helper.GetSign(thisValue) < 0) ^ (helper.GetSign(divisor) < 0);
      // Now the exponent's sign can only be 0 or positive
      if (helper.GetMantissa(ret).IsZero) {
        // Value is 0, so just change the exponent
        // to the preferred one
        BigInteger dividendExp=helper.GetExponent(thisValue);
        BigInteger divisorExp=helper.GetExponent(divisor);
        ret = helper.CreateNewWithFlags(BigInteger.Zero,
                                        (dividendExp - (BigInteger)divisorExp),helper.GetFlags(ret));
      } else {
        if (desiredScale.Sign < 0) {
          // Desired scale is negative, shift left
          desiredScale.Negate();
          BigInteger bigmantissa = BigInteger.Abs(helper.GetMantissa(ret));
          bigmantissa = helper.MultiplyByRadixPower(bigmantissa, desiredScale);
          ret = helper.CreateNewWithFlags(
            bigmantissa,
            helper.GetExponent(thisValue) - (BigInteger)(helper.GetExponent(divisor)),
            helper.GetFlags(ret));
        } else if (desiredScale.Sign > 0) {
          // Desired scale is positive, shift away zeros
          // but not after scale is reached
          BigInteger bigmantissa = BigInteger.Abs(helper.GetMantissa(ret));
          FastInteger fastexponent = FastInteger.FromBig(helper.GetExponent(ret));
          BigInteger bigradix = (BigInteger)(thisRadix);
          while(true){
            if(desiredScale.CompareTo(fastexponent)==0)
              break;
            BigInteger bigrem;
            BigInteger bigquo=BigInteger.DivRem(bigmantissa,bigradix,out bigrem);
            if(!bigrem.IsZero)
              break;
            bigmantissa=bigquo;
            fastexponent.AddInt(1);
          }
          ret = helper.CreateNewWithFlags(bigmantissa, fastexponent.AsBigInteger(),
                                          helper.GetFlags(ret));
        }
      }
      if (ctx != null) {
        ret = RoundToPrecision(ret, ctx);
      }
      ret = EnsureSign(ret, neg);
      return ret;
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T DivideToIntegerZeroScale(
      T thisValue,
      T divisor,
      PrecisionContext ctx
     ) {
      PrecisionContext ctx2=PrecisionContext.ForRounding(Rounding.Down).WithBigPrecision(
        ctx == null ? BigInteger.Zero : ctx.Precision).WithBlankFlags();
      T ret = DivideInternal(thisValue, divisor,
                             ctx2,
                             IntegerModeFixedScale, BigInteger.Zero,
                             null);
      if((ctx2.Flags&(PrecisionContext.FlagInvalid|PrecisionContext.FlagDivideByZero))!=0){
        if(ctx.HasFlags){
          ctx.Flags|=(ctx2.Flags&(PrecisionContext.FlagInvalid|PrecisionContext.FlagDivideByZero));
        }
        return ret;
      }
      if (ctx != null) {
        ctx2 = ctx.WithBlankFlags().WithUnlimitedExponents();
        ret = RoundToPrecision(ret, ctx2);
        if ((ctx2.Flags & PrecisionContext.FlagRounded) != 0) {
          return SignalInvalid(ctx);
        }
      }
      return ret;
    }
    
    /// <summary> </summary>
    /// <param name='value'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Abs(T value, PrecisionContext ctx){
      int flags=helper.GetFlags(value);
      if((flags&BigNumberFlags.FlagSignalingNaN)!=0){
        return SignalingNaNInvalid(value,ctx);
      }
      if((flags&BigNumberFlags.FlagQuietNaN)!=0){
        return ReturnQuietNaN(value,ctx);
      }
      if((flags&BigNumberFlags.FlagNegative)!=0){
        return RoundToPrecision(
          helper.CreateNewWithFlags(
            helper.GetMantissa(value),helper.GetExponent(value),
            flags&~BigNumberFlags.FlagNegative),
          ctx);
      }
      return RoundToPrecision(value,ctx);
    }

    /// <summary> </summary>
    /// <param name='value'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Negate(T value, PrecisionContext ctx){
      int flags=helper.GetFlags(value);
      if((flags&BigNumberFlags.FlagSignalingNaN)!=0){
        return SignalingNaNInvalid(value,ctx);
      }
      if((flags&BigNumberFlags.FlagQuietNaN)!=0){
        return ReturnQuietNaN(value,ctx);
      }
      BigInteger mant=helper.GetMantissa(value);
      if((flags&BigNumberFlags.FlagInfinity)==0 && mant.IsZero){
        if((flags&BigNumberFlags.FlagNegative)==0){
          // positive 0 minus positive 0 is always positive 0
          return RoundToPrecision(helper.CreateNewWithFlags(
            mant,helper.GetExponent(value),
            flags&~BigNumberFlags.FlagNegative),ctx);
        } else if((ctx!=null && ctx.Rounding== Rounding.Floor)){
          // positive 0 minus negative 0 is negative 0 only if
          // the rounding is Floor
          return RoundToPrecision(helper.CreateNewWithFlags(
            mant,helper.GetExponent(value),
            flags|BigNumberFlags.FlagNegative),ctx);
        } else {
          return RoundToPrecision(helper.CreateNewWithFlags(
            mant,helper.GetExponent(value),
            flags&~BigNumberFlags.FlagNegative),ctx);
        }
      }
      flags=flags^BigNumberFlags.FlagNegative;
      return RoundToPrecision(
        helper.CreateNewWithFlags(mant,helper.GetExponent(value),
                                  flags),
        ctx);
    }

    private T AbsRaw(T value){
      return EnsureSign(value,false);
    }

    private T NegateRaw(T val){
      if (val == null) return val;
      int sign = helper.GetFlags(val)&BigNumberFlags.FlagNegative;
      return helper.CreateNewWithFlags(helper.GetMantissa(val),helper.GetExponent(val),
                                       sign==0 ? BigNumberFlags.FlagNegative : 0);
    }

    private static void TransferFlags(PrecisionContext ctxDst, PrecisionContext ctxSrc){
      if(ctxDst!=null && ctxDst.HasFlags){
        if((ctxSrc.Flags&(PrecisionContext.FlagInvalid|PrecisionContext.FlagDivideByZero))!=0){
          ctxDst.Flags|=(ctxSrc.Flags&(PrecisionContext.FlagInvalid|PrecisionContext.FlagDivideByZero));
        } else {
          ctxDst.Flags|=ctxSrc.Flags;
        }
      }
    }
    
    /// <summary>Finds the remainder that results when dividing two T objects.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>The remainder of the two objects.</returns>
    public T Remainder(
      T thisValue,
      T divisor,
      PrecisionContext ctx
     ) {
      PrecisionContext ctx2=ctx==null ? null : ctx.WithBlankFlags();
      T ret=RemainderHandleSpecial(thisValue,divisor,ctx2);
      if((Object)ret!=(Object)default(T)){
        TransferFlags(ctx,ctx2);
        return ret;
      }
      ret=DivideToIntegerZeroScale(thisValue,divisor,ctx2);
      ret=Add(thisValue,NegateRaw(Multiply(ret,divisor,null)),ctx2);
      ret=EnsureSign(ret,(helper.GetFlags(thisValue)&BigNumberFlags.FlagNegative)!=0);
      TransferFlags(ctx,ctx2);
      return ret;
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RemainderNear(
      T thisValue,
      T divisor,
      PrecisionContext ctx
     ) {
      PrecisionContext ctx2=ctx==null ?
        PrecisionContext.ForRounding(Rounding.HalfEven).WithBlankFlags() :
        ctx.WithRounding(Rounding.HalfEven).WithBlankFlags();
      T ret=RemainderHandleSpecial(thisValue,divisor,ctx2);
      if((Object)ret!=(Object)default(T)){
        TransferFlags(ctx,ctx2);
        return ret;
      }
      ret=DivideInternal(thisValue,divisor,ctx2,
                         IntegerModeFixedScale,BigInteger.Zero,null);
      if((ctx2.Flags&(PrecisionContext.FlagInvalid))!=0){
        return SignalInvalid(ctx);
      }
      ctx2=ctx2.WithBlankFlags();
      ret=RoundToPrecision(ret,ctx2);
      if((ctx2.Flags&(PrecisionContext.FlagRounded|PrecisionContext.FlagInvalid))!=0){
        return SignalInvalid(ctx);
      }
      ctx2=ctx==null ? null : ctx.WithBlankFlags();
      T ret2=Add(thisValue,NegateRaw(Multiply(ret,divisor,null)),ctx2);
      if((ctx2.Flags&(PrecisionContext.FlagInvalid))!=0){
        return SignalInvalid(ctx);
      }
      if(helper.GetFlags(ret2)==0 && helper.GetMantissa(ret2).IsZero){
        ret2=EnsureSign(ret2,(helper.GetFlags(thisValue)&BigNumberFlags.FlagNegative)!=0);
      }
      TransferFlags(ctx,ctx2);
      return ret2;
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T NextMinus(
      T thisValue,
      PrecisionContext ctx
     ){
      if((ctx)==null)throw new ArgumentNullException("ctx");
      if((ctx.Precision).Sign<=0)throw new ArgumentException("ctx.Precision"+" not less than "+"0"+" ("+Convert.ToString((ctx.Precision),System.Globalization.CultureInfo.InvariantCulture)+")");
      if(!(ctx.HasExponentRange))throw new ArgumentException("doesn't satisfy ctx.HasExponentRange");
      int flags=helper.GetFlags(thisValue);
      if((flags&BigNumberFlags.FlagSignalingNaN)!=0){
        return SignalingNaNInvalid(thisValue,ctx);
      }
      if((flags&BigNumberFlags.FlagQuietNaN)!=0){
        return ReturnQuietNaN(thisValue,ctx);
      }
      if((flags&BigNumberFlags.FlagInfinity)!=0){
        if((flags&BigNumberFlags.FlagNegative)!=0){
          return thisValue;
        } else {
          BigInteger bigexp2 = ctx.EMax;
          bigexp2+=BigInteger.One;
          bigexp2-=(BigInteger)(ctx.Precision);
          BigInteger overflowMant=helper.MultiplyByRadixPower(
            BigInteger.One,FastInteger.FromBig(ctx.Precision));
          overflowMant -= BigInteger.One;
          return helper.CreateNewWithFlags(overflowMant,bigexp2,0);
        }
      }
      FastInteger minexp=FastInteger.FromBig(ctx.EMin).SubtractBig(ctx.Precision).AddInt(1);
      FastInteger bigexp=FastInteger.FromBig(helper.GetExponent(thisValue));
      if(bigexp.CompareTo(minexp)<=0){
        // Use a smaller exponent if the input exponent is already
        // very small
        minexp=FastInteger.Copy(bigexp).SubtractInt(2);
      }
      T quantum=helper.CreateNewWithFlags(
        BigInteger.One,
        minexp.AsBigInteger(),BigNumberFlags.FlagNegative);
      PrecisionContext ctx2;
      ctx2=ctx.WithRounding(Rounding.Floor);
      return Add(thisValue,quantum,ctx2);
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='otherValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T NextToward(
      T thisValue,
      T otherValue,
      PrecisionContext ctx
     ){
      if((ctx)==null)throw new ArgumentNullException("ctx");
      if((ctx.Precision).Sign<=0)throw new ArgumentException("ctx.Precision"+" not less than "+"0"+" ("+Convert.ToString((ctx.Precision),System.Globalization.CultureInfo.InvariantCulture)+")");
      if(!(ctx.HasExponentRange))throw new ArgumentException("doesn't satisfy ctx.HasExponentRange");
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(otherValue);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        T result=HandleNotANumber(thisValue,otherValue,ctx);
        if((Object)result!=(Object)default(T))return result;
      }
      PrecisionContext ctx2;
      int cmp=CompareTo(thisValue,otherValue);
      if(cmp==0){
        return RoundToPrecision(
          EnsureSign(thisValue,(otherFlags&BigNumberFlags.FlagNegative)!=0),
          ctx.WithNoFlags());
      } else {
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          if((thisFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative))==
             (otherFlags&(BigNumberFlags.FlagInfinity|BigNumberFlags.FlagNegative))){
            // both values are the same infinity
            return thisValue;
          } else {
            BigInteger bigexp2 = ctx.EMax;
            bigexp2+=BigInteger.One;
            bigexp2-=(BigInteger)(ctx.Precision);
            BigInteger overflowMant=helper.MultiplyByRadixPower(
              BigInteger.One,FastInteger.FromBig(ctx.Precision));
            overflowMant -= BigInteger.One;
            return helper.CreateNewWithFlags(overflowMant,bigexp2,
                                             thisFlags&BigNumberFlags.FlagNegative);
          }
        }
        FastInteger minexp=FastInteger.FromBig(ctx.EMin).SubtractBig(ctx.Precision).AddInt(1);
        FastInteger bigexp=FastInteger.FromBig(helper.GetExponent(thisValue));
        if(bigexp.CompareTo(minexp)<0){
          // Use a smaller exponent if the input exponent is already
          // very small
          minexp=FastInteger.Copy(bigexp).SubtractInt(2);
        } else {
          // Ensure the exponent is lower than the exponent range
          // (necessary to flag underflow correctly)
          minexp.SubtractInt(2);
        }
        T quantum=helper.CreateNewWithFlags(
          BigInteger.One,minexp.AsBigInteger(),
          (cmp>0) ? BigNumberFlags.FlagNegative : 0);
        T val=thisValue;
        ctx2=ctx.WithRounding((cmp>0) ? Rounding.Floor : Rounding.Ceiling).WithBlankFlags();
        val=Add(val,quantum,ctx2);
        if((ctx2.Flags&(PrecisionContext.FlagOverflow|PrecisionContext.FlagUnderflow))==0){
          // Don't set flags except on overflow or underflow
          // TODO: Pending clarification from Mike Cowlishaw,
          // author of the Decimal Arithmetic test cases from
          // speleotrove.com
          ctx2.Flags=0;
        }
        if((ctx2.Flags&(PrecisionContext.FlagUnderflow))!=0){
          BigInteger bigmant=BigInteger.Abs(helper.GetMantissa(val));
          BigInteger maxmant=helper.MultiplyByRadixPower(
            BigInteger.One,FastInteger.FromBig(ctx.Precision).SubtractInt(1));
          if(bigmant.CompareTo(maxmant)>=0 || (ctx.Precision).CompareTo(BigInteger.One)==0){
            // don't treat max-precision results as having underflowed
            ctx2.Flags=0;
          }
        }
        if(ctx.HasFlags){
          ctx.Flags|=ctx2.Flags;
        }
        return val;
      }
    }
    
    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T NextPlus(
      T thisValue,
      PrecisionContext ctx
     ){
      if((ctx)==null)throw new ArgumentNullException("ctx");
      if((ctx.Precision).Sign<=0)throw new ArgumentException("ctx.Precision"+" not less than "+"0"+" ("+Convert.ToString((ctx.Precision),System.Globalization.CultureInfo.InvariantCulture)+")");
      if(!(ctx.HasExponentRange))throw new ArgumentException("doesn't satisfy ctx.HasExponentRange");
      int flags=helper.GetFlags(thisValue);
      if((flags&BigNumberFlags.FlagSignalingNaN)!=0){
        return SignalingNaNInvalid(thisValue,ctx);
      }
      if((flags&BigNumberFlags.FlagQuietNaN)!=0){
        return ReturnQuietNaN(thisValue,ctx);
      }
      if((flags&BigNumberFlags.FlagInfinity)!=0){
        if((flags&BigNumberFlags.FlagNegative)!=0){
          BigInteger bigexp2 = ctx.EMax;
          bigexp2+=BigInteger.One;
          bigexp2-=(BigInteger)(ctx.Precision);
          BigInteger overflowMant=helper.MultiplyByRadixPower(
            BigInteger.One,FastInteger.FromBig(ctx.Precision));
          overflowMant -= BigInteger.One;
          return helper.CreateNewWithFlags(overflowMant,bigexp2,BigNumberFlags.FlagNegative);
        } else {
          return thisValue;
        }
      }
      FastInteger minexp=FastInteger.FromBig(ctx.EMin).SubtractBig(ctx.Precision).AddInt(1);
      FastInteger bigexp=FastInteger.FromBig(helper.GetExponent(thisValue));
      if(bigexp.CompareTo(minexp)<=0){
        // Use a smaller exponent if the input exponent is already
        // very small
        minexp=FastInteger.Copy(bigexp).SubtractInt(2);
      }
      T quantum=helper.CreateNewWithFlags(
        BigInteger.One,
        minexp.AsBigInteger(),0);
      PrecisionContext ctx2;
      T val=thisValue;
      ctx2=ctx.WithRounding(Rounding.Ceiling);
      return Add(val,quantum,ctx2);
    }

    /// <summary>Divides two T objects.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object.</param>
    /// <param name='desiredExponent'>A BigInteger object.</param>
    /// <param name='ctx'>A PrecisionContext object. Precision is ignored.</param>
    /// <returns>The quotient of the two objects.</returns>
    public T DivideToExponent(
      T thisValue,
      T divisor,
      BigInteger desiredExponent,
      PrecisionContext ctx
     ) {
      if(ctx!=null && !ctx.ExponentWithinRange(desiredExponent))
        return SignalInvalidWithMessage(ctx,"Exponent not within exponent range: "+desiredExponent.ToString());
      PrecisionContext ctx2 = (ctx == null) ?
        PrecisionContext.ForRounding(Rounding.HalfDown) :
        ctx.WithUnlimitedExponents().WithPrecision(0);
      T ret = DivideInternal(thisValue, divisor,
                             ctx2,
                             IntegerModeFixedScale, desiredExponent,null);
      if (ctx != null && ctx.HasFlags) {
        ctx.Flags |= ctx2.Flags;
      }
      return ret;
    }

    /// <summary>Divides two T objects.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='divisor'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>The quotient of the two objects.</returns>
    public T Divide(
      T thisValue,
      T divisor,
      PrecisionContext ctx
     ) {
      return DivideInternal(thisValue, divisor,
                            ctx, IntegerModeRegular, BigInteger.Zero, null);
    }

    private BigInteger RoundToScale(
      BigInteger mantissa, // Assumes mantissa is nonnegative
      BigInteger remainder,// Assumes value is nonnegative
      BigInteger divisor,// Assumes value is nonnegative
      FastInteger shift,// Number of digits to shift right
      bool neg,// Whether return value should be negated
      PrecisionContext ctx
     ) {
      IShiftAccumulator accum;
      Rounding rounding = (ctx == null) ? Rounding.HalfEven : ctx.Rounding;
      int lastDiscarded = 0;
      int olderDiscarded = 0;
      if (!(remainder.IsZero)) {
        if(rounding== Rounding.HalfDown ||
           rounding== Rounding.HalfUp ||
           rounding== Rounding.HalfEven){
          BigInteger halfDivisor = (divisor >> 1);
          int cmpHalf = remainder.CompareTo(halfDivisor);
          if ((cmpHalf == 0) && divisor.IsEven) {
            // remainder is exactly half
            lastDiscarded = (thisRadix / 2);
            olderDiscarded = 0;
          } else if (cmpHalf > 0) {
            // remainder is greater than half
            lastDiscarded = (thisRadix / 2);
            olderDiscarded = 1;
          } else {
            // remainder is less than half
            lastDiscarded = 0;
            olderDiscarded = 1;
          }
        } else {
          // Rounding mode doesn't care about
          // whether remainder is exactly half
          if (rounding == Rounding.Unnecessary)
            throw new ArithmeticException("Rounding was required");
          lastDiscarded = 1;
          olderDiscarded = 1;
        }
      }
      int flags = 0;
      BigInteger newmantissa=mantissa;
      if(shift.Sign==0){
        if ((lastDiscarded|olderDiscarded) != 0) {
          flags |= PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
          if (rounding == Rounding.Unnecessary)
            throw new ArithmeticException("Rounding was required");
          if (RoundGivenDigits(lastDiscarded,olderDiscarded,
                               rounding, neg, newmantissa)) {
            newmantissa += BigInteger.One;
          }
        }
      } else {
        accum = helper.CreateShiftAccumulatorWithDigits(
          mantissa, lastDiscarded, olderDiscarded);
        accum.ShiftRight(shift);
        newmantissa = accum.ShiftedInt;
        if ((accum.DiscardedDigitCount).Sign != 0 ||
            (accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
          if (!mantissa.IsZero)
            flags |= PrecisionContext.FlagRounded;
          if ((accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
            flags |= PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
            if (rounding == Rounding.Unnecessary)
              throw new ArithmeticException("Rounding was required");
          }
          if (RoundGivenBigInt(accum, rounding, neg, newmantissa)) {
            newmantissa += BigInteger.One;
          }
        }
      }
      if (ctx.HasFlags) {
        ctx.Flags |= flags;
      }
      if (neg) {
        newmantissa = -newmantissa;
      }
      return newmantissa;
    }

    private const int IntegerModeFixedScale = 1;
    private const int IntegerModeRegular = 0;

    private const int NonTerminatingCheckThreshold = 5;
    
    private T DivideInternal(
      T thisValue,
      T divisor,
      PrecisionContext ctx,
      int integerMode,
      BigInteger desiredExponent,
      T[] remainder
     ) {
      T ret=DivisionHandleSpecial(thisValue,divisor,ctx);
      if((Object)ret!=(Object)default(T))return ret;
      int signA = helper.GetSign(thisValue);
      int signB = helper.GetSign(divisor);
      if(signB==0){
        if(signA==0){
          return SignalInvalid(ctx);
        }
        return SignalDivideByZero(
          ctx,
          ((helper.GetFlags(thisValue)&BigNumberFlags.FlagNegative)!=0)^
          ((helper.GetFlags(divisor)&BigNumberFlags.FlagNegative)!=0));
      }
      int radix=thisRadix;
      if (signA == 0) {
        T retval=default(T);
        if (integerMode == IntegerModeFixedScale) {
          int newflags=((helper.GetFlags(thisValue)&BigNumberFlags.FlagNegative))^
            ((helper.GetFlags(divisor)&BigNumberFlags.FlagNegative));
          retval=helper.CreateNewWithFlags(BigInteger.Zero, desiredExponent,newflags);
        } else {
          BigInteger dividendExp=helper.GetExponent(thisValue);
          BigInteger divisorExp=helper.GetExponent(divisor);
          int newflags=((helper.GetFlags(thisValue)&BigNumberFlags.FlagNegative))^
            ((helper.GetFlags(divisor)&BigNumberFlags.FlagNegative));
          retval=RoundToPrecision(helper.CreateNewWithFlags(
            BigInteger.Zero, (dividendExp - (BigInteger)divisorExp),
            newflags), ctx);
        }
        if(remainder!=null){
          remainder[0]=retval;
        }
        return retval;
      } else {
        BigInteger mantissaDividend = BigInteger.Abs(helper.GetMantissa(thisValue));
        BigInteger mantissaDivisor = BigInteger.Abs(helper.GetMantissa(divisor));
        FastInteger expDividend = FastInteger.FromBig(helper.GetExponent(thisValue));
        FastInteger expDivisor = FastInteger.FromBig(helper.GetExponent(divisor));
        FastInteger expdiff = FastInteger.Copy(expDividend).Subtract(expDivisor);
        FastInteger adjust = new FastInteger(0);
        FastInteger result = new FastInteger(0);
        FastInteger naturalExponent = FastInteger.Copy(expdiff);
        bool resultNeg = (helper.GetFlags(thisValue)&BigNumberFlags.FlagNegative)!=
          (helper.GetFlags(divisor)&BigNumberFlags.FlagNegative);
        FastInteger fastPrecision=(ctx==null) ? new FastInteger(0) :
          FastInteger.FromBig(ctx.Precision);
        if(integerMode==IntegerModeFixedScale){
          FastInteger shift;
          BigInteger rem;
          FastInteger fastDesiredExponent = FastInteger.FromBig(desiredExponent);
          if(ctx!=null && ctx.HasFlags && fastDesiredExponent.CompareTo(naturalExponent)>0){
            // Treat as rounded if the desired exponent is greater
            // than the "ideal" exponent
            ctx.Flags|=PrecisionContext.FlagRounded;
          }
          if(expdiff.CompareTo(fastDesiredExponent)<=0){
            shift=FastInteger.Copy(fastDesiredExponent).Subtract(expdiff);
            BigInteger quo=BigInteger.DivRem(mantissaDividend,mantissaDivisor,out rem);
            quo=RoundToScale(quo,rem,mantissaDivisor,shift,resultNeg,ctx);
            return helper.CreateNewWithFlags(quo,desiredExponent,resultNeg ?
                                             BigNumberFlags.FlagNegative : 0);
          } else if (ctx != null && (ctx.Precision).Sign!=0 &&
                     FastInteger.Copy(expdiff).SubtractInt(8).CompareTo(fastPrecision) > 0
                    ) { // NOTE: 8 guard digits
            // Result would require a too-high precision since
            // exponent difference is much higher
            return SignalInvalidWithMessage(ctx,"Result can't fit the precision");
          } else {
            shift=FastInteger.Copy(expdiff).Subtract(fastDesiredExponent);
            mantissaDividend=helper.MultiplyByRadixPower(mantissaDividend,shift);
            BigInteger quo=BigInteger.DivRem(mantissaDividend,mantissaDivisor,out rem);
            quo=RoundToScale(quo,rem,mantissaDivisor,new FastInteger(0),resultNeg,ctx);
            return helper.CreateNewWithFlags(quo,desiredExponent,resultNeg ?
                                             BigNumberFlags.FlagNegative : 0);
          }
        }
        FastInteger resultPrecision = new FastInteger(1);
        int mantcmp = mantissaDividend.CompareTo(mantissaDivisor);
        if (mantcmp < 0) {
          // dividend mantissa is less than divisor mantissa
          FastInteger dividendPrecision =
            helper.CreateShiftAccumulator(mantissaDividend).GetDigitLength();
          FastInteger divisorPrecision =
            helper.CreateShiftAccumulator(mantissaDivisor).GetDigitLength();
          divisorPrecision.Subtract(dividendPrecision);
          if(divisorPrecision.Sign==0)
            divisorPrecision.AddInt(1);
          // multiply dividend mantissa so precisions are the same
          // (except if they're already the same, in which case multiply
          // by radix)
          mantissaDividend = helper.MultiplyByRadixPower(
            mantissaDividend, divisorPrecision);
          adjust.Add(divisorPrecision);
          if (mantissaDividend.CompareTo(mantissaDivisor) < 0) {
            // dividend mantissa is still less, multiply once more
            if(radix==2){
              mantissaDividend<<=1;
            } else {
              mantissaDividend*=(BigInteger)radix;
            }
            adjust.AddInt(1);
          }
        } else if (mantcmp > 0) {
          // dividend mantissa is greater than divisor mantissa
          FastInteger dividendPrecision =
            helper.CreateShiftAccumulator(mantissaDividend).GetDigitLength();
          FastInteger divisorPrecision =
            helper.CreateShiftAccumulator(mantissaDivisor).GetDigitLength();
          dividendPrecision.Subtract(divisorPrecision);
          BigInteger oldMantissaB = mantissaDivisor;
          mantissaDivisor = helper.MultiplyByRadixPower(
            mantissaDivisor, dividendPrecision);
          adjust.Subtract(dividendPrecision);
          if (mantissaDividend.CompareTo(mantissaDivisor) < 0) {
            // dividend mantissa is now less, divide by radix power
            if (dividendPrecision.CompareToInt(1)==0) {
              // no need to divide here, since that would just undo
              // the multiplication
              mantissaDivisor = oldMantissaB;
            } else {
              BigInteger bigpow = (BigInteger)(radix);
              mantissaDivisor /= bigpow;
            }
            adjust.AddInt(1);
          }
        }
        bool atMaxPrecision=false;
        if (mantcmp == 0) {
          result = new FastInteger(1);
          mantissaDividend = BigInteger.Zero;
        } else {
          int check = 0;
          FastInteger divs=FastInteger.FromBig(mantissaDivisor);
          FastInteger divd=FastInteger.FromBig(mantissaDividend);
          FastInteger divsHalfRadix=null;
          if(radix!=2){
            divsHalfRadix=FastInteger.FromBig(mantissaDivisor).Multiply(radix/2);
          }
          bool hasPrecision=ctx != null && (ctx.Precision).Sign!=0;
          while (true) {
            bool remainderZero=false;
            if (check == NonTerminatingCheckThreshold && !hasPrecision &&
                integerMode == IntegerModeRegular) {
              // Check for a non-terminating radix expansion
              // if using unlimited precision and not in integer
              // mode
              if (!helper.HasTerminatingRadixExpansion(
                divd.AsBigInteger(), mantissaDivisor)) {
                throw new ArithmeticException("Result would have a nonterminating expansion");
              }
              check++;
            } else if (check < NonTerminatingCheckThreshold) {
              check++;
            }
            int count=0;
            if(divsHalfRadix!=null && divd.CompareTo(divsHalfRadix)>=0){
              divd.Subtract(divsHalfRadix);
              count+=radix/2;
            }
            while(divd.CompareTo(divs)>=0){
              divd.Subtract(divs);
              count++;
            }
            result.AddInt(count);
            remainderZero=(divd.Sign==0);
            if (hasPrecision && resultPrecision.CompareTo(fastPrecision) == 0) {
              mantissaDividend=divd.AsBigInteger();
              atMaxPrecision=true;
              break;
            }
            if (remainderZero && adjust.Sign >= 0) {
              mantissaDividend=divd.AsBigInteger();
              break;
            }
            adjust.AddInt(1);
            if (result.Sign != 0) {
              resultPrecision.AddInt(1);
            }
            result.Multiply(radix);
            divd.Multiply(radix);
          }
        }
        // mantissaDividend now has the remainder
        FastInteger exp = FastInteger.Copy(expdiff).Subtract(adjust);
        Rounding rounding=(ctx==null) ? Rounding.HalfEven : ctx.Rounding;
        int lastDiscarded = 0;
        int olderDiscarded = 0;
        if (!(mantissaDividend.IsZero)) {
          if(rounding==Rounding.HalfDown ||
             rounding==Rounding.HalfEven ||
             rounding==Rounding.HalfUp
            ){
            BigInteger halfDivisor = (mantissaDivisor >> 1);
            int cmpHalf = mantissaDividend.CompareTo(halfDivisor);
            if ((cmpHalf == 0) && mantissaDivisor.IsEven) {
              // remainder is exactly half
              lastDiscarded = (radix / 2);
              olderDiscarded = 0;
            } else if (cmpHalf > 0) {
              // remainder is greater than half
              lastDiscarded = (radix / 2);
              olderDiscarded = 1;
            } else {
              // remainder is less than half
              lastDiscarded = 0;
              olderDiscarded = 1;
            }
          } else {
            if(rounding==Rounding.Unnecessary)
              throw new ArithmeticException("Rounding was required");
            lastDiscarded=1;
            olderDiscarded=1;
          }
        }
        BigInteger bigResult = result.AsBigInteger();
        BigInteger posBigResult=bigResult;
        if(ctx!=null && ctx.HasFlags && exp.CompareTo(naturalExponent)>0){
          // Treat as rounded if the true exponent is greater
          // than the "ideal" exponent
          ctx.Flags|=PrecisionContext.FlagRounded;
        }
        BigInteger bigexp=exp.AsBigInteger();
        T retval=helper.CreateNewWithFlags(
          bigResult, bigexp, resultNeg ? BigNumberFlags.FlagNegative : 0);
        if(atMaxPrecision && !ctx.HasExponentRange){
          // At this point, the check for rounding with Rounding.Unnecessary
          // already occurred above
          if(!RoundGivenDigits(lastDiscarded,olderDiscarded,rounding,resultNeg,posBigResult)){
            if(ctx!=null && ctx.HasFlags && (lastDiscarded|olderDiscarded)!=0){
              ctx.Flags|=PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
            }
            return retval;
          } else if(posBigResult.IsEven && (thisRadix&1)==0){
            posBigResult+=BigInteger.One;
            if(ctx!=null && ctx.HasFlags && (lastDiscarded|olderDiscarded)!=0){
              ctx.Flags|=PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
            }
            return helper.CreateNewWithFlags(posBigResult,bigexp,
                                             resultNeg ? BigNumberFlags.FlagNegative : 0);
          }
        }
        if(atMaxPrecision && ctx.HasExponentRange){
          BigInteger fastAdjustedExp = FastInteger.Copy(exp)
            .AddBig(ctx.Precision).SubtractInt(1).AsBigInteger();
          if(fastAdjustedExp.CompareTo(ctx.EMin)>=0 && fastAdjustedExp.CompareTo(ctx.EMax)<=0){
            // At this point, the check for rounding with Rounding.Unnecessary
            // already occurred above
            if(!RoundGivenDigits(lastDiscarded,olderDiscarded,rounding,resultNeg,posBigResult)){
              if(ctx!=null && ctx.HasFlags && (lastDiscarded|olderDiscarded)!=0){
                ctx.Flags|=PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
              }
              return retval;
            } else if(posBigResult.IsEven && (thisRadix&1)==0){
              posBigResult+=BigInteger.One;
              if(ctx!=null && ctx.HasFlags && (lastDiscarded|olderDiscarded)!=0){
                ctx.Flags|=PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
              }
              return helper.CreateNewWithFlags(posBigResult,bigexp,
                                               resultNeg ? BigNumberFlags.FlagNegative : 0);
            }
          }
        }
        return RoundToPrecisionWithShift(
          retval,
          ctx,
          lastDiscarded, olderDiscarded, new FastInteger(0),false);
      }
    }

    /// <summary> Gets the lesser value between two values, ignoring their
    /// signs. If the absolute values are equal, has the same effect as Min.
    /// </summary>
    /// <returns>A T object.</returns>
    /// <param name='a'>A T object.</param>
    /// <param name='b'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    public T MinMagnitude(T a, T b, PrecisionContext ctx) {
      if (a == null) throw new ArgumentNullException("a");
      if (b == null) throw new ArgumentNullException("b");
      // Handle infinity and NaN
      T result=MinMaxHandleSpecial(a,b,ctx,true,true);
      if((Object)result!=(Object)default(T))return result;
      int cmp = CompareTo(AbsRaw(a), AbsRaw(b));
      if (cmp == 0) return Min(a, b, ctx);
      return (cmp < 0) ? RoundToPrecision(a,ctx) :
        RoundToPrecision(b,ctx);
    }
    /// <summary> Gets the greater value between two values, ignoring their
    /// signs. If the absolute values are equal, has the same effect as Max.
    /// </summary>
    /// <returns>A T object.</returns>
    /// <param name='a'>A T object.</param>
    /// <param name='b'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    public T MaxMagnitude(T a, T b, PrecisionContext ctx) {
      if (a == null) throw new ArgumentNullException("a");
      if (b == null) throw new ArgumentNullException("b");
      // Handle infinity and NaN
      T result=MinMaxHandleSpecial(a,b,ctx,false,true);
      if((Object)result!=(Object)default(T))return result;
      int cmp = CompareTo(AbsRaw(a), AbsRaw(b));
      if (cmp == 0) return Max(a, b, ctx);
      return (cmp > 0) ? RoundToPrecision(a,ctx) :
        RoundToPrecision(b,ctx);
    }
    /// <summary> Gets the greater value between two T values. </summary>
    /// <returns>The larger value of the two objects.</returns>
    /// <param name='a'>A T object.</param>
    /// <param name='b'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    public T Max(T a, T b, PrecisionContext ctx) {
      if (a == null) throw new ArgumentNullException("a");
      if (b == null) throw new ArgumentNullException("b");
      // Handle infinity and NaN
      T result=MinMaxHandleSpecial(a,b,ctx,false,false);
      if((Object)result!=(Object)default(T))return result;
      int cmp = CompareTo(a, b);
      if (cmp != 0)
        return cmp < 0 ? RoundToPrecision(b,ctx) :
          RoundToPrecision(a,ctx);
      int flagNegA=(helper.GetFlags(a)&BigNumberFlags.FlagNegative);
      if(flagNegA!=(helper.GetFlags(b)&BigNumberFlags.FlagNegative)){
        return (flagNegA!=0) ? RoundToPrecision(b,ctx) :
          RoundToPrecision(a,ctx);
      }
      if (flagNegA==0) {
        return helper.GetExponent(a).CompareTo(helper.GetExponent(b)) > 0 ? RoundToPrecision(a,ctx) :
          RoundToPrecision(b,ctx);
      } else {
        return helper.GetExponent(a).CompareTo(helper.GetExponent(b)) > 0 ? RoundToPrecision(b,ctx) :
          RoundToPrecision(a,ctx);
      }
    }

    /// <summary> Gets the lesser value between two T values.</summary>
    /// <returns>The smaller value of the two objects.</returns>
    /// <param name='a'>A T object.</param>
    /// <param name='b'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    public T Min(T a, T b, PrecisionContext ctx) {
      if (a == null) throw new ArgumentNullException("a");
      if (b == null) throw new ArgumentNullException("b");
      // Handle infinity and NaN
      T result=MinMaxHandleSpecial(a,b,ctx,true,false);
      if((Object)result!=(Object)default(T))return result;
      int cmp = CompareTo(a, b);
      if (cmp != 0)
        return cmp > 0 ? RoundToPrecision(b,ctx) :
          RoundToPrecision(a,ctx);
      int signANeg=helper.GetFlags(a)&BigNumberFlags.FlagNegative;
      if(signANeg!=(helper.GetFlags(b)&BigNumberFlags.FlagNegative)){
        return (signANeg!=0) ? RoundToPrecision(a,ctx) :
          RoundToPrecision(b,ctx);
      }
      if (signANeg==0) {
        return (helper.GetExponent(a)).CompareTo(helper.GetExponent(b)) > 0 ? RoundToPrecision(b,ctx) :
          RoundToPrecision(a,ctx);
      } else {
        return (helper.GetExponent(a)).CompareTo(helper.GetExponent(b)) > 0 ? RoundToPrecision(a,ctx) :
          RoundToPrecision(b,ctx);
      }
    }

    /// <summary>Multiplies two T objects.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>The product of the two objects.</returns>
    /// <param name='other'>A T object.</param>
    public T Multiply(T thisValue, T other, PrecisionContext ctx) {
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(other);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        T result=HandleNotANumber(thisValue,other,ctx);
        if((Object)result!=(Object)default(T))return result;
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          // Attempt to multiply infinity by 0
          if((otherFlags&BigNumberFlags.FlagSpecial)==0 && helper.GetMantissa(other).IsZero)
            return SignalInvalid(ctx);
          return EnsureSign(thisValue,((thisFlags&BigNumberFlags.FlagNegative)^(otherFlags&BigNumberFlags.FlagNegative))!=0);
        }
        if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
          // Attempt to multiply infinity by 0
          if((thisFlags&BigNumberFlags.FlagSpecial)==0 && helper.GetMantissa(thisValue).IsZero)
            return SignalInvalid(ctx);
          return EnsureSign(other,((thisFlags&BigNumberFlags.FlagNegative)^(otherFlags&BigNumberFlags.FlagNegative))!=0);
        }
      }
      BigInteger bigintOp2 = helper.GetExponent(other);
      BigInteger newexp = (helper.GetExponent(thisValue) + (BigInteger)bigintOp2);
      thisFlags=(thisFlags&BigNumberFlags.FlagNegative)^(otherFlags&BigNumberFlags.FlagNegative);
      T ret = helper.CreateNewWithFlags(
        helper.GetMantissa(thisValue) * 
        (BigInteger)(helper.GetMantissa(other)), newexp,
        thisFlags
       );
      if (ctx != null) {
        ret = RoundToPrecision(ret, ctx);
      }
      return ret;
    }
    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='multiplicand'>A T object.</param>
    /// <param name='augend'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T MultiplyAndAdd(T thisValue, T multiplicand,
                            T augend,
                            PrecisionContext ctx) {
      PrecisionContext ctx2=PrecisionContext.Unlimited.WithBlankFlags();
      T ret=Add(Multiply(thisValue,multiplicand,ctx2), augend, ctx);
      if(ctx.HasFlags)ctx.Flags|=ctx2.Flags;
      return ret;
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='context'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToBinaryPrecision(
      T thisValue,
      PrecisionContext context
     ) {
      return RoundToBinaryPrecisionWithShift(thisValue, context, 0, 0, new FastInteger(0),false);
    }
    private T RoundToBinaryPrecisionWithShift(
      T thisValue,
      PrecisionContext context,
      int lastDiscarded,
      int olderDiscarded,
      FastInteger shift,
      bool adjustNegativeZero
     ) {
      if ((context) == null) return thisValue;
      if ((context.Precision).IsZero && !context.HasExponentRange &&
          (lastDiscarded | olderDiscarded) == 0 && shift.Sign==0)
        return thisValue;
      if((context.Precision).IsZero || thisRadix==2)
        return RoundToPrecisionWithShift(thisValue,context,lastDiscarded,olderDiscarded,shift,false);
      FastInteger fastEMin = (context.HasExponentRange) ? FastInteger.FromBig(context.EMin) : null;
      FastInteger fastEMax = (context.HasExponentRange) ? FastInteger.FromBig(context.EMax) : null;
      FastInteger fastPrecision = FastInteger.FromBig(context.Precision);
      int[] signals = new int[1];
      T dfrac = RoundToPrecisionInternal(
        thisValue,fastPrecision,
        context.Rounding, fastEMin, fastEMax,
        lastDiscarded,
        olderDiscarded,
        shift,true,false,
        signals);
      // Clamp exponents to eMax + 1 - precision
      // if directed
      if (context.ClampNormalExponents && dfrac != null) {
        FastInteger digitCount=null;
        if(thisRadix==2){
          digitCount=FastInteger.Copy(fastPrecision);
        } else {
          // TODO: Use a faster way to get the digit
          // count for radix 10
          BigInteger maxMantissa = BigInteger.One;
          FastInteger prec=FastInteger.Copy(fastPrecision);
          while(prec.Sign>0){
            int bitShift=prec.CompareToInt(1000000)>=0 ? 1000000 : prec.AsInt32();
            maxMantissa<<=bitShift;
            prec.SubtractInt(bitShift);
          }
          maxMantissa-=BigInteger.One;
          // Get the digit length of the maximum possible mantissa
          // for the given binary precision
          digitCount=helper.CreateShiftAccumulator(maxMantissa)
            .GetDigitLength();
        }
        FastInteger clamp = FastInteger.Copy(fastEMax).AddInt(1).Subtract(digitCount);
        FastInteger fastExp = FastInteger.FromBig(helper.GetExponent(dfrac));
        if (fastExp.CompareTo(clamp) > 0) {
          bool neg=(helper.GetFlags(dfrac)&BigNumberFlags.FlagNegative)!=0;
          BigInteger bigmantissa = BigInteger.Abs(helper.GetMantissa(dfrac));
          if (!(bigmantissa.IsZero)) {
            FastInteger expdiff = FastInteger.Copy(fastExp).Subtract(clamp);
            bigmantissa = helper.MultiplyByRadixPower(bigmantissa, expdiff);
          }
          if (signals != null)
            signals[0] |= PrecisionContext.FlagClamped;
          dfrac = helper.CreateNewWithFlags(bigmantissa, clamp.AsBigInteger(),
                                            neg ? BigNumberFlags.FlagNegative : 0);
        }
      }
      if (context.HasFlags) {
        context.Flags |= signals[0];
      }
      return dfrac;
    }
    
    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='context'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Plus(
      T thisValue,
      PrecisionContext context
     ) {
      return RoundToPrecisionWithShift(thisValue, context, 0, 0,new FastInteger(0), true);
    }
    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='context'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToPrecision(
      T thisValue,
      PrecisionContext context
     ) {
      return RoundToPrecisionWithShift(thisValue, context, 0, 0,new FastInteger(0), false);
    }
    private T RoundToPrecisionWithShift(
      T thisValue,
      PrecisionContext context,
      int lastDiscarded,
      int olderDiscarded,
      FastInteger shift,
      bool adjustNegativeZero
     ) {
      if ((context) == null) return thisValue;
      // If context has unlimited precision and exponent range,
      // and no discarded digits or shifting
      if ((context.Precision).IsZero && !context.HasExponentRange &&
          (lastDiscarded | olderDiscarded) == 0 && shift.Sign==0)
        return thisValue;
      FastInteger fastEMin = (context.HasExponentRange) ? FastInteger.FromBig(context.EMin) : null;
      FastInteger fastEMax = (context.HasExponentRange) ? FastInteger.FromBig(context.EMax) : null;
      FastInteger fastPrecision=FastInteger.FromBig(context.Precision);
      int thisFlags=helper.GetFlags(thisValue);
      if (fastPrecision.Sign > 0 && fastPrecision.CompareToInt(34) <= 0 &&
          (shift==null || shift.Sign==0) &&
          (thisFlags&BigNumberFlags.FlagSpecial)==0) {
        // Check if rounding is necessary at all
        // for small precisions
        BigInteger mantabs = BigInteger.Abs(helper.GetMantissa(thisValue));
        BigInteger radixPower=helper.MultiplyByRadixPower(BigInteger.One, fastPrecision);
        if(adjustNegativeZero &&
           (thisFlags&BigNumberFlags.FlagNegative)!=0 && mantabs.IsZero &&
           (context.Rounding!= Rounding.Floor)){
          // Change negative zero to positive zero
          // except if the rounding mode is Floor
          thisValue=EnsureSign(thisValue,false);
          thisFlags=0;
        }
        if (mantabs.CompareTo(radixPower) < 0) {
          if(!RoundGivenDigits(lastDiscarded,olderDiscarded,context.Rounding,
                               (thisFlags&BigNumberFlags.FlagNegative)!=0,mantabs)){
            if(context.HasFlags && (lastDiscarded|olderDiscarded)!=0){
              context.Flags|=PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
            }
            if (!context.HasExponentRange)
              return thisValue;
            FastInteger fastExp = FastInteger.FromBig(helper.GetExponent(thisValue));
            FastInteger fastAdjustedExp = FastInteger.Copy(fastExp)
              .Add(fastPrecision).SubtractInt(1);
            FastInteger fastNormalMin = FastInteger.Copy(fastEMin)
              .Add(fastPrecision).SubtractInt(1);
            if (fastAdjustedExp.CompareTo(fastEMax) <= 0 &&
                fastAdjustedExp.CompareTo(fastNormalMin) >= 0) {
              return thisValue;
            }
          } else {
            if(context.HasFlags && (lastDiscarded|olderDiscarded)!=0){
              context.Flags|=PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
            }
            mantabs+=BigInteger.One;
            if(mantabs.CompareTo(radixPower)<0){
              if (!context.HasExponentRange)
                return helper.CreateNewWithFlags(mantabs,helper.GetExponent(thisValue),thisFlags);
              FastInteger fastExp = FastInteger.FromBig(helper.GetExponent(thisValue));
              FastInteger fastAdjustedExp = FastInteger.Copy(fastExp)
                .Add(fastPrecision).SubtractInt(1);
              FastInteger fastNormalMin = FastInteger.Copy(fastEMin)
                .Add(fastPrecision).SubtractInt(1);
              if (fastAdjustedExp.CompareTo(fastEMax) <= 0 &&
                  fastAdjustedExp.CompareTo(fastNormalMin) >= 0) {
                return helper.CreateNewWithFlags(mantabs,helper.GetExponent(thisValue),thisFlags);
              }
            }
          }
        }
      }
      int[] signals = new int[1];
      T dfrac = RoundToPrecisionInternal(
        thisValue,fastPrecision,
        context.Rounding, fastEMin, fastEMax,
        lastDiscarded,
        olderDiscarded,
        shift,false,adjustNegativeZero,
        context.HasFlags ? signals : null);
      if (context.ClampNormalExponents && dfrac != null) {
        // Clamp exponents to eMax + 1 - precision
        // if directed
        FastInteger clamp = FastInteger.Copy(fastEMax).AddInt(1).Subtract(fastPrecision);
        FastInteger fastExp = FastInteger.FromBig(helper.GetExponent(dfrac));
        if (fastExp.CompareTo(clamp) > 0) {
          bool neg=(helper.GetFlags(dfrac)&BigNumberFlags.FlagNegative)!=0;
          BigInteger bigmantissa = BigInteger.Abs(helper.GetMantissa(dfrac));
          if (!(bigmantissa.IsZero)) {
            FastInteger expdiff = FastInteger.Copy(fastExp).Subtract(clamp);
            bigmantissa = helper.MultiplyByRadixPower(bigmantissa, expdiff);
          }
          if (signals != null)
            signals[0] |= PrecisionContext.FlagClamped;
          dfrac = helper.CreateNewWithFlags(bigmantissa, clamp.AsBigInteger(),
                                            neg ? BigNumberFlags.FlagNegative : 0);
        }
      }
      if (context.HasFlags) {
        context.Flags |= signals[0];
      }
      return dfrac;
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='otherValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Quantize(
      T thisValue,
      T otherValue,
      PrecisionContext ctx
     ) {
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(otherValue);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        T result=HandleNotANumber(thisValue,otherValue,ctx);
        if((Object)result!=(Object)default(T))return result;
        if(((thisFlags&otherFlags)&BigNumberFlags.FlagInfinity)!=0){
          return RoundToPrecision(thisValue,ctx);
        }
        if(((thisFlags|otherFlags)&BigNumberFlags.FlagInfinity)!=0){
          return SignalInvalid(ctx);
        }
      }
      BigInteger expOther = helper.GetExponent(otherValue);
      if(ctx!=null && !ctx.ExponentWithinRange(expOther))
        return SignalInvalidWithMessage(ctx,"Exponent not within exponent range: "+expOther.ToString());
      PrecisionContext tmpctx = (ctx == null ?
                                 PrecisionContext.ForRounding(Rounding.HalfEven) :
                                 ctx.Copy()).WithBlankFlags();
      BigInteger mantThis = BigInteger.Abs(helper.GetMantissa(thisValue));
      BigInteger expThis = helper.GetExponent(thisValue);
      int expcmp = expThis.CompareTo(expOther);
      int negativeFlag=(helper.GetFlags(thisValue)&BigNumberFlags.FlagNegative);
      T ret = default(T);
      if (expcmp == 0) {
        ret = RoundToPrecision(thisValue, tmpctx);
      } else if (mantThis.IsZero) {
        ret = helper.CreateNewWithFlags(BigInteger.Zero, expOther,negativeFlag);
        ret = RoundToPrecision(ret, tmpctx);
      } else if (expcmp > 0) {
        // Other exponent is less
        FastInteger radixPower = FastInteger.FromBig(expThis).SubtractBig(expOther);
        if ((tmpctx.Precision).Sign>0 &&
            radixPower.CompareTo(FastInteger.FromBig(tmpctx.Precision).AddInt(10)) > 0) {
          // Radix power is much too high for the current precision
          return SignalInvalidWithMessage(ctx,"Result too high for current precision");
        }
        mantThis = helper.MultiplyByRadixPower(mantThis, radixPower);
        ret = helper.CreateNewWithFlags(mantThis, expOther, negativeFlag);
        ret = RoundToPrecision(ret, tmpctx);
      } else {
        // Other exponent is greater
        FastInteger shift=FastInteger.FromBig(expOther).SubtractBig(expThis);
        ret = RoundToPrecisionWithShift(thisValue, tmpctx, 0, 0, shift, false);
      }
      if ((tmpctx.Flags & PrecisionContext.FlagOverflow) != 0) {
        return SignalInvalid(ctx);
      }
      if (ret == null || !helper.GetExponent(ret).Equals(expOther)) {
        return SignalInvalid(ctx);
      }
      ret=EnsureSign(ret,negativeFlag!=0);
      if (ctx != null && ctx.HasFlags) {
        int flags = tmpctx.Flags;
        flags &= ~PrecisionContext.FlagUnderflow;
        ctx.Flags |= flags;
      }
      return ret;
    }
    
    
    
    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='expOther'>A BigInteger object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToExponentExact(
      T thisValue,
      BigInteger expOther,
      PrecisionContext ctx) {
      if (helper.GetExponent(thisValue).CompareTo(expOther) >= 0) {
        return RoundToPrecision(thisValue, ctx);
      } else {
        PrecisionContext pctx = (ctx == null) ? null :
          ctx.WithPrecision(0).WithBlankFlags();
        T ret = Quantize(thisValue, helper.CreateNewWithFlags(
          BigInteger.One, expOther, 0),
                         pctx);
        if (ctx != null && ctx.HasFlags) {
          ctx.Flags |= pctx.Flags;
        }
        return ret;
      }
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='expOther'>A BigInteger object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T RoundToExponentSimple(
      T thisValue,
      BigInteger expOther,
      PrecisionContext ctx) {
      int thisFlags=helper.GetFlags(thisValue);
      if((thisFlags&BigNumberFlags.FlagSpecial)!=0){
        T result=HandleNotANumber(thisValue,thisValue,ctx);
        if((Object)result!=(Object)default(T))return result;
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          return thisValue;
        }
      }
      if (helper.GetExponent(thisValue).CompareTo(expOther) >= 0) {
        return RoundToPrecision(thisValue, ctx);
      } else {
        if(ctx!=null && !ctx.ExponentWithinRange(expOther))
          return SignalInvalidWithMessage(ctx,"Exponent not within exponent range: "+expOther.ToString());
        BigInteger bigmantissa=BigInteger.Abs(helper.GetMantissa(thisValue));
        FastInteger shift=FastInteger.FromBig(expOther).SubtractBig(helper.GetExponent(thisValue));
        IShiftAccumulator accum=helper.CreateShiftAccumulator(bigmantissa);
        accum.ShiftRight(shift);
        bigmantissa=accum.ShiftedInt;
        return RoundToPrecisionWithShift(
          helper.CreateNewWithFlags(bigmantissa,expOther,thisFlags),ctx,
          accum.LastDiscardedDigit,
          accum.OlderDiscardedDigits, new FastInteger(0), false);
      }
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    /// <param name='exponent'>A BigInteger object.</param>
    public T RoundToExponentNoRoundedFlag(
      T thisValue,
      BigInteger exponent,
      PrecisionContext ctx
     ){
      PrecisionContext pctx = (ctx == null) ? null :
        ctx.WithBlankFlags();
      T ret = RoundToExponentExact(thisValue, exponent, pctx);
      if (ctx != null && ctx.HasFlags) {
        ctx.Flags |=(pctx.Flags&~(PrecisionContext.FlagInexact|PrecisionContext.FlagRounded));
      }
      return ret;
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T Reduce(
      T thisValue,
      PrecisionContext ctx
     ){
      T ret = RoundToPrecision(thisValue, ctx);
      if(ret!=null && (helper.GetFlags(ret)&BigNumberFlags.FlagSpecial)==0){
        BigInteger bigmant=BigInteger.Abs(helper.GetMantissa(ret));
        FastInteger exp=FastInteger.FromBig(helper.GetExponent(ret));
        if(bigmant.IsZero){
          exp=new FastInteger(0);
        } else {
          int radix=thisRadix;
          BigInteger bigradix=(BigInteger)radix;
          while(!(bigmant.IsZero)){
            BigInteger bigrem;
            BigInteger bigquo=BigInteger.DivRem(bigmant,bigradix,out bigrem);
            if(!bigrem.IsZero)
              break;
            bigmant=bigquo;
            exp.AddInt(1);
          }
        }
        int flags=helper.GetFlags(thisValue);
        ret=helper.CreateNewWithFlags(bigmant,exp.AsBigInteger(),flags);
        if(ctx!=null && ctx.ClampNormalExponents){
          PrecisionContext ctxtmp=ctx.WithBlankFlags();
          ret=RoundToPrecision(ret,ctxtmp);
          if(ctx.HasFlags){
            ctx.Flags|=(ctxtmp.Flags&~PrecisionContext.FlagClamped);
          }
        }
        ret=EnsureSign(ret,(flags&BigNumberFlags.FlagNegative)!=0);
      }
      return ret;
    }
    
    private T RoundToPrecisionInternal(
      T thisValue,
      FastInteger precision,
      Rounding rounding,
      FastInteger fastEMin,
      FastInteger fastEMax,
      int lastDiscarded,
      int olderDiscarded,
      FastInteger shift,
      bool binaryPrec, // whether "precision" is the number of bits, not digits
      bool adjustNegativeZero,
      int[] signals
     ) {
      if (precision.Sign < 0) throw new ArgumentException("precision" + " not greater or equal to " + "0" + " (" + precision + ")");
      if(thisRadix==2 || precision.Sign==0){
        // "binaryPrec" will have no special effect here
        binaryPrec=false;
      }
      int thisFlags=helper.GetFlags(thisValue);
      if((thisFlags&BigNumberFlags.FlagSpecial)!=0){
        if((thisFlags&BigNumberFlags.FlagSignalingNaN)!=0){
          if(signals!=null){
            signals[0]|=PrecisionContext.FlagInvalid;
          }
          return ReturnQuietNaNFastIntPrecision(thisValue,precision);
        }
        if((thisFlags&BigNumberFlags.FlagQuietNaN)!=0){
          return ReturnQuietNaNFastIntPrecision(thisValue,precision);
        }
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          return thisValue;
        }
      }
      if(adjustNegativeZero &&
         (thisFlags&BigNumberFlags.FlagNegative)!=0 && helper.GetMantissa(thisValue).IsZero &&
         (rounding!= Rounding.Floor)){
        // Change negative zero to positive zero
        // except if the rounding mode is Floor
        thisValue=EnsureSign(thisValue,false);
        thisFlags=0;
      }
      bool neg=(thisFlags&BigNumberFlags.FlagNegative)!=0;
      BigInteger bigmantissa = BigInteger.Abs(helper.GetMantissa(thisValue));
      // save mantissa in case result is subnormal
      // and must be rounded again
      BigInteger oldmantissa = bigmantissa;
      bool mantissaWasZero=(oldmantissa.IsZero && (lastDiscarded|olderDiscarded)==0);
      BigInteger maxMantissa=BigInteger.One;
      FastInteger exp = FastInteger.FromBig(helper.GetExponent(thisValue));
      int flags = 0;
      IShiftAccumulator accum = helper.CreateShiftAccumulatorWithDigits(
        bigmantissa, lastDiscarded, olderDiscarded);
      bool unlimitedPrec = (precision.Sign==0);
      FastInteger fastPrecision=precision;
      if(binaryPrec){
        FastInteger prec=FastInteger.Copy(precision);
        while(prec.Sign>0){
          int bitShift=(prec.CompareToInt(1000000)>=0) ? 1000000 : prec.AsInt32();
          maxMantissa<<=bitShift;
          prec.SubtractInt(bitShift);
        }
        maxMantissa-=BigInteger.One;
        IShiftAccumulator accumMaxMant = helper.CreateShiftAccumulator(
          maxMantissa);
        // Get the digit length of the maximum possible mantissa
        // for the given binary precision
        fastPrecision=accumMaxMant.GetDigitLength();
      } else {
        fastPrecision=precision;
      }
      
      if(shift!=null){
        accum.ShiftRight(shift);
      }
      if (!unlimitedPrec) {
        accum.ShiftToDigits(fastPrecision);
      } else {
        fastPrecision = accum.GetDigitLength();
      }
      if(binaryPrec){
        while((accum.ShiftedInt).CompareTo(maxMantissa)>0){
          accum.ShiftRightInt(1);
        }
      }
      FastInteger discardedBits = FastInteger.Copy(accum.DiscardedDigitCount);
      exp.Add(discardedBits);
      FastInteger adjExponent = FastInteger.Copy(exp)
        .Add(accum.GetDigitLength()).SubtractInt(1);
      //Console.WriteLine("{0}->{1} digits={2} exp={3} [curexp={4}] adj={5},max={6}",bigmantissa,accum.ShiftedInt,
      //              accum.DiscardedDigitCount,exp,helper.GetExponent(thisValue),adjExponent,fastEMax);
      FastInteger newAdjExponent = adjExponent;
      FastInteger clamp = null;
      BigInteger earlyRounded=BigInteger.Zero;
      if(binaryPrec && fastEMax!=null && adjExponent.CompareTo(fastEMax)==0){
        // May or may not be an overflow depending on the mantissa
        FastInteger expdiff=FastInteger.Copy(fastPrecision).Subtract(accum.GetDigitLength());
        BigInteger currMantissa=accum.ShiftedInt;
        currMantissa=helper.MultiplyByRadixPower(currMantissa,expdiff);
        if((currMantissa).CompareTo(maxMantissa)>0){
          // Mantissa too high, treat as overflow
          adjExponent.AddInt(1);
        }
      }
      //Console.WriteLine("{0} adj={1} emin={2}",thisValue,adjExponent,fastEMin);
      if(signals!=null && fastEMin != null && adjExponent.CompareTo(fastEMin) < 0){
        earlyRounded=accum.ShiftedInt;
        if(RoundGivenBigInt(accum, rounding, neg, earlyRounded)){
          earlyRounded += BigInteger.One;
          //Console.WriteLine(earlyRounded);
          if (earlyRounded.IsEven && (thisRadix&1)==0) {
            IShiftAccumulator accum2 = helper.CreateShiftAccumulator(earlyRounded);
            accum2.ShiftToDigits(fastPrecision);
            //Console.WriteLine("{0} {1}",accum2.ShiftedInt,accum2.DiscardedDigitCount);
            if ((accum2.DiscardedDigitCount).Sign != 0) {
              //overPrecision=true;
              earlyRounded = accum2.ShiftedInt;
            }
            newAdjExponent=FastInteger.Copy(exp)
              .Add(accum2.GetDigitLength())
              .SubtractInt(1);
          }
        }
      }
      if (fastEMax != null && adjExponent.CompareTo(fastEMax) > 0) {
        if (mantissaWasZero) {
          if (signals != null) {
            signals[0] = flags | PrecisionContext.FlagClamped;
          }
          return helper.CreateNewWithFlags(oldmantissa, fastEMax.AsBigInteger(),thisFlags);
        }
        // Overflow
        flags |= PrecisionContext.FlagOverflow | PrecisionContext.FlagInexact |
          PrecisionContext.FlagRounded;
        if (rounding == Rounding.Unnecessary)
          throw new ArithmeticException("Rounding was required");
        if (!unlimitedPrec &&
            (rounding == Rounding.Down ||
             rounding == Rounding.ZeroFiveUp ||
             (rounding == Rounding.Ceiling && neg) ||
             (rounding == Rounding.Floor && !neg))) {
          // Set to the highest possible value for
          // the given precision
          BigInteger overflowMant = BigInteger.Zero;
          if(binaryPrec){
            overflowMant=maxMantissa;
          } else {
            overflowMant=helper.MultiplyByRadixPower(BigInteger.One, fastPrecision);
            overflowMant -= BigInteger.One;
          }
          if (signals != null) signals[0] = flags;
          clamp = FastInteger.Copy(fastEMax).AddInt(1)
            .Subtract(fastPrecision);
          return helper.CreateNewWithFlags(overflowMant,
                                           clamp.AsBigInteger(),
                                           neg ? BigNumberFlags.FlagNegative : 0
                                          );
        }
        if (signals != null) signals[0] = flags;
        return SignalOverflow(neg);
      } else if (fastEMin != null && adjExponent.CompareTo(fastEMin) < 0) {
        // Subnormal
        FastInteger fastETiny = FastInteger.Copy(fastEMin)
          .Subtract(fastPrecision)
          .AddInt(1);
        if (signals!=null){
          if(!earlyRounded.IsZero){
            if(newAdjExponent.CompareTo(fastEMin)<0){
              flags |= PrecisionContext.FlagSubnormal;
            }
          }
        }
        //Console.WriteLine("exp={0} eTiny={1}",exp,fastETiny);
        FastInteger subExp=FastInteger.Copy(exp);
        //Console.WriteLine("exp={0} eTiny={1}",subExp,fastETiny);
        if (subExp.CompareTo(fastETiny) < 0) {
          //Console.WriteLine("Less than ETiny");
          FastInteger expdiff = FastInteger.Copy(fastETiny).Subtract(exp);
          expdiff.Add(discardedBits);
          accum = helper.CreateShiftAccumulatorWithDigits(
            oldmantissa, lastDiscarded, olderDiscarded);
          accum.ShiftRight(expdiff);
          FastInteger newmantissa = accum.ShiftedIntFast;
          if ((accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
            if (rounding == Rounding.Unnecessary)
              throw new ArithmeticException("Rounding was required");
          }
          if ((accum.DiscardedDigitCount).Sign != 0 ||
              (accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
            if(signals!=null){
              if (!mantissaWasZero)
                flags |= PrecisionContext.FlagRounded;
              if ((accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
                flags |= PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
              }
            }
            if (Round(accum, rounding, neg, newmantissa)) {
              newmantissa.AddInt(1);
            }
          }
          if (signals != null){
            if (newmantissa.Sign==0)
              flags |= PrecisionContext.FlagClamped;
            if ((flags & (PrecisionContext.FlagSubnormal | PrecisionContext.FlagInexact)) ==
                (PrecisionContext.FlagSubnormal | PrecisionContext.FlagInexact))
              flags |= PrecisionContext.FlagUnderflow | PrecisionContext.FlagRounded;
            signals[0] = flags;
          }
          return helper.CreateNewWithFlags(newmantissa.AsBigInteger(), fastETiny.AsBigInteger(),
                                           neg ? BigNumberFlags.FlagNegative : 0);
        }
      }
      bool recheckOverflow = false;
      if ((accum.DiscardedDigitCount).Sign != 0 ||
          (accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
        if (!bigmantissa.IsZero)
          flags |= PrecisionContext.FlagRounded;
        bigmantissa = accum.ShiftedInt;
        if ((accum.LastDiscardedDigit | accum.OlderDiscardedDigits) != 0) {
          flags |= PrecisionContext.FlagInexact|PrecisionContext.FlagRounded;
          if (rounding == Rounding.Unnecessary)
            throw new ArithmeticException("Rounding was required");
        }
        if (RoundGivenBigInt(accum, rounding, neg, bigmantissa)) {
          bigmantissa += BigInteger.One;
          if(binaryPrec)recheckOverflow=true;
          if (bigmantissa.IsEven && (thisRadix&1)==0) {
            accum = helper.CreateShiftAccumulator(bigmantissa);
            accum.ShiftToDigits(fastPrecision);
            if(binaryPrec){
              while((accum.ShiftedInt).CompareTo(maxMantissa)>0){
                accum.ShiftRightInt(1);
              }
            }
            if ((accum.DiscardedDigitCount).Sign != 0) {
              exp.Add(accum.DiscardedDigitCount);
              discardedBits.Add(accum.DiscardedDigitCount);
              bigmantissa = accum.ShiftedInt;
              if(!binaryPrec)recheckOverflow = true;
            }
          }
        }
      }
      if (recheckOverflow && fastEMax != null) {
        // Check for overflow again
        adjExponent = FastInteger.Copy(exp);
        adjExponent.Add(accum.GetDigitLength()).SubtractInt(1);
        if(binaryPrec && fastEMax!=null && adjExponent.CompareTo(fastEMax)==0){
          // May or may not be an overflow depending on the mantissa
          FastInteger expdiff=FastInteger.Copy(fastPrecision).Subtract(accum.GetDigitLength());
          BigInteger currMantissa=accum.ShiftedInt;
          currMantissa=helper.MultiplyByRadixPower(currMantissa,expdiff);
          if((currMantissa).CompareTo(maxMantissa)>0){
            // Mantissa too high, treat as overflow
            adjExponent.AddInt(1);
          }
        }
        if (adjExponent.CompareTo(fastEMax) > 0) {
          flags |= PrecisionContext.FlagOverflow | PrecisionContext.FlagInexact | PrecisionContext.FlagRounded;
          if (!unlimitedPrec &&
              (rounding == Rounding.Down ||
               rounding == Rounding.ZeroFiveUp ||
               (rounding == Rounding.Ceiling && neg) ||
               (rounding == Rounding.Floor && !neg))) {
            // Set to the highest possible value for
            // the given precision
            BigInteger overflowMant = BigInteger.Zero;
            if(binaryPrec){
              overflowMant=maxMantissa;
            } else {
              overflowMant=helper.MultiplyByRadixPower(BigInteger.One, fastPrecision);
              overflowMant -= BigInteger.One;
            }
            if (signals != null) signals[0] = flags;
            clamp = FastInteger.Copy(fastEMax).AddInt(1)
              .Subtract(fastPrecision);
            return helper.CreateNewWithFlags(overflowMant,
                                             clamp.AsBigInteger(),
                                             neg ? BigNumberFlags.FlagNegative : 0);
          }
          if (signals != null) signals[0] = flags;
          return SignalOverflow(neg);
        }
      }
      if (signals != null) signals[0] = flags;
      return helper.CreateNewWithFlags(bigmantissa, exp.AsBigInteger(),
                                       neg ? BigNumberFlags.FlagNegative : 0);
    }
    
    private T AddCore(BigInteger mant1, // assumes mant1 is nonnegative
                      BigInteger mant2, // assumes mant2 is nonnegative
                      BigInteger exponent, int flags1, int flags2, PrecisionContext ctx){
      #if DEBUG
      if(mant1.Sign<0)throw new InvalidOperationException();
      if(mant2.Sign<0)throw new InvalidOperationException();
      #endif
      bool neg1=(flags1&BigNumberFlags.FlagNegative)!=0;
      bool neg2=(flags2&BigNumberFlags.FlagNegative)!=0;
      bool negResult=false;
      if(neg1!=neg2){
        // Signs are different, treat as a subtraction
        mant1-=(BigInteger)mant2;
        int mant1Sign=mant1.Sign;
        negResult=neg1^(mant1Sign==0 ? neg2 : (mant1Sign<0));
      } else {
        // Signs are same, treat as an addition
        mant1+=(BigInteger)mant2;
        negResult=neg1;
      }
      if(mant1.IsZero && negResult){
        // Result is negative zero
        if(!((neg1 && neg2) || ((neg1^neg2) && ctx!=null && ctx.Rounding== Rounding.Floor))){
          negResult=false;
        }
      }
      return helper.CreateNewWithFlags(mant1,exponent,negResult ? BigNumberFlags.FlagNegative : 0);
    }

    /// <summary> </summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    /// <param name='other'>A T object.</param>
    public T Add(T thisValue, T other, PrecisionContext ctx) {
      int thisFlags=helper.GetFlags(thisValue);
      int otherFlags=helper.GetFlags(other);
      if(((thisFlags|otherFlags)&BigNumberFlags.FlagSpecial)!=0){
        T result=HandleNotANumber(thisValue,other,ctx);
        if((Object)result!=(Object)default(T))return result;
        if((thisFlags&BigNumberFlags.FlagInfinity)!=0){
          if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
            if((thisFlags&BigNumberFlags.FlagNegative)!=(otherFlags&BigNumberFlags.FlagNegative))
              return SignalInvalid(ctx);
          }
          return thisValue;
        }
        if((otherFlags&BigNumberFlags.FlagInfinity)!=0){
          return other;
        }
      }
      int expcmp = helper.GetExponent(thisValue).CompareTo((BigInteger)helper.GetExponent(other));
      T retval = default(T);
      BigInteger op1MantAbs=BigInteger.Abs(helper.GetMantissa(thisValue));
      BigInteger op2MantAbs=BigInteger.Abs(helper.GetMantissa(other));
      if (expcmp == 0) {
        retval = AddCore(op1MantAbs,op2MantAbs,helper.GetExponent(thisValue),thisFlags,otherFlags,ctx);
      } else {
        // choose the minimum exponent
        T op1 = thisValue;
        T op2 = other;
        BigInteger op1Exponent = helper.GetExponent(op1);
        BigInteger op2Exponent = helper.GetExponent(op2);
        BigInteger resultExponent = (expcmp < 0 ? op1Exponent : op2Exponent);
        FastInteger fastOp1Exp=FastInteger.FromBig(op1Exponent);
        FastInteger fastOp2Exp=FastInteger.FromBig(op2Exponent);
        FastInteger expdiff = FastInteger.Copy(fastOp1Exp).Subtract(fastOp2Exp).Abs();
        if (ctx != null && (ctx.Precision).Sign > 0) {
          // Check if exponent difference is too big for
          // radix-power calculation to work quickly
          FastInteger fastPrecision=FastInteger.FromBig(ctx.Precision);
          // If exponent difference is greater than the precision
          if (FastInteger.Copy(expdiff).CompareTo(fastPrecision) > 0) {
            int expcmp2 = fastOp1Exp.CompareTo(fastOp2Exp);
            if (expcmp2 < 0) {
              if(!(op2MantAbs.IsZero)){
                // first operand's exponent is less
                // and second operand isn't zero
                // second mantissa will be shifted by the exponent
                // difference
                //                    111111111111|
                //        222222222222222|
                FastInteger digitLength1=helper.CreateShiftAccumulator(op1MantAbs)
                  .GetDigitLength();
                if (
                  FastInteger.Copy(fastOp1Exp)
                  .Add(digitLength1)
                  .AddInt(2)
                  .CompareTo(fastOp2Exp) < 0) {
                  // first operand's mantissa can't reach the
                  // second operand's mantissa, so the exponent can be
                  // raised without affecting the result
                  FastInteger tmp=FastInteger.Copy(fastOp2Exp).SubtractInt(4)
                    .Subtract(digitLength1)
                    .SubtractBig(ctx.Precision);
                  FastInteger newDiff=FastInteger.Copy(tmp).Subtract(fastOp2Exp).Abs();
                  if(newDiff.CompareTo(expdiff)<0){
                    // Can be treated as almost zero
                    if(helper.GetSign(thisValue)==helper.GetSign(other)){
                      FastInteger digitLength2=helper.CreateShiftAccumulator(
                        op2MantAbs).GetDigitLength();
                      if(digitLength2.CompareTo(fastPrecision)<0){
                        // Second operand's precision too short
                        FastInteger precisionDiff=FastInteger.Copy(fastPrecision).Subtract(digitLength2);
                        op2MantAbs=helper.MultiplyByRadixPower(
                          op2MantAbs,precisionDiff);
                        BigInteger bigintTemp=precisionDiff.AsBigInteger();
                        op2Exponent-=(BigInteger)bigintTemp;
                        other=helper.CreateNewWithFlags(op2MantAbs,op2Exponent,helper.GetFlags(other));
                        return RoundToPrecisionWithShift(other,ctx,0,1,null,false);
                      } else {
                        FastInteger shift=FastInteger.Copy(digitLength2).Subtract(fastPrecision);
                        return RoundToPrecisionWithShift(other,ctx,0,1,shift,false);
                      }
                    } else {
                      if(!(op1MantAbs.IsZero))
                        op1MantAbs = BigInteger.One;
                      op1Exponent = (tmp.AsBigInteger());
                    }
                  }
                }
              }
            } else if (expcmp2 > 0) {
              if(!(op1MantAbs.IsZero)){
                // first operand's exponent is greater
                // and first operand isn't zero
                // first mantissa will be shifted by the exponent
                // difference
                //       111111111111|
                //                222222222222222|
                FastInteger digitLength2=helper.CreateShiftAccumulator(op2MantAbs).GetDigitLength();
                if (
                  FastInteger.Copy(fastOp2Exp)
                  .Add(digitLength2)
                  .AddInt(2)
                  .CompareTo(fastOp1Exp) < 0) {
                  // second operand's mantissa can't reach the
                  // first operand's mantissa, so the exponent can be
                  // raised without affecting the result
                  FastInteger tmp=FastInteger.Copy(fastOp1Exp).SubtractInt(4)
                    .Subtract(digitLength2)
                    .SubtractBig(ctx.Precision);
                  FastInteger newDiff=FastInteger.Copy(tmp).Subtract(fastOp1Exp).Abs();
                  if(newDiff.CompareTo(expdiff)<0){
                    // Can be treated as almost zero
                    if(helper.GetSign(thisValue)==helper.GetSign(other)){
                      FastInteger digitLength1=helper.CreateShiftAccumulator(op1MantAbs).GetDigitLength();
                      if(digitLength1.CompareTo(fastPrecision)<0){
                        // First operand's precision too short
                        FastInteger precisionDiff=FastInteger.Copy(fastPrecision).Subtract(digitLength1);
                        op1MantAbs=helper.MultiplyByRadixPower(
                          op1MantAbs,precisionDiff);
                        BigInteger bigintTemp=precisionDiff.AsBigInteger();
                        op1Exponent-=(BigInteger)bigintTemp;
                        thisValue=helper.CreateNewWithFlags(op1MantAbs,op1Exponent,
                                                            helper.GetFlags(thisValue));
                        return RoundToPrecisionWithShift(thisValue,ctx,0,1, null,false);
                      } else {
                        FastInteger shift=FastInteger.Copy(digitLength1).Subtract(fastPrecision);
                        return RoundToPrecisionWithShift(thisValue,ctx,0,1, shift,false);
                      }
                    } else {
                      if(!(op2MantAbs.IsZero))
                        op2MantAbs = BigInteger.One;
                      op2Exponent = (tmp.AsBigInteger());
                    }
                  }
                }
              }
            }
            expcmp = op1Exponent.CompareTo((BigInteger)op2Exponent);
            resultExponent = (expcmp < 0 ? op1Exponent : op2Exponent);
          }
        }
        if (expcmp > 0) {
          op1MantAbs = helper.RescaleByExponentDiff(
            op1MantAbs, op1Exponent, op2Exponent);
          //Console.WriteLine("{0} {1} -> {2}",op1MantAbs,op2MantAbs,op2MantAbs-op1MantAbs);
          retval = AddCore(
            op1MantAbs, op2MantAbs,resultExponent,
            thisFlags,otherFlags,ctx);
        } else {
          op2MantAbs = helper.RescaleByExponentDiff(
            op2MantAbs, op1Exponent, op2Exponent);
          //Console.WriteLine("{0} {1} -> {2}",op1MantAbs,op2MantAbs,op2MantAbs-op1MantAbs);
          retval = AddCore(
            op1MantAbs, op2MantAbs,resultExponent,
            thisFlags,otherFlags,ctx);
        }
      }
      if (ctx != null) {
        retval = RoundToPrecision(retval, ctx);
      }
      return retval;
    }
    
    /// <summary>Compares a T object with this instance.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='decfrac'>A T object.</param>
    /// <param name='treatQuietNansAsSignaling'>A Boolean object.</param>
    /// <param name='ctx'>A PrecisionContext object.</param>
    /// <returns>A T object.</returns>
    public T CompareToWithContext(T thisValue, T decfrac, bool treatQuietNansAsSignaling, PrecisionContext ctx){
      if(decfrac==null)return SignalInvalid(ctx);
      T result=CompareToHandleSpecial(thisValue,decfrac,treatQuietNansAsSignaling,ctx);
      if((Object)result!=(Object)default(T))return result;
      return ValueOf(CompareTo(thisValue,decfrac),null);
    }
    
    /// <summary>Compares a T object with this instance.</summary>
    /// <param name='thisValue'>A T object.</param>
    /// <param name='decfrac'>A T object.</param>
    /// <returns>Zero if the values are equal; a negative number if this instance
    /// is less, or a positive number if this instance is greater.</returns>
public int CompareTo(T thisValue, T decfrac) {
      if (decfrac == null)return 1;
      int flagsThis=helper.GetFlags(thisValue);
      int flagsOther=helper.GetFlags(decfrac);
      if((flagsThis&BigNumberFlags.FlagNaN)!=0){
        if((flagsOther&BigNumberFlags.FlagNaN)!=0){
          return 0;
        }
        return 1; // Treat NaN as greater
      }
      if((flagsOther&BigNumberFlags.FlagNaN)!=0){
        return -1; // Treat as less than NaN
      }
      int s=CompareToHandleSpecialReturnInt(thisValue,decfrac);
      if(s<=1)return s;
      s = helper.GetSign(thisValue);
      int ds = helper.GetSign(decfrac);
      if (s != ds) return (s < ds) ? -1 : 1;
      if (ds == 0 || s==0) {
        // Special case: Either operand is zero
        return 0;
      }
      int expcmp = helper.GetExponent(thisValue).CompareTo((BigInteger)helper.GetExponent(decfrac));
      // At this point, the signs are equal so we can compare
      // their absolute values instead
      int mantcmp = BigInteger.Abs(helper.GetMantissa(thisValue))
        .CompareTo(BigInteger.Abs(helper.GetMantissa(decfrac)));
      if(s<0)mantcmp=-mantcmp;
      if (mantcmp == 0) {
        // Special case: Mantissas are equal
        return s<0 ? -expcmp : expcmp;
      }
      if(expcmp==0){
        return mantcmp;
      }
      BigInteger op1Exponent = helper.GetExponent(thisValue);
      BigInteger op2Exponent = helper.GetExponent(decfrac);
      FastInteger fastOp1Exp=FastInteger.FromBig(op1Exponent);
      FastInteger fastOp2Exp=FastInteger.FromBig(op2Exponent);
      FastInteger expdiff = FastInteger.Copy(fastOp1Exp).Subtract(fastOp2Exp).Abs();
      // Check if exponent difference is too big for
      // radix-power calculation to work quickly
      if (expdiff.CompareToInt(100) >= 0) {
        BigInteger op1MantAbs=BigInteger.Abs(helper.GetMantissa(thisValue));
        BigInteger op2MantAbs=BigInteger.Abs(helper.GetMantissa(decfrac));
        FastInteger precision1 = helper.CreateShiftAccumulator(
          op1MantAbs).GetDigitLength();
        FastInteger precision2 = helper.CreateShiftAccumulator(
          op2MantAbs).GetDigitLength();
        FastInteger maxPrecision=null;
        if(precision1.CompareTo(precision2)>0)
          maxPrecision=precision1;
        else
          maxPrecision=precision2;
        // If exponent difference is greater than the
        // maximum precision of the two operands
        if (FastInteger.Copy(expdiff).CompareTo(maxPrecision) > 0) {
          int expcmp2 = fastOp1Exp.CompareTo(fastOp2Exp);
          if (expcmp2 < 0) {
            if(!(op2MantAbs.IsZero)){
              // first operand's exponent is less
              // and second operand isn't zero
              // second mantissa will be shifted by the exponent
              // difference
              //                    111111111111|
              //        222222222222222|
              FastInteger digitLength1=helper.CreateShiftAccumulator(
                op1MantAbs).GetDigitLength();
              if (
                FastInteger.Copy(fastOp1Exp)
                .Add(digitLength1)
                .AddInt(2)
                .CompareTo(fastOp2Exp) < 0) {
                // first operand's mantissa can't reach the
                // second operand's mantissa, so the exponent can be
                // raised without affecting the result
                FastInteger tmp=FastInteger.Copy(fastOp2Exp).SubtractInt(8)
                  .Subtract(digitLength1)
                  .Subtract(maxPrecision);
                FastInteger newDiff=FastInteger.Copy(tmp).Subtract(fastOp2Exp).Abs();
                if(newDiff.CompareTo(expdiff)<0){
                  if(s==ds){
                    return (s<0) ? 1 : -1;
                  } else {
                    op1Exponent = (tmp.AsBigInteger());
                  }
                }
              }
            }
          } else if (expcmp2 > 0) {
            if(!(op1MantAbs.IsZero)){
              // first operand's exponent is greater
              // and second operand isn't zero
              // first mantissa will be shifted by the exponent
              // difference
              //       111111111111|
              //                222222222222222|
              FastInteger digitLength2=helper.CreateShiftAccumulator(
                op2MantAbs).GetDigitLength();
              if (
                FastInteger.Copy(fastOp2Exp)
                .Add(digitLength2)
                .AddInt(2)
                .CompareTo(fastOp1Exp) < 0) {
                // second operand's mantissa can't reach the
                // first operand's mantissa, so the exponent can be
                // raised without affecting the result
                FastInteger tmp=FastInteger.Copy(fastOp1Exp).SubtractInt(8)
                  .Subtract(digitLength2)
                  .Subtract(maxPrecision);
                FastInteger newDiff=FastInteger.Copy(tmp).Subtract(fastOp1Exp).Abs();
                if(newDiff.CompareTo(expdiff)<0){
                  if(s==ds){
                    return (s<0) ? -1 : 1;
                  } else {
                    op2Exponent = (tmp.AsBigInteger());
                  }
                }
              }
            }
          }
          expcmp = op1Exponent.CompareTo((BigInteger)op2Exponent);
        }
      }
      if (expcmp > 0) {
        BigInteger newmant = helper.RescaleByExponentDiff(
          helper.GetMantissa(thisValue), op1Exponent, op2Exponent);
        BigInteger othermant=BigInteger.Abs(helper.GetMantissa(decfrac));
        newmant=BigInteger.Abs(newmant);
        mantcmp=newmant.CompareTo(othermant);
        return (s<0) ? -mantcmp : mantcmp;
      } else {
        BigInteger newmant = helper.RescaleByExponentDiff(
          helper.GetMantissa(decfrac), op1Exponent, op2Exponent);
        BigInteger othermant=BigInteger.Abs(helper.GetMantissa(thisValue));
        newmant=BigInteger.Abs(newmant);
        mantcmp=othermant.CompareTo(newmant);
        return (s<0) ? -mantcmp : mantcmp;
      }
    }
  }
}