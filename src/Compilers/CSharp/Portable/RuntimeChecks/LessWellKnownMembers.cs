// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.RuntimeMembers;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp
{
    internal enum LessWellKnownMember
    {
        System_ArgumentNullException__ctor,
        System_ArgumentException__ctor,
        System_InvalidOperationException__ctor,
        Count
    }

    internal static class LessWellKnownMembers
    {
        private static readonly ImmutableArray<MemberDescriptor> s_descriptors;

        private static string[] Names => new[]
        {
            ".ctor",
            ".ctor",
            ".ctor"
        };

        static LessWellKnownMembers()
        {
            byte[] initializationBytes = new byte[]
            {
                // System_ArgumentNullException__ctor
                (byte)MemberFlags.Constructor,
                (byte)LessWellKnownType.System_ArgumentNullException,
                0,
                    1,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.System_Void,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.System_String,
                // System_ArgumentException__ctor
                (byte)MemberFlags.Constructor,
                (byte)LessWellKnownType.System_ArgumentException,
                0,
                    2,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.System_Void,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.System_String,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.System_String,
                // System_InvalidOperationException__ctor
                (byte)MemberFlags.Constructor,
                (byte)LessWellKnownType.System_InvalidOperationException,
                0,
                    1,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.System_Void,
                    (byte)SignatureTypeCode.TypeHandle, (byte)SpecialType.System_String
            };

            s_descriptors = MemberDescriptor.InitializeFromStream(new MemoryStream(initializationBytes, writable: false), Names);
        }

        public static MemberDescriptor GetDescriptor(LessWellKnownMember member)
        {
            return s_descriptors[(int)member];
        }
    }
}
