﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// An object with a requirement to be disposed when no longer needed. If this structure is given as a result of
    /// a method, the caller will be responsible for the management of the object. If given as an argument,
    /// the object will need to be disposed in the method.
    /// </summary>
    public struct Disposable<T> : IDisposable
    {
        public Disposable(T Object)
        {
            this.Object = Object;
        }

        public void Dispose()
        {
            IDisposable dis = this.Object as IDisposable;
            if (dis != null)
            {
                dis.Dispose();
            }
        }

        /// <summary>
        /// Gets wether the object needs to be disposed. If this is false, the Disposable structure may safely be removed.
        /// </summary>
        public bool NeedDispose
        {
            get
            {
                return this.Object is IDisposable;
            }
        }

        /// <summary>
        /// Gets wether this is null.
        /// </summary>
        public bool IsNull
        {
            get
            {
                return this.Object == null;
            }
        }

        public static implicit operator T(Disposable<T> Disposable)
        {
            return Disposable.Object;
        }

        public static implicit operator Disposable<T>(T Object)
        {
            return new Disposable<T>(Object);
        }

        public static T operator ~(Disposable<T> Object)
        {
            return Object.Object;
        }

        /// <summary>
        /// The object to be used.
        /// </summary>
        public T Object;
    }
}