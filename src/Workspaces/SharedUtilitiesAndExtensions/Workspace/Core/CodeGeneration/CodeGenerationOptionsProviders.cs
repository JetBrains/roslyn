﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.AddImport;
using Microsoft.CodeAnalysis.CodeCleanup;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeGeneration;

internal static class CodeGenerationOptionsProviders
{
    public static CodeGenerationOptions GetCodeGenerationOptions(this IOptionsReader options, LanguageServices languageServices)
        => languageServices.GetRequiredService<ICodeGenerationService>().GetCodeGenerationOptions(options);

    public static CodeAndImportGenerationOptions GetCodeAndImportGenerationOptions(this IOptionsReader options, LanguageServices languageServices, bool? allowImportsInHiddenRegions = null)
        => new()
        {
            GenerationOptions = options.GetCodeGenerationOptions(languageServices),
            AddImportOptions = options.GetAddImportPlacementOptions(languageServices, allowImportsInHiddenRegions)
        };

    public static CleanCodeGenerationOptions GetCleanCodeGenerationOptions(this IOptionsReader options, LanguageServices languageServices, bool? allowImportsInHiddenRegions = null)
        => new()
        {
            GenerationOptions = options.GetCodeGenerationOptions(languageServices),
            CleanupOptions = options.GetCodeCleanupOptions(languageServices, allowImportsInHiddenRegions)
        };

    public static async ValueTask<CodeGenerationOptions> GetCodeGenerationOptionsAsync(this Document document, CancellationToken cancellationToken)
    {
        var configOptions = await document.GetAnalyzerConfigOptionsAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable RS0030 // Do not use banned APIs
        return configOptions.GetCodeGenerationOptions(document.Project.LanguageServices.LanguageServices);
#pragma warning restore RS0030 // Do not use banned APIs
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public static async ValueTask<CodeGenerationContextInfo> GetCodeGenerationInfoAsync(this Document document, CodeGenerationContext context, CancellationToken cancellationToken)
    {
        Contract.ThrowIfNull(document.Project.ParseOptions);

        var options = await GetCodeGenerationOptionsAsync(document, cancellationToken).ConfigureAwait(false);
        var service = document.GetRequiredLanguageService<ICodeGenerationService>();
        return service.GetInfo(context, options, document.Project.ParseOptions);
    }

    public static CodeGenerationOptions GetDefault(LanguageServices languageServices)
        => languageServices.GetRequiredService<ICodeGenerationService>().DefaultOptions;
}
