// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.CSharp.RuntimeChecks;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    internal abstract partial class PEAssemblyBuilderBase
    {
        private SynthesizedThrowHelperType? _throwHelperClass;

        private void SynthesizeAdditionalEmbeddedTypes()
        {
            if (Compilation.Options.RuntimeChecksMode != RuntimeChecksMode.Disable)
            {
                var containingNamespace = GetOrSynthesizeNamespace("System.Runtime.CompilerServices");
                var helper = new SynthesizedThrowHelperType(containingNamespace, SourceModule);
                AddSynthesizedDefinition(containingNamespace, helper);
                _throwHelperClass = helper;
            }
        }

        private void AddAdditionalEmbeddedTypes(ArrayBuilder<NamedTypeSymbol> buidler)
        {
            buidler.AddIfNotNull(_throwHelperClass);
        }

        public SynthesizedThrowHelperType GetEmbeddedThrowHelper()
        {
            RoslynDebug.AssertNotNull(_throwHelperClass);
            return _throwHelperClass;
        }
    }
}
