using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>Attribute가 붙은 Quest Action과 Condition을 찾아 등록하고 호출합니다.</summary>
    public static class QuestMethodInvoker
    {
        private static readonly Dictionary<string, QuestMethodDescriptor> actionRegistry = new();
        private static readonly Dictionary<string, QuestMethodDescriptor> conditionRegistry = new();
        private static readonly HashSet<string> invalidActionKeys = new();
        private static readonly HashSet<string> invalidConditionKeys = new();
        private static bool isInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            actionRegistry.Clear();
            conditionRegistry.Clear();
            invalidActionKeys.Clear();
            invalidConditionKeys.Clear();
            isInitialized = false;
        }

        /// <summary>Reflection으로 Quest Action과 Condition 등록부를 만들기</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            actionRegistry.Clear();
            conditionRegistry.Clear();
            invalidActionKeys.Clear();
            invalidConditionKeys.Clear();


#if UNITY_EDITOR
            // Editor 전용 어셈블리는 게임 메서드 검색에서 제외
            HashSet<string> runtimeAssemblyNames = new();
            foreach (UnityEditor.Compilation.Assembly runtimeAssembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies))
            {
                runtimeAssemblyNames.Add(runtimeAssembly.name);
                foreach (string reference in runtimeAssembly.compiledAssemblyReferences)
                {
                    runtimeAssemblyNames.Add(System.IO.Path.GetFileNameWithoutExtension(reference));
                }
            }
#endif
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
#if UNITY_EDITOR
                if (!runtimeAssemblyNames.Contains(assembly.GetName().Name))
                {
                    continue;
                }
#endif
                if (!CanUseQuestAttributes(assembly, typeof(QuestActionAttribute).Assembly.GetName().Name))
                {
                    continue;
                }

                ScanAssembly(assembly);
            }

            isInitialized = true;
        }

        private static bool CanUseQuestAttributes(Assembly assembly, string runtimeAssemblyName)
        {
            if (assembly.IsDynamic)
            {
                return false;
            }

            string name = assembly.GetName().Name;
            if (name == runtimeAssemblyName)
            {
                return true;
            }

            try
            {
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    if (reference.Name == runtimeAssemblyName)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        //=========================== 메서드를 가져오기 (reflection)===============================================

        /// <summary>
        /// 리플렉션으로 메서드 가져오기(Assembly -> Type -> method -> attribute순)
        /// </summary>
        private static void ScanAssembly(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Quest] 어셈블리 '{assembly.GetName().Name}'을 검색하지 못했습니다: {exception.Message}");
                return;
            }

            foreach (Type type in types)
            {
                if (type == null)
                {
                    continue;
                }

                MethodInfo[] methods;
                try
                {
                    //현재 타입에 직접 선언된 메서드라면, 공개 여부와 static 여부에 관계없이 전부 가져온다
                    methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Quest] '{type.FullName}'의 메서드를 검사하지 못했습니다: {exception.Message}");
                    continue;
                }

                foreach (MethodInfo method in methods)
                {
                    QuestActionAttribute action = method.GetCustomAttribute<QuestActionAttribute>(false);
                    if (action != null)
                    {
                        RegisterMethod(method, MethodKind.Action, action.Key, action.Owner);
                    }

                    QuestConditionAttribute condition = method.GetCustomAttribute<QuestConditionAttribute>(false);
                    if (condition != null)
                    {
                        RegisterMethod(method, MethodKind.Condition, condition.Key, condition.Owner);
                    }
                }
            }
        }

        //=========================== 메서드를 가져온 이후 ===============================================

        /// <summary>Reflection으로 찾은 메서드의 설명서를 만들고 등록</summary>
        private static void RegisterMethod(MethodInfo method, MethodKind kind, string key, QuestMethodOwner owner)
        {
            if (!QuestMethodDescriptorFactory.CreateDescriptor(method, kind, key, owner, out QuestMethodDescriptor descriptor, out string error))
            {
                Debug.LogError($"[Quest] {error}");
                return;
            }

            RegisterDescriptor(descriptor);
        }

        /// <summary>
        /// 메서드 설명서를 꺼내 쓸 수 있게 등록해두기
        /// </summary>
        private static void RegisterDescriptor(QuestMethodDescriptor descriptor)
        {
            IDictionary<string, QuestMethodDescriptor> registry = descriptor.Kind == MethodKind.Action ? actionRegistry : conditionRegistry;
            ISet<string> invalidKeys = descriptor.Kind == MethodKind.Action ? invalidActionKeys : invalidConditionKeys;

            if (invalidKeys.Contains(descriptor.Key))
            {
                return;
            }

            if (registry.TryGetValue(descriptor.Key, out QuestMethodDescriptor duplicate))
            {
                registry.Remove(descriptor.Key);
                invalidKeys.Add(descriptor.Key);
                Debug.LogError($"[Quest] 중복된 {descriptor.Kind} 키 '{descriptor.Key}': " + $"{duplicate.QualifiedMethodName}, {descriptor.QualifiedMethodName}");
                return;
            }

            registry.Add(descriptor.Key, descriptor);
        }



        /// <summary>호출 키 조회, 인수 복원과 대상 결정을 마친 뒤 메서드를 실행</summary>
        public static bool InvokeMethod(MethodBindingData bindingData, QuestExecutionContext context, MethodKind kind, out bool conditionResult)
        {
            conditionResult = false;
            if (kind != MethodKind.Action && kind != MethodKind.Condition)
            {
                Debug.LogError("[Quest] 메서드 종류가 올바르지 않습니다.");
                return false;
            }

            Initialize();

            //Key 가져오기
            string key = bindingData?.Key;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError($"[Quest] {kind} 키가 비어 있습니다.");
                return false;
            }

            //registry에서 descriptor 가져오기
            Dictionary<string, QuestMethodDescriptor> registry = kind == MethodKind.Action ? actionRegistry : conditionRegistry;
            if (!registry.TryGetValue(key, out QuestMethodDescriptor descriptor))
            {
                Debug.LogError($"[Quest] {kind} '{key}'이 등록되지 않았습니다.");
                return false;
            }

            //descriptor로 인수 생성
            if (!MethodArgumentCodec.CreateQuestRuntimeArguments(bindingData.Arguments, descriptor, context, out object[] arguments, out string error))
            {
                Debug.LogError($"[Quest] {error}");
                return false;
            }

            //메서드를 가지고 있는 객체를 찾기
            object ownerInstance = GetMethodOwnerInstance(descriptor, context?.Controller);
            if (!descriptor.IsStatic && ownerInstance == null)
            {
                return false;
            }

            //실제 실행 부분
            try
            {
                object methodResult = descriptor.MethodInfo.Invoke(ownerInstance, arguments);

                if (kind == MethodKind.Condition)
                {
                    conditionResult = (bool)methodResult;
                }

                return true;
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogError($"[Quest] 메서드 '{descriptor.Key}' 실행 중 예외가 발생했습니다.\n{exception.InnerException ?? exception}");
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Quest] 메서드 '{descriptor.Key}'를 호출하지 못했습니다.\n{exception}");
                return false;
            }
        }

        /// <summary>
        /// 주어진 설명서에 해당하는 메서드를 가지고 있는 Instance를 가져오는 함수
        /// </summary>
        private static object GetMethodOwnerInstance(QuestMethodDescriptor descriptor, IQuestController controller)
        {
            if (descriptor.Owner == QuestMethodOwner.Global)
            {
                return null;
            }

            if (descriptor.DeclaringType.IsInstanceOfType(controller))
            {
                return controller;
            }

            Debug.LogError(
                $"[Quest] Controller {descriptor.Kind} '{descriptor.Key}'에는 '{descriptor.DeclaringType.FullName}' 타입이 필요하지만, " +
                $"현재 Controller 타입은 '{controller?.GetType().FullName ?? "null"}'입니다.");
            return null;
        }

    }
}
