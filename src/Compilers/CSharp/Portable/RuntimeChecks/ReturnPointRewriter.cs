// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal sealed class ReturnPointRewriter : BoundTreeRewriterWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator
    {
        private readonly Func<BoundExpression, BoundStatement?> _generateReturnValueCheckFunc;
        private readonly Func<BoundStatement?> _generateArgumentChecksFunc;

        private readonly List<LocalSymbol> _locals = new();

        private ReturnPointRewriter(
            Func<BoundExpression, BoundStatement?> generateReturnValueCheckFunc,
            Func<BoundStatement?> generateArgumentChecksFunc)
        {
            _generateReturnValueCheckFunc = generateReturnValueCheckFunc;
            _generateArgumentChecksFunc = generateArgumentChecksFunc;
        }

        public static BoundStatement InsertChecks(
            BoundStatement body,
            Func<BoundExpression, BoundStatement?> generateReturnValueCheckFunc,
            Func<BoundStatement?> generateArgumentChecksFunc)
        {
            var rewriter = new ReturnPointRewriter(generateReturnValueCheckFunc, generateArgumentChecksFunc);
            return (BoundStatement)rewriter.Visit(body)!;
        }

        public override BoundNode VisitReturnStatement(BoundReturnStatement node)
        {
            var argChecks = _generateArgumentChecksFunc();
            BoundStatement retStmt = node;
            if (node.ExpressionOpt is { } value && _generateReturnValueCheckFunc(value) is { } check)
            {
                retStmt = check;
            }

            return List(node.Syntax, argChecks, retStmt) ?? node;
        }

        public override BoundNode VisitYieldReturnStatement(BoundYieldReturnStatement node)
        {
            var argChecks = _generateArgumentChecksFunc();
            var retStmt = _generateReturnValueCheckFunc(node.Expression) ?? node;
            return List(node.Syntax, argChecks, retStmt) ?? node;
        }

        private static BoundNode? List(SyntaxNode syntax, BoundStatement? stmt1, BoundStatement? stmt2)
        {
            var stmts = ImmutableArray<BoundStatement>.Empty;
            if (stmt1 is not null)
            {
                stmts = stmts.Add(stmt1);
            }
            if (stmt2 is not null)
            {
                stmts = stmts.Add(stmt2);
            }

            return !stmts.IsEmpty ? new BoundStatementList(syntax, stmts) : null;
        }
    }
}
