﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;

namespace Microsoft.CodeAnalysis.Features.Intents
{
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class IntentProviderAttribute : ExportAttribute, IIntentProviderMetadata
    {
        public string IntentName { get; }
        public string LanguageName { get; }

        public IntentProviderAttribute(string intentName, string languageName) : base(typeof(IIntentProvider))
        {
            IntentName = intentName;
            LanguageName = languageName;
        }
    }
}
