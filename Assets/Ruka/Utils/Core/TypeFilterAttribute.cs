using System;
using UnityEngine;

namespace Ruka.Utils.Core
{
    public sealed class TypeFilterAttribute : PropertyAttribute
    {
        public TypeFilterAttribute(Type filterType, TypeFilterFlag filterFlag = TypeFilterFlag.Class)
        {
            FilterType = filterType;
            FilterFlag = filterFlag;
        }

        public Type FilterType { get; }
        public TypeFilterFlag FilterFlag { get; }
    }

    [Flags]
    public enum TypeFilterFlag
    {
        Class = 1,
        Interface = 1 << 1
    }
}
