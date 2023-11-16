// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal class IgnoreAccessibilityOptions
    {
        public IgnoreAccessibilityOptions(ImmutableArray<string> giveInternalAccessTo, string assembly)
        {
            GiveInternalAccessTo = giveInternalAccessTo;
            Assembly = assembly;
        }

        internal ImmutableArray<string> GiveInternalAccessTo { get; }

        internal string Assembly { get; }
    }
}
