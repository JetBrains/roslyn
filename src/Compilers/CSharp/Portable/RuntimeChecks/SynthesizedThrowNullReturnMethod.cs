// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal sealed class SynthesizedThrowNullReturnMethod : SynthesizedThrowHelperMethod
    {
        public SynthesizedThrowNullReturnMethod(SynthesizedThrowHelperType containingType) : base(containingType)
        {
        }

        public override string Name => "NullReturn";
        public override ImmutableArray<ParameterSymbol> Parameters => ImmutableArray<ParameterSymbol>.Empty;

        internal override void GenerateMethodBody(TypeCompilationState compilationState, BindingDiagnosticBag diagnostics)
        {
            var F = new SyntheticBoundNodeFactory(this, this.GetNonNullSyntaxNode(), compilationState, diagnostics);
            F.CurrentFunction = this;
            try
            {
                MethodSymbol exceptionCtor = (MethodSymbol)F.LessWellKnownMember(LessWellKnownMember.System_InvalidOperationException__ctor);
                var throwStmt = F.Throw(F.New(exceptionCtor, F.Literal("Return value cannot be null.")));
                F.CloseMethod(throwStmt);
            }
            catch (SyntheticBoundNodeFactory.MissingPredefinedMember ex)
            {
                diagnostics.Add(ex.Diagnostic);
                F.CloseMethod(F.Block());
            }
        }
    }
}
