﻿#pragma warning disable CS8524
#pragma warning disable CS8509

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.ValueProviders
{
	internal static class ValueProviderUtility
	{
		internal const string CREATE_ASSET_MENU_GROUP = "Value Providers/";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValueProvider(Type type)
			=> typeof(IValueProvider).IsAssignableFrom(type)
			|| typeof(IValueByTypeProvider).IsAssignableFrom(type)
			|| typeof(IValueProviderAsync).IsAssignableFrom(type)
			|| typeof(IValueByTypeProviderAsync).IsAssignableFrom(type);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValueProvider([AllowNull] Object obj) => obj is IValueProvider or IValueByTypeProvider or IValueByTypeProviderAsync or IValueProviderAsync;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsAsyncValueProvider([AllowNull] Object obj) => obj is IValueProviderAsync or IValueByTypeProviderAsync;

		public static bool TryGetValueProviderValue([AllowNull] object potentialValueProvider, [DisallowNull] Type valueType, [NotNullWhen(true), MaybeNullWhen(false)] out object valueOrAwaitableToGetValue)
		{
			if(potentialValueProvider is IValueProviderAsync valueProviderAsync)
			{
				valueOrAwaitableToGetValue = valueProviderAsync.GetForAsync(potentialValueProvider as Component);
				return valueOrAwaitableToGetValue is not null;
			}

			if(potentialValueProvider is IValueProvider valueProvider)
			{
				return valueProvider.TryGetFor(potentialValueProvider as Component, out valueOrAwaitableToGetValue);
			}

			if(potentialValueProvider is IValueByTypeProviderAsync)
			{
				object[] args = { potentialValueProvider as Component };
				if((bool)typeof(IValueByTypeProviderAsync).GetMethod(nameof(IValueByTypeProviderAsync.GetForAsync)).MakeGenericMethod(valueType).Invoke(potentialValueProvider, args))
				{
					valueOrAwaitableToGetValue = args[1];
					return valueOrAwaitableToGetValue is not null;
				}
			}

			if(potentialValueProvider is IValueByTypeProvider)
			{
				object[] args = { potentialValueProvider as Component, null };
				if((bool)typeof(IValueByTypeProvider).GetMethod(nameof(IValueByTypeProvider.TryGetFor)).MakeGenericMethod(valueType).Invoke(potentialValueProvider, args))
				{
					valueOrAwaitableToGetValue = args[1];
					return valueOrAwaitableToGetValue is not null;
				}
			}
			
			valueOrAwaitableToGetValue = null;
			return false;
		}

		public static bool TryGetValueProviderValue<TValue>([AllowNull] object potentialValueProvider, [NotNullWhen(true), MaybeNullWhen(false)] out TValue value) => potentialValueProvider switch
        {
        	// Prefer non-async value provider interfaces over async ones
        	IValueProvider<TValue> valueProvider => valueProvider.TryGetFor(null, out value),
        	IValueByTypeProvider valueProvider => valueProvider.TryGetFor(null, out value),
        	IValueProvider valueProvider when valueProvider.TryGetFor(null, out var objectValue) && Find.In(objectValue, out value) => true,
        	IValueProviderAsync<TValue> valueProvider => TryGetFromAwaitableIfCompleted(valueProvider.GetForAsync(null), out value),
        	IValueByTypeProviderAsync valueProvider => TryGetFromAwaitableIfCompleted(valueProvider.GetForAsync<TValue>(null), out value),
			_ => None(out value)
        };

		public static TValue GetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<TValue>
			#else
			System.Threading.Tasks.Task<TValue>
			#endif
			awaitable
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			return awaiter.IsCompleted ? awaiter.GetResult() : default;
			#else
			return awaitable.IsCompletedSuccessfully ? awaitable.Result : default;
			#endif
		}
		
		internal static TValue GetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<object>
			#else
			System.Threading.Tasks.Task<object>
			#endif
			awaitable
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			return awaiter.IsCompleted ? (TValue)awaiter.GetResult() : default;
			#else
			return awaitable.IsCompletedSuccessfully ? (TValue)awaitable.Result : default;
			#endif
		}
		
		internal static bool TryGetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<TValue>
			#else
			System.Threading.Tasks.Task<TValue>
			#endif
			awaitable, out TValue result
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			if(awaiter.IsCompleted && awaiter.GetResult()
			#else
			if(awaitable.IsCompletedSuccessfully && awaitable.Result
			#endif
				is TValue service)
			{
				result = service;
				return true;
			}

			result = default;
			return false;
		}
		
		internal static bool TryGetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<object>
			#else
			System.Threading.Tasks.Task<object>
			#endif
			awaitable, out TValue result
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			if(awaiter.IsCompleted && awaiter.GetResult()
			#else
			if(awaitable.IsCompletedSuccessfully && awaitable.Result
			#endif
				is TValue service)
			{
				result = service;
				return true;
			}

			result = default;
			return false;
		}

		private static bool None<TValue>(out TValue result)
		{
			result = default;
			return false;
		}
	}
}