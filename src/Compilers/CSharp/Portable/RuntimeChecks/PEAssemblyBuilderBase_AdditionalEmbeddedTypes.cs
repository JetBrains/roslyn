// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CSharp.RuntimeChecks;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    internal abstract partial class PEAssemblyBuilderBase
    {
        private SynthesizedThrowHelperType? _lazyThrowHelperClass;

        private void AddAdditionalEmbeddedTypes(ArrayBuilder<NamedTypeSymbol> buidler)
        {
            if (Compilation.Options.RuntimeChecks)
            {
                buidler.Add(GetEmbeddedThrowHelper());
            }
        }

        public SynthesizedThrowHelperType GetEmbeddedThrowHelper()
        {
            if (_lazyThrowHelperClass is null)
            {
                var containingNamespace = GetOrSynthesizeNamespace("System.Runtime.CompilerServices");
                var helper = new SynthesizedThrowHelperType(containingNamespace);
                AddSynthesizedDefinition(containingNamespace, helper);
                _lazyThrowHelperClass = helper;
            }

            return _lazyThrowHelperClass;
        }
    }
}
