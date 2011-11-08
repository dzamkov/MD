using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Contains helper functions for reflection.
    /// </summary>
    public static class Reflection
    {
        /// <summary>
        /// Tries getting the value of a public static member from a type.
        /// </summary>
        public static bool Get<T>(Type Type, string Name, ref T Value)
        {
            BindingFlags bf = BindingFlags.Public | BindingFlags.Static;

            FieldInfo fi = Type.GetField(Name, bf);
            if (fi != null)
            {
                object val = fi.GetValue(null);
                if (val is T)
                {
                    Value = (T)val;
                    return true;
                }
            }

            PropertyInfo pi = Type.GetProperty(Name, bf);
            if (pi != null)
            {
                object val = pi.GetValue(null, null);
                if (val is T)
                {
                    Value = (T)val;
                    return true;
                }
            }

            if (typeof(T).IsSubclassOf(typeof(MulticastDelegate)))
            {
                MethodInfo[] mis = Type.GetMethods(bf);
                foreach (MethodInfo mi in mis)
                {
                    Value = (T)(object)Delegate.CreateDelegate(typeof(T), mi);
                    return true;
                }
            }

            return false;
        }
    }
}
