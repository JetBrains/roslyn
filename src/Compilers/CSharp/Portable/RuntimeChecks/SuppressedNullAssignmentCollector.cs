// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Symbols;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal class SuppressedNullAssignmentCollector : BoundTreeWalkerWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator
    {
        public HashSet<SourcePropertySymbol> CollectedProperties { get; } = new();

        public override BoundNode? VisitAssignmentOperator(BoundAssignmentOperator node)
        {
            if (node.Left.ExpressionSymbol is SourcePropertySymbol prop
                && node.Right is BoundConversion { Operand: { ConstantValue: { IsNull: true }, IsSuppressed: true } })
            {
                CollectedProperties.Add(prop);
            }

            return base.VisitAssignmentOperator(node);
        }
    }
}
