using System;

namespace UltimateReplay
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ReplayComponentPreparerAttribute : Attribute
    {
        // Public
        public Type componentType;

        // Constructor
        public ReplayComponentPreparerAttribute(Type componentType)
        {
            this.componentType = componentType;
        }
    }
}
