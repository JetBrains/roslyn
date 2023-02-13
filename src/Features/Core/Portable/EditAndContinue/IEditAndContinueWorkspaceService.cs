﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.EditAndContinue.Contracts;

namespace Microsoft.CodeAnalysis.EditAndContinue
{
    internal interface IEditAndContinueWorkspaceService : IWorkspaceService
    {
        DebuggingSession? GetDebuggingSession(DebuggingSessionId id);

        ValueTask<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, ActiveStatementSpanProvider activeStatementSpanProvider, CancellationToken cancellationToken);
        ValueTask<EmitSolutionUpdateResults> EmitSolutionUpdateAsync(DebuggingSessionId sessionId, Solution solution, ActiveStatementSpanProvider activeStatementSpanProvider, CancellationToken cancellationToken);

        void CommitSolutionUpdate(DebuggingSessionId sessionId, out ImmutableArray<DocumentId> documentsToReanalyze);
        void DiscardSolutionUpdate(DebuggingSessionId sessionId);

        ValueTask<DebuggingSessionId> StartDebuggingSessionAsync(Solution solution, IManagedHotReloadService debuggerService, IPdbMatchingSourceTextProvider sourceTextProvider, ImmutableArray<DocumentId> captureMatchingDocuments, bool captureAllMatchingDocuments, bool reportDiagnostics, CancellationToken cancellationToken);
        void BreakStateOrCapabilitiesChanged(DebuggingSessionId sessionId, bool? inBreakState, out ImmutableArray<DocumentId> documentsToReanalyze);
        void EndDebuggingSession(DebuggingSessionId sessionId, out ImmutableArray<DocumentId> documentsToReanalyze);

        ValueTask<bool?> IsActiveStatementInExceptionRegionAsync(DebuggingSessionId sessionId, Solution solution, ManagedInstructionId instructionId, CancellationToken cancellationToken);
        ValueTask<LinePositionSpan?> GetCurrentActiveStatementPositionAsync(DebuggingSessionId sessionId, Solution solution, ActiveStatementSpanProvider activeStatementSpanProvider, ManagedInstructionId instructionId, CancellationToken cancellationToken);

        ValueTask<ImmutableArray<ImmutableArray<ActiveStatementSpan>>> GetBaseActiveStatementSpansAsync(DebuggingSessionId sessionId, Solution solution, ImmutableArray<DocumentId> documentIds, CancellationToken cancellationToken);
        ValueTask<ImmutableArray<ActiveStatementSpan>> GetAdjustedActiveStatementSpansAsync(DebuggingSessionId sessionId, TextDocument document, ActiveStatementSpanProvider activeStatementSpanProvider, CancellationToken cancellationToken);
    }
}
