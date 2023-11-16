// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;

namespace Microsoft.CodeAnalysis.CSharp;

internal interface ITypeSymbolAdditionalEqualityComparer
{
    internal bool Equals(SourceNamedTypeSymbol a, TypeSymbol b);
    internal bool Equals(PENamedTypeSymbol a, TypeSymbol b);
}
