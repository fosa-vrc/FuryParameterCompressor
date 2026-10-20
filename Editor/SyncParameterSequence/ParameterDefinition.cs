// Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterDefinition.cs
// Changes:
//   - Added Canonical(), used to build the deterministic solution hash that secondary platforms verify.

using System;
using System.Collections.Generic;
using System.Globalization;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.SyncParameterSequence
{
    internal class ParameterDefinition
    {
        public string Name { get; set; }
        public ValueType ValueType { get; set; }
        public float DefaultValue { get; set; }
        public bool Saved { get; set; }

        private sealed class CriticalValueEqualityComparer : IEqualityComparer<ParameterDefinition>
        {
            public bool Equals(ParameterDefinition x, ParameterDefinition y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null) return false;
                if (y is null) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.Name == y.Name && x.ValueType == y.ValueType && x.DefaultValue.Equals(y.DefaultValue);
            }

            public int GetHashCode(ParameterDefinition obj)
            {
                return HashCode.Combine(obj.Name, (int)obj.ValueType, obj.DefaultValue);
            }
        }

        public static IEqualityComparer<ParameterDefinition> CriticalValueComparer { get; } =
            new CriticalValueEqualityComparer();

        public static ParameterDefinition FromVRC(VRCExpressionParameters.Parameter p)
        {
            ValueType ty;
            switch (p.valueType)
            {
                case VRCExpressionParameters.ValueType.Int: ty = ValueType.Int; break;
                case VRCExpressionParameters.ValueType.Float: ty = ValueType.Float; break;
                case VRCExpressionParameters.ValueType.Bool: ty = ValueType.Bool; break;
                default: throw new Exception($"Unsupported parameter type {p.valueType}");
            }

            return new ParameterDefinition
            {
                Name = p.name,
                ValueType = ty,
                DefaultValue = p.defaultValue,
                Saved = p.saved
            };
        }

        public VRCExpressionParameters.Parameter ToVRC()
        {
            VRCExpressionParameters.ValueType ty;

            switch (ValueType)
            {
                case ValueType.Bool: ty = VRCExpressionParameters.ValueType.Bool; break;
                case ValueType.Int: ty = VRCExpressionParameters.ValueType.Int; break;
                case ValueType.Float: ty = VRCExpressionParameters.ValueType.Float; break;
                default: throw new Exception($"Unsupported parameter type {ValueType}");
            }

            return new VRCExpressionParameters.Parameter
            {
                name = Name,
                valueType = ty,
                defaultValue = DefaultValue,
                saved = Saved,
                networkSynced = true
            };
        }

        // A culture-invariant, stable string form of this parameter,
        // used as hash input so that the same avatar produces the same solution hash on every machine.
        // Deliberately covers exactly the fields MA's CriticalValueComparer considers
        // `Saved` and `DefaultValue` are allowed to differ between platforms.
        public string Canonical()
        {
            return Name + "\u0001" + (int)ValueType;
        }
    }
}
