// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System.Collections.Immutable;
using System.Linq;

#pragma warning disable RS0016

namespace System.Runtime.InteropServices
{
    // public static class ImmutableCollectionsMarshal
    // {
    //     public static ImmutableArray<T> AsImmutableArray<T>(T[]? array)
    //     {
    //         return array.ToImmutableArray();
    //     }
    //
    //     public static T[]? AsArray<T>(ImmutableArray<T> array)
    //     {
    //         return array.ToArray();
    //     }
    // }

    public static class ImmutableCollectionsMarshal4Hack
    {
        public static ImmutableArray<T> AsImmutableArray<T>(T[]? array)
        {
            return array.ToImmutableArray();
        }

        public static T[]? AsArray<T>(ImmutableArray<T> array)
        {
            return array.ToArray();
        }
    }
}


#pragma warning restore RS0016
