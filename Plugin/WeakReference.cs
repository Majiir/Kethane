using System;
using System.Runtime.Serialization;

namespace Kethane
{
    /// <summary>
    /// Strongly-typed weak reference.
    /// See: http://blog.somewhatabstract.com/2012/03/24/strongly-typed-weakreference/
    /// </summary>
    /// <typeparam name="T">The type of object being referenced.</typeparam>
    internal class WeakReference<T> : WeakReference, ISerializable where T : class
    {
        /// <summary>
        /// Initializes a new instance of the  <see cref="WeakReference&lt;T&gt;"/> class, referencing
        /// the specified object.
        /// </summary>
        /// <param name="target">An object to track.</param>
        public WeakReference(T target)
            : base(target)
        {

        }

        /// <summary>
        /// Initializes a new instance of the  <see cref="WeakReference&lt;T&gt;"/> class, referencing
        /// the specified object and using the specified resurrection tracking.
        /// </summary>
        /// <param name="target">An object to track.</param>
        /// <param name="trackResurrection">Indicates when to stop tracking the object. If <c>true</c>, the object is tracked
        /// after finalization; if <c>false</c>, the object is only tracked until finalization..</param>
        public WeakReference(T target, bool trackResurrection)
            : base(target, trackResurrection)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WeakReference&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="info">An object that holds all the data needed to serialize or deserialize the current <see cref="T:System.WeakReference"/> object.</param>
        /// <param name="context">(Reserved) Describes the source and destination of the serialized stream specified by <paramref name="info"/>.</param>
        /// <exception cref="T:System.ArgumentNullException">
        ///     <paramref name="info"/> is null. </exception>
        protected WeakReference(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets or sets the object (the target) referenced by the current <see cref="T:System.WeakReference"/> object.
        /// </summary>
        /// <value></value>
        /// <returns>null if the object referenced by the current <see cref="T:System.WeakReference"/> object has been garbage collected; otherwise, a reference to the object referenced by the current <see cref="T:System.WeakReference"/> object.</returns>
        /// <exception cref="T:System.InvalidOperationException">The reference to the target object is invalid. This exception can be thrown while setting this property if the value is a null reference or if the object has been finalized during the set operation.</exception>
        public new T Target
        {
            get
            {
                return (T)base.Target;
            }

            set
            {
                base.Target = value;
            }
        }
    }
}
