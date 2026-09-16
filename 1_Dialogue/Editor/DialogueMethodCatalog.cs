using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>
    /// 에디터에서 선택할 수 있는 Dialogue 메서드를 찾아 설명서 목록과 키별 조회 정보를 제공
    /// </summary>
    internal static class DialogueMethodCatalog
    {
        //메서드들 보관
        private static readonly List<DialogueMethodDescriptor> actions = new();
        private static readonly List<DialogueMethodDescriptor> conditions = new();
        private static readonly Dictionary<string, DialogueMethodDescriptor> actionByKey = new();
        private static readonly Dictionary<string, DialogueMethodDescriptor> conditionByKey = new();

        static DialogueMethodCatalog()
        {
            BuildCatalog();
        }

        /// <summary>바인딩 종류에 사용할 수 있는 메서드를 반환</summary>
        public static IReadOnlyList<DialogueMethodDescriptor> GetMethodList(MethodKind kind)
        {
            return kind == MethodKind.Action ? actions : conditions;
        }

        /// <summary>kind랑 key로 메서드 설명서 가져오기</summary>
        public static bool GetMethodDescriptor(MethodKind kind, string key, out DialogueMethodDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            Dictionary<string, DialogueMethodDescriptor> methodsByKey = kind == MethodKind.Action ? actionByKey : conditionByKey;
            return methodsByKey.TryGetValue(key, out descriptor);
        }

        /// <summary>
        /// 플레이어 어셈블리만 검사해서 드롭다운에 띄우기위해서 Catalog를 만듦
        /// </summary>
        private static void BuildCatalog()
        {
            HashSet<string> runtimeAssemblyNames = new();
            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                runtimeAssemblyNames.Add(assembly.name);
                foreach (string reference in assembly.compiledAssemblyReferences)
                {
                    runtimeAssemblyNames.Add(System.IO.Path.GetFileNameWithoutExtension(reference));
                }
            }

            //action 함수들
            Dictionary<string, List<DialogueMethodDescriptor>> actionCandidates = new ();
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<DialogueActionAttribute>())
            {
                var attribute = method.GetCustomAttribute<DialogueActionAttribute>(inherit: false);
                if (attribute != null && IsRuntimeMethod(method, runtimeAssemblyNames))
                {
                    AddCandidate(method, MethodKind.Action, attribute.Key, attribute.Owner, actionCandidates);
                }
            }

            //condition 함수들
            Dictionary<string, List<DialogueMethodDescriptor>> conditionCandidates = new ();
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<DialogueConditionAttribute>())
            {
                var attribute = method.GetCustomAttribute<DialogueConditionAttribute>(inherit: false);
                if (attribute != null && IsRuntimeMethod(method, runtimeAssemblyNames))
                {
                    AddCandidate(method, MethodKind.Condition, attribute.Key, attribute.Owner, conditionCandidates);
                }
            }

            FinalizeCandidates(actionCandidates, actions, actionByKey);
            FinalizeCandidates(conditionCandidates, conditions, conditionByKey);
        }

        /// <summary>
        /// 플레이어 어셈블리인지 ?
        /// </summary>
        private static bool IsRuntimeMethod(MethodInfo method, HashSet<string> runtimeAssemblyNames)
        {
            string assemblyName = method.DeclaringType?.Assembly.GetName().Name;
            return !string.IsNullOrEmpty(assemblyName) && runtimeAssemblyNames.Contains(assemblyName);
        }

        /// <summary>
        /// 메서드 후보를 목록에 추가
        /// </summary>
        private static void AddCandidate(MethodInfo method, MethodKind kind, string key, DialogueMethodOwner owner, Dictionary<string, List<DialogueMethodDescriptor>> candidatesByKey)
        {
            if (!DialogueMethodDescriptorFactory.CreateDescriptor(method, kind, key, owner, out DialogueMethodDescriptor descriptor, out _))
            {
                return;
            }

            if (!candidatesByKey.TryGetValue(descriptor.Key, out List<DialogueMethodDescriptor> candidates))
            {
                candidates = new List<DialogueMethodDescriptor>();
                candidatesByKey.Add(descriptor.Key, candidates);
            }

            candidates.Add(descriptor);
        }

        /// <summary>
        /// 후보 메서드들을 검증 후 확정
        /// </summary>
        private static void FinalizeCandidates(
            Dictionary<string, List<DialogueMethodDescriptor>> candidatesByKey,
            List<DialogueMethodDescriptor> list,
            Dictionary<string, DialogueMethodDescriptor> listByKey)
        {
            foreach (KeyValuePair<string, List<DialogueMethodDescriptor>> pair in candidatesByKey)
            {
                if (pair.Value.Count != 1)
                {
                    continue;
                }
                //유일한것만 담기
                DialogueMethodDescriptor descriptor = pair.Value[0];
                list.Add(descriptor);
                listByKey.Add(descriptor.Key, descriptor);
            }

            list.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        }
    }
}
