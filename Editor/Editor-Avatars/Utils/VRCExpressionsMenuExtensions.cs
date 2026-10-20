// Source: com.vrcfury.vrcfury/Editor-Avatars/Utils/VRCExpressionsMenuExtensions.cs
// Changes:
//   - Dropped RewriteParameters and the Delete result (we only ever read menus here), and dropped the .Dirty() calls that went with it.

using System;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Utils {
    internal static class VRCExpressionsMenuExtensions {
        /**
         * This method is our primary way of iterating through menus. It needs to be recursion-aware,
         * since many avatars have recursion in their menus for some reason.
         */
        public static void ForEachMenu(
            this VRCExpressionsMenu root,
            Action<VRCExpressionsMenu,IList<string>> ForEachMenu = null,
            Func<VRCExpressionsMenu.Control,IList<string>,ForEachMenuItemResult> ForEachItem = null
        ) {
            var stack = new Stack<Tuple<string[],VRCExpressionsMenu>>();
            var seen = new HashSet<VRCExpressionsMenu>();
            stack.Push(Tuple.Create(new string[]{}, root));
            while (stack.Count > 0) {
                var (path,menu) = stack.Pop();
                if (menu == null || seen.Contains(menu)) continue;
                seen.Add(menu);
                if (ForEachMenu != null)
                    ForEachMenu(menu, path);
                for (var i = 0; i < menu.controls.Count; i++) {
                    var item = menu.controls[i];
                    var itemPath = new List<string>();
                    itemPath.AddRange(path);
                    itemPath.Add(item.name);
                    var itemPathArr = itemPath.ToArray();

                    var recurse = true;
                    if (ForEachItem != null) {
                        var result = ForEachItem(item, itemPathArr);
                        if (result == ForEachMenuItemResult.Skip) {
                            recurse = false;
                        }
                    }
                    if (recurse && item.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                        stack.Push(Tuple.Create(itemPathArr, item.subMenu));
                    }
                }
            }
        }

        public enum ForEachMenuItemResult {
            Continue,
            Skip
        }
    }
}
