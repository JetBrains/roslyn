// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CSharp.Symbols;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal static class SyntheticBoundNodeFactoryExtensions
    {
        public static NamedTypeSymbol LessWellKnownType(this SyntheticBoundNodeFactory factory, LessWellKnownType type)
        {
            NamedTypeSymbol symbol = factory.Compilation.GetLessWellKnownType(type);
            Binder.ReportUseSite(symbol, factory.Diagnostics, factory.Syntax);
            return symbol;
        }

        public static Symbol LessWellKnownMember(this SyntheticBoundNodeFactory factory, LessWellKnownMember member)
        {
            Symbol? symbol = factory.Compilation.GetLessWellKnownTypeMember(member);
            if (symbol is null)
            {
                RuntimeMembers.MemberDescriptor memberDescriptor = LessWellKnownMembers.GetDescriptor(member);
                var declaringTypeMdName = ((LessWellKnownType)memberDescriptor.DeclaringTypeId).GetMetadataName();
                var diagnostic = new CSDiagnostic(new CSDiagnosticInfo(ErrorCode.ERR_MissingPredefinedMember, declaringTypeMdName, memberDescriptor.Name), Location.None);
                throw new SyntheticBoundNodeFactory.MissingPredefinedMember(diagnostic);
            }

            return symbol;
        }
    }
}
