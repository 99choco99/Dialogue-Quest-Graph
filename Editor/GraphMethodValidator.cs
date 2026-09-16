using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;

namespace UniversalGraph.Editor
{
    /// <summary>Attribute가 붙은 런타임 메서드의 선언과 중복 키를 Reflection으로 검증</summary>
    internal static class GraphMethodValidator
    {
        /// <summary>플레이어 어셈블리에 들어가는 메서드만 검사하고 에디터 전용은 제외</summary>
        internal static List<string> Validate()
        {
            HashSet<string> runtimeAssemblyNames = new ();
            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                runtimeAssemblyNames.Add(assembly.name);
                foreach (string reference in assembly.compiledAssemblyReferences)
                {
                    runtimeAssemblyNames.Add(System.IO.Path.GetFileNameWithoutExtension(reference));
                }
            }

            List<MethodInfo> methods = new ();
            methods.AddRange(TypeCache.GetMethodsWithAttribute<DialogueActionAttribute>());
            methods.AddRange(TypeCache.GetMethodsWithAttribute<DialogueConditionAttribute>());
            methods.AddRange(TypeCache.GetMethodsWithAttribute<QuestActionAttribute>());
            methods.AddRange(TypeCache.GetMethodsWithAttribute<QuestConditionAttribute>());

            return Validate(methods.Where(method => method.DeclaringType != null
                && runtimeAssemblyNames.Contains(method.DeclaringType.Assembly.GetName().Name)));
        }

        /// <summary>메서드를 검사해서 어셈블리 사이의 중복 키를 검사</summary>
        internal static List<string> Validate(IEnumerable<MethodInfo> methods)
        {
            List<string> errors = new ();
            HashSet<(string Domain, MethodKind Kind, string Key)> checkedKeys = new ();

            //attribute가 여러개 붙는 경우를 위해 Distinct로 메서드는 한번씩만으로 제한하되, if문으로 여러곳에 등록
            foreach (MethodInfo method in methods.Distinct())
            {
                // 한 메서드에 여러 Attribute가 붙어 있어도 각각의 사용 규칙을 검사
                DialogueActionAttribute dialogueAction = method.GetCustomAttribute<DialogueActionAttribute>(false);
                if (dialogueAction != null)
                {
                    ValidateDialogueMethod(method, MethodKind.Action, dialogueAction.Key, dialogueAction.Owner);
                }

                DialogueConditionAttribute dialogueCondition = method.GetCustomAttribute<DialogueConditionAttribute>(false);
                if (dialogueCondition != null)
                {
                    ValidateDialogueMethod(method, MethodKind.Condition, dialogueCondition.Key, dialogueCondition.Owner);
                }

                QuestActionAttribute questAction = method.GetCustomAttribute<QuestActionAttribute>(false);
                if (questAction != null)
                {
                    ValidateQuestMethod(method, MethodKind.Action, questAction.Key, questAction.Owner);
                }

                QuestConditionAttribute questCondition = method.GetCustomAttribute<QuestConditionAttribute>(false);
                if (questCondition != null)
                {
                    ValidateQuestMethod(method, MethodKind.Condition, questCondition.Key, questCondition.Owner);
                }
            }

            return errors;


            //==================================== 내부 함수 ===================================

            //dialogue전용 메서드 검증
            void ValidateDialogueMethod(MethodInfo method, MethodKind kind, string key, DialogueMethodOwner owner)
            {
                if (!DialogueMethodDescriptorFactory.CreateDescriptor(method, kind, key, owner, out DialogueMethodDescriptor descriptor, out string error))
                {
                    errors.Add("[Dialogue] " + error);
                    return;
                }

                CheckDuplicateKey("Dialogue", descriptor);
            }

            //Quest전용 메서드 검증
            void ValidateQuestMethod(MethodInfo method, MethodKind kind, string key, QuestMethodOwner owner)
            {
                if (!QuestMethodDescriptorFactory.CreateDescriptor(method, kind, key, owner, out QuestMethodDescriptor descriptor, out string error))
                {
                    errors.Add("[Quest] " + error);
                    return;
                }

                CheckDuplicateKey("Quest", descriptor);
            }

            //중복검사
            void CheckDuplicateKey(string domain, MethodDescriptor descriptor)
            {
                if (!checkedKeys.Add((domain, descriptor.Kind, descriptor.Key)))
                {
                    errors.Add($"[{domain}] 중복된 {descriptor.Kind} 키 '{descriptor.Key}'.");
                }
            }
        }
    }
}
