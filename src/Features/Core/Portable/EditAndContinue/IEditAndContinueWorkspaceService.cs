// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.EditAndContinue
{
    internal interface IEditAndContinueWorkspaceService : IWorkspaceService, IActiveStatementSpanProvider
    {
        EditSession? EditSession { get; }
        DebuggingSession? DebuggingSession { get; }
        
        ValueTask<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, DocumentActiveStatementSpanProvider activeStatementSpanProvider, CancellationToken cancellationToken);
        ValueTask<bool> HasChangesAsync(Solution solution, SolutionActiveStatementSpanProvider activeStatementSpanProvider, string? sourceFilePath, CancellationToken cancellationToken);
        ValueTask<EmitSolutionUpdateResults> EmitSolutionUpdateAsync(Solution solution, SolutionActiveStatementSpanProvider activeStatementSpanProvider, CancellationToken cancellationToken);

        void CommitSolutionUpdate(out ImmutableArray<DocumentId> documentsToReanalyze);
        void DiscardSolutionUpdate();

        void OnSourceFileUpdated(Document document);

        ValueTask StartDebuggingSessionAsync(Solution solution, IManagedEditAndContinueDebuggerService debuggerService, bool captureMatchingDocuments, CancellationToken cancellationToken);
        void BreakStateEntered(out ImmutableArray<DocumentId> documentsToReanalyze);
        void EndDebuggingSession(out ImmutableArray<DocumentId> documentsToReanalyze);

        ValueTask<bool?> IsActiveStatementInExceptionRegionAsync(Solution solution, ManagedInstructionId instructionId, CancellationToken cancellationToken);
        ValueTask<LinePositionSpan?> GetCurrentActiveStatementPositionAsync(Solution solution, SolutionActiveStatementSpanProvider activeStatementSpanProvider, ManagedInstructionId instructionId, CancellationToken cancellationToken);
    }

    interface IManagedEditAndContinueDebuggerService
    {
        Task<ImmutableArray<ManagedActiveStatementDebugInfo>> GetActiveStatementsAsync(
            CancellationToken cancellationToken);

        Task<ManagedEditAndContinueAvailability> GetAvailabilityAsync(
            Guid module,
            CancellationToken cancellationToken);

        Task PrepareModuleForUpdateAsync(Guid module, CancellationToken cancellationToken);

        Task<ImmutableArray<string>> GetCapabilitiesAsync(
            CancellationToken cancellationToken);
    }
    
    internal readonly struct ManagedActiveStatementDebugInfo
    {
        public ManagedActiveStatementDebugInfo(
            ManagedInstructionId activeInstruction,
            string? documentName,
            SourceSpan sourceSpan,
            ActiveStatementFlags flags)
        {
            this.ActiveInstruction = activeInstruction;
            this.DocumentName = documentName;
            this.SourceSpan = sourceSpan;
            this.Flags = flags;
        }

        public ManagedInstructionId ActiveInstruction { get; }
        public string? DocumentName { get; }
        public SourceSpan SourceSpan { get; }
        public ActiveStatementFlags Flags { get; }
        public bool HasSourceLocation => this.DocumentName != null;
    }
    
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    internal readonly struct ManagedInstructionId : IEquatable<ManagedInstructionId>
    {
        public ManagedInstructionId(ManagedMethodId method, int ilOffset)
        {
            this.Method = method;
            this.ILOffset = ilOffset;
        }

        public ManagedMethodId Method { get; }
        public int ILOffset { get; }

        public bool Equals(ManagedInstructionId other) => this.Method.Equals(other.Method) && this.ILOffset == other.ILOffset;

        public override bool Equals(object obj) => obj is ManagedInstructionId other && this.Equals(other);

        public override int GetHashCode() => this.Method.GetHashCode() ^ this.ILOffset;

        public static bool operator ==(ManagedInstructionId left, ManagedInstructionId right) => left.Equals(right);

        public static bool operator !=(ManagedInstructionId left, ManagedInstructionId right) => !(left == right);

        internal string GetDebuggerDisplay() => string.Format("{0} IL_{1:X4}", (object) this.Method.GetDebuggerDisplay(), (object) this.ILOffset);
    }
    
    internal readonly struct ManagedEditAndContinueAvailability
    {
        public ManagedEditAndContinueAvailability(
            ManagedEditAndContinueAvailabilityStatus status,
            string? localizedMessage = null)
        {
            this.Status = status;
            this.LocalizedMessage = localizedMessage;
        }
        public ManagedEditAndContinueAvailabilityStatus Status { get; }
        public string? LocalizedMessage { get; }
    }
    
    internal enum ManagedEditAndContinueAvailabilityStatus
    {
        Available,
        Interop,
        SqlClr,
        Minidump,
        Attach,
        ModuleNotLoaded,
        ModuleReloaded,
        InRunMode,
        NotBuilt,
        EngineMetricFalse,
        NotSupportedForClr64Version,
        NotAllowedForModule,
        Optimized,
        DomainNeutralAssembly,
        ReflectionAssembly,
        IntelliTrace,
        NotAllowedForRuntime,
        InternalError,
        Unavailable,
    }
    
    internal readonly struct ManagedMethodId : IEquatable<ManagedMethodId>
    {
        public ManagedMethodId(Guid module, ManagedModuleMethodId method)
        {
            this.Module = module;
            this.Method = method;
        }

        public ManagedMethodId(Guid module, int token, int version)
            : this(module, new ManagedModuleMethodId(token, version))
        {
        }

        public Guid Module { get; }
        public ManagedModuleMethodId Method { get; }
        public int Token => this.Method.Token;
        public int Version => this.Method.Version;

        public bool Equals(ManagedMethodId other) => this.Module == other.Module && this.Method.Equals(other.Method);

        public override bool Equals(object obj) => obj is ManagedMethodId other && this.Equals(other);

        public override int GetHashCode() => this.Module.GetHashCode() ^ this.Method.GetHashCode();

        public static bool operator ==(ManagedMethodId left, ManagedMethodId right) => left.Equals(right);

        public static bool operator !=(ManagedMethodId left, ManagedMethodId right) => !(left == right);

        internal string GetDebuggerDisplay() => string.Format("mvid={0} {1}", (object) this.Module, (object) this.Method.GetDebuggerDisplay());
    }
    
    internal readonly struct ManagedModuleMethodId : IEquatable<ManagedModuleMethodId>
    {
        public ManagedModuleMethodId(int token, int version)
        {
            if (token <= 100663296)
                throw new ArgumentOutOfRangeException(nameof (token));
            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof (version));
            this.Token = token;
            this.Version = version;
        }

        public int Token { get; }
        public int Version { get; }

        public bool Equals(ManagedModuleMethodId other) => this.Token == other.Token && this.Version == other.Version;

        public override bool Equals(object obj) => obj is ManagedModuleMethodId other && this.Equals(other);

        public override int GetHashCode() => this.Token ^ this.Version;

        public static bool operator ==(ManagedModuleMethodId left, ManagedModuleMethodId right) => left.Equals(right);

        public static bool operator !=(ManagedModuleMethodId left, ManagedModuleMethodId right) => !(left == right);

        internal string GetDebuggerDisplay() => string.Format("0x{0:X8} v{1}", (object) this.Token, (object) this.Version);
    }
    
    [Flags]
    internal enum ActiveStatementFlags
    {
        None = 0,
        IsLeafFrame = 1,
        PartiallyExecuted = 2,
        NonUserCode = 4,
        MethodUpToDate = 8,
        IsNonLeafFrame = 16, // 0x00000010
        IsStale = 32, // 0x00000020
    }
    
    internal readonly struct ManagedModuleUpdate
    {
        public ManagedModuleUpdate(
            Guid module,
            ImmutableArray<byte> ilDelta,
            ImmutableArray<byte> metadataDelta,
            ImmutableArray<byte> pdbDelta,
            ImmutableArray<SequencePointUpdates> sequencePoints,
            ImmutableArray<int> updatedMethods,
            ImmutableArray<int> updatedTypes,
            ImmutableArray<ManagedActiveStatementUpdate> activeStatements,
            ImmutableArray<ManagedExceptionRegionUpdate> exceptionRegions)
        {
            this.Module = module;
            this.ILDelta = ilDelta;
            this.MetadataDelta = metadataDelta;
            this.PdbDelta = pdbDelta;
            this.SequencePoints = sequencePoints;
            this.UpdatedMethods = updatedMethods;
            this.UpdatedTypes = updatedTypes;
            this.ActiveStatements = activeStatements;
            this.ExceptionRegions = exceptionRegions;
        }

        public Guid Module { get; }
        public ImmutableArray<byte> ILDelta { get; }
        public ImmutableArray<byte> MetadataDelta { get; }
        public ImmutableArray<byte> PdbDelta { get; }
        public ImmutableArray<SequencePointUpdates> SequencePoints { get; }
        public ImmutableArray<int> UpdatedMethods { get; }
        public ImmutableArray<int> UpdatedTypes { get; }
        public ImmutableArray<ManagedActiveStatementUpdate> ActiveStatements { get; }
        public ImmutableArray<ManagedExceptionRegionUpdate> ExceptionRegions { get; }
    }
    
    internal readonly struct SequencePointUpdates
    {
        public SequencePointUpdates(string fileName, ImmutableArray<SourceLineUpdate> lineUpdates)
        {
            this.FileName = fileName;
            this.LineUpdates = lineUpdates;
        }
        public string FileName { get; }
        public ImmutableArray<SourceLineUpdate> LineUpdates { get; }
    }
    
    internal readonly struct SourceLineUpdate
    {
        public SourceLineUpdate(int oldLine, int newLine)
        {
            if (oldLine < 0)
                throw new ArgumentOutOfRangeException(nameof (oldLine));
            this.OldLine = newLine >= 0 && oldLine != newLine ? oldLine : throw new ArgumentOutOfRangeException(nameof (newLine));
            this.NewLine = newLine;
        }

        public int OldLine { get; }
        public int NewLine { get; }
    }

    internal readonly struct ManagedActiveStatementUpdate
    {
        public ManagedActiveStatementUpdate(
            ManagedModuleMethodId method,
            int ilOffset,
            SourceSpan newSpan)
        {
            this.Method = method;
            this.ILOffset = ilOffset;
            this.NewSpan = newSpan;
        }

        public ManagedModuleMethodId Method { get; }
        public int ILOffset { get; }
        public SourceSpan NewSpan { get; }
    }
    
  [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
  public readonly struct SourceSpan : IEquatable<SourceSpan>
  {
    public SourceSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
      if (startLine < 0)
        throw new ArgumentOutOfRangeException(nameof (startLine));
      if (startColumn < -1)
        throw new ArgumentOutOfRangeException(nameof (startColumn));
      if (endLine < 0)
        throw new ArgumentOutOfRangeException(nameof (endLine));
      if (endColumn < -1)
        throw new ArgumentOutOfRangeException(nameof (endColumn));
      if ((startColumn == -1 || endColumn == -1) && startColumn != endColumn)
        throw new ArgumentOutOfRangeException(startColumn == -1 ? nameof (endColumn) : nameof (startColumn));
      this.StartLine = startLine;
      this.StartColumn = startColumn;
      this.EndLine = endLine;
      this.EndColumn = endColumn;
    }

    public int StartLine { get; }
    public int StartColumn { get; }
    public int EndLine { get; }
    public int EndColumn { get; }

    public bool Equals(SourceSpan other) => this.StartLine == other.StartLine && this.StartColumn == other.StartColumn && this.EndLine == other.EndLine && this.EndColumn == other.EndColumn;

    public override bool Equals(object obj) => obj is SourceSpan other && this.Equals(other);

    public override int GetHashCode() => (this.StartLine & (int) ushort.MaxValue) << 16 | (this.StartColumn & (int) byte.MaxValue) << 8 | (this.EndLine ^ this.EndColumn) % (int) byte.MaxValue;

    public static bool operator ==(SourceSpan left, SourceSpan right) => left.Equals(right);

    public static bool operator !=(SourceSpan left, SourceSpan right) => !(left == right);

    internal string GetDebuggerDisplay()
    {
      if (this.StartColumn < 0)
        return string.Format("{0}-{1}", (object) this.StartLine, (object) this.EndLine);
      return string.Format("({0},{1})-({2},{3})", (object) this.StartLine, (object) this.StartColumn, (object) this.EndLine, (object) this.EndColumn);
    }
  }
  
  internal readonly struct ManagedExceptionRegionUpdate
  {
      public ManagedExceptionRegionUpdate(
          ManagedModuleMethodId method,
          int delta,
          SourceSpan newSpan)
      {
          this.Method = method;
          this.Delta = delta;
          this.NewSpan = newSpan;
      }

      public ManagedModuleMethodId Method { get; }
      public int Delta { get; }
      public SourceSpan NewSpan { get; }
  }
  
  internal readonly struct ManagedModuleUpdates
  {
      public ManagedModuleUpdates(
          ManagedModuleUpdateStatus status,
          ImmutableArray<ManagedModuleUpdate> updates)
      {
          this.Status = status;
          this.Updates = updates;
      }
      public ManagedModuleUpdateStatus Status { get; }
      public ImmutableArray<ManagedModuleUpdate> Updates { get; }
  }
  
  internal enum ManagedModuleUpdateStatus
  {
      None,
      Ready,
      Blocked,
  }
  
  public readonly struct ManagedHotReloadDiagnostic
  {
      public ManagedHotReloadDiagnostic(
          string id,
          string message,
          ManagedHotReloadDiagnosticSeverity severity,
          string filePath,
          SourceSpan span)
      {
          this.Id = id;
          this.Message = message;
          this.Severity = severity;
          this.FilePath = filePath;
          this.Span = span;
      }

      public string Id { get; }
      public string Message { get; }
      public ManagedHotReloadDiagnosticSeverity Severity { get; }
      public string FilePath { get; }
      public SourceSpan Span { get; }
  }
  
  public enum ManagedHotReloadDiagnosticSeverity
  {
      Warning = 1,
      RudeEdit = 2,
      Error = 3,
  }
}
