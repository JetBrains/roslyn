// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp
{
    internal enum LessWellKnownType
    {
        System_ArgumentNullException,
        System_ArgumentException,
        System_InvalidOperationException,
        JetBrains_Annotations_NotNullAttribute,
        Count
    }

    internal static class LessWellKnownTypes
    {
        private static readonly string[] s_metadataNames = new[]
        {
            "System.ArgumentNullException",
            "System.ArgumentException",
            "System.InvalidOperationException",
            "JetBrains.Annotations.NotNullAttribute"
        };

        public static string GetMetadataName(this LessWellKnownType id)
        {
            return s_metadataNames[(int)id];
        }
    }
}
