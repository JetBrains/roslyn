// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace System.Numerics
{
#if !NET
    internal static class BitOperations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));
    }
#endif
}

namespace System.Runtime.CompilerServices
{
#if !NET
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName) => ParameterName = parameterName;

        public string ParameterName { get; }
    }
#endif
}
