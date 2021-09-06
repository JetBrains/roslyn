// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public abstract class RuntimeCheckTestsBase : CompilingTestBase
    {
        private const string JetBrainsAnnotations = @"
using System;
namespace JetBrains.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.Delegate | AttributeTargets.GenericParameter)]
    public sealed class NotNullAttribute : Attribute
    {
    }
}";

        protected CSharpCompilation CreateCompilation(
            string source,
            RuntimeChecksMode runtimeChecksMode,
            bool nullableContext = true,
            bool useAsyncStreams = false)
        {
            var sources = new List<string>
            {
                source,
                AllowNullAttributeDefinition,
                DisallowNullAttributeDefinition,
                NotNullAttributeDefinition,
                JetBrainsAnnotations
            };
            if (useAsyncStreams)
            {
                sources.Add(AsyncStreamsTypes);
            }
            return CreateCompilationWithTasksExtensions(sources.ToArray(), options: WithRuntimeChecks(runtimeChecksMode, nullableContext));
        }

        protected CompilationVerifier CompileAndVerifyException<T>(
            CSharpCompilation comp,
            string? expectedMessage = null,
            string expectedOutput = "",
            Verification verify = Verification.Passes) where T : Exception
        {
            try
            {
                CompileAndVerify(comp, expectedOutput: expectedOutput, verify: verify);
                Assert.False(true, $"Expected exception {typeof(T).Name}({expectedMessage})");
            }
            catch (Exception x)
            {
                Exception? e = x.InnerException;
                Assert.IsType<T>(e);
                Debug.Assert(e != null);
                if (expectedMessage != null)
                {
                    Assert.Equal(expectedMessage, e.Message);
                }
            }

            return CompileAndVerify(comp, verify: verify);
        }

        protected CSharpCompilationOptions WithRuntimeChecks(RuntimeChecksMode mode, bool nullableContext)
        {
            var options = TestOptions.ReleaseExe;
            var nrtOptions = nullableContext
                ? NullableContextOptions.Enable
                : NullableContextOptions.Disable;
            return options
                .WithNullableContextOptions(nrtOptions)
                .WithRuntimeChecks(mode);
        }
    }
}
