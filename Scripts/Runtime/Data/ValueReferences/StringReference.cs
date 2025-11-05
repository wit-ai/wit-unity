/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.Data.ValueReferences
{
    using UnityEngine;

    /// <summary>
    /// A serializable reference wrapper that allows fields to either use an inline string value
    /// or reference a reusable ScriptableObject asset that implements IStringReference.
    /// This pattern enables Unity Inspector workflows where values can be edited inline or
    /// shared across multiple components via ScriptableObject assets.
    /// </summary>
    /// <example>
    /// Usage example:
    /// <code>
    /// // Create a custom ScriptableObject type
    /// [CreateAssetMenu(menuName = "My Game/Dialog String")]
    /// public class DialogString : ScriptableObject, IStringReference
    /// {
    ///     [SerializeField] private string _value;
    ///     public string Value
    ///     {
    ///         get => _value;
    ///         set => _value = value;
    ///     }
    /// }
    ///
    /// // Use in a MonoBehaviour
    /// public class DialogDisplay : MonoBehaviour
    /// {
    ///     [SerializeField] private StringReference&lt;DialogString&gt; dialogText;
    ///
    ///     void Start()
    ///     {
    ///         // Access the value - works with both inline strings and asset references
    ///         Debug.Log(dialogText.Value);
    ///     }
    /// }
    /// </code>
    /// In the Inspector, you can either:
    /// - Type a string directly (inline value)
    /// - Drag a DialogString asset to reference a reusable string asset
    /// If both are set, the asset reference takes precedence over the inline value.
    /// </example>
    /// <typeparam name="T">A ScriptableObject type that implements IStringReference</typeparam>
    [System.Serializable]
    public class StringReference<T> : IStringReference where T : ScriptableObject, IStringReference
    {
        [SerializeField] private string stringValue;
        [SerializeField] private T stringObject;

        /// <summary>
        /// Gets the string value from the referenced asset if set, otherwise returns the inline string value.
        /// Setting a value clears any asset reference and stores the value inline.
        /// </summary>
        public string Value
        {
            get => stringObject ? stringObject.Value : stringValue;
            set
            {
                stringObject = null;
                stringValue = value;
            }
        }
    }

    /// <summary>
    /// Interface that must be implemented by ScriptableObject types used with StringReference.
    /// Provides a string Value property for getting and setting the string data.
    /// </summary>
    public interface IStringReference
    {
        string Value { get; set; }
    }
}
