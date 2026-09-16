using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>그래프 작성에서 선택할 수 있는 유효한 Quest Attribute 메서드를 나열</summary>
    internal static class QuestMethodCatalog
    {
        private static readonly List<QuestMethodDescriptor> actions = new();
        private static readonly List<QuestMethodDescriptor> conditions = new();
        private static readonly Dictionary<string, QuestMethodDescriptor> actionByKey = new();
        private static readonly Dictionary<string, QuestMethodDescriptor> conditionByKey = new();

        static QuestMethodCatalog()
        {
            BuildCatalog();
        }

        /// <summary>메서드 설명서 목록 반환</summary>
        public static IReadOnlyList<QuestMethodDescriptor> GetMethodList(MethodKind kind)
        {
            return kind == MethodKind.Action ? actions : conditions;
        }

        /// <summary>키로 메서드 설명서 하나 찾기</summary>
        public static bool GetMethodDescriptor(MethodKind kind, string key, out QuestMethodDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return (kind == MethodKind.Action ? actionByKey : conditionByKey).TryGetValue(key, out descriptor);
        }

        /// <summary>플레이어 어셈블리를 검사하고 대상을 확정할 수 없는 중복 키를 제외합니다.</summary>
        private static void BuildCatalog()
        {
            //플레이어 어셈블리만 가져오기
            HashSet<string> runtimeAssemblyNames = new ();
            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                runtimeAssemblyNames.Add(assembly.name);
                foreach (string reference in assembly.compiledAssemblyReferences)
                {
                    runtimeAssemblyNames.Add(System.IO.Path.GetFileNameWithoutExtension(reference));
                }
            }

            //action 메서드들 TypeCache로 가져오기
            Dictionary<string, List<QuestMethodDescriptor>> actionCandidates = new();
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<QuestActionAttribute>())
            {
                QuestActionAttribute attribute = method.GetCustomAttribute<QuestActionAttribute>(false);
                if (attribute != null && IsRuntimeMethod(method, runtimeAssemblyNames))
                {
                    AddCandidate(method, MethodKind.Action, attribute.Key, attribute.Owner, actionCandidates);
                }
            }

            //condition 메서드들 TypeCache로 가져오기
            Dictionary<string, List<QuestMethodDescriptor>> conditionCandidates = new ();
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<QuestConditionAttribute>())
            {
                QuestConditionAttribute attribute = method.GetCustomAttribute<QuestConditionAttribute>(false);
                if (attribute != null && IsRuntimeMethod(method, runtimeAssemblyNames))
                {
                    AddCandidate(method, MethodKind.Condition, attribute.Key, attribute.Owner, conditionCandidates);
                }
            }

            //확정짓기
            FinalizeCandidates(actionCandidates, actions, actionByKey);
            FinalizeCandidates(conditionCandidates, conditions, conditionByKey);
        }

        /// <summary>
        /// 플레이어 어셈블리의 메서드인지?
        /// </summary>
        private static bool IsRuntimeMethod(MethodInfo method, ISet<string> runtimeAssemblyNames)
        {
            string assemblyName = method.DeclaringType?.Assembly.GetName().Name;
            return !string.IsNullOrWhiteSpace(assemblyName) && runtimeAssemblyNames.Contains(assemblyName);
        }
        

        /// <summary>
        /// 메서드 후보를 목록에 추가
        /// </summary>
        private static void AddCandidate(MethodInfo method, MethodKind kind, string key, QuestMethodOwner owner, IDictionary<string, List<QuestMethodDescriptor>> candidatesByKey)
        {
            if (!QuestMethodDescriptorFactory.CreateDescriptor(method, kind, key, owner, out QuestMethodDescriptor descriptor, out _))
            {
                return;
            }

            if (!candidatesByKey.TryGetValue(descriptor.Key, out List<QuestMethodDescriptor> candidates))
            {
                candidates = new List<QuestMethodDescriptor>();
                candidatesByKey.Add(descriptor.Key, candidates);
            }

            candidates.Add(descriptor);
        }


        /// <summary>
        /// 후보 메서드들을 검증 후 확정
        /// </summary>
        private static void FinalizeCandidates(
            IReadOnlyDictionary<string, List<QuestMethodDescriptor>> candidatesByKey,
            List<QuestMethodDescriptor> list,
            IDictionary<string, QuestMethodDescriptor> listByKey)
        {
            foreach (KeyValuePair<string, List<QuestMethodDescriptor>> pair in candidatesByKey)
            {
                if (pair.Value.Count != 1)
                {
                    continue;
                }

                //유일한것만 담기
                QuestMethodDescriptor descriptor = pair.Value[0];
                list.Add(descriptor);
                listByKey.Add(descriptor.Key, descriptor);
            }

            list.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        }
    }
}
