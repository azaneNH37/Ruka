using System;
using UnityEngine;

namespace Ruka.Utils.Core
{
    [Serializable]
    public sealed class InterfaceReference<TInterface, TObject>
        where TObject : UnityEngine.Object
        where TInterface : class
    {
        [SerializeField] private TObject obj;

        public TObject Object => obj;
        public TInterface Value => obj as TInterface;
        public bool IsValid => obj != null && obj is TInterface;

        public bool TryGet(out TInterface value)
        {
            value = obj as TInterface;
            return value != null;
        }
    }
}
