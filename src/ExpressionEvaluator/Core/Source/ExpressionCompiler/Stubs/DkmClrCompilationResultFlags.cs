using System;

namespace Microsoft.VisualStudio.Debugger.Evaluation.ClrCompilation
{
    [Flags]
    public enum DkmClrCompilationResultFlags
    {
        BoolResult = 4,
        None = 0,
        PotentialSideEffect = 1,
        ReadOnlyResult = 2,
    }
}
