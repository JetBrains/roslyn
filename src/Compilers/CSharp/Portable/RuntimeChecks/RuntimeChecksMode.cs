// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.CSharp
{
    public enum RuntimeChecksMode
    {
        Disable,
        PreconditionsOnly,
        PostconditionsOnly,
        Enable
    }

    public static class RuntimeChecksModeExtensions
    {
        public static bool GeneratePreconditionChecks(this RuntimeChecksMode mode)
            => mode is RuntimeChecksMode.PreconditionsOnly or RuntimeChecksMode.Enable;

        public static bool GeneratePostconditionChecks(this RuntimeChecksMode mode)
            => mode is RuntimeChecksMode.PostconditionsOnly or RuntimeChecksMode.Enable;
    }
}
