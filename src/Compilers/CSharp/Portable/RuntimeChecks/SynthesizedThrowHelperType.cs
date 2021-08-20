// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Cci;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal sealed class SynthesizedThrowHelperType : NamedTypeSymbol
    {
        public SynthesizedThrowHelperMethod ThrowArgumentNullMethod { get; }

        public SynthesizedThrowHelperType(NamespaceSymbol containingNamespace, ModuleSymbol containingModule)
        {
            ContainingModule = containingModule;
            ContainingSymbol = ContainingNamespace = containingNamespace;
            ThrowArgumentNullMethod = new SynthesizedThrowHelperMethod(this);
        }

        public override string Name => "ThrowHelper";
        internal override bool MangleName => false;

        public override IEnumerable<string> MemberNames => GetMembers().Select(x => x.Name);

        public override ImmutableArray<Symbol> GetMembers() => ImmutableArray.Create<Symbol>(ThrowArgumentNullMethod);

        public override ImmutableArray<Symbol> GetMembers(string name)
        {
            return name.Equals(ThrowArgumentNullMethod.Name, StringComparison.Ordinal)
                ? ImmutableArray.Create<Symbol>(ThrowArgumentNullMethod)
                : ImmutableArray<Symbol>.Empty;
        }

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers() => ImmutableArray<NamedTypeSymbol>.Empty;
        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name) => ImmutableArray<NamedTypeSymbol>.Empty;
        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name, int arity) => ImmutableArray<NamedTypeSymbol>.Empty;

        internal override bool HasPossibleWellKnownCloneMethod() => false;

        internal override ModuleSymbol ContainingModule { get; }
        public override NamespaceSymbol ContainingNamespace { get; }
        public override Symbol ContainingSymbol { get; }
        public override ImmutableArray<Location> Locations => ImmutableArray<Location>.Empty;
        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => ImmutableArray<SyntaxReference>.Empty;
        internal override ObsoleteAttributeData? ObsoleteAttributeData => null;
        public override Accessibility DeclaredAccessibility => Accessibility.Internal;

        internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers() => GetMembersUnordered();

        internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers(string name) => GetMembers(name);

        internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) => BaseTypeNoUseSiteDiagnostics;

        internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved)
            => ImmutableArray<NamedTypeSymbol>.Empty;

        internal override bool HasCodeAnalysisEmbeddedAttribute => false;
        internal override bool HasSpecialName => false;
        internal override bool IsComImport => false;
        internal override bool IsWindowsRuntimeImport => false;
        internal override bool ShouldAddWinRTMembers => false;
        public override bool IsSerializable => false;
        public override bool AreLocalsZeroed => ContainingModule.AreLocalsZeroed;

        internal override TypeLayout Layout => default;
        internal override CharSet MarshallingCharSet => DefaultMarshallingCharSet;
        internal override bool HasDeclarativeSecurity => false;
        internal override IEnumerable<SecurityAttribute>? GetSecurityInformation() => null;

        internal override ImmutableArray<string> GetAppliedConditionalSymbols() => ImmutableArray<string>.Empty;

        internal override NamedTypeSymbol AsNativeInteger() => throw ExceptionUtilities.Unreachable;
        internal override NamedTypeSymbol? NativeIntegerUnderlyingType => null;

        internal override bool IsInterface => false;
        public override bool IsStatic => true;
        public override bool IsAbstract => true;
        public override bool IsSealed => true;

        internal override NamedTypeSymbol BaseTypeNoUseSiteDiagnostics
            => ContainingAssembly.GetSpecialType(SpecialType.System_Object);

        internal override ImmutableArray<NamedTypeSymbol> InterfacesNoUseSiteDiagnostics(ConsList<TypeSymbol>? basesBeingResolved = null)
            => ImmutableArray<NamedTypeSymbol>.Empty;

        public override TypeKind TypeKind => TypeKind.Class;
        internal override bool IsRecord => false;
        public override bool IsRefLikeType => false;
        public override bool IsReadOnly => false;

        internal override IEnumerable<FieldSymbol> GetFieldsToEmit() => Enumerable.Empty<FieldSymbol>();

        internal override ImmutableArray<NamedTypeSymbol> GetInterfacesToEmit() => ImmutableArray<NamedTypeSymbol>.Empty;

        protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) => throw ExceptionUtilities.Unreachable;

        public override int Arity => 0;
        public override ImmutableArray<TypeParameterSymbol> TypeParameters => ImmutableArray<TypeParameterSymbol>.Empty;
        internal override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotationsNoUseSiteDiagnostics
            => ImmutableArray<TypeWithAnnotations>.Empty;

        public override NamedTypeSymbol ConstructedFrom => this;
        public override bool MightContainExtensionMethods => false;
        internal override AttributeUsageInfo GetAttributeUsageInfo() => AttributeUsageInfo.Default;
    }
}
