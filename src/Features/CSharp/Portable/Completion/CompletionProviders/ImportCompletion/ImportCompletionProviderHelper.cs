﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Completion.Providers
{
    internal static class ImportCompletionProviderHelper
    {
        public static ImmutableArray<string> GetImportedNamespaces(
            SyntaxNode location,
            SemanticModel semanticModel)
            => semanticModel.GetUsingNamespacesInScope(location)
                .SelectAsArray(namespaceSymbol => namespaceSymbol.ToDisplayString(SymbolDisplayFormats.NameFormat));

        public static async Task<SyntaxContext> CreateContextAsync(Document document, int position, CancellationToken cancellationToken)
        {
            // Need regular semantic model because we will use it to get imported namespace symbols. Otherwise we will try to 
            // reach outside of the span and ended up with "node not within syntax tree" error from the speculative model.
            // Also we use partial model so that we don't have to wait for all semantics to be computed.
            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var frozenDocument = sourceText.GetDocumentWithFrozenPartialSemantics(cancellationToken);
            Contract.ThrowIfNull(frozenDocument);

            var semanticModel = await frozenDocument.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            return CSharpSyntaxContext.CreateContext(frozenDocument.Project.Solution.Workspace, semanticModel, position, cancellationToken);
        }
    }
}
