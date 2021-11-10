// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

#if !NETCOREAPP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Microsoft.CodeAnalysis
{
    internal class DefaultAnalyzerAssemblyLoader : AnalyzerAssemblyLoader
    {
        private readonly Type _assemblyLoadContextType;
        private readonly Delegate _relatedAssemblyResolveDelegate;
        private readonly Delegate _resourcesResolveDelegate;
        private readonly EventInfo _resolveEvent;
        private readonly MethodInfo _loadFromAssemblyPathMethod;
        private readonly MethodInfo _unloadMethod;
        private readonly PropertyInfo _assemblyLoadName;

        public DefaultAnalyzerAssemblyLoader()
        {
            _assemblyLoadContextType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext") ?? throw new InvalidOperationException();
            _resolveEvent = _assemblyLoadContextType.GetEvent("Resolving", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException();

            var handlerType = _resolveEvent.EventHandlerType;
            var relatedAssemblyResolveMethodInfo = GetType().GetMethod("RelatedAssemblyResolve", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException();
            var resourcesResolveMethodInfo = GetType().GetMethod("ResourcesResolve", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException();

            _relatedAssemblyResolveDelegate = Delegate.CreateDelegate(handlerType, this, relatedAssemblyResolveMethodInfo);
            _resourcesResolveDelegate = Delegate.CreateDelegate(handlerType, this, resourcesResolveMethodInfo);
            _loadFromAssemblyPathMethod = _assemblyLoadContextType.GetMethod("LoadFromAssemblyPath", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException();
            _assemblyLoadName = _assemblyLoadContextType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException();
            _unloadMethod = _assemblyLoadContextType.GetMethod("Unload", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException();
        }

        private readonly Dictionary<string, object> _loadContexts = new();
        private readonly object _guard = new();

        protected override Assembly LoadFromPathUncheckedImpl(string fullPath)
        {
            object loadContext;

            lock (_guard)
            {
                if (_loadContexts.TryGetValue(fullPath, out loadContext))
                {
                    _unloadMethod.Invoke(loadContext, Array.Empty<object>());
                    _loadContexts.Remove(fullPath);
                }

                loadContext = Activator.CreateInstance(_assemblyLoadContextType, fullPath, true);

                _resolveEvent.AddEventHandler(loadContext, _relatedAssemblyResolveDelegate);
                _resolveEvent.AddEventHandler(loadContext, _resourcesResolveDelegate);

                _loadContexts.Add(fullPath, loadContext);
            }

            return LoadFromAssemblyPath(loadContext, fullPath);
        }

        public Assembly RelatedAssemblyResolve(object loadContext, AssemblyName assemblyName)
        {
            try
            {
                var paths = GetPaths(assemblyName.Name);

                if (paths == null || paths.Count == 0)
                    return null;

                AssemblyName bestCandidateName = null;
                string bestCandidatePath = null;

                foreach (var candidatePath in paths)
                {
                    var candidateName = AssemblyName.GetAssemblyName(candidatePath);

                    if (candidateName.FullName.Equals(assemblyName.FullName, StringComparison.OrdinalIgnoreCase))
                        return LoadFromAssemblyPath(loadContext, candidatePath);

                    if (bestCandidateName != null && bestCandidateName.Version >= candidateName.Version)
                        continue;

                    bestCandidateName = candidateName;
                    bestCandidatePath = candidatePath;
                }

                if (bestCandidatePath == null)
                    return null;

                return LoadFromAssemblyPath(loadContext, bestCandidatePath);
            }
            catch
            {
                return null;
            }
        }

        public Assembly ResourcesResolve(object loadContext, AssemblyName assemblyName)
        {
            try
            {
                string GetResourcePath(string loadAssemblyDirectory, string cultureName)
                {
                    return Path.Combine(loadAssemblyDirectory, cultureName, assemblyName.Name + ".dll");
                }

                if (!assemblyName.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                    return null;

                var fullName = (string)_assemblyLoadName.GetValue(loadContext);

                var loadAssemblyDirectory = GetDirectoryToLoad(fullName);
                if (loadAssemblyDirectory == null)
                    return null;

                var resourcePaths = new[]
                {
                    GetResourcePath(loadAssemblyDirectory, assemblyName.CultureInfo.TwoLetterISOLanguageName),
                    GetResourcePath(loadAssemblyDirectory, assemblyName.CultureInfo.Name)
                };

                foreach (var resourcePath in resourcePaths)
                {
                    if (File.Exists(resourcePath))
                        return (Assembly)_loadFromAssemblyPathMethod.Invoke(loadContext, new object[] { resourcePath });
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private Assembly LoadFromAssemblyPath(object loadContext, string fullPath)
        {
            var pathToLoad = GetPathToLoad(fullPath);
            return (Assembly)_loadFromAssemblyPathMethod.Invoke(loadContext, new object[] { pathToLoad });
        }
    }
}

#endif
