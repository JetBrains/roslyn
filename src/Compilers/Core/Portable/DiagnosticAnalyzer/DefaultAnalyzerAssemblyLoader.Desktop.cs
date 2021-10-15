// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

#if !NETCOREAPP

using System;
using System.Reflection;
using System.Threading;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Loads analyzer assemblies from their original locations in the file system.
    /// Assemblies will only be loaded from the locations specified when the loader
    /// is instantiated.
    /// </summary>
    /// <remarks>
    /// This type is meant to be used in scenarios where it is OK for the analyzer
    /// assemblies to be locked on disk for the lifetime of the host; for example,
    /// csc.exe and vbc.exe. In scenarios where support for updating or deleting
    /// the analyzer on disk is required a different loader should be used.
    /// </remarks>
    internal class DefaultAnalyzerAssemblyLoader : AnalyzerAssemblyLoader
    {
        protected override Assembly LoadFromPathImpl(string fullPath)
        {
            var pathToLoad = GetPathToLoad(fullPath);
            return Assembly.LoadFrom(pathToLoad);
        }
    }
}

#endif
