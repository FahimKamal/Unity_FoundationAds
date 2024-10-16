﻿//#define DEBUG_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	[InitializeOnLoad]
	internal static class LayoutUtility
	{
		public static Editor NowDrawing { get; internal set; }

		private static readonly List<Action> deferredActions = new();

		static LayoutUtility()
		{
			Editor.finishedDefaultHeaderGUI -= BeginGUI;
			Editor.finishedDefaultHeaderGUI += BeginGUI;
		}

		//public static void BeginInitSection() => ObjectFieldBackgroundColor = HelpBoxBackgroundColor;
		//public static void EndInitSection() => ObjectFieldBackgroundColor = InspectorBackgroundColor;

		public static void BeginGUI([DisallowNull] Editor editor)
		{
			NowDrawing = editor;
			//ObjectFieldBackgroundColor = InspectorBackgroundColor;

			if(deferredActions.Count > 0)
			{
				if(Event.current.type is not EventType.Layout)
				{
					Repaint(editor);
					return;
				}

				ApplyImmediate(deferredActions);
			}
		}

		public static void EndGUI([DisallowNull] Editor editor)
		{
			if(deferredActions.Count <= 0)
			{
				return;
			}

			if(Event.current.type is not EventType.Repaint)
			{
				Repaint(editor);
				return;
			}

			ApplyImmediate(deferredActions);
		}

		public static void ExitGUI()
		{
			if(Event.current is null)
			{
				#if DEV_MODE
				Debug.LogWarning($"{nameof(LayoutUtility)}.{nameof(ExitGUI)} with Event.current:None.");
				#endif

				return;
			}

			#if DEV_MODE && DEBUG_ENABLED
			Debug.Log($"{nameof(LayoutUtility)}.{nameof(ExitGUI)} with Event.current:{Event.current.type}.");
			#endif

			NowDrawing = null;
			GUIUtility.ExitGUI();
		}

		public static void OnNestedInspectorGUI([DisallowNull] this Editor editor)
		{
			var parentEditor = NowDrawing;

			editor.OnBeforeNestedInspectorGUI();

			try
			{
				if(CustomEditorUtility.GenericInspectorType.IsInstanceOfType(editor))
				{
					editor.serializedObject.DrawPropertiesWithoutScriptField();
				}
				else
				{
					editor.OnInspectorGUI();
				}
			}
			catch(ArgumentException e) when (ShouldHideExceptionFromUsers(e))
			{
				#if DEV_MODE
				Debug.LogWarning(e);
				#endif
			}
			finally
			{
				editor.OnAfterNestedInspectorGUI(parentEditor);
			}
		}

		public static void OnBeforeNestedInspectorGUI([DisallowNull] this Editor editor)
		{
			BeginGUI(editor);
			editor.serializedObject.Update();
		}

		public static void OnAfterNestedInspectorGUI([DisallowNull] this Editor editor, Editor parentEditor)
		{
			editor.serializedObject.ApplyModifiedProperties();

			EndGUI(editor);

			NowDrawing = parentEditor;
		}

		public static void ApplyWhenSafe([DisallowNull] Action action, Editor editor = null, ExecutionOptions executionOptions = ExecutionOptions.Default)
		{
			EventType eventType = Event.current?.type ?? EventType.Ignore;
			if(eventType == EventType.Layout)
			{
				if(executionOptions.HasFlag(ExecutionOptions.ExecuteImmediateIfLayoutEvent))
				{
					action();
					return;
				}
			}
			else if(executionOptions.HasFlag(ExecutionOptions.ExitGUIIfNotLayoutEvent))
			{
				Repaint(editor);
				ExitGUI();
				return;
			}

			if(!executionOptions.HasFlag(ExecutionOptions.AllowDuplicates))
			{
				deferredActions.Remove(action);
			}

			deferredActions.Add(action);
			Repaint(editor);
		}

		private static void ApplyImmediate(List<Action> deferredActions) //, out bool exitGUI)
		{
			#if DEV_MODE && DEBUG_ENABLED
			Debug.Log($"{nameof(LayoutUtility)}.{nameof(ApplyImmediate)}({deferredActions[0].Method?.Name ?? deferredActions[0].ToString()}) with Event.current:{Event.current.type}.");
			#endif

			Action[] actions = deferredActions.ToArray();
			deferredActions.Clear();
			Exception deferredException = null;

			for(int index = 0, count = actions.Length; index < count; index++)
			{
				Action action = actions[index];
				try
				{
					action();
				}
				catch(ArgumentException e) when(ShouldHideExceptionFromUsers(e))
				{
					#if DEV_MODE
					Debug.LogException(e);
					#endif
				}
				catch(ExitGUIException e) when(index < count - 1)
				{
					deferredException = e;
				}
				catch(Exception exception)
				{
					Debug.LogException(exception);
				}
			}

			if(deferredException is not null)
			{
				#if DEV_MODE && DEBUG_ENABLED
				Debug.Log($"{deferredException.GetType().Name} with Event.current:{Event.current.type}.");
				#endif
				throw deferredException;
			}
		}

		public static void Repaint([AllowNull] Editor editor = null)
		{
			GUI.changed = true;

			if(editor)
			{
				editor.Repaint();

				if(NowDrawing != editor && NowDrawing)
				{
					NowDrawing.Repaint();
				}
			}
			else if(NowDrawing)
			{
				NowDrawing.Repaint();
			}
			else
			{
				InspectorContents.Repaint();
			}
		}

		private static bool IsExitGUIException([DisallowNull] Exception exception)
		{
			while(exception is TargetInvocationException && exception.InnerException != null)
			{
				exception = exception.InnerException;
			}

			return exception is ExitGUIException;
		}

		private static bool ShouldHideExceptionFromUsers([DisallowNull] ArgumentException exception)
			=> exception.Message.EndsWith("controls when doing repaint");
	}
}