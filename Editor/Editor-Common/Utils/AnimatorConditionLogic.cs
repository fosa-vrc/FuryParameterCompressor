// Source: com.vrcfury.vrcfury/Editor-Common/Utils/AnimatorConditionLogic.cs
// Changes:
//   - VRCFuryEditorUtils.NextFloatUp/NextFloatDown were inlined as private helpers
//     (source: Packages/com.vrcfury.vrcfury/Editor-Common/Inspector/VRCFuryEditorUtils.cs).
//   - Dropped the unused Stringify() debug helper.
//   - Simplify() sorts conditions with StringComparer.Ordinal instead of the culture-sensitive default,
//     so the generated controller doesn't depend on the editor's locale.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;

namespace FOSA.FPC.Utils {
    /**
     * This class can take any arbitrary set of animator transform conditions,
     * and AND, OR, or NOT them cleanly with other conditions.
     */
    internal static class AnimatorConditionLogic {
        // Source: com.vrcfury.vrcfury/Editor-Common/Inspector/VRCFuryEditorUtils.cs
        private static float NextFloat(float input, int offset) {
            if (float.IsNaN(input) || float.IsPositiveInfinity(input) || float.IsNegativeInfinity(input))
                return input;

            var bytes = BitConverter.GetBytes(input);
            var bits = BitConverter.ToInt32(bytes, 0);

            if (input > 0) {
                bits += offset;
            } else if (input < 0) {
                bits -= offset;
            } else if (input == 0) {
                return (offset > 0) ? float.Epsilon : -float.Epsilon;
            }

            bytes = BitConverter.GetBytes(bits);
            return BitConverter.ToSingle(bytes, 0);
        }

        public static float NextFloatUp(float input) {
            return NextFloat(input, 1);
        }
        public static float NextFloatDown(float input) {
            return NextFloat(input, -1);
        }

        private static AnimatorCondition Not(AnimatorCondition input) {
            var copy = input;
            switch (copy.mode) {
                case AnimatorConditionMode.Equals:
                    copy.mode = AnimatorConditionMode.NotEqual;
                    break;
                case AnimatorConditionMode.Greater:
                    copy.mode = AnimatorConditionMode.Less;
                    copy.threshold = NextFloatUp(copy.threshold);
                    break;
                case AnimatorConditionMode.If:
                    copy.mode = AnimatorConditionMode.IfNot;
                    break;
                case AnimatorConditionMode.Less:
                    copy.mode = AnimatorConditionMode.Greater;
                    copy.threshold = NextFloatDown(copy.threshold);
                    break;
                case AnimatorConditionMode.IfNot:
                    copy.mode = AnimatorConditionMode.If;
                    break;
                case AnimatorConditionMode.NotEqual:
                    copy.mode = AnimatorConditionMode.Equals;
                    break;
                default:
                    throw new Exception("Unknown condition mode: " + copy.mode);
            }
            return copy;
        }

        public static AnimatorCondition[][] Not(AnimatorCondition[][] input) {
            var output = input.Aggregate(
                new [] { Array.Empty<AnimatorCondition>() },
                (accumulator, sequence) => (
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Append(Not(item)).ToArray()
                ).ToArray());
            return Simplify(output);
        }

        public static AnimatorCondition[][] And(AnimatorCondition[][] in1, AnimatorCondition[][] in2) {
            var combined = in1.SelectMany(a =>
                in2.Select(b => a.Concat(b).ToArray())
            ).ToArray();
            return Simplify(combined);
        }

        public static AnimatorCondition[][] Or(AnimatorCondition[][] in1, AnimatorCondition[][] in2) {
            var combined = in1.Concat(in2).ToArray();
            return Simplify(combined);
        }

        private static AnimatorCondition[][] Simplify(AnimatorCondition[][] conds) {
            // Simplify each transform
            conds = conds.Select(Simplify).ToArray();

            // Remove duplicates
            conds = conds
                .Select(c => new HashSet<AnimatorCondition>(c))
                .Distinct(HashSet<AnimatorCondition>.CreateSetComparer())
                .Select(c => c.ToArray())
                .ToArray();

            // Remove supersets
            conds = conds.Where(c => {
                var set = c.ToImmutableHashSet();
                var hasSubset = conds.Any(other => c != other && set.IsProperSupersetOf(other));
                return !hasSubset;
            }).ToArray();

            // Remove impossible
            conds = conds.Where(c => !IsImpossible(c)).ToArray();

            return conds;
        }

        private static AnimatorCondition[] Simplify(AnimatorCondition[] all) {

            // Remove redundant conditions
            var stream = all.Where(c => !IsRedundant(c, all));

            // Remove duplicate conditions
            stream = stream.GroupBy(c => c).Select(grp => grp.First());

            // Sort.
            // Ordinal rather than upstream's culture-sensitive default,
            // so that the generated controller is byte-identical regardless of the editor's locale.
            stream = stream.OrderBy(c => c.parameter, StringComparer.Ordinal);

            return stream.ToArray();
        }

        private static bool IsImpossible(AnimatorCondition[] conds) {
            return conds.Any(c => IsImpossible(c, conds));
        }

        private static bool IsImpossible(AnimatorCondition rule, AnimatorCondition[] others) {
            foreach (var other in others) {
                if (rule.parameter != other.parameter) continue;
                switch (rule.mode) {
                    case AnimatorConditionMode.Equals:
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold >= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold != rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Less && other.threshold <= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.NotEqual && other.threshold == rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.Greater:
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold <= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Less && other.threshold <= rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.Less:
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold >= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold >= rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.NotEqual:
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold == rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.If:
                        if (other.mode == AnimatorConditionMode.IfNot) return true;
                        break;
                    case AnimatorConditionMode.IfNot:
                        if (other.mode == AnimatorConditionMode.If) return true;
                        break;
                }
            }
            return false;
        }
        private static bool IsRedundant(AnimatorCondition rule, AnimatorCondition[] others) {
            foreach (var other in others) {
                if (rule.parameter != other.parameter) continue;
                switch (rule.mode) {
                    case AnimatorConditionMode.Greater:
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold > rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold > rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.Less:
                        if (other.mode == AnimatorConditionMode.Less && other.threshold < rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold < rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.NotEqual:
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold >= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold != rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Less && other.threshold <= rule.threshold)
                            return true;
                        break;
                }
            }
            return false;
        }
    }
}
