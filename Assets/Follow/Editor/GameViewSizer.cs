using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Follow.EditorTools
{
    /// <summary>
    /// Forces the Game view to a fixed 1920x1080 so UI work is judged at the resolution
    /// the CanvasScaler is authored for. Screenshots taken at an arbitrary window aspect
    /// give a false read of every layout decision.
    /// </summary>
    public static class GameViewSizer
    {
        [MenuItem("Follow/Set Game View 1920x1080", priority = 40)]
        public static void SetFullHd() => SetSize(1920, 1080, "Follow 1920x1080");

        public static void SetSize(int width, int height, string label)
        {
            var sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleType.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup")
                .Invoke(instance, new object[] { (int)GetCurrentGroupType() });

            var groupType = group.GetType();
            var total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var builtin = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null);

            int found = -1;
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var name = size.GetType().GetProperty("baseText").GetValue(size) as string;
                if (name == label) { found = i; break; }
            }

            if (found < 0)
            {
                var sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
                var kindType = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
                var ctor = sizeType.GetConstructors()
                    .First(c => c.GetParameters().Length == 4);
                var newSize = ctor.Invoke(new object[]
                {
                    Enum.Parse(kindType, "FixedResolution"), width, height, label
                });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                found = total;
            }

            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            var window = EditorWindow.GetWindow(gameViewType, false, null, false);
            gameViewType.GetMethod("SizeSelectionCallback",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(window, new object[] { found, null });

            Debug.Log("Game view set to " + width + "x" + height + " (index " + found + ", builtin " + builtin + ")");
        }

        static object GetCurrentGroupType()
        {
            var sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleType.GetProperty("instance").GetValue(null);
            return sizesType.GetProperty("currentGroupType").GetValue(instance);
        }
    }
}
