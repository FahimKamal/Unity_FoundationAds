using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Sisus.Init.Internal;
using Sisus.Init.ValueProviders;
using UnityEngine;
using static Sisus.NullExtensions;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
using UnityEngine.ResourceManagement.ResourceLocations;
#endif

namespace Sisus.Init
{
	/// <summary>
	/// Utility that clients can use to retrieve services that are accessible to them.
	/// <para>
	/// A service can be located from <see cref="Services"/> components found in the active scenes
	/// which are accessible to the client or from the globally shared <see cref="Service{TService}.Instance"/>
	/// which can be accessed by any client.
	/// </para>
	/// </summary>
	public static class Service
	{
		internal static Type nowSettingInstance;

		#if UNITY_EDITOR
		internal static event Action AnyChangedEditorOnly;
		private static readonly List<ServiceInfo> activeInstancesEditorOnlyEditMode = new(64);
		private static readonly List<ServiceInfo> activeInstancesEditorOnlyPlayMode = new(64);
		private static readonly ServiceInfoOrderer serviceInfoOrdererEditorOnly = new();
		private static List<ServiceInfo> ActiveInstancesEditorOnly => Application.isPlaying ? activeInstancesEditorOnlyPlayMode : activeInstancesEditorOnlyEditMode;

		internal static void UsingActiveInstancesEditorOnly(Action<List<ServiceInfo>> action)
		{
			lock(ActiveInstancesEditorOnly)
			{
				action(ActiveInstancesEditorOnly);
			}
		}
		#endif

		/// <summary>
		/// Determines whether service of type <typeparamref name="TService"/>
		/// is available for the <paramref name="client"/>.
		/// <para>
		/// The service can be located from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ExistsFor<TService>([DisallowNull] object client)
			=> ServiceInjector.CanProvideService<TService>()
			|| (TryGetGameObject(client, out GameObject gameObject)
				? TryGetFor<TService>(gameObject, out _)
				: TryGet<TService>(out _));

		/// <summary>
		/// Gets a value indicating whether service of type <typeparamref name="TService"/> is available for the <paramref name="client"/>.
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service is available for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ExistsFor<TService>([DisallowNull] Component client) => ServiceInjector.CanProvideService<TService>() || TryGetFor<TService>(client, out _);

		/// <summary>
		/// Determines whether service of type <typeparamref name="TService"/> is available
		/// for the <paramref name="client"/>.
		/// <para>
		/// The service can be located from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ExistsFor<TService>([DisallowNull] GameObject client) => ServiceInjector.CanProvideService<TService>() || TryGetFor<TService>(client, out _);

		/// <summary>
		/// Determines whether service of type <typeparamref name="TService"/>
		/// is available and globally accessible by any client.
		/// <para>
		/// The service can be located from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool Exists<TService>() => ServiceInjector.CanProvideService<TService>() || TryGet<TService>(out _);

		/// <summary>
		/// Determines whether the given <paramref name="object"/> is a service accessible by the <paramref name="client"/>.
		/// <para>
		/// Services are components that have the <see cref="ServiceTag"/> attached to them,
		/// have been defined as a service in a <see cref="Services"/> component,
		/// have the <see cref="ServiceAttribute"/> on their class,
		/// or have been <see cref="Set{TService}">manually registered</see> as a service in code.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that has to be able to access the service. </param>
		/// <param name="object"> The object to test. </param>
		/// <returns>
		/// <see langword="true"/> if the <paramref name="object"/> is a service accessible by the <paramref name="client"/>;
		/// otherwise, <see langword="false"/>.
		/// </returns>
		public static bool ForEquals<TService>([DisallowNull] GameObject client, TService @object)
			=> TryGetFor(client, out TService service) && ReferenceEquals(service, @object);

		public static bool TryGetClients<TService>([AllowNull] TService service, out Clients clients)
		{
			if(service is null)
			{
				if(ServiceAttributeUtility.definingTypes.ContainsKey(typeof(TService)))
				{
					clients = Clients.Everywhere;
					return true;
				}

				clients = default;
				return false;
			}

			if(!typeof(TService).IsValueType && ReferenceEquals(Service<TService>.Instance, service))
			{
				clients = Clients.Everywhere;
				return true;
			}

			foreach(var instance in ScopedService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.serviceProvider)
				{
					continue;
				}
				#endif

				if(ReferenceEquals(instance.service, service))
				{
					clients = instance.clients;
					return true;
				}
			}

			clients = default;
			return false;
		}

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>,
		/// if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGetFor<TService>([DisallowNull] object client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
			=> TryGetGameObject(client, out GameObject gameObject)
				? TryGetFor(gameObject.transform, out service)
				: TryGet(out service);

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="Component"/> that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGetFor<TService>([DisallowNull] Component client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
		{
			#if DEV_MODE
			Debug.Assert(client);
			#endif

			bool foundResult = false;
			ScopedService<TService>.Instance nearest = default;

			foreach(var instance in ScopedService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.serviceProvider)
				{
					continue;
				}
				#endif

				if(!IsAccessibleTo(instance, client.transform))
				{
					continue;
				}

				var someService = instance.service;
				if(someService is null)
				{
					continue;
				}

				var nearestService = nearest.service;
				if(nearestService is null)
				{
					nearest = instance;
					foundResult = true;
					continue;
				}

				if(ReferenceEquals(someService, nearestService))
				{
					continue;
				}

				var clientScene = client.gameObject.scene;
				if(instance.Scene != clientScene)
				{
					#if DEBUG || INIT_ARGS_SAFE_MODE && !INIT_ARGS_DISABLE_WARNINGS
					if(nearest.Scene == clientScene)
					{
						continue;
					}

					#if UNITY_EDITOR
					// Prioritize scene objects over uninstantiated prefabs
					var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
					if(!instance.Scene.IsValid() || (prefabStage && instance.Scene == prefabStage.scene))
					{
						continue;
					}

					if(!nearest.Scene.IsValid() || (prefabStage && nearest.Scene == prefabStage.scene))
					{
						nearest = instance;
						foundResult = true;
						continue;
					}

					// Don't spam warnings when services are requested in edit mode, for example in Inspector code.
					if(!Application.isPlaying || !clientScene.IsValid() || clientScene == prefabStage.scene)
					{
						continue;
					}
					#endif

					Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services {TypeUtility.ToString(instance.service.GetType())} and {TypeUtility.ToString(nearest.service.GetType())} via {nameof(Services)}s and unable to determine which one should be prioritized.", client);
					#endif

					continue;
				}

				if(nearest.Scene != clientScene)
				{
					nearest = instance;
					continue;
				}

				var instanceTransform = instance.Transform;
				var nearestTransform = nearest.Transform;

				#if DEBUG || INIT_ARGS_SAFE_MODE
				bool betterMatchFound = false;
				#endif

				for(var clientParent = client.transform; clientParent; clientParent = clientParent.parent)
				{
					if(clientParent == instanceTransform)
					{
						#if DEBUG || INIT_ARGS_SAFE_MODE
						if(clientParent == nearestTransform)
						{
							break;
						}

						betterMatchFound = true;
						#endif

						nearest = instance;
						break;
					}

					if(clientParent == nearestTransform)
					{
						#if DEBUG || INIT_ARGS_SAFE_MODE
						betterMatchFound = true;
						#endif
						break;
					}
				}

				#if DEBUG || INIT_ARGS_SAFE_MODE && !INIT_ARGS_DISABLE_WARNINGS
				if(!betterMatchFound)
				{
					#if UNITY_EDITOR
					// Don't spam warnings when services are requested in edit mode, for example in Inspector code.
					var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
					if(!Application.isPlaying || !clientScene.IsValid() || clientScene == prefabStage.scene)
					{
						continue;
					}
					#endif

					Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services \"{instance.Transform.name}\"/{TypeUtility.ToString(instance.service.GetType())} and \"{nearest.Transform.name}\"/{TypeUtility.ToString(nearest.service.GetType())} via {nameof(Services)} and unable to determine which one should be prioritized.", client);
				}
				#endif
			}

			service = nearest.service;
			if(foundResult && service != Null)
			{
				return true;
			}

			if(!typeof(TService).IsValueType)
			{
				#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
				if(ServiceInjector.TryGetUninitializedServiceInfo(typeof(TService), out var serviceInfo))
				{
					if(serviceInfo.LazyInit
					#if UNITY_EDITOR
					&& Application.isPlaying
					#endif
					)
					{
						var task = ServiceInjector.LazyInit(serviceInfo, typeof(TService));
						if(task.IsCompleted)
						{
							if(task.Result is TService result)
							{
								service = result;
								return service is not null;
							}

							#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
							if(task.IsFaulted)
							{
								throw task.Exception;
							}
							#endif
						}
					}
				}
				#endif

				var globalService = Service<TService>.Instance;
				if(globalService != Null)
				{
					service = globalService;
					return true;
				}

				#if UNITY_EDITOR
				if(ServiceAttributeUtility.definingTypes.TryGetValue(typeof(TService), out GlobalServiceInfo info) && info.FindFromScene)
				{
					service = Find.Any<TService>();
					return Find.Any(out service);
				}
				#endif
			}

			if(!typeof(IValueProvider).IsAssignableFrom(typeof(TService)) && TryGetFor(client, out IValueProvider<TService> serviceProvider))
			{
				service = serviceProvider.Value;
				return service != Null;
			}

			return ServiceProvider<TService>.TryGetValue(client, out service);
		}

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="GameObject"/> that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGetFor<TService>([DisallowNull] GameObject client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
			=> TryGetFor(client.transform, out service);

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can be called from any thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		public static bool TryGet<TService>([NotNullWhen(true), MaybeNullWhen(false)] out TService service)
		{
			bool foundResult = false;
			ScopedService<TService>.Instance nearest = default;

			foreach(var instance in ScopedService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.serviceProvider)
				{
					continue;
				}
				#endif

				if(instance.clients != Clients.Everywhere)
				{
					continue;
				}

				#if DEBUG || INIT_ARGS_SAFE_MODE && !INIT_ARGS_DISABLE_WARNINGS

				#if UNITY_EDITOR
				// Prioritize scene objects over uninstantiated prefabs
				var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
				if(!instance.Scene.IsValid() || (prefabStage && instance.Scene == prefabStage.scene))
				{
					continue;
				}

				if(!nearest.Scene.IsValid() || (prefabStage && nearest.Scene == prefabStage.scene))
				{
					nearest = instance;
					foundResult = true;
					continue;
				}

				// Don't spam warnings when services are requested in edit mode, for example in Inspector code.
				if(!Application.isPlaying)
				{
					continue;
				}
				#endif

				if(!EqualityComparer<TService>.Default.Equals(nearest.service, instance.service) && nearest.service != Null && instance.service != Null)
				{
					Debug.LogWarning($"AmbiguousMatchWarning: All clients have access to both services \"{nearest.Transform.name}\"/{TypeUtility.ToString(nearest.service.GetType())} and \"{instance.Transform.name}\"/{TypeUtility.ToString(instance.service.GetType())} and unable to determine which one should be prioritized.");
				}
				#endif

				nearest = instance;
				foundResult = true;

				#if !DEBUG
				break;
				#endif
			}

			service = nearest.service;
			if(foundResult && service != Null)
			{
				return true;
			}

			if(!typeof(TService).IsValueType)
			{
				#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
				if(ServiceInjector.TryGetUninitializedServiceInfo(typeof(TService), out var serviceInfo)
				#if UNITY_EDITOR
				&& EditorOnly.ThreadSafe.Application.IsPlaying
				#endif
				)
				{
					_ = ServiceInjector.LazyInit(serviceInfo, typeof(TService));
				}
				#endif

				var globalService = Service<TService>.Instance;
				if(globalService != Null)
				{
					service = globalService;
					return true;
				}

				#if UNITY_EDITOR
				if(ServiceAttributeUtility.definingTypes.TryGetValue(typeof(TService), out GlobalServiceInfo info) && info.FindFromScene)
				{
					service = Find.Any<TService>();
					return Find.Any(out service);
				}
				#endif
			}

			if(!typeof(IValueProvider).IsAssignableFrom(typeof(TService)) && TryGet(out IValueProvider<TService> serviceProvider))
			{
				service = serviceProvider.Value;
				return service != Null;
			}

			return ServiceProvider<TService>.TryGetValue(null, out service);
		}

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes,
		/// or failing that, from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is accessible to the <paramref name="client"/>. </exception>
		[return: NotNull]
		public static TService GetFor<TService>([DisallowNull] Component client)
			=> TryGetFor(client.gameObject, out TService result)
			? result
			: throw new NullReferenceException($"No service of type {typeof(TService).Name} was found that was accessible to client {TypeUtility.ToString(client.GetType())}.");

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes, or failing that,
		/// from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="GameObject"/> that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is accessible to the <paramref name="client"/>. </exception>
		[return: NotNull]
		public static TService GetFor<TService>([DisallowNull] GameObject client) => TryGetFor(client, out TService service) ? service : throw new NullReferenceException($"No service of type {typeof(TService).Name} was found that was accessible to client {client.GetType().Name}.");

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for any client.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes, or failing that,
		/// from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is globally accessible to any client. </exception>
		[return: NotNull]
		public static TService Get<TService>() => TryGet(out TService service) ? service : throw new NullReferenceException($"No globally accessible Service of type {typeof(TService).Name} was found.");

		private static bool TryGetGameObject(object client, out GameObject gameObject)
		{
			var component = client as Component;
			if(component)
			{
				gameObject = component.gameObject;
				return true;
			}

			gameObject = client as GameObject;
			if(gameObject)
			{
				return true;
			}

			if(!Find.WrapperOf(client, out var wrapper))
			{
				gameObject = null;
				return false;
			}

			component = wrapper as Component;
			if(component)
			{
				gameObject = component.gameObject;
				return true;
			}

			gameObject = null;
			return false;
		}

		/// <summary>
		/// Sets the <typeparamref name="TService"/> service instance
		/// shared across clients to the given value.
		/// <para>
		/// If the provided instance is not equal to the old <see cref="ScopedService{TService}.Instance"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="newInstance"> The new instance of the service. </param>
		public static void Set<TService>([DisallowNull] TService newInstance)
		{
			Debug.Assert(newInstance != null, typeof(TService).Name);

			nowSettingInstance = typeof(TService);

			var oldInstance = Service<TService>.Instance;

			if(ReferenceEquals(oldInstance, newInstance))
			{
				nowSettingInstance = null;
				return;
			}
			
			Service<TService>.Instance = newInstance;

			nowSettingInstance = null;

			HandleInstanceChanged(Clients.Everywhere, oldInstance, newInstance);
		}

		public static TService Unset<TService>()
		{
			nowSettingInstance = typeof(TService);

			var currentInstance = Service<TService>.Instance;

			if(currentInstance == Null)
			{
				nowSettingInstance = null;
				return currentInstance;
			}

			Service<TService>.Instance = default;

			nowSettingInstance = null;

			HandleInstanceChanged(Clients.Everywhere, currentInstance, default);
			return currentInstance;
		}

		public static bool Unset<TService>(TService instance)
		{
			var currentInstance = Service<TService>.Instance;

			if(!ReferenceEquals(currentInstance, instance) || currentInstance == Null)
			{
				return false;
			}

			nowSettingInstance = typeof(TService);
			Service<TService>.Instance = default;
			nowSettingInstance = null;

			HandleInstanceChanged(Clients.Everywhere, currentInstance, default);
			return true;
		}

		public static void Dispose<TService>()
		{
			nowSettingInstance = typeof(TService);

			var oldInstance = Service<TService>.Instance;

			if(oldInstance is null)
			{
				nowSettingInstance = null;
				return;
			}

			Service<TService>.Instance = default;

			if(oldInstance is Object unityObject)
			{
				if(unityObject)
				{
					Object.Destroy(unityObject);
				}
			}
			else if(oldInstance is IDisposable disposable)
			{
				disposable.Dispose();
			}

			nowSettingInstance = null;

			HandleInstanceChanged(Clients.Everywhere, oldInstance, default);
		}

		// #if DEV_MODE
		// // Limitation: can only have a single defining type!
		// public static void Set<TService, TInstance>
		// (
		// 	bool findFromScene = false,
		// 	bool lazyInit = false
		// )
		// 	where TInstance : TService
		// {
		// 	Debug.Assert(!typeof(TInstance).IsAbstract, $"{nameof(Service)}.{nameof(Set)}<{TypeUtility.ToString(typeof(TService))}, {TypeUtility.ToString(typeof(TInstance))}> an abstract instance type: {TypeUtility.ToString(typeof(TInstance))}.");
		// 	Debug.Assert(!typeof(TInstance).IsValueType, $"{nameof(Service)}.{nameof(Set)}<{TypeUtility.ToString(typeof(TService))}, {TypeUtility.ToString(typeof(TInstance))}> was with a instance type that was a struct: {TypeUtility.ToString(typeof(TInstance))}.");
		//
		// 	var attribute = new ServiceAttribute
		// 	{
		// 		FindFromScene = findFromScene,
		// 		LazyInit = lazyInit
		// 	};
		//
		// 	var concreteType = typeof(TService);
		// 	var definingType = typeof(TInstance);
		// 	var serviceInfo = new GlobalServiceInfo(concreteType, new[] { attribute }, concreteType, new [] { definingType });
		// 	ServiceInjector.Register(serviceInfo);
		// }
		// #endif

		#if DEV_MODE // TODO: Finish these up + add unit tests

		public static TService CreateInstance<TInstance, TService>(bool lazyInit = false) where TInstance : class, TService => Register(typeof(TService), concreteType:typeof(TInstance), lazyInit:lazyInit, instantiate:true) as TInstance;
		public static TService CreateInstance<TService>(bool lazyInit = false) where TService : class => Register(typeof(TService), concreteType:typeof(TService), lazyInit:lazyInit, instantiate:true) as TService;
		//public static TService CreateInstance<TService>(IValueProvider<TService> valueProvider, bool lazyInit = false) where TService : class => Register(typeof(TService), concreteType:typeof(TService), lazyInit:lazyInit, instantiate:true) as TService;

		private static object Register
		(
			Type definingType,
			Type concreteType = null,
			bool findFromScene = false,
			bool lazyInit = false,
			bool? instantiate = default,
			bool loadAsync = false,
			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			string addressableKey = null,
			#endif
			string resourcePath = null
		)
		{
			Debug.Assert(definingType is not null, $"{nameof(Service)} registration was attempted with a null defining type: {TypeUtility.ToString(definingType)}.");
			Debug.Assert(!definingType.IsAbstract, $"{nameof(Service)}.registration was attempted with an abstract defining type: {TypeUtility.ToString(definingType)}.");
			Debug.Assert(!definingType.IsValueType, $"{nameof(Service)}.registration was attempted with a defining type that was a struct: {TypeUtility.ToString(definingType)}.\nYou should register struct type services via a IValueProvider<{TypeUtility.ToString(definingType)}> instead.");
			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			Debug.Assert(string.IsNullOrEmpty(resourcePath) || string.IsNullOrEmpty(addressableKey), $"{nameof(Service)}. registration was attempted with both a {nameof(resourcePath)} (\"{resourcePath}\") and an {nameof(addressableKey)} (\"{addressableKey}\") provided. A service can't be loaded using two different methods simultaneously.");
			#endif

			var attribute = new ServiceAttribute
			{
				FindFromScene = findFromScene,
				LazyInit = lazyInit,
				Instantiate = instantiate,
				LoadAsync = loadAsync,
				#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
				AddressableKey = addressableKey,
				#endif
				ResourcePath = resourcePath
			};

			var serviceInfo = new GlobalServiceInfo(concreteType ?? definingType, new[] { attribute }, concreteType, new [] { definingType });
			var task = ServiceInjector.Register(serviceInfo);
			return task.IsCompleted ? task.Result : default;
		}

		public static TService Instantiate<TService>(TService prefab, bool lazyInit = false) where TService : Object => throw new NotImplementedException();
		public static Task<TService> InstantiateAsync<TService, TInstance>(TInstance prefab, bool lazyInit = false) => throw new NotImplementedException();
		public static TService FindFromScene<TService>(bool lazyInit = false) => throw new NotImplementedException();
		public static TService LoadResource<TService>(string resourcePath, bool lazyInit = false) => throw new NotImplementedException();
		public static Task<TService> LoadResourceAsync<TService>(string resourcePath, bool lazyInit = false) => throw new NotImplementedException();
		public static TService InstantiateResource<TService>(string resourcePath, bool lazyInit = false) => throw new NotImplementedException();
		public static Task<TService> InstantiateResourceAsync<TService>(string resourcePath, bool lazyInit = false) => throw new NotImplementedException();
		#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
		public static TService LoadAsset<TService>(string addressableKey, bool lazyInit = false) => throw new NotImplementedException();
		public static Task<TService> LoadAssetAsync<TService>(string addressableKey, bool lazyInit = false) => throw new NotImplementedException();
		public static TService LoadAsset<TService>(IResourceLocation resourceLocation, bool lazyInit = false) => LoadAsset<TService>(resourceLocation.PrimaryKey);
		public static Task<TService> LoadAssetAsync<TService>(IResourceLocation resourceLocation, bool lazyInit = false) => LoadAssetAsync<TService>(resourceLocation.PrimaryKey);
		public static TService InstantiateAsset<TService>(string addressableKey, bool lazyInit = false) => throw new NotImplementedException();
		public static Task<TService> InstantiateAssetAsync<TService>(string addressableKey, bool lazyInit = false) => throw new NotImplementedException();
		public static TService InstantiateAsset<TService>(IResourceLocation resourceLocation, bool lazyInit = false) => LoadAsset<TService>(resourceLocation.PrimaryKey);
		public static Task<TService> InstantiateAssetAsync<TService>(IResourceLocation resourceLocation, bool lazyInit = false) => LoadAssetAsync<TService>(resourceLocation.PrimaryKey);
		#endif

		#endif

		#if DEV_MODE
		// Limitation: can only have a single defining type!
		internal static void Set<TService, TInstance>
		(
			string resourcePath,
			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			string addressableKey = null,
			#endif
			bool lazyInit = false,
			bool loadAsync = false,
			bool? instantiate = null
		)
			where TInstance : TService
		{
			Debug.Assert(!typeof(TInstance).IsAbstract, $"{nameof(Service)}.{nameof(Set)}<{TypeUtility.ToString(typeof(TService))}, {TypeUtility.ToString(typeof(TInstance))}> an abstract instance type: {TypeUtility.ToString(typeof(TInstance))}.");
			Debug.Assert(!typeof(TInstance).IsValueType, $"{nameof(Service)}.{nameof(Set)}<{TypeUtility.ToString(typeof(TService))}, {TypeUtility.ToString(typeof(TInstance))}> was with a instance type that was a struct: {TypeUtility.ToString(typeof(TInstance))}.");
			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			Debug.Assert(string.IsNullOrEmpty(resourcePath) || string.IsNullOrEmpty(addressableKey), $"{nameof(Service)}.{nameof(Set)} was called with both a {nameof(resourcePath)} (\"{resourcePath}\") and an {nameof(addressableKey)} (\"{addressableKey}\"). A service can't be loaded using two different methods simultaneously.");
			#endif

			var attribute = new ServiceAttribute
			{
				LazyInit = lazyInit,
				LoadAsync = loadAsync,
				Instantiate = instantiate,
				ResourcePath = resourcePath,
				#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
				AddressableKey = addressableKey
				#endif
			};

			var concreteType = typeof(TService);
			var definingType = typeof(TInstance);
			var serviceInfo = new GlobalServiceInfo(concreteType, new[] { attribute }, concreteType, new [] { definingType });
			ServiceInjector.Register(serviceInfo);
		}
		#endif

		/// <summary>
		/// Obsolete.
		/// <para>
		/// Use <see cref="Set{TService}"/> instead.
		/// </para>
		/// </summary>
		public static void SetInstance<TService>([DisallowNull] TService newInstance) => Set(newInstance);

		internal static void SetSilently<TService>([AllowNull] TService newInstance)
		{
			Debug.Assert(newInstance != null, typeof(TService).Name);

			nowSettingInstance = typeof(TService);

			var oldInstance = Service<TService>.Instance;

			if(ReferenceEquals(oldInstance, newInstance))
			{
				nowSettingInstance = null;
				return;
			}

			Service<TService>.Instance = newInstance;

			nowSettingInstance = null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void HandleInstanceChanged<TService>(Clients clients, TService oldInstance, TService newInstance)
		{
			#if DEV_MODE && DEBUG_SERVICE_CHANGED
			Debug.Log($"Service Changed: {oldInstance?.GetType().Name} -> {newInstance?.GetType().Name})");
			#endif

			ServiceChanged<TService>.listeners?.Invoke(clients, oldInstance, newInstance);

			#if UNITY_EDITOR
			AnyChangedEditorOnly?.Invoke();

			var activeInstances = ActiveInstancesEditorOnly;
			if(oldInstance is not null)
			{
				lock(activeInstances)
				{
					activeInstances.Remove(new(typeof(TService), clients, oldInstance));
				}
			}

			if(newInstance is not null)
			{
				var serviceInfo = new ServiceInfo(typeof(TService), clients, newInstance);
				lock(activeInstances)
				{
					int index = activeInstances.BinarySearch(serviceInfo, serviceInfoOrdererEditorOnly);
					if(index >= 0)
					{
						activeInstances[index] = serviceInfo;
					}
					else
					{
						activeInstances.Insert(~index, serviceInfo);
					}
				}
			}
			#endif
		}

		[Conditional("UNITY_EDITOR")]
		internal static void HandleInitializationFailed(InitArgsException exception, [DisallowNull] GlobalServiceInfo globalServiceInfo, ServiceInitFailReason reason, Object asset, Object sceneObject, object initializerOrWrapper,  Type concreteType)
		{
			#if UNITY_EDITOR
			var serviceInfo = new ServiceInfo(globalServiceInfo.definingTypes.FirstOrDefault() ?? concreteType, Clients.Everywhere, sceneObject ?? asset ?? initializerOrWrapper, exception.Message, reason);

			var activeInstances = ActiveInstancesEditorOnly;
			lock(activeInstances)
			{
				int index = activeInstances.BinarySearch(serviceInfo, serviceInfoOrdererEditorOnly);
				if(index >= 0)
				{
					activeInstances[index] = serviceInfo;
				}
				else
				{
					activeInstances.Insert(~index, serviceInfo);
				}
			}
			#endif
		}

		/// <summary>
		/// Registers a service with the defining type <typeparamref name="TService"/>
		/// available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// </typeparam>
		/// <param name="clients">
		/// Specifies which client objects can receive the service instance in their Init function
		/// during their initialization.
		/// </param>
		/// <param name="service"> The service instance to add. </param>
		/// <param name="container">
		/// Component that is registering the service. This can also be the service itself, if it is a component.
		/// <para>
		/// This same argument should be passed when <see cref="RemoveFrom{TService}">removing the instance</see>.
		/// </para>
		/// </param>
		public static void AddFor<TService>(Clients clients, [DisallowNull] TService service, [DisallowNull] Component container)
		{
			#if DEV_MODE
			Debug.Assert(service != null);
			Debug.Assert(container);
			#endif

			if(ScopedService<TService>.Add(service, clients, container))
			{
				HandleInstanceChanged(clients, default, service);
			}
		}

		/// <summary>
		/// Unregisters a service with the defining type <typeparamref name="TService"/>
		/// that has been available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// </typeparam>
		/// <param name="clients"> The availability of the service being removed. </param>
		/// <param name="service"> The service instance to remove. </param>
		/// <param name="container"> Component that registered the service. </param>
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] TService service, [DisallowNull] Component container)
		{
			if(ScopedService<TService>.Remove(service, clients, container))
			{
				HandleInstanceChanged(clients, service, default);
			}
		}

		/// <summary>
		/// Subscribes the provided <paramref name="method"/> to listen for changes made to the shared instance of service of type <typeparamref name="TService"/>.
		/// <para>
		/// The method will only be called when in reaction to services that are accesible by all <see cref="Clients"/> changing.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="method">
		/// Method to call when the shared instance of service of type <typeparamref name="TService"/> has changed to a different one.
		/// </param>
		public static void AddInstanceChangedListener<TService>(ServiceChangedHandler<TService> method) => ServiceChanged<TService>.listeners += method;

		/// <summary>
		/// Unsubscribes the provided <paramref name="method"/> from listening for changes made to the shared instance of service of type <typeparamref name="TService"/>.
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="method">
		/// Method that should no longer be called when the shared instance of service of type <typeparamref name="TService"/> has changed to a different one.
		/// </param>
		public static void RemoveInstanceChangedListener<TService>(ServiceChangedHandler<TService> method) => ServiceChanged<TService>.listeners -= method;

		internal static bool IsServiceFor<TService>([DisallowNull] Component client, [DisallowNull] TService test)
		{
			#if DEV_MODE
			Debug.Assert(client is not null, "Service.IsServiceFor called with null client");
			Debug.Assert(test is not null, "Service.IsServiceFor called with null object to test");
			#endif

			foreach(var instance in ScopedService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.serviceProvider)
				{
					continue;
				}
				#endif

				if(ReferenceEquals(test, instance.service))
				{
					return IsAccessibleTo(instance, client.transform);
				}
			}

			if(!typeof(TService).IsValueType)
			{
				#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
				if(ServiceInjector.TryGetUninitializedServiceInfo(typeof(TService), out var serviceInfo))
				{
					#if UNITY_EDITOR
					if(EditorOnly.ThreadSafe.Application.IsPlaying)
					#endif
						_ = ServiceInjector.LazyInit(serviceInfo, typeof(TService));
				}
				#endif

				#if UNITY_EDITOR
				if(ServiceAttributeUtility.definingTypes.ContainsKey(typeof(TService)))
				{
					return true;
				}
				#endif

				if(ReferenceEquals(test, Service<TService>.Instance))
				{
					return true;
				}
			}

			// If TService is not a service, but IValueProvider<TService> is,
			// then check if value returned by the IValueProvider<TService> service
			// matches the object being tested. If it does, then consider the tested
			// object a service as well.
			return !typeof(IValueProvider).IsAssignableFrom(typeof(TService))
				&& TryGetFor(client, out IValueProvider<TService> serviceProvider)
				&& serviceProvider.TryGetFor(client, out TService service)
				&& ReferenceEquals(test, service);
		}

		internal static bool IsServiceOrServiceProvider<TService>([DisallowNull] TService test)
		{
			if(TryGet(out TService globalService) && ReferenceEquals(test, globalService))
			{
				return true;
			}

			if(IsServiceProvider(test))
			{
				return true;
			}

			return false;
		}

		internal static bool IsServiceProvider<TService>([DisallowNull] TService test)
		{
			if(!ValueProviderUtility.TryGetValueProviderValue(test, out TService value))
			{
				return false;
			}

			return ScopedService<TService>.IsService(value) || ReferenceEquals(Service<TService>.Instance, value);
		}

		public static bool IsServiceOrServiceProviderFor<TService>([DisallowNull] Component client, [DisallowNull] object test)
		{
			if(test is TService service && IsServiceFor(client, service))
			{
				return true;
			}

			if(IsServiceProviderFor<TService>(client, test))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Gets a value indicating whether <see cref="test"/> is a value provider service which is accessible to client.
		/// <para>
		/// NOTE: Does not consider whether <see cref="test"/> is the closest service provider for the client. It's
		/// possible that even if this method returns <see langword="true"/>, there are other services with the same
		/// defining type at a closer proximity to the client, which would be prioritized over this one when
		/// initializing the client.
		/// </para>
		/// </summary>
		/// <param name="client"> Type of the client. </param>
		/// <param name="test"> Type of the object to test. </param>
		/// <typeparam name="TService"> Type of the client's dependency. </typeparam>
		/// <returns>
		/// <see cref="test"/> if <see langword="true"/> is a value provider service accessible to the client; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool IsServiceProviderFor<TService>([DisallowNull] Component client, [DisallowNull] object test)
		{
			#if DEV_MODE
			Debug.Assert(client);
			Debug.Assert(test != null);
			#endif

			if(!ValueProviderUtility.TryGetValueProviderValue(test, out TService providedValue))
			{
				return false;
			}

			if(ScopedService<TService>.IsServiceFor(client, providedValue))
			{
				return true;
			}

			if(typeof(TService).IsValueType)
			{
				return false;
			}

			#if UNITY_EDITOR
			if(ServiceAttributeUtility.definingTypes.ContainsKey(typeof(IValueProvider<TService>)))
			{
				return true;
			}
			#endif

			#if UNITY_EDITOR && !INIT_ARGS_DISABLE_SERVICE_INJECTION
			if(ServiceInjector.TryGetServiceInfo(typeof(IValueProvider<TService>), out _))
			{
				return true;
			}
			#endif

			if(ReferenceEquals(test, Service<IValueProvider<TService>>.Instance))
			{
				return true;
			}

			return false;
		}

		internal static bool IsAccessibleTo<TService>(ScopedService<TService>.Instance instance, [DisallowNull] Transform clientTransform)
		{
			#if DEV_MODE
			Debug.Assert(clientTransform);
			Debug.Assert(instance.serviceProvider);
			#endif

			#if UNITY_EDITOR
			// Skip services from prefabs - this can help avoid AmbiguousMatchWarning issues.
			// However, if both objects are part of the same prefab, then it's important to not return false,
			// as otherwise Service tags would never get drawn inside prefabs being Inspected.
			if(clientTransform.gameObject.scene != instance.Scene && instance.Scene.IsInvalidOrPrefabStage())
			{
				return false;
			}
			#endif

			switch(instance.clients)
			{
				case Clients.InGameObject:
					return instance.Transform == clientTransform;
				case Clients.InChildren:
					for(var parent = clientTransform.transform; parent; parent = parent.parent)
					{
						if(parent == instance.Transform)
						{
							return true;
						}
					}
					return false;
				case Clients.InParents:
					for(var parent = instance.Transform; parent; parent = parent.parent)
					{
						if(parent == clientTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InHierarchyRootChildren:
					return instance.Transform.root == clientTransform.root;
				case Clients.InScene:
					return clientTransform.gameObject.scene == instance.Scene;
				case Clients.InAllScenes:
				case Clients.Everywhere:
					return true;
				default:
					Debug.LogError($"Unrecognized {nameof(Clients)} value: {instance.clients}.", instance.Transform);
					return false;
			}
		}

		internal static bool IsAccessibleTo([DisallowNull] Transform serviceTransform, Clients clients, [AllowNull] Transform clientTransform)
		{
			if(!clientTransform)
			{
				return clients == Clients.Everywhere;
			}

			#if UNITY_EDITOR
			// Skip services from prefabs. This can help avoid AmbiguousMatchWarning issues.
			if(clientTransform.gameObject.scene.IsValid() && (!serviceTransform.gameObject.scene.IsValid() || (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() is var prefabStage && prefabStage && serviceTransform.gameObject.scene == prefabStage.scene)))
			{
				return false;
			}
			#endif

			switch(clients)
			{
				case Clients.InGameObject:
					return serviceTransform == clientTransform;
				case Clients.InChildren:
					for(var parent = clientTransform.transform; parent; parent = parent.parent)
					{
						if(parent == serviceTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InParents:
					for(var parent = serviceTransform; parent; parent = parent.parent)
					{
						if(parent == clientTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InHierarchyRootChildren:
					return serviceTransform.root == clientTransform.root;
				case Clients.InScene:
					return clientTransform.gameObject.scene == serviceTransform.gameObject.scene;
				case Clients.InAllScenes:
				case Clients.Everywhere:
					return true;
				default:
					Debug.LogError($"Unrecognized {nameof(Clients)} value: {clients}.", serviceTransform);
					return false;
			}
		}

		#if UNITY_EDITOR
		internal struct ServiceInfo : IEquatable<ServiceInfo>
		{
			[MaybeNull]
			public readonly Type ConcreteType;
			public readonly Clients ToClients;
			[MaybeNull]
			public readonly object Service;
			public readonly string ClientsText;
			private GUIContent label;
			private string tooltip;
			private bool? isAsset;
			private readonly Type definingType;

			public GUIContent Label => label ??= new GUIContent(GetLabel(), tooltip);
			public bool IsAsset => isAsset ??= IsPrefabAsset(Service);
			public readonly ServiceInitFailReason InitFailReason;

			public ServiceInfo(Type definingType, Clients toClients, [AllowNull] object service, string tooltip = "", ServiceInitFailReason initFailReason = ServiceInitFailReason.None)
			{
				ToClients = toClients;
				Service = service;
				this.tooltip = tooltip;
				InitFailReason = initFailReason;

				ConcreteType = service?.GetType();
				if(ConcreteType?.IsGenericType ?? false)
				{
					var typeDefinition = ConcreteType.GetGenericTypeDefinition();
					if(typeDefinition == typeof(ValueTask<>) || typeDefinition == typeof(Task<>) || typeDefinition == typeof(Lazy<>))
					{
						ConcreteType = ConcreteType.GetGenericArguments()[0];
					}
				}

				ClientsText = ToClients.ToString();
				isAsset = null;
				label = null;
				this.definingType = definingType;
			}

			private string GetLabel()
			{
				var sb = new StringBuilder();
				var definingTypes = GetDefiningTypes();
				if(ConcreteType is not null)
				{
					sb.Append(TypeUtility.ToString(ConcreteType));


					int count = definingTypes.Length;
					if(count == 0 || (count == 1 && definingTypes[0] == ConcreteType))
					{
						return sb.ToString();
					}

					sb.Append(" <color=grey>(");
					AddDefiningtypes();
					sb.Append(")</color>");
				}
				else
				{
					AddDefiningtypes();
				}

				return sb.ToString();

				void AddDefiningtypes()
				{
					sb.Append(TypeUtility.ToString(definingTypes[0]));
					for(int i = 1, count = definingTypes.Length; i < count; i++)
					{
						sb.Append(", ");
						sb.Append(TypeUtility.ToString(definingTypes[i]));
					}
				}
			}

			private Type[] GetDefiningTypes()
			{
				var result = Service == Null ? Array.Empty<Type>() : ServiceTagUtility.GetServiceDefiningTypes(Service).ToArray();
				return result.Length > 0 ? result : new[] { definingType };
			}

			public bool Equals(ServiceInfo other) => ReferenceEquals(Service, other.Service);
			public override bool Equals(object obj) => obj is ServiceInfo serviceInfo && Equals(serviceInfo);
			public override int GetHashCode() => Service.GetHashCode();

			private static bool IsPrefabAsset([AllowNull] object obj) => obj != Null && Find.In(obj, out Transform transform) && transform.gameObject.IsPartOfPrefabAsset();
		}

		private sealed class ServiceInfoOrderer : IComparer<ServiceInfo>
		{
			public int Compare(ServiceInfo x, ServiceInfo y) => x.Label.text.CompareTo(y.Label.text);
		}
		#endif
	}
}