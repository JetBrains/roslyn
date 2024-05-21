﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Roslyn.Utilities
{
    internal static class ImmutableArrayExtensions
    {
        public static ImmutableArray<T> ToImmutableArray<T>(this HashSet<T> set)
        {
            var builder = ImmutableArray.CreateBuilder<T>();
            builder.AddRange(set);
            return builder.ToImmutable();
        }

        public static bool Contains<T>(this ImmutableArray<T> items, T item, IEqualityComparer<T>? equalityComparer)
            => items.IndexOf(item, 0, equalityComparer) >= 0;

        public static ImmutableArray<T> ToImmutableArrayOrEmpty<T>(this T[]? items)
        {
            if (items == null)
            {
                return ImmutableArray.Create<T>();
            }

            return ImmutableArray.Create<T>(items);
        }

        public static ConcatImmutableArray<T> ConcatFast<T>(this ImmutableArray<T> first, ImmutableArray<T> second)
            => new(first, second);

        public static ImmutableArray<T> TakeAsArray<T>(this ImmutableArray<T> array, int count)
        {
            using var _ = ArrayBuilder<T>.GetInstance(count, out var result);
            for (var i = 0; i < count; i++)
                result.Add(array[i]);

            return result.ToImmutableAndClear();
        }

        public static ImmutableArray<T> ToImmutableAndClear<T>(this ImmutableArray<T>.Builder builder)
        {
            if (builder.Count == 0)
                return ImmutableArray<T>.Empty;

            if (builder.Count == builder.Capacity)
                return builder.MoveToImmutable();

            var result = builder.ToImmutable();
            builder.Clear();
            return result;
        }
    }
}
