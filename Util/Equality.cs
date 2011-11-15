using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// A definition for equality between values of a type.
    /// </summary>
    public interface Equality<T>
    {
        /// <summary>
        /// Determines wether the given values are equal based on this definition of equality.
        /// </summary>
        bool Equal(T A, T B);

        /// <summary>
        /// Gets a hash for the given value such that all values that are mutually equivalent have the same hash. Note
        /// that the converse does not need to be true, values with the same hash can still be distinct.
        /// </summary>
        int Hash(T Value);
    }

    /// <summary>
    /// A definition of equality for reference values that tests wether the references are equivalent.
    /// </summary>
    public struct ReferenceEquality<T> : Equality<T>
        where T : class
    {
        public bool Equal(T A, T B)
        {
            return A == B;
        }

        public int Hash(T Value)
        {
            // RuntimeHelpers.GetHashCode gurantees that the hashcode will remain the same throughout the object's lifetime.
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Value);
        }
    }
}