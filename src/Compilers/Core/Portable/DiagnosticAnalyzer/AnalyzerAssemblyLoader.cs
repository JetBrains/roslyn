// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// The base implementation for <see cref="IAnalyzerAssemblyLoader"/>. This type provides caching and tracking of inputs given
    /// to <see cref="AddDependencyLocation(string)"/>.
    /// </summary>
    /// <remarks>
    /// This type generally assumes that files on disk aren't changing, since it ensure that two calls to <see cref="LoadFromPath(string)"/>
    /// will always return the same thing, per that interface's contract.
    /// </remarks>
    internal abstract class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        private readonly object _guard = new();

        // lock _guard to read/write
        private readonly Dictionary<string, Assembly> _loadedAssembliesByPath = new();
        private readonly Dictionary<string, AssemblyIdentity?> _loadedAssemblyIdentitiesByPath = new();
        private readonly Dictionary<AssemblyIdentity, Assembly> _loadedAssembliesByIdentity = new();

        // maps file name to a full path (lock _guard to read/write):
        private readonly Dictionary<string, ImmutableHashSet<string>> _knownAssemblyPathsBySimpleName = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Implemented by derived types to actually perform the load for an assembly that doesn't have a cached result.
        /// </summary>
        protected abstract Assembly LoadFromPathUncheckedImpl(string fullPath);

        #region Public API

        public void AddDependencyLocation(string fullPath)
        {
            CompilerPathUtilities.RequireAbsolutePath(fullPath, nameof(fullPath));
            string simpleName = PathUtilities.GetFileName(fullPath, includeExtension: false);

            lock (_guard)
            {
                if (!_knownAssemblyPathsBySimpleName.TryGetValue(simpleName, out var paths))
                {
                    paths = ImmutableHashSet.Create(PathUtilities.Comparer, fullPath);
                    _knownAssemblyPathsBySimpleName.Add(simpleName, paths);
                }
                else
                {
                    _knownAssemblyPathsBySimpleName[simpleName] = paths.Add(fullPath);
                }
            }
        }

        public Assembly LoadFromPath(string fullPath)
        {
            CompilerPathUtilities.RequireAbsolutePath(fullPath, nameof(fullPath));
            return LoadFromPathUnchecked(fullPath);
        }

        #endregion

        /// <summary>
        /// Returns the cached assembly for fullPath if we've done a load for this path before, or calls <see cref="LoadFromPathUncheckedImpl"/> if
        /// it needs to be loaded. This method skips the check in release builds that the path is an absolute path, hence the "Unchecked" in the name.
        /// </summary>
        protected Assembly LoadFromPathUnchecked(string fullPath)
        {
            Debug.Assert(PathUtilities.IsAbsolute(fullPath));

            // Check if we have already loaded an assembly from the given path.
            Assembly? loadedAssembly = null;
            AssemblyIdentity? identity = null;
            lock (_guard)
            {
                if (_loadedAssembliesByPath.TryGetValue(fullPath, out var existingAssembly))
                {
                    Module module = existingAssembly.ManifestModule;
                    Guid runtimeMvid = module.ModuleVersionId;
                    Guid assemblyMvid = ReadMvid(fullPath);
                    if (runtimeMvid == assemblyMvid)
                    {
                        loadedAssembly = existingAssembly;
                    }
                    else
                    {
                        _loadedAssembliesByPath.Remove(fullPath);
                        if (_loadedAssemblyIdentitiesByPath.TryGetValue(fullPath, out var oldIdentity))
                        {
                            _loadedAssembliesByIdentity.Remove(oldIdentity);
                            _loadedAssemblyIdentitiesByPath.Remove(fullPath);
                        }

                        string simpleName = PathUtilities.GetFileName(fullPath, includeExtension: false);
                        if (_knownAssemblyPathsBySimpleName.TryGetValue(simpleName, out var set))
                        {
                            _knownAssemblyPathsBySimpleName.Remove(simpleName);
                            _knownAssemblyPathsBySimpleName[simpleName] = set.Remove(fullPath);
                        }
                    }
                }

                if (loadedAssembly == null)
                {
                    identity = GetOrAddAssemblyIdentity(fullPath);
                    if (identity != null && _loadedAssembliesByIdentity.TryGetValue(identity, out existingAssembly))
                    {
                        loadedAssembly = existingAssembly;
                    }
                }
            }

            // Otherwise, load the assembly.
            if (loadedAssembly == null)
            {
                loadedAssembly = LoadFromPathUncheckedImpl(fullPath);
            }

            identity ??= identity ?? AssemblyIdentity.FromAssemblyDefinition(loadedAssembly);

            lock (_guard)
            {
                // The same assembly may be loaded from two different full paths (e.g. when loaded from GAC, etc.),
                // or another thread might have loaded the assembly after we checked above.
                if (_loadedAssembliesByIdentity.TryGetValue(identity, out var existingAssembly))
                {
                    loadedAssembly = existingAssembly;
                }
                else
                {
                    _loadedAssembliesByIdentity.Add(identity, loadedAssembly);
                }

                // Add the loaded assembly to the path cache.
                _loadedAssembliesByPath[fullPath] = loadedAssembly;

                return loadedAssembly;
            }
        }

        private AssemblyIdentity? GetOrAddAssemblyIdentity(string fullPath)
        {
            Debug.Assert(PathUtilities.IsAbsolute(fullPath));

            lock (_guard)
            {
                if (_loadedAssemblyIdentitiesByPath.TryGetValue(fullPath, out var existingIdentity))
                {
                    return existingIdentity;
                }
            }

            var identity = AssemblyIdentityUtils.TryGetAssemblyIdentity(fullPath);

            lock (_guard)
            {
                if (_loadedAssemblyIdentitiesByPath.TryGetValue(fullPath, out var existingIdentity) && existingIdentity != null)
                {
                    identity = existingIdentity;
                }
                else
                {
                    _loadedAssemblyIdentitiesByPath[fullPath] = identity;
                }
            }

            return identity;
        }

        protected ImmutableHashSet<string>? GetPaths(string simpleName)
        {
            lock (_guard)
            {
                _knownAssemblyPathsBySimpleName.TryGetValue(simpleName, out var paths);
                return paths;
            }
        }

        /// <summary>
        /// When overridden in a derived class, allows substituting an assembly path after we've
        /// identified the context to load an assembly in, but before the assembly is actually
        /// loaded from disk. This is used to substitute out the original path with the shadow-copied version.
        /// </summary>
        protected virtual string GetPathToLoad(string fullPath)
        {
            return fullPath;
        }

        protected virtual string? GetDirectoryToLoad(string fullPath)
        {
            return Path.GetDirectoryName(fullPath);
        }

        private static Guid ReadMvid(string filePath)
        {
            RoslynDebug.Assert(PathUtilities.IsAbsolute(filePath));

            using var reader = new PEReader(FileUtilities.OpenRead(filePath));
            var metadataReader = reader.GetMetadataReader();
            var mvidHandle = metadataReader.GetModuleDefinition().Mvid;
            var fileMvid = metadataReader.GetGuid(mvidHandle);

            return fileMvid;
        }
    }
}
