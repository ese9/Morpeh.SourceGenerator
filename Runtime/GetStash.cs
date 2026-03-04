using System;

namespace Scellecs.Morpeh.SourceGenerator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class GetStashAttribute : Attribute
    {
        public Type StashType { get; set; }
        public string StashName { get; set; }

        public GetStashAttribute(Type stashType, string stashName = null)
        {
            StashType = stashType;
            StashName = stashName;
        }
    }
}