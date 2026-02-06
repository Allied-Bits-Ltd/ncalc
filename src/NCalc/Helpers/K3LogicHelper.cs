using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCalc.Helpers
{
    /// <summary>
    /// Kleene's Strong Three-Valued Logic (K3).
    ///   F = 0
    ///   U = 1
    ///   T = 2
    /// </summary>
    public enum K3 : byte
    {
        False = 0,
        Unknown = 1,
        True = 2,
    }

    public static class K3LogicHelper
    {
        // NOT table: index by A
        // A:   F  U  T
        // ¬A:  T  U  F
        private static readonly K3[] NotTable = new K3[]
        {
            /* F */ K3.True,
            /* U */ K3.Unknown,
            /* T */ K3.False
        };

        // Binary operator tables (3x3), flattened row-major:
        // index = (int)A * 3 + (int)B

        // AND (∧) for K3:
        //     B:  T  U  F
        // A T     T  U  F
        //   U     U  U  F
        //   F     F  F  F
        //
        // With our encoding order (F=0,U=1,T=2), the table is:
        // Rows A=F,U,T; Cols B=F,U,T
        private static readonly K3[] AndTable = new K3[]
        {
            // A=F: B=F, U, T
            K3.False,   K3.False,   K3.False,

            // A=U: B=F, U, T
            K3.False,   K3.Unknown, K3.Unknown,

            // A=T: B=F, U, T
            K3.False,   K3.Unknown, K3.True
        };

        // OR (∨) for K3:
        //     B:  T  U  F
        // A T     T  T  T
        //   U     T  U  U
        //   F     T  U  F
        //
        // Rows A=F,U,T; Cols B=F,U,T
        private static readonly K3[] OrTable = new K3[]
        {
            // A=F: B=F, U, T
            K3.False,   K3.Unknown, K3.True,

            // A=U: B=F, U, T
            K3.Unknown, K3.Unknown, K3.True,

            // A=T: B=F, U, T
            K3.True,    K3.True,    K3.True
        };

        // XOR (⊕) derived for consistency:
        // A ⊕ B = (A ∨ B) ∧ ¬(A ∧ B)
        //
        // Direct LUT for XOR in K3, matching the derived definition:
        //     B:  T  U  F
        // A T     F  U  T
        //   U     U  U  U
        //   F     T  U  F
        //
        // Rows A=F,U,T; Cols B=F,U,T
        private static readonly K3[] XorTable = new K3[]
        {
            // A=F: B=F, U, T
            K3.False,   K3.Unknown, K3.True,

            // A=U: B=F, U, T
            K3.Unknown, K3.Unknown, K3.Unknown,

            // A=T: B=F, U, T
            K3.True,    K3.Unknown, K3.False
        };

        public static K3 BoolToK3(bool? value)
        {
            return value switch
            {
                null => K3.Unknown,
                true => K3.True,
                false => K3.False,
            };
        }

        public static bool? K3ToBool(K3 value)
        {
            return value switch
            {
                K3.Unknown => null,
                K3.False => false,
                K3.True => true,
                _ => null
            };
        }

        /// <summary>Branchless NOT.</summary>
        public static bool? Not(object? a)
        {
            int aInt = a is bool ab ? (ab ? 2 : 0) : 1;

            K3 result = NotTable[aInt];
            return K3ToBool(result);
        }

        /// <summary>Branchless NOT.</summary>
        public static K3 Not(K3 a)
        {
            int aInt = (int)a;
            K3 result = NotTable[aInt];
            return result;
        }

        /// <summary>Branchless AND.</summary>
        public static bool? And(object? a, object? b)
        {
            int aInt = a is bool ab ? (ab ? 2 : 0) : 1;
            int bInt = b is bool bb ? (bb ? 2 : 0) : 1;

            int rowBase = aInt * 3;
            int index = rowBase + bInt;

            K3 result = AndTable[index];
            return K3ToBool(result);
        }

        /// <summary>Branchless AND.</summary>
        public static K3 And(K3 a, K3 b)
        {
            int aInt = (int)a;
            int bInt = (int)b;

            int rowBase = aInt * 3;
            int index = rowBase + bInt;

            K3 result = AndTable[index];
            return result;
        }

        /// <summary>Branchless OR.</summary>
        public static bool? Or(object? a, object? b)
        {
            int aInt = a is bool ab ? (ab ? 2 : 0) : 1;
            int bInt = b is bool bb ? (bb ? 2 : 0) : 1;

            int rowBase = aInt * 3;
            int index = rowBase + bInt;

            K3 result = OrTable[index];
            return K3ToBool(result);
        }

        /// <summary>Branchless OR.</summary>
        public static K3 Or(K3 a, K3 b)
        {
            int aInt = (int)a;
            int bInt = (int)b;

            int rowBase = aInt * 3;
            int index = rowBase + bInt;

            K3 result = OrTable[index];
            return result;
        }

        /// <summary>
        /// XOR computed via the derived definition:
        /// A ⊕ B = (A ∨ B) ∧ ¬(A ∧ B)
        /// </summary>
        public static bool? XorDerived(object? a, object? b)
        {
            bool? orResult = Or(a, b);
            bool? andResult = And(a, b);
            bool? notAndResult = Not(andResult);
            bool? result = And(orResult, notAndResult);
            return result;
        }

        /// <summary>
        /// XOR computed via the derived definition:
        /// A ⊕ B = (A ∨ B) ∧ ¬(A ∧ B)
        /// </summary>
        public static K3 XorDerived(K3 a, K3 b)
        {
            K3 orResult = Or(a, b);
            K3 andResult = And(a, b);
            K3 notAndResult = Not(andResult);
            K3 result = And(orResult, notAndResult);
            return result;
        }

        /// <summary>
        /// Branchless XOR via direct LUT (matches XorDerived).
        /// </summary>
        public static bool? Xor(object? a, object? b)
        {
            int aInt = a is bool ab ? (ab ? 2 : 0) : 1;
            int bInt = b is bool bb ? (bb ? 2 : 0) : 1;

            int rowBase = aInt * 3;
            int index = rowBase + bInt;

            K3 result = XorTable[index];
            return K3ToBool(result);
        }

        /// <summary>
        /// Branchless XOR via direct LUT (matches XorDerived).
        /// </summary>
        public static K3 Xor(K3 a, K3 b)
        {
            int aInt = (int)a;
            int bInt = (int)b;

            int rowBase = aInt * 3;
            int index = rowBase + bInt;

            K3 result = XorTable[index];
            return result;
        }
    }
}